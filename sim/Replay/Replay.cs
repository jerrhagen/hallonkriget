using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Hallonkriget.Sim.Commands;
using Hallonkriget.Sim.Data;
using Hallonkriget.Sim.Determinism;
using Hallonkriget.Sim.Map;

namespace Hallonkriget.Sim.Replay;

/// <summary>
/// En reprisfil: allt som behövs för att spela upp en match igen. Matchens uppsättning (frö,
/// karta, spelare), alla kommandon i den ordning de gavs, och kontrollsummor längs vägen.
/// Sparfiler är reprisfiler: att ladda är att spela upp kommandona till sista ticket.
///
/// Formatet: "HKREPRIS", formatversion, regelversion, datans kontrollsumma, uppsättningen,
/// kommandona med CommandCodec, kontrollsummorna och sista ticket. Heltal som zigzag-varint.
/// </summary>
public sealed class Replay
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("HKREPRIS");

    /// <summary>Filformatets version. Höjs när filens uppbyggnad ändras.</summary>
    public const int FormatVersion = 2;

    /// <summary>
    /// Reglernas version. Höjs med flit när en ändring i simuleringen gör att gamla repriser spelas
    /// upp annorlunda. Reprisregressionen kräver att sparade repriser med samma version stämmer.
    /// </summary>
    public const int RulesVersion = 5;

    /// <summary>Så här ofta sparas en kontrollsumma, i tick.</summary>
    public const int ChecksumInterval = 100;

    public int Rules { get; }
    public ulong DataFingerprint { get; }
    public ulong Seed { get; }
    public MapDef Map { get; }
    public IReadOnlyList<PlayerSetup> Players { get; }

    /// <summary>Alla kommandon, sorterade på tick, i den ordning de gavs inom samma tick.</summary>
    public IReadOnlyList<Command> Commands => _commands;

    /// <summary>Kontrollsumman efter varje ChecksumInterval:e tick: (tick, summa).</summary>
    public IReadOnlyList<(int Tick, ulong Hash)> Checksums => _checksums;

    /// <summary>Hur många tick matchen kom fram till.</summary>
    public int EndTick { get; private set; }

    private readonly List<Command> _commands = new();
    private readonly List<(int, ulong)> _checksums = new();

    public Replay(ulong seed, MapDef map, IReadOnlyList<PlayerSetup> players, ulong dataFingerprint, int rules = RulesVersion)
    {
        Seed = seed;
        Map = map;
        Players = players;
        DataFingerprint = dataFingerprint;
        Rules = rules;
    }

    /// <summary>Uppsättningen för att starta matchen med den här datan.</summary>
    public MatchSetup Setup(GameData data) => new(Seed, Map.Width, Map.Height, Players, data, Map);

    /// <summary>Lägger till ett ticks kommandon. Anropas före GameState.Tick, med samma lista.</summary>
    public void Record(int tick, IReadOnlyList<Command> commands)
    {
        if (tick < EndTick) throw new ArgumentException($"Tick {tick} är redan inspelat");
        foreach (var c in commands)
        {
            if (c.Tick != tick) throw new ArgumentException($"Kommando för tick {c.Tick} spelades in vid tick {tick}");
            _commands.Add(c);
        }
    }

    /// <summary>Anropas efter GameState.Tick. Sparar kontrollsumman när det är dags.</summary>
    public void AfterTick(GameState state)
    {
        EndTick = state.TickCount;
        if (state.TickCount % ChecksumInterval == 0) _checksums.Add((state.TickCount, state.Hash()));
    }

    /// <summary>
    /// Spelar upp matchen från början till sista ticket (eller till untilTick) och kontrollerar
    /// kontrollsummorna. Kastar ReplayMismatchException med första avvikande tick.
    /// </summary>
    public GameState Play(GameData data, int untilTick = int.MaxValue)
    {
        if (data.Fingerprint != DataFingerprint)
            throw new ReplayMismatchException(0, "Reprisen spelades in med annan speldata");
        var state = GameState.NewMatch(Setup(data));
        int next = 0, check = 0;
        var tickCommands = new List<Command>();
        int end = IntMath.Min(EndTick, untilTick);
        while (state.TickCount < end)
        {
            tickCommands.Clear();
            while (next < _commands.Count && _commands[next].Tick == state.TickCount) tickCommands.Add(_commands[next++]);
            state.Tick(tickCommands);
            while (check < _checksums.Count && _checksums[check].Item1 < state.TickCount) check++;
            if (check < _checksums.Count && _checksums[check].Item1 == state.TickCount && _checksums[check].Item2 != state.Hash())
                throw new ReplayMismatchException(state.TickCount, $"Kontrollsumman skiljer sig vid tick {state.TickCount}");
        }
        return state;
    }

    // ---- Fil ----

    public void Write(Stream stream)
    {
        stream.Write(Magic);
        CommandCodec.WriteVarInt(stream, FormatVersion);
        CommandCodec.WriteVarInt(stream, Rules);
        WriteULong(stream, DataFingerprint);
        WriteULong(stream, Seed);

        WriteString(stream, Map.Id);
        CommandCodec.WriteVarInt(stream, Map.Width);
        CommandCodec.WriteVarInt(stream, Map.Height);
        CommandCodec.WriteVarInt(stream, Map.Starts.Length);
        foreach (var s in Map.Starts) WritePoint(stream, s);
        CommandCodec.WriteVarInt(stream, Map.Areas.Length);
        foreach (var a in Map.Areas)
        {
            stream.WriteByte((byte)a.Terrain);
            CommandCodec.WriteVarInt(stream, a.X0);
            CommandCodec.WriteVarInt(stream, a.Y0);
            CommandCodec.WriteVarInt(stream, a.X1);
            CommandCodec.WriteVarInt(stream, a.Y1);
        }
        CommandCodec.WriteVarInt(stream, Map.Territories.Length);
        foreach (var a in Map.Territories)
        {
            CommandCodec.WriteVarInt(stream, a.X0);
            CommandCodec.WriteVarInt(stream, a.Y0);
            CommandCodec.WriteVarInt(stream, a.X1);
            CommandCodec.WriteVarInt(stream, a.Y1);
        }

        CommandCodec.WriteVarInt(stream, Players.Count);
        foreach (var p in Players)
        {
            stream.WriteByte((byte)p.Faction);
            stream.WriteByte(p.IsComputer ? (byte)1 : (byte)0);
            stream.WriteByte((byte)p.Difficulty);
            stream.WriteByte(p.Start.HasValue ? (byte)1 : (byte)0);
            if (p.Start is { } start) WritePoint(stream, start);
        }

        CommandCodec.WriteVarInt(stream, _commands.Count);
        foreach (var c in _commands) CommandCodec.Write(stream, c);
        CommandCodec.WriteVarInt(stream, _checksums.Count);
        foreach (var (tick, hash) in _checksums)
        {
            CommandCodec.WriteVarInt(stream, tick);
            WriteULong(stream, hash);
        }
        CommandCodec.WriteVarInt(stream, EndTick);
    }

    public static Replay Read(Stream stream)
    {
        var magic = new byte[Magic.Length];
        if (stream.Read(magic, 0, magic.Length) != magic.Length || !magic.AsSpan().SequenceEqual(Magic))
            throw new InvalidDataException("Det här är ingen reprisfil");
        int format = CommandCodec.ReadVarInt(stream);
        if (format != FormatVersion) throw new InvalidDataException($"Reprisfilen har format {format}, spelet läser {FormatVersion}");
        int rules = CommandCodec.ReadVarInt(stream);
        ulong fingerprint = ReadULong(stream);
        ulong seed = ReadULong(stream);

        string mapId = ReadString(stream);
        int w = CommandCodec.ReadVarInt(stream), h = CommandCodec.ReadVarInt(stream);
        var starts = new TilePoint[CommandCodec.ReadVarInt(stream)];
        for (int i = 0; i < starts.Length; i++) starts[i] = ReadPoint(stream);
        var areas = new TerrainArea[CommandCodec.ReadVarInt(stream)];
        for (int i = 0; i < areas.Length; i++)
        {
            var terrain = (Terrain)CommandCodec.ReadByte(stream);
            areas[i] = new TerrainArea(terrain, CommandCodec.ReadVarInt(stream), CommandCodec.ReadVarInt(stream),
                CommandCodec.ReadVarInt(stream), CommandCodec.ReadVarInt(stream));
        }
        var territories = new TerrainArea[CommandCodec.ReadVarInt(stream)];
        for (int i = 0; i < territories.Length; i++)
            territories[i] = new TerrainArea(Terrain.Clearing, CommandCodec.ReadVarInt(stream), CommandCodec.ReadVarInt(stream),
                CommandCodec.ReadVarInt(stream), CommandCodec.ReadVarInt(stream));

        var players = new List<PlayerSetup>();
        int playerCount = CommandCodec.ReadVarInt(stream);
        for (int i = 0; i < playerCount; i++)
        {
            var faction = (Faction)CommandCodec.ReadByte(stream);
            bool computer = CommandCodec.ReadByte(stream) != 0;
            var difficulty = (Difficulty)CommandCodec.ReadByte(stream);
            TilePoint? start = CommandCodec.ReadByte(stream) != 0 ? ReadPoint(stream) : null;
            players.Add(new PlayerSetup(faction, computer, start, difficulty));
        }

        var replay = new Replay(seed, new MapDef(mapId, w, h, starts, areas) { Territories = territories }, players, fingerprint, rules);
        int count = CommandCodec.ReadVarInt(stream);
        for (int i = 0; i < count; i++) replay._commands.Add(CommandCodec.Read(stream));
        int checks = CommandCodec.ReadVarInt(stream);
        for (int i = 0; i < checks; i++) replay._checksums.Add((CommandCodec.ReadVarInt(stream), ReadULong(stream)));
        replay.EndTick = CommandCodec.ReadVarInt(stream);
        return replay;
    }

    public byte[] ToBytes()
    {
        using var ms = new MemoryStream();
        Write(ms);
        return ms.ToArray();
    }

    public static Replay FromBytes(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes, writable: false);
        return Read(ms);
    }

    private static void WritePoint(Stream s, TilePoint p)
    {
        CommandCodec.WriteVarInt(s, p.X);
        CommandCodec.WriteVarInt(s, p.Y);
    }

    private static TilePoint ReadPoint(Stream s) => new(CommandCodec.ReadVarInt(s), CommandCodec.ReadVarInt(s));

    private static void WriteULong(Stream s, ulong v)
    {
        for (int i = 0; i < 8; i++) s.WriteByte((byte)(v >> (8 * i)));
    }

    private static ulong ReadULong(Stream s)
    {
        ulong v = 0;
        for (int i = 0; i < 8; i++) v |= (ulong)CommandCodec.ReadByte(s) << (8 * i);
        return v;
    }

    private static void WriteString(Stream s, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        CommandCodec.WriteVarInt(s, bytes.Length);
        s.Write(bytes);
    }

    private static string ReadString(Stream s)
    {
        int n = CommandCodec.ReadVarInt(s);
        if (n is < 0 or > 1000) throw new InvalidDataException("För lång text i reprisfilen");
        var bytes = new byte[n];
        for (int i = 0; i < n; i++) bytes[i] = CommandCodec.ReadByte(s);
        return Encoding.UTF8.GetString(bytes);
    }
}

/// <summary>Reprisen går inte att spela upp likadant: annan data, eller en kontrollsumma som skiljer sig.</summary>
public sealed class ReplayMismatchException : Exception
{
    public int Tick { get; }

    public ReplayMismatchException(int tick, string message) : base(message) => Tick = tick;
}

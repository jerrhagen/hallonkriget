using System;
using System.Collections.Generic;
using System.Text.Json;
using Hallonkriget.Sim.Buildings;
using Hallonkriget.Sim.People;

namespace Hallonkriget.Sim.Advice;

/// <summary>Det Sixten eller Major varnar för. Ordningen är viktigast först.</summary>
public enum WarningKind : byte
{
    NoTable,
    EmptyTable,
    Starving,
    GaveUp,
    LowCoffeeSaturday,
    NoWorker,
    NoCarriers,
    CarriersBusy,
    StorageFull,
    NoBeds,
}

/// <summary>En pratbubbla. Warning är null för pratet.</summary>
public sealed record Remark(int Tick, string Text, WarningKind? Warning);

/// <summary>Replikerna i data/radgivare.json för en av rådgivarna.</summary>
public sealed class AdvisorLines
{
    public static readonly string[] WarningIds =
    {
        "inget_kafferep", "inget_pa_bordet", "svalt", "gav_upp", "lite_kaffe_lordag",
        "ingen_arbetare", "inga_barare", "bararna_hinner_inte", "forradet_fullt", "inga_sangar",
    };

    public string Name { get; }
    public IReadOnlyList<string[]> Warnings { get; }
    public IReadOnlyList<string> Chatter { get; }

    private AdvisorLines(string name, string[][] warnings, string[] chatter)
    {
        Name = name;
        Warnings = warnings;
        Chatter = chatter;
    }

    /// <summary>Läser rådgivaren för fraktionen: Sixten för Torpet, Major för Storgården.</summary>
    public static AdvisorLines Parse(string json, Faction faction)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement.GetProperty(faction == Faction.Torpet ? "sixten" : "major");
        var warnings = new string[WarningIds.Length][];
        var w = root.GetProperty("varningar");
        for (int i = 0; i < WarningIds.Length; i++)
        {
            if (!w.TryGetProperty(WarningIds[i], out var list) || list.GetArrayLength() == 0)
                throw new FormatException($"radgivare.json: {WarningIds[i]} saknas");
            warnings[i] = Strings(list);
        }
        return new AdvisorLines(root.GetProperty("namn").GetString()!, warnings, Strings(root.GetProperty("prat")));
    }

    private static string[] Strings(JsonElement list)
    {
        var result = new string[list.GetArrayLength()];
        int i = 0;
        foreach (var e in list.EnumerateArray()) result[i++] = e.GetString()!;
        return result;
    }
}

/// <summary>
/// Sixten (eller Major) i hörnet. Tittar på tillståndet och säger något var tredje minut eller när
/// något händer. Varningarna är alltid rätt och går inte att slå av; pratet går att slå av.
///
/// Rådgivaren är inte en del av GameState och påverkar inte matchen. Den läser bara, och är
/// deterministisk så att den kan testas: samma tillstånd i samma ordning ger samma repliker.
/// </summary>
public sealed class Advisor
{
    /// <summary>En pratbubbla var tredje minut.</summary>
    public const int BubbleInterval = 3 * 60 * GameState.TicksPerSecond;

    /// <summary>Hur länge ett läge ska ha hållit i sig innan det är värt en varning.</summary>
    public const int Grace = 20 * GameState.TicksPerSecond;

    /// <summary>Varningar kollas inte varje tick.</summary>
    public const int CheckInterval = GameState.TicksPerSecond;

    /// <summary>Humöret där en person räknas som nära att ge upp.</summary>
    public const int StarvingMood = 10;

    private readonly AdvisorLines _lines;
    private readonly byte _player;
    private readonly int[] _since = new int[WarningCount];   // ticket läget började, eller -1
    private readonly bool[] _said = new bool[WarningCount];  // varnat för det här läget redan
    private readonly int[] _subject = new int[WarningCount]; // byggnaden varningen gällde
    private readonly int[] _lineIndex = new int[WarningCount];
    private int _chatterIndex;
    private int _lastBubble;
    private int _gaveUpSeen;
    private int _lastCheck = -CheckInterval;

    private static readonly int WarningCount = Enum.GetValues<WarningKind>().Length;

    /// <summary>Pratet kan slås av. Varningarna kommer ändå.</summary>
    public bool Chatter { get; set; } = true;

    public string Name => _lines.Name;

    public Advisor(AdvisorLines lines, byte player)
    {
        _lines = lines;
        _player = player;
        Array.Fill(_since, -1);
        Array.Fill(_subject, -1);
    }

    /// <summary>Lägena som gäller just nu, viktigast först, oavsett om de har varnats för.</summary>
    public List<WarningKind> Active { get; } = new();

    /// <summary>Anropas efter varje tick (eller mer sällan). Returnerar en ny replik eller null.</summary>
    public Remark? Update(GameState s)
    {
        int tick = s.TickCount;
        if (tick - _lastCheck < CheckInterval) return null;
        _lastCheck = tick;

        var found = new (bool On, int Count, int Subject, int Extra)[WarningCount];
        Check(s, found);

        Active.Clear();
        Remark? fresh = null;
        for (int k = 0; k < WarningCount; k++)
        {
            var kind = (WarningKind)k;
            if (!found[k].On || (_subject[k] >= 0 && found[k].Subject != _subject[k]))
            {
                _since[k] = -1;
                _said[k] = false;
                _subject[k] = -1;
            }
            if (!found[k].On) continue;
            if (_since[k] < 0)
            {
                _since[k] = tick;
                _subject[k] = found[k].Subject;
            }
            bool instant = kind is WarningKind.GaveUp;
            if (!instant && tick - _since[k] < Grace) continue;
            Active.Add(kind);
            if (!_said[k] && fresh is null)
            {
                _said[k] = true;
                fresh = Say(s, kind, found[k].Count, found[k].Subject, found[k].Extra);
            }
        }
        if (fresh is not null)
        {
            _lastBubble = tick;
            return fresh;
        }

        if (tick - _lastBubble < BubbleInterval) return null;
        _lastBubble = tick;
        // Var tredje minut: det viktigaste som fortfarande gäller, annars prat om det är på.
        foreach (var kind in Active)
        {
            if (kind == WarningKind.GaveUp) continue;
            int k = (int)kind;
            return Say(s, kind, found[k].Count, found[k].Subject, found[k].Extra);
        }
        if (!Chatter || _lines.Chatter.Count == 0) return null;
        var text = _lines.Chatter[_chatterIndex++ % _lines.Chatter.Count];
        return new Remark(tick, text, null);
    }

    private Remark Say(GameState s, WarningKind kind, int count, int subject, int extra)
    {
        var options = _lines.Warnings[(int)kind];
        var text = options[_lineIndex[(int)kind]++ % options.Length].Replace("{antal}", count.ToString());
        if (subject >= 0) text = text.Replace("{byggnad}", s.Buildings[subject].Def.Name);
        if (extra >= 0 && kind == WarningKind.NoWorker)
            text = text.Replace("{yrke}", s.Data.Professions[extra].Name.ToLowerInvariant());
        return new Remark(s.TickCount, text, kind);
    }

    private void Check(GameState s, (bool On, int Count, int Subject, int Extra)[] found)
    {
        var data = s.Data;
        var player = s.Players[_player];

        // Maten.
        int hungry = 0, starving = 0, carriers = 0, idleCarriers = 0;
        foreach (var p in s.People)
        {
            if (p.Owner != _player) continue;
            if (p.Role == PersonRole.Carrier)
            {
                carriers++;
                if (p.Job == PersonJob.Idle) idleCarriers++;
            }
            if (p.Job is PersonJob.Resting or PersonJob.ToHome) continue;
            if (p.Mood <= data.Mood.EatAt) hungry++;
            if (p.Mood <= StarvingMood) starving++;
        }
        bool anyTable = false, anyFood = false;
        int coffee = 0, kaffe = Has(s, "kaffe");
        foreach (var b in s.Buildings)
        {
            if (b.Owner != _player || b.Stage != BuildingStage.Done) continue;
            if (b.Def.Table > 0)
            {
                anyTable = true;
                foreach (var f in data.Foods)
                    if (f.With < 0 && b.InputCount(f.Good) > 0) anyFood = true;
            }
            if (b.Def.IsStorage && kaffe >= 0) coffee += b.OutputCount(kaffe);
        }
        if (hungry > 0 && !anyTable) found[(int)WarningKind.NoTable] = (true, hungry, -1, -1);
        if (hungry > 0 && anyTable && !anyFood) found[(int)WarningKind.EmptyTable] = (true, hungry, -1, -1);
        if (starving > 0) found[(int)WarningKind.Starving] = (true, starving, -1, -1);

        // Någon gav upp sedan sist. Räknas som ett nytt läge varje gång.
        if (player.GaveUp > _gaveUpSeen)
        {
            int n = player.GaveUp - _gaveUpSeen;
            _gaveUpSeen = player.GaveUp;
            _since[(int)WarningKind.GaveUp] = -1;
            _said[(int)WarningKind.GaveUp] = false;
            found[(int)WarningKind.GaveUp] = (true, n, -1, -1);
        }

        // Lördag och lite kaffe: lanthandeln är stängd i morgon.
        if (kaffe >= 0 && GameClock.Weekday(s.TickCount) == 5 && coffee <= 2)
            found[(int)WarningKind.LowCoffeeSaturday] = (true, coffee, -1, -1);

        // En färdig byggnad som saknar arbetare, och ingen med yrket finns.
        foreach (var b in s.Buildings)
        {
            if (b.Owner != _player || b.Stage != BuildingStage.Done || b.Def.Worker is null || b.HasWorker) continue;
            int profession = data.ProfessionIndex(b.Def.Worker);
            if (HasProfession(s, b.Def.Worker)) continue;
            found[(int)WarningKind.NoWorker] = (true, 1, b.Id, profession);
            break;
        }

        // Bärarna.
        if (carriers == 0) found[(int)WarningKind.NoCarriers] = (true, 0, -1, -1);
        else if (idleCarriers == 0) found[(int)WarningKind.CarriersBusy] = (true, carriers, -1, -1);

        // Förråden och sängarna.
        foreach (var b in s.Buildings)
        {
            if (b.Owner != _player || b.Stage != BuildingStage.Done || !b.Def.IsStorage) continue;
            if (b.StoredTotal < b.Def.Storage) continue;
            found[(int)WarningKind.StorageFull] = (true, 1, b.Id, -1);
            break;
        }
        // Sängarna spelar roll först när någon står i kö i bygdegården.
        bool queued = false;
        foreach (var b in s.Buildings)
            if (b.Owner == _player && b.TrainingQueue.Count > 0) queued = true;
        if (queued && s.Population(_player) >= s.Beds(_player)) found[(int)WarningKind.NoBeds] = (true, 0, -1, -1);
    }

    private bool HasProfession(GameState s, string profession)
    {
        foreach (var p in s.People)
            if (p.Owner == _player && p.Profession == profession) return true;
        // En i utbildning räknas också.
        foreach (var b in s.Buildings)
        {
            if (b.Owner != _player) continue;
            foreach (var q in b.TrainingQueue)
                if (q.Id == profession) return true;
        }
        return false;
    }

    private static int Has(GameState s, string good)
    {
        foreach (var g in s.Data.Goods) if (g.Id == good) return g.Index;
        return -1;
    }
}

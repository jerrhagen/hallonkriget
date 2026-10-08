using System;
using System.Collections.Generic;
using Hallonkriget.Sim.Commands;
using Hallonkriget.Sim.Determinism;
using Hallonkriget.Sim.Map;

namespace Hallonkriget.Sim;

/// <summary>
/// Hela spelets tillstånd. Kärnan är en funktion: Tick(kommandon). Samma tillstånd och samma
/// kommandon ger exakt samma nya tillstånd på alla datorer. Se determinismreglerna i CLAUDE.md.
/// </summary>
public sealed class GameState
{
    public const int TicksPerSecond = 10;

    // Fas 0: tak för vandrare så att testerna går fort. Försvinner med Walker.
    public const int MaxWalkersPerPlayer = 256;

    private readonly List<Player> _players = new();
    private readonly List<Walker> _walkers = new();
    private int _nextWalkerId;

    public int TickCount { get; private set; }
    public Rng Rng { get; }
    public GameMap Map { get; }
    public int MapWidth => Map.Width;
    public int MapHeight => Map.Height;
    public IReadOnlyList<Player> Players => _players;
    public IReadOnlyList<Walker> Walkers => _walkers;

    private GameState(ulong seed, int mapWidth, int mapHeight)
    {
        Rng = new Rng(seed);
        Map = new GameMap(mapWidth, mapHeight);
    }

    public static GameState NewMatch(MatchSetup setup)
    {
        if (setup.MapWidth <= 0 || setup.MapHeight <= 0) throw new ArgumentException("Kartan måste ha en storlek");
        if (setup.Players.Count is 0 or > 8) throw new ArgumentException("1–8 spelare");

        var state = new GameState(setup.Seed, setup.MapWidth, setup.MapHeight);
        for (int i = 0; i < setup.Players.Count; i++)
        {
            var p = setup.Players[i];
            state._players.Add(new Player((byte)i, p.Faction, p.IsComputer));
        }
        return state;
    }

    /// <summary>
    /// Kör ett tick. Kommandona ska alla gälla det här ticket. De tillämpas sorterade per spelare,
    /// och i given ordning inom samma spelare, så att ankomstordningen i nätverket inte spelar roll.
    /// </summary>
    public void Tick(IReadOnlyList<Command> commands)
    {
        ApplyCommands(commands);
        MovePeople();
        UpdateProduction();
        MatchDeliveries();
        UpdateMood();
        ResolveCombat();
        RunComputerPlayers();
        TickCount++;
    }

    /// <summary>Kontrollsumma över hela tillståndet. Nya fält i tillståndet ska läggas till här.</summary>
    public ulong Hash()
    {
        var h = new StateHasher();
        h.Add(TickCount);
        h.Add(Rng.State);
        Map.AddToHash(ref h);
        h.Add(_players.Count);
        foreach (var p in _players)
        {
            h.Add(p.Id);
            h.Add((byte)p.Faction);
            h.Add(p.IsComputer);
        }
        h.Add(_nextWalkerId);
        h.Add(_walkers.Count);
        foreach (var w in _walkers) w.AddToHash(ref h);
        return h.Value;
    }

    private void ApplyCommands(IReadOnlyList<Command> commands)
    {
        var ordered = SortByPlayer(commands);
        foreach (var c in ordered)
        {
            if (c.Tick != TickCount)
                throw new ArgumentException($"Kommando för tick {c.Tick} kom till tick {TickCount}");
            if (c.Player >= _players.Count) continue;

            switch (c.Type)
            {
                case CommandType.SpawnWalker:
                    SpawnWalker(c.Player, c.A, c.B);
                    break;
                case CommandType.MoveWalker:
                    MoveWalker(c.Player, c.A, c.B, c.C);
                    break;
            }
        }
    }

    /// <summary>Stabil sortering på spelare. Egen insättningssortering: List.Sort är inte stabil.</summary>
    private static Command[] SortByPlayer(IReadOnlyList<Command> commands)
    {
        var arr = new Command[commands.Count];
        for (int i = 0; i < arr.Length; i++)
        {
            var c = commands[i];
            int j = i - 1;
            while (j >= 0 && arr[j].Player > c.Player)
            {
                arr[j + 1] = arr[j];
                j--;
            }
            arr[j + 1] = c;
        }
        return arr;
    }

    private bool InsideMap(int x, int y) => Map.Inside(new TilePoint(x, y));

    private void SpawnWalker(byte owner, int tileX, int tileY)
    {
        if (!InsideMap(tileX, tileY)) return;
        int owned = 0;
        foreach (var w in _walkers) if (w.Owner == owner) owned++;
        if (owned >= MaxWalkersPerPlayer) return;

        // En pysslings fart: 1,2 rutor per sekund.
        var speed = Fixed.FromRatio(12, 10 * TicksPerSecond);
        var center = Fixed.FromRatio(1, 2);
        _walkers.Add(new Walker(_nextWalkerId++, owner, Fixed.FromInt(tileX) + center, Fixed.FromInt(tileY) + center, speed));
    }

    private void MoveWalker(byte owner, int walkerId, int tileX, int tileY)
    {
        if (!InsideMap(tileX, tileY)) return;
        var w = FindWalker(walkerId);
        if (w is null || w.Owner != owner) return;
        var center = Fixed.FromRatio(1, 2);
        w.TargetX = Fixed.FromInt(tileX) + center;
        w.TargetY = Fixed.FromInt(tileY) + center;
        w.HasTarget = true;
    }

    private Walker? FindWalker(int id)
    {
        foreach (var w in _walkers) if (w.Id == id) return w;
        return null;
    }

    private void MovePeople()
    {
        foreach (var w in _walkers)
        {
            if (!w.HasTarget)
            {
                // Lediga vandrare strövar ibland iväg, så att slumpen påverkar tillståndet.
                if (Rng.Chance(2))
                {
                    int tx = IntMath.Clamp(w.X.Floor + Rng.Range(-5, 5), 0, MapWidth - 1);
                    int ty = IntMath.Clamp(w.Y.Floor + Rng.Range(-5, 5), 0, MapHeight - 1);
                    w.TargetX = Fixed.FromInt(tx) + Fixed.FromRatio(1, 2);
                    w.TargetY = Fixed.FromInt(ty) + Fixed.FromRatio(1, 2);
                    w.HasTarget = true;
                }
                continue;
            }

            var dx = w.TargetX - w.X;
            var dy = w.TargetY - w.Y;
            var dist = Fixed.Length(dx, dy);
            if (dist <= w.Speed)
            {
                w.X = w.TargetX;
                w.Y = w.TargetY;
                w.HasTarget = false;
                continue;
            }
            w.X = Fixed.FromRaw(w.X.Raw + (int)((long)dx.Raw * w.Speed.Raw / dist.Raw));
            w.Y = Fixed.FromRaw(w.Y.Raw + (int)((long)dy.Raw * w.Speed.Raw / dist.Raw));
        }
    }

    // Stegen nedan fylls i under fas 1 och 3. Ordningen är fast och står i CLAUDE.md.
    private void UpdateProduction() { }
    private void MatchDeliveries() { }
    private void UpdateMood() { }
    private void ResolveCombat() { }
    private void RunComputerPlayers() { }
}

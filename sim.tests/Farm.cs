using Hallonkriget.Sim.Buildings;
using Hallonkriget.Sim.Commands;
using Hallonkriget.Sim.Data;
using Hallonkriget.Sim.Map;

namespace Hallonkriget.Sim.Tests;

/// <summary>Hjälp för tester som kör en gård: placera, lägga stigar, köra tick och vänta på villkor.</summary>
public sealed class Farm
{
    public GameState State { get; }
    public GameData Data => State.Data;

    public Farm(int width = 40, int height = 30, TilePoint? start = null)
    {
        State = GameState.NewMatch(new MatchSetup(
            Seed: 7, MapWidth: width, MapHeight: height,
            Players: new[]
            {
                new PlayerSetup(Faction.Torpet, IsComputer: false, start),
                new PlayerSetup(Faction.Storgarden, IsComputer: false),
            },
            Data: TestData.Game));
    }

    public int G(string id) => Data.GoodIndex(id);

    public Building Home => State.Buildings[0];

    public void Command(CommandType type, int a = 0, int b = 0, int c = 0, byte player = 0) =>
        State.Tick(new[] { new Command(State.TickCount, player, type, a, b, c) });

    public Building Place(string id, int x, int y)
    {
        int count = State.Buildings.Count;
        Command(CommandType.PlaceBuilding, Data.Building(id).Index, x, y);
        Assert.True(State.Buildings.Count == count + 1, $"{id} kunde inte placeras på ({x},{y})");
        return State.Buildings[^1];
    }

    /// <summary>Placerar en byggnad som är färdig direkt, utan material och hantlangare.</summary>
    public Building PlaceDone(string id, int x, int y)
    {
        var b = Place(id, x, y);
        b.CompleteAtOnce(Array.Empty<GoodAmount>());
        return b;
    }

    /// <summary>Lägger varor direkt i en byggnads inlager.</summary>
    public void Put(Building b, string good, int count = 1)
    {
        for (int i = 0; i < count; i++) Assert.True(b.PutInput(G(good)), $"{b.Def.Id} tog inte emot {good}");
    }

    /// <summary>Planerar en rak eller vinklad stig, ruta för ruta, från a till b (först i x, sedan i y).</summary>
    public void PlanPath(TilePoint a, TilePoint b)
    {
        var commands = new List<Command>();
        int x = a.X, y = a.Y;
        commands.Add(new Command(State.TickCount, 0, CommandType.PlanPath, x, y));
        while (x != b.X)
        {
            x += Math.Sign(b.X - x);
            commands.Add(new Command(State.TickCount, 0, CommandType.PlanPath, x, y));
        }
        while (y != b.Y)
        {
            y += Math.Sign(b.Y - y);
            commands.Add(new Command(State.TickCount, 0, CommandType.PlanPath, x, y));
        }
        State.Tick(commands);
    }

    /// <summary>Lägger upptrampad stig direkt, utan hantlangare.</summary>
    public void LayPath(TilePoint a, TilePoint b)
    {
        int x = a.X, y = a.Y;
        State.Map.SetPath(new TilePoint(x, y), PathState.Trodden);
        while (x != b.X)
        {
            x += Math.Sign(b.X - x);
            State.Map.SetPath(new TilePoint(x, y), PathState.Trodden);
        }
        while (y != b.Y)
        {
            y += Math.Sign(b.Y - y);
            State.Map.SetPath(new TilePoint(x, y), PathState.Trodden);
        }
    }

    public void Run(int ticks)
    {
        for (int i = 0; i < ticks; i++) State.Tick(Array.Empty<Command>());
    }

    /// <summary>Kör tills villkoret gäller och returnerar hur många tick det tog. Misslyckas efter max.</summary>
    public int RunUntil(Func<bool> condition, int max, string what)
    {
        for (int i = 0; i < max; i++)
        {
            if (condition()) return i;
            State.Tick(Array.Empty<Command>());
        }
        Assert.Fail($"{what}: inte klart efter {max} tick");
        return max;
    }
}

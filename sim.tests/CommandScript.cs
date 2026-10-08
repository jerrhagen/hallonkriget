using Hallonkriget.Sim.Commands;
using Hallonkriget.Sim.Determinism;
using Hallonkriget.Sim.Map;

namespace Hallonkriget.Sim.Tests;

/// <summary>
/// Hittar på kommandon för en hel match i förväg, styrt av ett eget frö. Samma frö ger samma manus,
/// så två körningar kan jämföras. Manuset innehåller även ogiltiga kommandon, som kärnan ska ignorera.
/// </summary>
public static class CommandScript
{
    public static MatchSetup Setup(ulong seed) => new(
        seed,
        MapWidth: 96,
        MapHeight: 96,
        Players: new[]
        {
            new PlayerSetup(Faction.Torpet, IsComputer: false, Start: new TilePoint(10, 10)),
            new PlayerSetup(Faction.Storgarden, IsComputer: false, Start: new TilePoint(80, 80)),
            new PlayerSetup(Faction.Torpet, IsComputer: true, Start: new TilePoint(10, 80)),
            new PlayerSetup(Faction.Storgarden, IsComputer: true),
        },
        Data: TestData.Game);

    public static List<Command>[] Generate(ulong scriptSeed, int ticks, int players)
    {
        var rng = new Rng(scriptSeed);
        var perTick = new List<Command>[ticks];
        for (int t = 0; t < ticks; t++)
        {
            var list = new List<Command>();
            int count = rng.Chance(30) ? rng.Range(1, 6) : 0;
            for (int i = 0; i < count; i++)
            {
                // Spelare utanför matchen och rutor utanför kartan förekommer med flit.
                byte player = (byte)rng.Range(0, players);
                int x = rng.Range(-4, 99);
                int y = rng.Range(-4, 99);
                int kind = rng.Range(0, 99);
                if (kind < 30)
                    list.Add(new Command(t, player, CommandType.SpawnWalker, x, y));
                else if (kind < 60)
                    list.Add(new Command(t, player, CommandType.MoveWalker, rng.Range(0, 400), x, y));
                else if (kind < 75)
                    list.Add(new Command(t, player, CommandType.PlanPath, x, y));
                else if (kind < 90)
                    list.Add(new Command(t, player, CommandType.PlaceBuilding, rng.Range(-1, 16), x, y));
                else if (kind < 95)
                    list.Add(new Command(t, player, CommandType.SelectRecipe, rng.Range(-1, 60), rng.Range(-2, 8)));
                else if (kind < 98)
                    list.Add(new Command(t, player, CommandType.Train, rng.Range(-1, 60), rng.Range(-1, 13)));
                else if (kind < 99)
                    list.Add(new Command(t, player, CommandType.BlockGood, rng.Range(-1, 60), rng.Range(-1, 55), rng.Range(0, 1)));
                else
                    list.Add(new Command(t, player, CommandType.CancelTraining, rng.Range(-1, 60)));
            }
            perTick[t] = list;
        }
        return perTick;
    }

    /// <summary>Kör manuset och returnerar kontrollsumman var 10:e tick, som i multiplayer.</summary>
    public static List<ulong> Run(GameState state, List<Command>[] script)
    {
        var hashes = new List<ulong>();
        for (int t = 0; t < script.Length; t++)
        {
            state.Tick(script[t]);
            if (state.TickCount % 10 == 0) hashes.Add(state.Hash());
        }
        return hashes;
    }
}

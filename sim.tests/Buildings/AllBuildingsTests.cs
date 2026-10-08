using Hallonkriget.Sim.Buildings;
using Hallonkriget.Sim.Commands;
using Hallonkriget.Sim.Data;
using Hallonkriget.Sim.Map;
using Hallonkriget.Sim.People;

namespace Hallonkriget.Sim.Tests.Buildings;

/// <summary>Fas 2: alla 45 byggnader i data, och reglerna de nya byggnaderna behöver.</summary>
public class AllBuildingsTests
{
    private static GameData Data => TestData.Game;
    private static int G(string id) => Data.GoodIndex(id);

    private static GameState NewMatch() => GameState.NewMatch(new MatchSetup(
        Seed: 1, MapWidth: 40, MapHeight: 40,
        Players: new[]
        {
            new PlayerSetup(Faction.Torpet, IsComputer: false),
            new PlayerSetup(Faction.Storgarden, IsComputer: false),
        },
        Data: Data));

    private static Building Place(GameState s, string id, int x, int y, byte player = 0)
    {
        int count = s.Buildings.Count;
        s.Tick(new[] { new Command(s.TickCount, player, CommandType.PlaceBuilding, Data.Building(id).Index, x, y) });
        Assert.True(s.Buildings.Count == count + 1, $"{id} kunde inte placeras på ({x},{y})");
        return s.Buildings[^1];
    }

    /// <summary>Färdig byggnad med arbetaren på plats.</summary>
    private static Building Working(GameState s, string id, int x, int y, byte player = 0)
    {
        var b = Place(s, id, x, y, player);
        b.CompleteAtOnce(Array.Empty<GoodAmount>());
        b.HasWorker = true;
        return b;
    }

    private static void Run(GameState s, int ticks)
    {
        for (int i = 0; i < ticks; i++) s.Tick(Array.Empty<Command>());
    }

    private static void Fill(GameState s, Terrain t, int x0, int y0, int x1, int y1)
    {
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
            s.Map.SetTerrain(new TilePoint(x, y), t);
    }

    [Fact]
    public void EveryBuildingInTheTableIsInTheData()
    {
        // Produktionskedjor, Alla byggnader: 45 rader, där stugan och mangårdsbyggnaden delar en.
        string[] table =
        {
            "stugan", "mangardsbyggnaden", "boden", "skogshuggarkojan", "sagboden", "vedboden", "kolmilan",
            "stenrojarboden", "brunnen", "akern", "angen", "honshuset", "gashagen", "grisstian", "ladugarden",
            "farhagen", "fiskeboden", "barplockarstugan", "kvarnen", "bagarstugan", "koket", "mejeristugan",
            "syltkoket", "slakteriet", "rokeriet", "bryggstugan", "skrotboden", "smedjan", "platslageriet",
            "snickarboden", "spinnstugan", "stickstugan", "garveriet", "sadelmakeriet", "bygdegarden",
            "kafferepet", "logen", "lanthandeln", "vedtraven", "hundkojan", "uppfinnarverkstan", "bikuporna",
            "rosteriet", "maskinhallen", "mjolkpallen", "hundgarden",
        };
        Assert.Equal(table.Order(), Data.Buildings.Select(b => b.Id).Order());
        Assert.Equal(54, Data.Goods.Count);
    }

    [Fact]
    public void SizesAndCostsFollowTheTable()
    {
        // En 1×1 kostar 3 + 1, en 2×2 6 + 3, en 3×3 10 + 6.
        foreach (var b in Data.Buildings.Where(b => b.Buildable))
        {
            Assert.Equal(b.Width, b.Height);
            var (boards, stone) = b.Width switch { 1 => (3, 1), 2 => (6, 3), _ => (10, 6) };
            Assert.Equal(new[] { (G("brador"), boards), (G("sten"), stone) }, b.Cost.Select(c => (c.Good, c.Count)).Order());
        }
        Assert.Equal(3, Data.Building("ladugarden").Width);
        Assert.Equal(3, Data.Building("logen").Width);
        Assert.Equal(3, Data.Building("maskinhallen").Width);
        Assert.Equal(1, Data.Building("mejeristugan").Width);
    }

    [Fact]
    public void EveryFoodHasAKitchen()
    {
        // Hela matkedjan: varje mat på kafferepet kommer från en byggnad (kaffet från lanthandeln eller mjölkpallen).
        foreach (var food in Data.Foods)
            Assert.Contains(Data.Buildings, b => b.Recipes.Any(r => r.Out.Any(o => o.Good == food.Good)));
    }

    [Fact]
    public void EveryWorkerCanBeTrained()
    {
        foreach (var b in Data.Buildings.Where(b => b.Worker is not null))
        {
            var p = Data.Professions[Data.ProfessionIndex(b.Worker!)];
            Assert.Equal(PersonRole.Worker, p.Role);
            Assert.Equal(G("verktyg"), p.Tool);
        }
    }

    [Fact]
    public void FieldTakesSixTilesNextToTheFarm()
    {
        // Åkern är 2×2 plus 6 odlingsrutor, till höger om byggnaden.
        var s = NewMatch();
        var aker = Place(s, "akern", 10, 10);
        for (int y = 10; y <= 11; y++)
        for (int x = 12; x <= 14; x++)
        {
            var p = new TilePoint(x, y);
            Assert.Equal(Terrain.Field, s.Map.TerrainAt(p));
            Assert.Equal(0, s.Map.OwnerAt(p));
            Assert.True(s.Map.IsWalkable(p, MoveClass.Foot), "man kan gå över åkern");
            Assert.False(s.Map.CanLayPath(p));
        }
        Assert.Equal(new TilePoint(11, 12), aker.Entrance);
        Assert.False(s.CanPlace(0, Data.Building("vedboden"), new TilePoint(13, 10)));

        // Ingen plats för åkern där odlingsrutorna skulle hamna på en annan byggnad.
        Place(s, "vedboden", 22, 10);
        Assert.False(s.CanPlace(0, Data.Building("akern"), new TilePoint(19, 10)));
        Assert.True(s.CanPlace(0, Data.Building("akern"), new TilePoint(17, 10)));
    }

    [Fact]
    public void StoneRunsOutAndLeavesAClearing()
    {
        var s = NewMatch();
        var bod = Working(s, "stenrojarboden", 10, 10);
        var stony = new TilePoint(10, 14);
        s.Map.SetTerrain(stony, Terrain.Stony);
        int perTile = bod.Def.Gather!.PerTile;
        Assert.True(perTile > 0);

        for (int i = 0; i < perTile; i++)
        {
            Run(s, 301);
            bod.TakeOutput(G("sten"));
        }
        Assert.Equal(Terrain.Clearing, s.Map.TerrainAt(stony));
        Assert.Equal(perTile, s.Players[0].Produced[G("sten")]);
        Run(s, 600);
        Assert.Equal(perTile, s.Players[0].Produced[G("sten")]);
    }

    [Fact]
    public void ScrapHeapsRunOut()
    {
        var s = NewMatch();
        var bod = Working(s, "skrotboden", 10, 10);
        s.Map.SetTerrain(new TilePoint(12, 12), Terrain.ScrapHeap);
        int perTile = bod.Def.Gather!.PerTile;
        for (int i = 0; i < perTile + 3; i++)
        {
            Run(s, 301);
            bod.TakeOutput(G("skrot"));
        }
        Assert.Equal(perTile, s.Players[0].Produced[G("skrot")]);
        Assert.Equal(Terrain.Clearing, s.Map.TerrainAt(new TilePoint(12, 12)));
    }

    [Fact]
    public void WoodcutterPlantsOneSpruceForEveryTwoFelled()
    {
        var s = NewMatch();
        var koja = Working(s, "skogshuggarkojan", 10, 10);
        Fill(s, Terrain.Forest, 5, 15, 9, 16);
        var gather = koja.Def.Gather!;
        Assert.Equal(2, gather.PlantEvery);
        int Trees() => s.Map.AllTiles().Where(p => s.Map.TerrainAt(p) == Terrain.Forest)
            .Sum(p => gather.PerTile - s.Map.TakenAt(p));
        int before = Trees();

        for (int i = 0; i < 6; i++)
        {
            Run(s, 301);
            koja.TakeOutput(G("timmer"));
        }
        Assert.Equal(6, s.Players[0].Produced[G("timmer")]);
        Assert.Equal(before - 6 + 3, Trees());
    }

    [Fact]
    public void WoodcutterPlantsOnAClearingWhenTheForestIsGone()
    {
        var s = NewMatch();
        var koja = Working(s, "skogshuggarkojan", 10, 10);
        var tree = new TilePoint(10, 14);
        s.Map.SetTerrain(tree, Terrain.Forest);
        for (int i = 0; i < 20 && s.Map.TerrainAt(tree) == Terrain.Forest; i++)
        {
            Run(s, 301);
            koja.TakeOutput(G("timmer"));
        }
        Assert.Equal(Terrain.Clearing, s.Map.TerrainAt(tree));
        // Granen som planterades när den sista fälldes står nära kojan, och kan huggas igen.
        var forest = s.Map.AllTiles().Where(p => s.Map.TerrainAt(p) == Terrain.Forest).ToList();
        var planted = Assert.Single(forest);
        Assert.True(GameState.Distance(planted, koja.Entrance) <= 30);
        Assert.NotEqual(PathState.Trodden, s.Map.PathAt(planted));
    }

    [Fact]
    public void BerryPickerPicksWhatGrowsNearby()
    {
        var s = NewMatch();
        var stuga = Working(s, "barplockarstugan", 10, 10);
        s.Map.SetTerrain(new TilePoint(15, 15), Terrain.PlumTree);
        Run(s, 600);
        Assert.Equal(0, stuga.OutputCount(G("hallon")));
        Assert.Equal(2, stuga.OutputCount(G("kart")));

        s.Map.SetTerrain(new TilePoint(5, 5), Terrain.RaspberryThicket);
        stuga.TakeOutput(G("kart"));
        stuga.TakeOutput(G("kart"));
        Run(s, 1000);
        Assert.True(stuga.OutputCount(G("hallon")) > 0);
        Assert.True(stuga.OutputCount(G("kart")) > 0);
    }

    [Fact]
    public void FishermanNeedsTheLake()
    {
        var s = NewMatch();
        Assert.False(s.CanPlace(0, Data.Building("fiskeboden"), new TilePoint(10, 10)));
        s.Map.SetTerrain(new TilePoint(11, 9), Terrain.Water);
        var bod = Working(s, "fiskeboden", 10, 10);
        Run(s, 351);
        Assert.Equal(1, bod.OutputCount(G("abborre")));
    }

    [Fact]
    public void HenHouseLaysEggsUnlessHensAreOrdered()
    {
        var s = NewMatch();
        var hus = Working(s, "honshuset", 10, 10);
        Assert.Equal(0, hus.SelectedRecipe);
        for (int i = 0; i < 3; i++) hus.PutInput(G("vete"));
        Run(s, 301);
        Assert.Equal(2, hus.OutputCount(G("agg")));
        Assert.Equal(0, hus.OutputCount(G("hona")));
    }

    [Fact]
    public void MilkStandTradesMilkForCoffeeEveryMorningButSunday()
    {
        var s = NewMatch();
        s.Map.SetTerrain(new TilePoint(20, 31), Terrain.Road);
        Assert.False(s.CanPlace(0, Data.Building("mjolkpallen"), new TilePoint(20, 30)), "bara Storgården");
        var pall = Working(s, "mjolkpallen", 20, 30, player: 1);
        for (int i = 0; i < 4; i++) pall.PutInput(G("mjolk"));

        int day = GameClock.WeekTicks / 7;
        Run(s, day - 2);
        Assert.Equal(1, pall.OutputCount(G("kaffe")));
        Run(s, day);
        Assert.Equal(2, pall.OutputCount(G("kaffe")));
        Assert.Equal(0, pall.InputCount(G("mjolk")));

        // Söndag: ingen mejeribil.
        while (!GameClock.IsSunday(s.TickCount)) Run(s, 1);
        for (int i = 0; i < 2; i++) pall.PutInput(G("mjolk"));
        Run(s, day - 10);
        Assert.Equal(2, pall.OutputCount(G("kaffe")));
        Run(s, 25);
        Assert.Equal(3, pall.OutputCount(G("kaffe")));
    }
}

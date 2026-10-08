using Hallonkriget.Sim.Buildings;
using Hallonkriget.Sim.Commands;
using Hallonkriget.Sim.Map;
using Hallonkriget.Sim.Military;
using Hallonkriget.Sim.People;

namespace Hallonkriget.Sim.Tests.Military;

/// <summary>Gärdsgård, vedtrave, hundkoja, råttfällor, att ta en byggnad och segern, designdokumentet (Strid).</summary>
public class DefenceTests
{
    private static Person Soldier(Farm farm, byte owner, string unit, int x, int y) =>
        farm.State.SpawnSoldier(owner, farm.Data.Unit(unit), new TilePoint(x, y));

    /// <summary>En vägg av gärdsgård tvärs över kartan vid x.</summary>
    private static List<Building> Wall(Farm farm, int x, int height)
    {
        var list = new List<Building>();
        for (int y = 0; y < height; y++) list.Add(farm.PlaceDone("gardsgard", x, y));
        return list;
    }

    [Fact]
    public void AStoneWallStopsRakesButNotTheTractor()
    {
        var farm = new Farm(width: 40, height: 12);
        var wall = Wall(farm, 20, 12);
        var rake = Soldier(farm, 1, "rafsbrigaden", 30, 6);
        farm.Run(2);
        farm.Command(CommandType.MoveGroup, rake.Group, 10, 6, player: 1);
        farm.Run(600);
        Assert.True(rake.Tile.X > 20, "räfsan ska inte komma förbi gärdsgården");
        Assert.All(wall, w => Assert.Equal(BuildingStage.Done, w.Stage));

        var tractor = Soldier(farm, 1, "traktorn", 30, 4);
        farm.Run(2);
        farm.Command(CommandType.MoveGroup, tractor.Group, 10, 4, player: 1);
        farm.RunUntil(() => wall.Any(w => w.Stage == BuildingStage.Ruined), 2000, "traktorn har rammat sönder gärdsgården");
        farm.RunUntil(() => tractor.Tile.X < 20, 1000, "traktorn kommer igenom");
    }

    [Fact]
    public void LaborersMendTheWall()
    {
        var farm = new Farm(start: new TilePoint(5, 5));
        var wall = farm.PlaceDone("gardsgard", 12, 12);
        wall.Damage = 30;
        farm.RunUntil(() => wall.Damage == 0, 1000, "gärdsgården är lagad");
    }

    [Fact]
    public void TheWoodpileThrowsLogsWhileThereIsWood()
    {
        var farm = new Farm(start: new TilePoint(5, 5));
        var pile = farm.PlaceDone("vedtraven", 14, 12);
        farm.State.SpawnPerson(0, PersonRole.Worker, pile.Entrance, "vedkastare");
        farm.RunUntil(() => pile.HasWorker, 100, "vedkastaren på plats");
        farm.Put(pile, "ved", 2);
        var rake = Soldier(farm, 1, "rafsbrigaden", 18, 12);
        rake.MoodTimer = 10_000;
        farm.Run(60);
        // Två vedträn à 5 - 1, sedan är veden slut.
        Assert.Equal(60 - 8, rake.Mood);
        Assert.Equal(0, pile.InputCount(farm.G("ved")));
    }

    [Fact]
    public void TheChainedDogBitesAndTorpetHasOnlyOne()
    {
        var farm = new Farm(start: new TilePoint(5, 5));
        var house = farm.PlaceDone("hundkojan", 14, 12);
        Assert.False(farm.State.CanPlace(0, farm.Data.Building("hundkojan"), new TilePoint(18, 12)));
        var hen = Soldier(farm, 1, "honschocktruppen", 16, 12);
        farm.RunUntil(() => farm.State.People.All(p => p.Id != hen.Id), 200, "hönan har gett upp för hunden");
    }

    [Fact]
    public void ARatTrapCatchesTheFirstEnemy()
    {
        var farm = new Farm(start: new TilePoint(5, 5));
        farm.Command(CommandType.PlaceTrap, 14, 14);
        farm.RunUntil(() => farm.State.Traps.Single().Armed, 2000, "fällan är gillrad");
        var rake = Soldier(farm, 1, "rafsbrigaden", 20, 14);
        rake.MoodTimer = 10_000;
        farm.Run(2);
        farm.Command(CommandType.MoveGroup, rake.Group, 10, 14, player: 1);
        farm.RunUntil(() => farm.State.Traps.Count == 0, 600, "fällan slår igen");
        Assert.Equal(60 - 15, rake.Mood);
    }

    [Fact]
    public void AGroupTakesABuildingItStandsAtForTwentySeconds()
    {
        var farm = new Farm(start: new TilePoint(5, 5));
        var mill = farm.PlaceDone("kvarnen", 14, 5);
        farm.Put(mill, "vete", 3);
        farm.RunUntil(() => mill.HasWorker || farm.State.TickCount > 50, 100, "");
        var men = Enumerable.Range(0, 3).Select(i => Soldier(farm, 1, "hogaffelgardet", 30 + i, 20)).ToList();
        farm.Run(2);
        farm.Command(CommandType.AttackBuilding, men[0].Group, mill.Id, player: 1);
        farm.RunUntil(() => mill.IsTaken, 1500, "kvarnen är tagen");
        Assert.Equal(1, mill.TakenBy);
        Assert.Equal(0, mill.InputCount(farm.G("vete")));
        Assert.Equal(1, farm.State.Players[1].BuildingsTaken);

        // När fienden har gått och en egen soldat kommer blir den ägarens igen.
        farm.Command(CommandType.MoveGroup, men[0].Group, 35, 25, player: 1);
        farm.Run(300);
        Assert.True(mill.IsTaken);
        var own = Soldier(farm, 0, "rafsbrigaden", 13, 10);
        farm.Run(1);
        farm.Command(CommandType.MoveGroup, own.Group, 13, 9, player: 0);
        farm.RunUntil(() => !mill.IsTaken, 1500, "kvarnen är fri");
    }

    [Fact]
    public void TakingTheHouseWithNoSoldiersLeftWinsTheMatch()
    {
        var farm = new Farm(start: new TilePoint(5, 5));
        // Gubben är spelare 0:s enda soldat. Han får ge upp först.
        var hero = farm.State.People.Single(p => p.Role == PersonRole.Soldier);
        var men = Enumerable.Range(0, 3).Select(i => Soldier(farm, 1, "hogaffelgardet", 20 + i, 20)).ToList();
        farm.Run(2);
        farm.Command(CommandType.AttackBuilding, men[0].Group, farm.Home.Id, player: 1);
        farm.RunUntil(() => farm.State.Winner == 1, 6000, "Storgården har vunnit");
        Assert.True(farm.State.Players[0].Defeated);
        Assert.True(farm.Home.IsTaken);
    }
}

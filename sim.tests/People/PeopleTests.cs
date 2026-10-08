using Hallonkriget.Sim.Buildings;
using Hallonkriget.Sim.Map;
using Hallonkriget.Sim.People;

namespace Hallonkriget.Sim.Tests.People;

public class PeopleTests
{
    [Fact]
    public void MatchStartsWithThreeCarriersAndTwoLaborers()
    {
        var farm = new Farm(start: new TilePoint(5, 5));
        Assert.Equal(3, farm.State.People.Count(p => p.Role == PersonRole.Carrier && p.Owner == 0));
        Assert.Equal(2, farm.State.People.Count(p => p.Role == PersonRole.Laborer && p.Owner == 0));
        Assert.All(farm.State.People, p => Assert.Equal(farm.Home.Entrance, p.Tile));
        Assert.DoesNotContain(farm.State.People, p => p.Owner == 1); // spelare 2 har ingen start
    }

    [Theory]
    [InlineData(true, 100)]   // 12 rutor stig: 1,2 rutor per sekund
    [InlineData(false, 200)]  // samma sträcka på gräs tar dubbelt så lång tid
    public void WalksAtTheSpeedOfTheGround(bool path, int expectedTicks)
    {
        var farm = new Farm();
        if (path) farm.LayPath(new TilePoint(2, 10), new TilePoint(14, 10));
        var p = farm.State.SpawnPerson(0, PersonRole.Worker, new TilePoint(2, 10), "ingen");
        var target = new TilePoint(14, 10);
        WalkTo(farm.State, p, target);
        int ticks = farm.RunUntil(() => p.Tile == target && !p.IsMoving, 1000, "gå");
        Assert.InRange(ticks, expectedTicks - 2, expectedTicks + 2);
    }

    [Fact]
    public void MeetingOnANarrowPathMakesAQueueButNoJam()
    {
        var farm = new Farm();
        farm.LayPath(new TilePoint(2, 10), new TilePoint(20, 10));
        // En vägg av vatten runt stigen, så att ingen kan gå runt.
        for (int x = 1; x <= 21; x++)
        {
            farm.State.Map.SetTerrain(new TilePoint(x, 9), Terrain.Water);
            farm.State.Map.SetTerrain(new TilePoint(x, 11), Terrain.Water);
        }

        // Fyra från vänster och fyra från höger, på samma smala stig.
        var people = new List<(Person P, TilePoint Goal)>();
        for (int i = 0; i < 4; i++)
        {
            people.Add((farm.State.SpawnPerson(0, PersonRole.Worker, new TilePoint(2, 10), "ingen"), new TilePoint(20, 10)));
            people.Add((farm.State.SpawnPerson(0, PersonRole.Worker, new TilePoint(20, 10), "ingen"), new TilePoint(2, 10)));
        }
        foreach (var (p, goal) in people) WalkTo(farm.State, p, goal);

        // Mötet ger kö, men ingen fastnar för alltid.
        int waited = 0;
        farm.RunUntil(() =>
        {
            waited += people.Count(q => q.P.WaitTicks > 0);
            return people.All(q => q.P.Tile == q.Goal && !q.P.IsMoving);
        }, 3000, "alla fram");
        Assert.True(waited > 0, "ingen behövde vänta");
    }

    [Fact]
    public void LaborerTreadsAPlannedPath()
    {
        var farm = new Farm(start: new TilePoint(5, 5));
        farm.PlanPath(new TilePoint(6, 9), new TilePoint(12, 9));
        Assert.Equal(PathState.Planned, farm.State.Map.PathAt(new TilePoint(12, 9)));
        farm.RunUntil(() => Enumerable.Range(6, 7).All(x => farm.State.Map.PathAt(new TilePoint(x, 9)) == PathState.Trodden),
            2000, "stigen upptrampad");
    }

    [Fact]
    public void PathsCannotBePlannedOnBuildingsOrWater()
    {
        var farm = new Farm(start: new TilePoint(5, 5));
        farm.State.Map.SetTerrain(new TilePoint(20, 20), Terrain.Water);
        farm.PlanPath(new TilePoint(6, 6), new TilePoint(6, 6));
        farm.PlanPath(new TilePoint(20, 20), new TilePoint(20, 20));
        Assert.Equal(PathState.None, farm.State.Map.PathAt(new TilePoint(6, 6)));
        Assert.Equal(PathState.None, farm.State.Map.PathAt(new TilePoint(20, 20)));
    }

    [Fact]
    public void LaborersBuildWhatTheMaterialAllows()
    {
        var farm = new Farm();
        var site = farm.Place("vedboden", 10, 10);
        var a = farm.State.SpawnPerson(0, PersonRole.Laborer, new TilePoint(10, 14));
        var b = farm.State.SpawnPerson(0, PersonRole.Laborer, new TilePoint(10, 14));
        var c = farm.State.SpawnPerson(0, PersonRole.Laborer, new TilePoint(10, 14));
        farm.Run(50);
        Assert.Equal(0, site.WorkDone); // inget material, inget arbete

        foreach (var cost in site.Def.Cost)
            for (int i = 0; i < cost.Count; i++) site.DeliverMaterial(cost.Good);
        farm.Run(30);
        Assert.Equal(2, new[] { a, b, c }.Count(p => p.Job is PersonJob.ToSite or PersonJob.Building));

        // Två hantlangare bygger dubbelt så fort: 600 tick arbete på ungefär 300.
        farm.RunUntil(() => site.Stage == BuildingStage.Done, 400, "bygget klart");
        farm.Run(2);
        Assert.All(new[] { a, b, c }, p => Assert.Equal(PersonJob.Idle, p.Job));
    }

    [Fact]
    public void WorkerGoesToItsBuilding()
    {
        var farm = new Farm();
        var vedboden = farm.Place("vedboden", 10, 10);
        var wrong = farm.State.SpawnPerson(0, PersonRole.Worker, new TilePoint(2, 2), "bagare");
        var right = farm.State.SpawnPerson(0, PersonRole.Worker, new TilePoint(2, 2), "vedhuggare");
        farm.Run(20);
        Assert.Equal(PersonJob.Idle, right.Job); // byggplatsen behöver ingen arbetare än

        foreach (var cost in vedboden.Def.Cost)
            for (int i = 0; i < cost.Count; i++) vedboden.DeliverMaterial(cost.Good);
        while (vedboden.Stage == BuildingStage.Construction) vedboden.AddWork();

        farm.RunUntil(() => vedboden.HasWorker, 500, "vedhuggaren på plats");
        Assert.True(right.Inside);
        Assert.Equal(PersonJob.Idle, wrong.Job);
    }

    [Fact]
    public void BuildingInTheWayMakesPeopleWalkAround()
    {
        var farm = new Farm();
        var p = farm.State.SpawnPerson(0, PersonRole.Worker, new TilePoint(2, 10), "ingen");
        WalkTo(farm.State, p, new TilePoint(20, 10));
        farm.Run(10);
        farm.Place("sagboden", 10, 9);
        farm.RunUntil(() => p.Tile == new TilePoint(20, 10) && !p.IsMoving, 1000, "fram runt sågboden");
    }

    /// <summary>Skickar iväg en person direkt, som ett jobb skulle göra.</summary>
    private static void WalkTo(GameState state, Person p, TilePoint goal)
    {
        var path = new List<TilePoint>();
        Assert.True(new Pathfinder(state.Map).FindPath(p.Tile, goal, MoveClass.Foot, path));
        p.Path.Clear();
        p.Path.AddRange(path);
        p.PathIndex = 0;
        p.StepProgress = p.StepTotal = 0;
    }
}

using Hallonkriget.Sim.Buildings;
using Hallonkriget.Sim.Commands;
using Hallonkriget.Sim.Map;
using Hallonkriget.Sim.People;

namespace Hallonkriget.Sim.Tests.People;

public class MoodTests
{
    /// <summary>En gård utan start: inga personer utom de testet skapar.</summary>
    private static Farm Empty() => new(width: 30, height: 20);

    [Fact]
    public void MoodDropsOnePointEverySixSeconds()
    {
        var farm = Empty();
        var p = farm.State.SpawnPerson(0, PersonRole.Worker, new TilePoint(3, 3), "ingen");
        Assert.Equal(100, p.Mood);
        farm.Run(60);
        Assert.Equal(99, p.Mood);
        farm.Run(60 * 69);
        Assert.Equal(30, p.Mood); // sju minuter till 30, då man går och äter
    }

    [Fact]
    public void HungryPersonEatsAtTheKafferep()
    {
        var farm = Empty();
        var table = farm.PlaceDone("kafferepet", 10, 5);
        farm.Put(table, "knackebrod");
        var p = farm.State.SpawnPerson(0, PersonRole.Worker, new TilePoint(3, 10), "ingen");
        p.Mood = 31;
        farm.Run(10);
        Assert.Equal(PersonJob.Idle, p.Job); // inte hungrig än
        p.Mood = 30;
        farm.RunUntil(() => table.InputCount(farm.G("knackebrod")) == 0, 300, "knäckebrödet uppätet");
        Assert.Equal(table.Entrance, p.Tile);
        Assert.InRange(p.Mood, 55, 60); // +30, minus det som gick åt på vägen
    }

    [Fact]
    public void EatsTheBestFirstAndOnePortionOfEachKindUntilFull()
    {
        var farm = Empty();
        var table = farm.PlaceDone("kafferepet", 10, 5);
        farm.Put(table, "knackebrod", 2);
        farm.Put(table, "korv");
        farm.Put(table, "svagdricka");
        var p = farm.State.SpawnPerson(0, PersonRole.Worker, table.Entrance, "ingen");
        p.Mood = 30;
        farm.Run(2);
        // Korv (+45) först, sedan ett knäckebröd (+30). Då är humöret fullt och svagdrickan står kvar.
        Assert.Equal(100, p.Mood);
        Assert.Equal(0, table.InputCount(farm.G("korv")));
        Assert.Equal(1, table.InputCount(farm.G("knackebrod")));
        Assert.Equal(1, table.InputCount(farm.G("svagdricka")));
    }

    [Fact]
    public void JamCountsOnlyWithPancakes()
    {
        var farm = Empty();
        var table = farm.PlaceDone("kafferepet", 10, 5);
        farm.Put(table, "sylt");
        var p = farm.State.SpawnPerson(0, PersonRole.Worker, table.Entrance, "ingen");
        p.Mood = 20;
        farm.Run(2);
        Assert.Equal(PersonJob.Idle, p.Job); // bara sylt på bordet: inget att äta
        Assert.Equal(1, table.InputCount(farm.G("sylt")));
        farm.Put(table, "pannkakor");
        farm.Run(2);
        Assert.Equal(20 + 60 + 15, p.Mood);
        Assert.Equal(0, table.InputCount(farm.G("sylt")));
    }

    [Fact]
    public void CoffeeIsBlockedOnTheTableUntilThePlayerOpensIt()
    {
        var farm = Empty();
        var table = farm.PlaceDone("kafferepet", 10, 5);
        Assert.Equal(0, table.InputSpace(farm.G("kaffe")));
        Assert.Equal(40, table.InputSpace(farm.G("knackebrod")));
        farm.Command(CommandType.BlockGood, table.Id, farm.G("kaffe"), 0);
        Assert.Equal(40, table.InputSpace(farm.G("kaffe")));
        farm.Command(CommandType.BlockGood, table.Id, farm.G("knackebrod"), 1);
        Assert.Equal(0, table.InputSpace(farm.G("knackebrod")));
    }

    [Fact]
    public void TheTableHoldsFortyPortionsInTotal()
    {
        var farm = Empty();
        var table = farm.PlaceDone("kafferepet", 10, 5);
        farm.Put(table, "knackebrod", 30);
        farm.Put(table, "korv", 10);
        Assert.False(table.PutInput(farm.G("abborre")));
    }

    [Fact]
    public void WithoutFoodPeopleKeepWorkingThenGiveUpRestAndComeBack()
    {
        var farm = new Farm(width: 30, height: 20, start: new TilePoint(5, 5));
        var home = farm.Home;
        var p = farm.State.SpawnPerson(0, PersonRole.Worker, new TilePoint(12, 15), "ingen");
        p.Mood = 1;
        p.MoodTimer = 1;
        farm.Run(1);
        Assert.Equal(PersonJob.ToHome, p.Job);
        Assert.Equal(1, farm.State.Players[0].GaveUp);
        farm.RunUntil(() => p.Job == PersonJob.Resting, 600, "hemma");
        Assert.Equal(home.Entrance, p.Tile);
        Assert.True(p.Inside);
        int rest = farm.RunUntil(() => p.Job != PersonJob.Resting, 3000, "utvilad");
        Assert.InRange(rest, 1790, 1800); // tre minuter
        Assert.Equal(100, p.Mood);
        Assert.False(p.Inside);
    }

    [Fact]
    public void CarrierThatGivesUpTakesTheGoodsHomeAndReservationsBalance()
    {
        var farm = new Farm(width: 30, height: 20, start: new TilePoint(5, 5));
        var site = farm.Place("vedboden", 20, 12);
        farm.RunUntil(() => farm.State.People.Any(p => p.Carrying >= 0), 600, "någon bär");
        var carrier = farm.State.People.First(p => p.Carrying >= 0);
        carrier.Mood = 1;
        carrier.MoodTimer = 1;
        int good = carrier.Carrying;
        int atHome = farm.Home.OutputCount(good);
        farm.Run(1);
        Assert.Equal(PersonJob.ToHome, carrier.Job);
        Assert.DoesNotContain(farm.State.Deliveries, d => d.Carrier == carrier.Id);
        foreach (var b in farm.State.Buildings)
        for (int g = 0; g < farm.Data.Goods.Count; g++)
            Assert.Equal(farm.State.Deliveries.Count(d => d.To == b.Id && d.Good == g), b.Incoming[g]);
        // Varan följer med hem och läggs i stugan.
        farm.RunUntil(() => carrier.Job == PersonJob.Resting, 600, "hemma");
        Assert.Equal(-1, carrier.Carrying);
        Assert.Equal(atHome + 1, farm.Home.OutputCount(good));
        // Bygget blir ändå klart: de andra bär det som behövs.
        farm.RunUntil(() => site.Stage == BuildingStage.Done, 5000, "vedboden byggd");
    }

    [Fact]
    public void WorkerEatsBetweenCyclesAndReturnsToTheSameWorkplace()
    {
        var farm = Empty();
        var well = farm.PlaceDone("brunnen", 4, 4);
        var otherWell = farm.PlaceDone("brunnen", 8, 4);
        var table = farm.PlaceDone("kafferepet", 14, 4);
        farm.Put(table, "knackebrod");
        var p = farm.State.SpawnPerson(0, PersonRole.Worker, well.Entrance, "vattenbarare");
        farm.RunUntil(() => well.HasWorker, 100, "vattenbäraren på plats");
        Assert.Equal(p.Id, well.WorkerId);
        p.Mood = 30;
        farm.RunUntil(() => p.Job == PersonJob.ToEat, 400, "går och äter");
        Assert.Equal(-1, well.CurrentRecipe); // gick mellan två omgångar
        farm.RunUntil(() => p.Mood > 50 && well.HasWorker, 1000, "tillbaka efter maten");
        Assert.Equal(-1, otherWell.WorkerId);
    }
}

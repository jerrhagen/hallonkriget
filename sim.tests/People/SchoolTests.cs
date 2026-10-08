using Hallonkriget.Sim.Buildings;
using Hallonkriget.Sim.Commands;
using Hallonkriget.Sim.Map;
using Hallonkriget.Sim.People;

namespace Hallonkriget.Sim.Tests.People;

public class SchoolTests
{
    private static int Count(Farm farm, string profession) => farm.State.People.Count(p => p.Profession == profession);

    [Fact]
    public void MatchStartsWithTheStartCrew()
    {
        // Beslut 2026-10-08: tre bärare, två hantlangare och sex arbetare för de första husen.
        var farm = new Farm(start: new TilePoint(5, 5));
        Assert.Equal(11, farm.State.People.Count(p => p.Owner == 0));
        foreach (var job in new[] { "skogshuggare", "sagare", "vedhuggare", "stenrojare", "bonde", "vattenbarare" })
            Assert.Equal(1, Count(farm, job));
    }

    [Fact]
    public void TrainsAWorkerForCoffeeAndATool()
    {
        var farm = new Farm(start: new TilePoint(5, 5));
        var school = farm.PlaceDone("bygdegarden", 12, 5);
        farm.PlaceDone("boden", 16, 5); // tio sovplatser till
        farm.Command(CommandType.Train, school.Id, farm.Data.ProfessionIndex("bagare"));
        Assert.Single(school.TrainingQueue);

        int ticks = farm.RunUntil(() => Count(farm, "bagare") == 1, 2000, "en bagare");
        Assert.InRange(ticks, 300, 1000); // bärarna hämtar kaffe och verktyg, sedan 30 sekunder
        Assert.Empty(school.TrainingQueue);
        Assert.Equal(1, farm.Home.OutputCount(farm.G("kaffe")));
        Assert.Equal(1, farm.Home.OutputCount(farm.G("verktyg")));
        var baker = farm.State.People.Single(p => p.Profession == "bagare");
        Assert.Equal(PersonRole.Worker, baker.Role);
    }

    [Fact]
    public void NoTrainingWithoutBeds()
    {
        // Stugan har 4 sovplatser, bygdegården 1, och gården har redan 11 personer.
        var farm = new Farm(start: new TilePoint(5, 5));
        var school = farm.PlaceDone("bygdegarden", 12, 5);
        Assert.Equal(5, farm.State.Beds(0));
        farm.Command(CommandType.Train, school.Id, farm.Data.ProfessionIndex("barare"));
        farm.Run(1500);
        Assert.Equal(-1, school.Training);
        Assert.Equal(11, farm.State.People.Count);
    }

    [Fact]
    public void TwoCupsOfSurrogateCountAsOneCupOfCoffee()
    {
        var farm = new Farm(width: 30, height: 20);
        var school = farm.PlaceDone("bygdegarden", 10, 5);
        farm.Command(CommandType.Train, school.Id, farm.Data.ProfessionIndex("bagare"));
        farm.Put(school, "surrogatkaffe", 1);
        farm.Put(school, "verktyg");
        farm.Run(20);
        Assert.Equal(-1, school.Training); // en kopp surrogat räcker inte
        farm.Put(school, "surrogatkaffe", 1);
        farm.RunUntil(() => Count(farm, "bagare") == 1, 400, "en bagare");
        Assert.Equal(0, school.InputCount(farm.G("surrogatkaffe")));
    }

    [Fact]
    public void CarriersAndLaborersNeedNoTool()
    {
        var farm = new Farm(width: 30, height: 20);
        var school = farm.PlaceDone("bygdegarden", 10, 5);
        farm.PlaceDone("boden", 14, 5);
        farm.Command(CommandType.Train, school.Id, farm.Data.ProfessionIndex("barare"));
        farm.Command(CommandType.Train, school.Id, farm.Data.ProfessionIndex("hantlangare"));
        Assert.Equal(0, school.InputSpace(farm.G("verktyg"))); // ingen i kön behöver verktyg
        farm.Put(school, "kaffe", 2);
        farm.RunUntil(() => farm.State.People.Count == 2, 700, "två nya");
        Assert.Contains(farm.State.People, p => p.Role == PersonRole.Carrier);
        Assert.Contains(farm.State.People, p => p.Role == PersonRole.Laborer);
    }

    [Fact]
    public void TheQueueHoldsSixAndTheLastCanBeCancelled()
    {
        var farm = new Farm(width: 30, height: 20);
        var school = farm.PlaceDone("bygdegarden", 10, 5);
        for (int i = 0; i < 8; i++) farm.Command(CommandType.Train, school.Id, farm.Data.ProfessionIndex("barare"));
        Assert.Equal(6, school.TrainingQueue.Count);
        farm.Command(CommandType.CancelTraining, school.Id);
        Assert.Equal(5, school.TrainingQueue.Count);
        farm.Command(CommandType.Train, school.Id, 999); // okänt yrke
        Assert.Equal(5, school.TrainingQueue.Count);
    }

    [Fact]
    public void TheSchoolAsksOnlyForWhatTheQueueNeeds()
    {
        var farm = new Farm(width: 30, height: 20);
        var school = farm.PlaceDone("bygdegarden", 10, 5);
        Assert.Equal(0, school.InputSpace(farm.G("kaffe"))); // tom kö: begär inget
        farm.Command(CommandType.Train, school.Id, farm.Data.ProfessionIndex("bagare"));
        farm.Command(CommandType.Train, school.Id, farm.Data.ProfessionIndex("mjolnare"));
        // Ett verktyg i taget, och kaffe eller surrogat för två.
        Assert.Equal(1, school.InputSpace(farm.G("verktyg")));
        Assert.Equal(2, school.InputSpace(farm.G("kaffe")));
        Assert.Equal(4, school.InputSpace(farm.G("surrogatkaffe")));
    }
}

using Hallonkriget.Sim.Buildings;
using Hallonkriget.Sim.Map;
using Hallonkriget.Sim.People;

namespace Hallonkriget.Sim.Tests.Economy;

public class DeliveryTests
{
    [Fact]
    public void CarriersBringMaterialAndLaborersBuild()
    {
        // Allt från start: stugan med brädor och sten, tre bärare och två hantlangare.
        var farm = new Farm(start: new TilePoint(5, 5));
        var site = farm.Place("vedboden", 12, 6);
        int ticks = farm.RunUntil(() => site.Stage == BuildingStage.Done, 3000, "vedboden byggd");

        Assert.Equal(10 - 3, farm.Home.OutputCount(farm.G("brador")));
        Assert.Equal(6 - 1, farm.Home.OutputCount(farm.G("sten")));
        Assert.Empty(farm.State.Deliveries);
        // 60 sekunders arbete, delat på två hantlangare, plus att bära dit materialet.
        Assert.InRange(ticks, 300, 1200);
    }

    [Fact]
    public void InputsComeFromStorageAndOutputsGoBack()
    {
        var farm = new Farm(start: new TilePoint(5, 5));
        var vedboden = farm.Place("vedboden", 12, 6);
        farm.RunUntil(() => vedboden.Stage == BuildingStage.Done, 3000, "vedboden byggd");

        farm.Home.PutInput(farm.G("timmer"));
        farm.Home.PutInput(farm.G("timmer"));
        farm.State.SpawnPerson(0, PersonRole.Worker, farm.Home.Entrance, "vedhuggare");

        // Två timmer blir sex ved, som bärs tillbaka till stugan. Stugan tar bara emot 7 ved som överskott
        // (en fjärdedel av sina 30 platser), så resten blir kvar i vedboden.
        farm.RunUntil(() => farm.State.Players[0].Produced[farm.G("ved")] == 6 && farm.State.Deliveries.Count == 0, 3000, "veden gjord och buren");
        Assert.Equal(0, farm.Home.OutputCount(farm.G("timmer")));
        Assert.Equal(7, farm.Home.OutputCount(farm.G("ved")));
        Assert.Equal(3, vedboden.OutputCount(farm.G("ved")));
    }

    [Fact]
    public void ProducerIsPreferredOverStorage()
    {
        var farm = new Farm(start: new TilePoint(5, 5));
        var vedboden = farm.Place("vedboden", 12, 6);
        var bageri = farm.Place("bagarstugan", 16, 6);
        farm.RunUntil(() => vedboden.Stage == BuildingStage.Done && bageri.Stage == BuildingStage.Done, 6000, "båda byggda");

        farm.RunUntil(() => bageri.InputCount(farm.G("ved")) == 4, 3000, "stugans ved i bagarstugan");

        // Vedboden gör 3 ved. Bagarstugan begär en till, resten finns ingen begäran för och går till stugan.
        vedboden.HasWorker = true;
        vedboden.PutInput(farm.G("timmer"));
        int direct = 0;
        farm.RunUntil(() =>
        {
            direct = Math.Max(direct, farm.State.Deliveries.Count(d => d.From == vedboden.Id && d.To == bageri.Id));
            return bageri.InputCount(farm.G("ved")) == 5 && farm.Home.OutputCount(farm.G("ved")) == 2;
        }, 3000, "veden fördelad");
        Assert.Equal(1, direct);
    }

    [Fact]
    public void WithoutCarriersNothingMoves()
    {
        var farm = new Farm(start: new TilePoint(5, 5));
        foreach (var p in farm.State.People.Where(p => p.Role == PersonRole.Carrier).ToList())
            p.Job = PersonJob.AtWork; // upptagna med annat
        var site = farm.Place("vedboden", 12, 6);
        farm.Run(500);
        Assert.Equal(3, site.MaterialNeeded(farm.G("brador")));
        Assert.Empty(farm.State.Deliveries);
    }

    [Fact]
    public void ReservationsBalanceWhenEverythingIsDone()
    {
        var farm = new Farm(start: new TilePoint(5, 5));
        var a = farm.Place("vedboden", 12, 6);
        var b = farm.Place("brunnen", 12, 10);
        var c = farm.Place("stenrojarboden", 16, 6);
        farm.RunUntil(() => new[] { a, b, c }.All(x => x.Stage == BuildingStage.Done), 8000, "alla byggda");
        farm.Run(100);
        foreach (var building in farm.State.Buildings)
        for (int g = 0; g < farm.Data.Goods.Count; g++)
        {
            Assert.Equal(0, building.Incoming[g]);
            Assert.Equal(0, building.Outgoing[g]);
        }
    }

    [Fact]
    public void StorageIsNotRefilledFromStorage()
    {
        var farm = new Farm(start: new TilePoint(5, 5));
        var boden = farm.Place("boden", 14, 6);
        farm.RunUntil(() => boden.Stage == BuildingStage.Done, 3000, "boden byggd");
        farm.Run(200);
        Assert.Equal(0, boden.StoredTotal);
    }
}

using Hallonkriget.Sim.Map;
using Hallonkriget.Sim.People;

namespace Hallonkriget.Sim.Tests.People;

/// <summary>Designdokumentet, Mat och humör: pannkakor ger fart, svagdricka arbetstakt, sylt anfall. Tre minuter var.</summary>
public class FoodBonusTests
{
    private static Farm Empty() => new(width: 40, height: 20);

    [Fact]
    public void PancakesMakeYouWalkTenPercentFaster()
    {
        var farm = Empty();
        var table = farm.PlaceDone("kafferepet", 10, 5);
        farm.Put(table, "pannkakor");
        var p = farm.State.SpawnPerson(0, PersonRole.Worker, table.Entrance, "ingen");
        p.Mood = 30;
        farm.Run(2);
        Assert.InRange(p.SpeedBonusTicks, 1798, 1800);
        Assert.Equal(GameState.WalkSpeed * 110 / 100, farm.State.EffectiveSpeed(p));
        farm.Run(1800);
        Assert.Equal(0, p.SpeedBonusTicks);
        Assert.Equal(GameState.WalkSpeed, farm.State.EffectiveSpeed(p));
    }

    [Fact]
    public void JamGivesTheAttackBonus()
    {
        var farm = Empty();
        var table = farm.PlaceDone("kafferepet", 10, 5);
        farm.Put(table, "pannkakor");
        farm.Put(table, "sylt");
        var p = farm.State.SpawnPerson(0, PersonRole.Worker, table.Entrance, "ingen");
        p.Mood = 10;
        farm.Run(2);
        Assert.InRange(p.AttackBonusTicks, 1798, 1800);
    }

    [Fact]
    public void SmallBeerMakesTheWorkerTenPercentFaster()
    {
        // Två vedbodar, 20 sekunder per omgång, men vedhuggaren i den ena har druckit svagdricka.
        var farm = Empty();
        var plain = farm.PlaceDone("vedboden", 10, 10);
        var bonus = farm.PlaceDone("vedboden", 30, 10);
        farm.State.SpawnPerson(0, PersonRole.Worker, plain.Entrance, "vedhuggare");
        var b = farm.State.SpawnPerson(0, PersonRole.Worker, bonus.Entrance, "vedhuggare");
        b.WorkBonusTicks = 1800;
        farm.RunUntil(() => plain.HasWorker && bonus.HasWorker, 200, "vedhuggarna på plats");
        farm.Put(plain, "timmer");
        farm.Put(bonus, "timmer");

        int fast = farm.RunUntil(() => bonus.OutputCount(farm.G("ved")) > 0, 400, "ved med svagdricka");
        int slow = fast + farm.RunUntil(() => plain.OutputCount(farm.G("ved")) > 0, 400, "ved utan");
        Assert.InRange(slow, 199, 202);
        Assert.InRange(fast, 180, 184);
    }
}

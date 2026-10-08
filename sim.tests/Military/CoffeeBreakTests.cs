using Hallonkriget.Sim.Commands;
using Hallonkriget.Sim.Map;
using Hallonkriget.Sim.People;

namespace Hallonkriget.Sim.Tests.Military;

/// <summary>Kafferasten var tionde minut och kaffedrängen, designdokumentet (Mat och humör).</summary>
public class CoffeeBreakTests
{
    private const int Break = 6000;

    private static Person Soldier(Farm farm, byte owner, string unit, int x, int y)
    {
        var p = farm.State.SpawnSoldier(owner, farm.Data.Unit(unit), new TilePoint(x, y));
        p.MoodTimer = 100_000;
        return p;
    }

    private static void RunTo(Farm farm, int tick) => farm.Run(tick - farm.State.TickCount);

    [Fact]
    public void WithoutCoffeeTheGroupSitsDownAndSulks()
    {
        var farm = new Farm(width: 60, height: 20);
        var man = Soldier(farm, 0, "rafsbrigaden", 5, 5);
        RunTo(farm, Break - 10);
        farm.Command(CommandType.MoveGroup, man.Group, 50, 5);
        RunTo(farm, Break + 5);
        Assert.True(man.CoffeeDue);
        var at = man.Tile;
        RunTo(farm, Break + 600 + 5);
        Assert.Equal(60 - 20, man.Mood);
        RunTo(farm, Break + 1150);
        Assert.Equal(at, man.Tile); // en minut till på samma ställe
        farm.Run(300);
        Assert.NotEqual(at, man.Tile);
    }

    [Fact]
    public void TheCoffeeHandServesFromHisTray()
    {
        var farm = new Farm(width: 60, height: 20);
        var men = Enumerable.Range(0, 4).Select(i => Soldier(farm, 0, "hogaffelgardet", 20 + i, 10)).ToList();
        var hand = Soldier(farm, 0, "kaffedrangen", 10, 10);
        hand.TrayCoffee = 4;
        RunTo(farm, Break + 600 + 5);
        Assert.All(men, m => Assert.Equal(90, m.Mood));
        Assert.Equal(0, hand.TrayCoffee);
    }

    [Fact]
    public void TheCoffeeHandFillsHisTrayAtTheStoreAndFeedsTheHungry()
    {
        var farm = new Farm(start: new TilePoint(5, 5));
        var hand = Soldier(farm, 0, "kaffedrangen", 12, 12);
        var man = Soldier(farm, 0, "hogaffelgardet", 20, 14);
        man.Mood = 20;
        farm.RunUntil(() => man.Mood > 20, 1500, "drängen har bjudit på knäckebröd");
        Assert.Equal(20 + 30, man.Mood);
        Assert.Equal(farm.G("knackebrod"), hand.TrayFoodGood);
        Assert.Equal(2, hand.TrayCoffee); // stugans två koppar
    }
}

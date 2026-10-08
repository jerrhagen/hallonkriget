using Hallonkriget.Sim.Commands;
using Hallonkriget.Sim.Map;
using Hallonkriget.Sim.Military;
using Hallonkriget.Sim.People;

namespace Hallonkriget.Sim.Tests.Military;

/// <summary>Grupper, formationer, order och striden, designdokumentet (Enheter, Strid).</summary>
public class CombatTests
{
    /// <summary>En öppen äng utan gårdar, två spelare.</summary>
    private static Farm Field() => new(width: 50, height: 40);

    private static List<Person> Squad(Farm farm, byte owner, string unit, int count, int x, int y)
    {
        var u = farm.Data.Unit(unit);
        var list = new List<Person>();
        for (int i = 0; i < count; i++) list.Add(farm.State.SpawnSoldier(owner, u, new TilePoint(x + i % 5, y + i / 5)));
        farm.Run(2);
        return list;
    }

    private static Group GroupOf(Farm farm, Person p) => farm.State.Groups[p.Group];

    [Fact]
    public void AGroupMarchesIntoFormation()
    {
        var farm = Field();
        var men = Squad(farm, 0, "rafsbrigaden", 10, 5, 5);
        var g = GroupOf(farm, men[0]);
        Assert.Equal(10, g.Members.Count);
        farm.Command(CommandType.SetFormation, g.Id, 5);
        farm.Command(CommandType.MoveGroup, g.Id, 30, 10);
        Assert.Equal(2, g.Facing); // österut
        farm.RunUntil(() => g.Order == GroupOrder.Idle, 600, "gruppen framme");
        // Två rader om fem, mitten av främsta raden på (30,10), den andra raden bakom.
        var tiles = men.Select(m => m.Tile).ToHashSet();
        for (int dy = -2; dy <= 2; dy++)
        {
            Assert.Contains(new TilePoint(30, 10 + dy), tiles);
            Assert.Contains(new TilePoint(29, 10 + dy), tiles);
        }
    }

    [Fact]
    public void SplitAndMerge()
    {
        var farm = Field();
        var men = Squad(farm, 0, "rafsbrigaden", 10, 5, 5);
        var g = GroupOf(farm, men[0]);
        farm.Command(CommandType.SplitGroup, g.Id);
        Assert.Equal(5, g.Members.Count);
        var other = farm.State.Groups[^1];
        Assert.Equal(5, other.Members.Count);
        farm.Command(CommandType.MergeGroups, g.Id, other.Id);
        Assert.Equal(10, g.Members.Count);
        Assert.True(other.IsEmpty);
    }

    [Fact]
    public void ForksBeatRakes()
    {
        // Räfsan slår 4 mot försvar 4: 1 per slag. Högaffeln slår 6 mot 1: 5 per slag.
        var farm = Field();
        var rakes = Squad(farm, 0, "rafsbrigaden", 5, 10, 10);
        var forks = Squad(farm, 1, "hogaffelgardet", 5, 30, 10);
        farm.Command(CommandType.AttackGroup, rakes[0].Group, forks[0].Group);
        farm.RunUntil(() => rakes.All(r => r.Role != PersonRole.Soldier), 3000, "räfsbrigaden har gett upp");
        Assert.All(forks, f => Assert.Equal(PersonRole.Soldier, f.Role));
        Assert.True(forks.Sum(f => f.Mood) > 5 * 60);
        Assert.Equal(5, farm.State.Players[0].SoldiersLost);
        // De som gav upp är rekryter utan utrustning och går hem.
        Assert.All(rakes, r => Assert.Equal(PersonRole.Recruit, r.Role));
        Assert.True(GroupEmpty(farm, rakes[0]));
    }

    private static bool GroupEmpty(Farm farm, Person p) => farm.State.Groups.Where(g => g.Owner == p.Owner && g.Unit == farm.Data.Unit("rafsbrigaden").Index).All(g => g.IsEmpty);

    [Theory]
    [InlineData(19, 2)] // framifrån: 6 - 4
    [InlineData(21, 4)] // bakifrån: dubbelt
    public void AnAttackFromBehindHurtsTwiceAsMuch(int fromX, int damage)
    {
        var farm = Field();
        var target = farm.State.SpawnSoldier(1, farm.Data.Unit("hogaffelgardet"), new TilePoint(20, 20));
        farm.Run(1);
        farm.Command(CommandType.TurnGroup, target.Group, 6, player: 1); // mot väster
        target.MoodTimer = 10_000;
        var attacker = farm.State.SpawnSoldier(0, farm.Data.Unit("hogaffelgardet"), new TilePoint(fromX, 20));
        farm.RunUntil(() => target.Mood < 90, 100, "första slaget");
        Assert.Equal(90 - damage, target.Mood);
    }

    [Fact]
    public void SlingshotsShootFromAfarAndRunOutOfShots()
    {
        var farm = Field();
        var boys = Squad(farm, 0, "slangbellepojkarna", 5, 10, 10);
        var target = Squad(farm, 1, "hogaffelgardet", 20, 15, 8);
        Assert.All(boys, b => Assert.Equal(10, b.Shots));
        farm.RunUntil(() => boys.All(b => b.Shots == 0), 2000, "skotten slut");
        Assert.True(target.Sum(t => 90 - t.Mood) > 0, "något skott ska ha träffat");
    }

    [Fact]
    public void GeeseScareHensTwiceAsMuch()
    {
        var farm = Field();
        Squad(farm, 0, "gasskvadronen", 1, 10, 10);
        var hen = farm.State.SpawnSoldier(1, farm.Data.Unit("honschocktruppen"), new TilePoint(12, 10));
        var rake = farm.State.SpawnSoldier(1, farm.Data.Unit("rafsbrigaden"), new TilePoint(12, 11));
        int henBefore = hen.Mood, rakeBefore = rake.Mood;
        farm.Run(10);
        Assert.True(henBefore - hen.Mood >= 10, $"hönan tappade {henBefore - hen.Mood}");
        Assert.True(rakeBefore - rake.Mood >= 5);
    }

    [Fact]
    public void ThreeClashesGiveAMedal()
    {
        var farm = Field();
        var vet = farm.State.SpawnSoldier(0, farm.Data.Unit("hogaffelgardet"), new TilePoint(10, 10));
        for (int i = 0; i < 3; i++)
        {
            var hen = farm.State.SpawnSoldier(1, farm.Data.Unit("rafsbrigaden"), new TilePoint(11, 10));
            farm.RunUntil(() => hen.Role != PersonRole.Soldier, 600, "räfsan ger upp");
            farm.RunUntil(() => vet.CombatTicks == 0, 200, "striden är över");
            vet.Mood = 90;
        }
        Assert.True(vet.Medal);
    }

    [Fact]
    public void AnimalsThatGiveUpGoHomeAndAreGone()
    {
        var farm = Field();
        var hen = farm.State.SpawnSoldier(0, farm.Data.Unit("honschocktruppen"), new TilePoint(10, 10));
        Squad(farm, 1, "hogaffelgardet", 1, 11, 10);
        farm.RunUntil(() => farm.State.People.All(p => p.Id != hen.Id), 300, "hönan är borta");
    }

    [Fact]
    public void SoldiersEatWhenHungryAndNothingIsGoingOn()
    {
        var farm = new Farm(start: new TilePoint(5, 5));
        var table = farm.PlaceDone("kafferepet", 12, 5);
        farm.Put(table, "knackebrod", 5);
        var man = farm.State.SpawnSoldier(0, farm.Data.Unit("rafsbrigaden"), new TilePoint(14, 12));
        man.Mood = 15;
        farm.RunUntil(() => man.Mood > 15, 600, "soldaten har ätit");
        Assert.True(man.Mood <= 60);
    }
}

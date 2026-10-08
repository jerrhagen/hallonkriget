using Hallonkriget.Sim.Commands;
using Hallonkriget.Sim.Map;
using Hallonkriget.Sim.People;

namespace Hallonkriget.Sim.Tests.Military;

/// <summary>Logen, rekryterna och utrustningen från units.json, designdokumentet (Enheter).</summary>
public class BarracksTests
{
    private static int U(Farm farm, string id) => farm.Data.Unit(id).Index;

    [Fact]
    public void UnitsAreReadFromData()
    {
        var data = TestData.Game;
        Assert.Equal(12, data.Units.Count);
        var fork = data.Unit("hogaffelgardet");
        Assert.Equal((6, 4, 90), (fork.Attack, fork.Defence, fork.Mood));
        // 0,8 rutor per sekund på gräs, i samma enhet som personernas fart.
        Assert.Equal(64, fork.Speed);
        Assert.Equal(10, data.Unit("honschocktruppen").GroupMax);
        Assert.Equal(1, data.Unit("traktorn").GroupMax);
        var barn = data.Building("logen");
        Assert.True(barn.Barracks);
        Assert.Contains(data.GoodIndex("rafsa"), barn.Accepts);
        Assert.Contains(data.GoodIndex("hona"), barn.Accepts);
        Assert.DoesNotContain(data.GoodIndex("saltpatroner"), barn.Accepts);
        Assert.Equal(data.Unit("vedtraven").Index, data.Building("vedtraven").FixedUnit);
    }

    [Fact]
    public void EveryPlayerStartsWithTheOldMan()
    {
        var farm = new Farm(start: new TilePoint(5, 5));
        var hero = Assert.Single(farm.State.People, p => p.Role == PersonRole.Soldier);
        Assert.Equal("gubben", farm.Data.Units[hero.Unit].Id);
        Assert.Equal(6, hero.Shots);
        farm.Run(10);
        Assert.Equal(PersonJob.Soldiering, hero.Job);
        Assert.Single(farm.State.Groups);
        // Han står inte på stigar eller dörrar, där bärarna går.
        Assert.NotEqual(farm.Home.Entrance, hero.Tile);
    }

    [Fact]
    public void ARecruitWaitsInTheBarnAndBecomesARakeSoldier()
    {
        var farm = new Farm(start: new TilePoint(5, 5));
        var barn = farm.PlaceDone("logen", 14, 5);
        var school = farm.PlaceDone("bygdegarden", 10, 5);
        farm.Command(CommandType.Train, school.Id, farm.Data.ProfessionIndex("rekryt"));
        farm.Put(school, "kaffe");
        farm.RunUntil(() => farm.State.RecruitsIn(barn).Count == 1, 2000, "rekryten i logen");
        var recruit = farm.State.RecruitsIn(barn)[0];
        Assert.True(recruit.Inside);

        // Utan utrustning händer inget.
        farm.Command(CommandType.Equip, barn.Id, U(farm, "rafsbrigaden"), 1);
        Assert.Equal(PersonRole.Recruit, recruit.Role);

        farm.Put(barn, "rafsa");
        farm.Put(barn, "stickad_mossa");
        farm.Command(CommandType.Equip, barn.Id, U(farm, "rafsbrigaden"), 1);
        Assert.Equal(PersonRole.Soldier, recruit.Role);
        Assert.Equal(60, recruit.Mood);
        Assert.False(recruit.Inside);
        Assert.Equal(0, barn.InputCount(farm.G("rafsa")));
        farm.Run(5);
        Assert.True(recruit.Group >= 0);
        Assert.Equal(1, farm.State.Players[0].Equipped[U(farm, "rafsbrigaden")]);
    }

    [Fact]
    public void TenHensMakeOneChickenShockTroop()
    {
        var farm = new Farm(start: new TilePoint(5, 5));
        var barn = farm.PlaceDone("logen", 14, 5);
        foreach (var good in new[] { "hona", "syfingerborgar", "stickad_mossa", "hardkokta_agg" }) farm.Put(barn, good, 10);
        farm.Command(CommandType.Equip, barn.Id, U(farm, "honschocktruppen"), 1);
        farm.Run(5);
        var hens = farm.State.People.Where(p => p.Unit == U(farm, "honschocktruppen")).ToList();
        Assert.Equal(10, hens.Count);
        Assert.All(hens, h => Assert.Equal(10, h.Shots)); // ett hårdkokt ägg räcker till tio kast
        Assert.Single(hens.Select(h => h.Group).Distinct());
        // Hönsen är inte folk och behöver inga sovplatser.
        Assert.Equal(11, farm.State.Population(0));
    }

    [Fact]
    public void StorgardenCannotMakeHens()
    {
        var farm = new Farm(start: new TilePoint(5, 5));
        var barn = farm.PlaceDone("logen", 14, 5);
        foreach (var good in new[] { "hona", "syfingerborgar", "stickad_mossa", "hardkokta_agg" }) farm.Put(barn, good, 10);
        // Spelare 1 är Storgården men logen är spelare 0:s, så kommandot gäller inte.
        farm.Command(CommandType.Equip, barn.Id, U(farm, "honschocktruppen"), 1, player: 1);
        Assert.DoesNotContain(farm.State.People, p => p.Unit == U(farm, "honschocktruppen"));
    }
}

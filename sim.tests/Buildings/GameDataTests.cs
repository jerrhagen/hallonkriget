using Hallonkriget.Sim.Data;
using Hallonkriget.Sim.Map;

namespace Hallonkriget.Sim.Tests.Buildings;

public class GameDataTests
{
    private const string Goods = """{ "goods": [ { "id": "timmer", "name": "Timmer" }, { "id": "ved", "name": "Ved" } ] }""";

    private static GameData WithBuildings(string buildings) => GameData.Parse(Goods, $$"""{ "buildings": [ {{buildings}} ] }""");

    [Fact]
    public void RealDataLoads()
    {
        var data = TestData.Game;
        Assert.Equal(54, data.Goods.Count);
        Assert.Equal(data.Goods.Count, data.Goods.Select(g => g.Id).Distinct().Count());
        Assert.Contains(data.Buildings, b => b.Id == "bagarstugan");
        Assert.All(data.Goods, g => Assert.Matches("^[a-z_]+$", g.Id));
        Assert.All(data.Buildings, b => Assert.Matches("^[a-z_]+$", b.Id));
    }

    [Fact]
    public void BakeryMatchesTheTable()
    {
        // Produktionskedjor: Bagarstugan, 2×2, rågmjöl + vatten + ved ger knäckebröd ×3 på 18 sekunder
        // (specen säger 30, kortat 2026-10-08 för snabbare matcher, se beslut.md).
        var data = TestData.Game;
        var b = data.Building("bagarstugan");
        Assert.Equal((2, 2), (b.Width, b.Height));
        var recipe = Assert.Single(b.Recipes);
        Assert.Equal(180, recipe.Ticks);
        Assert.Equal(new[] { "ragmjol", "vatten", "ved" }, recipe.In.Select(a => data.Goods[a.Good].Id).Order());
        Assert.Equal(new GoodAmount(data.GoodIndex("knackebrod"), 3), Assert.Single(recipe.Out));
        // En 2×2 kostar 6 brädor och 3 sten.
        Assert.Equal(new[] { (data.GoodIndex("brador"), 6), (data.GoodIndex("sten"), 3) },
                     b.Cost.Select(c => (c.Good, c.Count)).Order());
    }

    [Fact]
    public void EveryGoodThatIsUsedCanBeMadeFromScratch()
    {
        // Implementationsplanen: varje vara som används någonstans ska också produceras någonstans,
        // och ingen kedja får vara cirkulär utan källa. Startförrådet räknas inte.
        var data = TestData.Game;
        var producible = new bool[data.Goods.Count];
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var r in data.Buildings.SelectMany(b => b.Recipes))
            {
                if (!r.In.All(a => producible[a.Good])) continue;
                foreach (var o in r.Out)
                    if (!producible[o.Good]) producible[o.Good] = changed = true;
            }
        }

        // Lanthandelns byten räknas inte: de är olika sätt att betala, och spelaren väljer det som finns.
        var used = data.Buildings.Where(b => !b.IsTrade)
            .SelectMany(b => b.Recipes.SelectMany(r => r.In).Concat(b.Cost)).Select(a => a.Good).Distinct();
        var missing = used.Where(g => !producible[g]).Select(g => data.Goods[g].Id).ToList();
        Assert.True(missing.Count == 0, "Kan inte tillverkas: " + string.Join(", ", missing));
    }

    [Fact]
    public void BuildTimeDefaultsBySize()
    {
        var data = WithBuildings("""
            { "id": "liten", "name": "Liten", "size": [1, 1], "faction": "both", "cost": { "ved": 1 } },
            { "id": "stor", "name": "Stor", "size": [3, 2], "faction": "both", "cost": { "ved": 1 }, "build_seconds": 7 }
            """);
        Assert.Equal(600, data.Building("liten").BuildTicks);
        Assert.Equal(70, data.Building("stor").BuildTicks);
    }

    [Fact]
    public void GathererReadsTerrain()
    {
        var data = TestData.Game;
        var b = data.Building("skogshuggarkojan");
        Assert.Equal(Terrain.Forest, b.GathersFrom);
        Assert.Equal(8, b.GatherRadius);
    }

    [Theory]
    [InlineData("""{ "id": "x", "name": "X", "size": [1, 1], "faction": "both", "cost": { "guld": 1 } }""", "guld")]
    [InlineData("""{ "id": "x", "name": "X", "size": [4, 1], "faction": "both", "cost": { "ved": 1 } }""", "storleken")]
    [InlineData("""{ "id": "x", "name": "X", "size": [1, 1], "faction": "grannen", "cost": { "ved": 1 } }""", "läger")]
    [InlineData("""{ "id": "x", "name": "X", "size": [1, 1], "faction": "both" }""", "kostnad")]
    [InlineData("""{ "id": "x", "name": "X", "size": [1, 1], "faction": "both", "cost": { "ved": 1 } }, { "id": "x", "name": "X", "size": [1, 1], "faction": "both", "cost": { "ved": 1 } }""", "två gånger")]
    [InlineData("""{ "id": "x", "name": "X", "size": [1, 1], "faction": "both", "cost": { "ved": 1 }, "recipes": [ { "in": { "ved": 1 }, "out": {}, "seconds": 5 } ] }""", "ge något")]
    [InlineData("""{ "id": "x", "name": "X", "size": [1, 1], "faction": "both", "cost": { "ved": 0 } }""", "minst 1")]
    public void BadDataIsRejectedWithAReason(string buildings, string reason)
    {
        var e = Assert.Throws<GameDataException>(() => WithBuildings(buildings));
        Assert.Contains(reason, e.Message);
    }

    [Fact]
    public void FingerprintFollowsTheContent()
    {
        const string a = """{ "id": "x", "name": "X", "size": [1, 1], "faction": "both", "cost": { "ved": 1 } }""";
        const string b = """{ "id": "x", "name": "X", "size": [1, 1], "faction": "both", "cost": { "ved": 2 } }""";
        Assert.Equal(WithBuildings(a).Fingerprint, WithBuildings(a).Fingerprint);
        Assert.NotEqual(WithBuildings(a).Fingerprint, WithBuildings(b).Fingerprint);
        // Namnen är bara text för spelaren och påverkar inte matchen.
        Assert.Equal(WithBuildings(a).Fingerprint, WithBuildings(a.Replace("\"X\"", "\"Y\"")).Fingerprint);
    }

    [Fact]
    public void FoodMatchesTheTable()
    {
        // Designdokumentet, Mat och humör.
        var data = TestData.Game;
        var mood = data.Foods.ToDictionary(f => data.Goods[f.Good].Id, f => f.Mood);
        Assert.Equal(30, mood["knackebrod"]);
        Assert.Equal(30, mood["abborre"]);
        Assert.Equal(45, mood["korv"]);
        Assert.Equal(60, mood["pannkakor"]);
        Assert.Equal(15, mood["sylt"]);
        Assert.Equal(10, mood["svagdricka"]);
        Assert.Equal(20, mood["kaffe"]);
        Assert.Equal(data.GoodIndex("pannkakor"), data.Food(data.GoodIndex("sylt"))!.With);
        // Det bästa står först, så att det äts först.
        Assert.Equal(data.Foods.Where(f => f.With < 0).OrderByDescending(f => f.Mood).Select(f => f.Good),
                     data.Foods.Where(f => f.With < 0).Select(f => f.Good));
        Assert.Equal(90, data.Mood.TicksPerPoint); // specen säger 6 s, ändrat till 9 s 2026-10-08
        Assert.Equal(30, data.Mood.EatAt);
        Assert.Equal(1800, data.Mood.RestTicks);
    }

    [Fact]
    public void EveryWorkerHasAProfession()
    {
        var data = TestData.Game;
        foreach (var b in data.Buildings.Where(b => b.Worker is not null))
            Assert.Contains(data.Professions, p => p.Id == b.Worker);
        // Bärare och hantlangare behöver inget verktyg, alla andra ett.
        Assert.All(data.Professions, p => Assert.Equal(p.Role == Hallonkriget.Sim.People.PersonRole.Worker, p.Tool >= 0));
    }

    [Fact]
    public void UnknownWorkerIsAnError()
    {
        const string professions = """{ "professions": [ { "id": "vedhuggare", "name": "Vedhuggare", "role": "worker" } ] }""";
        var e = Assert.Throws<GameDataException>(() => GameData.Parse(Goods,
            """{ "buildings": [ { "id": "x", "name": "X", "size": [1, 1], "faction": "both", "worker": "smed", "cost": { "ved": 1 } } ] }""",
            professionsJson: professions));
        Assert.Contains("smed", e.Message);
    }

    [Fact]
    public void FingerprintChangesWithTheFood()
    {
        const string buildings = """{ "buildings": [] }""";
        string Food(int mood) => $$"""{ "mood": { "max": 100, "seconds_per_point": 6, "eat_at": 30, "rest_seconds": 180 }, "food": [ { "good": "ved", "mood": {{mood}} } ] }""";
        Assert.NotEqual(GameData.Parse(Goods, buildings, Food(30)).Fingerprint, GameData.Parse(Goods, buildings, Food(31)).Fingerprint);
    }
}

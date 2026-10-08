using Hallonkriget.Sim.Advice;
using Hallonkriget.Sim.Map;
using Hallonkriget.Sim.People;

namespace Hallonkriget.Sim.Tests.Advice;

/// <summary>Designdokumentet, Gränssnittet: Sixten i hörnet. Varningarna är alltid rätt, pratet kan slås av.</summary>
public class AdvisorTests
{
    private static AdvisorLines Lines(Faction f = Faction.Torpet) => AdvisorLines.Parse(TestData.Read("radgivare.json"), f);

    /// <summary>Kör tick för tick och samlar rådgivarens repliker.</summary>
    private static List<Remark> Run(Farm farm, Advisor advisor, int ticks, Action? everyTick = null)
    {
        var said = new List<Remark>();
        for (int i = 0; i < ticks; i++)
        {
            everyTick?.Invoke();
            farm.Run(1);
            if (advisor.Update(farm.State) is { } r) said.Add(r);
        }
        return said;
    }

    private static void SetMood(Farm farm, int mood)
    {
        foreach (var p in farm.State.People) if (p.Owner == 0) p.Mood = mood;
    }

    [Fact]
    public void SixtenAndMajorHaveALineForEveryWarning()
    {
        var sixten = Lines(Faction.Torpet);
        var major = Lines(Faction.Storgarden);
        Assert.Equal("Sixten", sixten.Name);
        Assert.Equal("Major", major.Name);
        int kinds = Enum.GetValues<WarningKind>().Length;
        Assert.Equal(kinds, AdvisorLines.WarningIds.Length);
        Assert.All(new[] { sixten, major }, l =>
        {
            Assert.Equal(kinds, l.Warnings.Count);
            Assert.True(l.Chatter.Count >= 10);
        });
    }

    [Fact]
    public void HungryFolkWithoutAKafferepGetAWarningAfterAWhile()
    {
        var farm = new Farm(start: new TilePoint(5, 5));
        var advisor = new Advisor(Lines(), 0);
        SetMood(farm, 25);
        var said = Run(farm, advisor, Advisor.Grace + 100);
        var warning = Assert.Single(said, r => r.Warning == WarningKind.NoTable);
        Assert.InRange(warning.Tick, Advisor.Grace, Advisor.Grace + 100);
        Assert.Contains("kafferep", warning.Text);
        Assert.Contains(WarningKind.NoTable, advisor.Active);

        // Samma läge varnas inte igen förrän bubblan var tredje minut.
        said = Run(farm, advisor, 600);
        Assert.DoesNotContain(said, r => r.Warning == WarningKind.NoTable);
    }

    [Fact]
    public void AnEmptyTableIsWarnedAboutAndFoodClearsIt()
    {
        var farm = new Farm(start: new TilePoint(5, 5));
        var table = farm.PlaceDone("kafferepet", 20, 20);
        var advisor = new Advisor(Lines(), 0);
        SetMood(farm, 25);
        var said = Run(farm, advisor, Advisor.Grace + 100);
        Assert.Contains(said, r => r.Warning == WarningKind.EmptyTable);
        Assert.DoesNotContain(said, r => r.Warning == WarningKind.NoTable);

        farm.Put(table, "pannkakor", 4);
        Run(farm, advisor, 20);
        Assert.DoesNotContain(WarningKind.EmptyTable, advisor.Active);
    }

    [Fact]
    public void GivingUpIsReportedAtOnce()
    {
        var farm = new Farm(start: new TilePoint(5, 5));
        var advisor = new Advisor(Lines(), 0);
        SetMood(farm, 100);
        var p = farm.State.People.First(p => p.Owner == 0);
        p.Mood = 1;
        p.MoodTimer = 1;
        var said = Run(farm, advisor, 20);
        var r = Assert.Single(said, r => r.Warning == WarningKind.GaveUp);
        Assert.Contains("1", r.Text);
        Assert.Equal(1, farm.State.Players[0].GaveUp);
    }

    [Fact]
    public void ABuildingWithoutItsWorkerSaysWhoIsMissing()
    {
        var farm = new Farm(start: new TilePoint(5, 5));
        var advisor = new Advisor(Lines(), 0);
        SetMood(farm, 100);
        // Det finns ingen bagare från början.
        farm.PlaceDone("bagarstugan", 20, 20);
        var said = Run(farm, advisor, Advisor.Grace + 100);
        var r = Assert.Single(said, r => r.Warning == WarningKind.NoWorker);
        Assert.Contains("Bagarstugan", r.Text);
        Assert.Contains("bagare", r.Text);

        // En bagare som finns, även på väg, tar bort varningen.
        farm.State.SpawnPerson(0, PersonRole.Worker, farm.Home.Entrance, "bagare");
        Run(farm, advisor, 20);
        Assert.DoesNotContain(WarningKind.NoWorker, advisor.Active);
    }

    [Fact]
    public void TwoCoffeeOnSaturday()
    {
        var farm = new Farm(start: new TilePoint(5, 5));
        var advisor = new Advisor(Lines(), 0);
        int saturday = GameClock.WeekTicks * 5 / 7 + 1;
        var said = Run(farm, advisor, saturday + Advisor.Grace + 20, () =>
        {
            foreach (var p in farm.State.People) if (p.Owner == 0) p.Mood = 100;
        });
        var r = Assert.Single(said, r => r.Warning == WarningKind.LowCoffeeSaturday);
        Assert.True(r.Tick >= saturday + Advisor.Grace);
        Assert.Equal("Vi har 2 kaffe kvar och det är lördag. Jag säger inget mer.", r.Text);
    }

    [Fact]
    public void ChatterEveryThreeMinutesUnlessItIsTurnedOff()
    {
        var farm = new Farm(start: new TilePoint(5, 5));
        var talking = new Advisor(Lines(), 0);
        var quiet = new Advisor(Lines(), 0) { Chatter = false };
        // Stugan är full från början (30 av 30), vilket annars är värt en varning.
        farm.Home.TakeOutput(farm.G("brador"));
        var talked = new List<Remark>();
        var silent = new List<Remark>();
        for (int i = 0; i < 2 * Advisor.BubbleInterval + 20; i++)
        {
            foreach (var p in farm.State.People) if (p.Owner == 0) p.Mood = 100;
            farm.Run(1);
            if (talking.Update(farm.State) is { } a) talked.Add(a);
            if (quiet.Update(farm.State) is { } b) silent.Add(b);
        }
        Assert.DoesNotContain(talked, r => r.Warning is not null);
        Assert.Equal(2, talked.Count);
        Assert.Equal(Advisor.BubbleInterval, talked[1].Tick - talked[0].Tick);
        Assert.NotEqual(talked[0].Text, talked[1].Text);
        Assert.Empty(silent);
    }

    [Fact]
    public void WarningsComeEvenWhenTheChatterIsOff()
    {
        var farm = new Farm(start: new TilePoint(5, 5));
        var advisor = new Advisor(Lines(), 0) { Chatter = false };
        SetMood(farm, 25);
        var said = Run(farm, advisor, Advisor.Grace + 100);
        Assert.Contains(said, r => r.Warning == WarningKind.NoTable);
    }
}

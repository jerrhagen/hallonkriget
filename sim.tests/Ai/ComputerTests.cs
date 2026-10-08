using System;
using System.Linq;
using Hallonkriget.Sim;
using Hallonkriget.Sim.Buildings;
using Hallonkriget.Sim.Commands;
using Hallonkriget.Sim.People;
using Xunit;

namespace Hallonkriget.Sim.Tests.Ai;

/// <summary>Datorspelaren på grannarna: byggordningen, bygdegården, logen och anfallet.</summary>
public class ComputerTests
{
    private static GameState Match(ulong seed, Difficulty difficulty = Difficulty.Normal)
    {
        var setup = MatchSetup.OnMap(seed, TestData.Map("grannarna"), TestData.Game,
            new PlayerSetup(Faction.Torpet, true, null, difficulty), new PlayerSetup(Faction.Storgarden, true, null, difficulty));
        return GameState.NewMatch(setup);
    }

    private static void RunMinutes(GameState state, int minutes)
    {
        var none = Array.Empty<Command>();
        for (int t = 0; t < minutes * 60 * GameState.TicksPerSecond && state.Winner < 0; t++) state.Tick(none);
    }

    private static bool Has(GameState s, byte player, string building) =>
        s.Buildings.Any(b => b.Owner == player && b.Def.Id == building && b.Stage == BuildingStage.Done);

    [Fact]
    public void TheComputerBuildsTheBreadEconomyAndTrainsATrader()
    {
        var s = Match(1);
        RunMinutes(s, 30);
        foreach (byte p in new byte[] { 0, 1 })
        {
            foreach (var id in new[] { "kafferepet", "bygdegarden", "lanthandeln", "bagarstugan", "fiskeboden" })
                Assert.True(Has(s, p, id), $"spelare {p} saknar {id}");
            Assert.Contains(s.People, q => q.Owner == p && q.Profession == "handlare");
            Assert.True(s.Players[p].Eaten.Sum() > 10, $"spelare {p} har bara ätit {s.Players[p].Eaten.Sum()} gånger");
        }
    }

    [Fact]
    public void TwoComputersPlayTheSameMatchTwice()
    {
        var a = Match(7);
        var b = Match(7);
        RunMinutes(a, 20);
        RunMinutes(b, 20);
        Assert.Equal(a.Hash(), b.Hash());
    }

    [Fact]
    public void AnEasyComputerNeverAttacksFirst()
    {
        var s = Match(3, Difficulty.Easy);
        RunMinutes(s, 100);
        Assert.Equal(-1, s.Winner);
        Assert.All(s.Players, p => Assert.Equal(0, p.BuildingsTaken));
    }

    [Fact]
    public void ANormalComputerEquipsAnArmyAndAttacks()
    {
        var s = Match(2);
        RunMinutes(s, 120);
        Assert.Contains(s.Players, p => p.Equipped[s.Data.Unit("rafsbrigaden").Index] > 0);
        Assert.Contains(s.Players, p => s.Computer(p.Id)!.Attacking || p.BuildingsTaken > 0 || s.Winner >= 0);
    }
}

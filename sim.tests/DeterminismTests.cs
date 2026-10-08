using Hallonkriget.Sim.Commands;

namespace Hallonkriget.Sim.Tests;

public class DeterminismTests
{
    private const ulong MatchSeed = 1958_07_14;
    private const ulong ScriptSeed = 42;
    private const int Ticks = 10_000;

    [Fact]
    public void TwoRunsWithSameCommandsGiveSameHashes()
    {
        var script = CommandScript.Generate(ScriptSeed, Ticks, players: 5);

        var first = CommandScript.Run(GameState.NewMatch(CommandScript.Setup(MatchSeed)), script);
        var second = CommandScript.Run(GameState.NewMatch(CommandScript.Setup(MatchSeed)), script);

        Assert.Equal(Ticks / 10, first.Count);
        for (int i = 0; i < first.Count; i++)
            Assert.True(first[i] == second[i], $"Körningarna gick isär vid tick {(i + 1) * 10}");
    }

    /// <summary>
    /// Kontrollsumman efter 10 000 tick är inskriven här. Testet körs på Linux och Windows i CI,
    /// så om någon plattform räknar annorlunda blir det rött. Ändras tillståndet med flit
    /// (nya fält, nya regler) skrivs det nya värdet in här i samma commit.
    /// </summary>
    [Fact]
    public void HashMatchesRecordedValueOnEveryPlatform()
    {
        var script = CommandScript.Generate(ScriptSeed, Ticks, players: 5);
        var state = GameState.NewMatch(CommandScript.Setup(MatchSeed));
        CommandScript.Run(state, script);

        Assert.True(state.Walkers.Count > 50, "Manuset ska skapa vandrare, annars testar det ingenting");
        Assert.True(state.Buildings.Count > 30, "Manuset ska placera byggnader, annars testar det ingenting");
        Assert.Contains(state.Buildings, b => b.Stage == Hallonkriget.Sim.Buildings.BuildingStage.Done && b.Def.Buildable);
        Assert.True(state.Map.AllTiles().Count(p => state.Map.PathAt(p) == Hallonkriget.Sim.Map.PathState.Trodden) > 10,
            "Hantlangarna ska ha trampat upp stigar");
        Assert.True(state.Players.Any(p => p.GaveUp > 0), "Humöret ska ha tagit slut för någon");
        Assert.Equal(GoldenHash, state.Hash());
    }

    // Ändrad när humör, kafferepet, bygdegården och lanthandeln kom in (fas 1), och när alla byggnaderna,
    // odlingsrutorna, terräng som tar slut och matens bonusar kom in (fas 2), och när logen, grupperna
    // och gubben kom in (fas 3).
    private const ulong GoldenHash = 1978580761277421186UL;

    [Fact]
    public void ReplayThroughByteFormatGivesSameHash()
    {
        var script = CommandScript.Generate(ScriptSeed, 2_000, players: 4);
        var replay = script.Select(tick => CommandCodec.Decode(CommandCodec.Encode(tick))).ToArray();

        var original = GameState.NewMatch(CommandScript.Setup(MatchSeed));
        var replayed = GameState.NewMatch(CommandScript.Setup(MatchSeed));
        CommandScript.Run(original, script);
        CommandScript.Run(replayed, replay);

        Assert.Equal(original.Hash(), replayed.Hash());
    }

    [Fact]
    public void ArrivalOrderBetweenPlayersDoesNotMatter()
    {
        var script = CommandScript.Generate(ScriptSeed, 2_000, players: 4);
        // Vänd ordningen mellan spelare men behåll ordningen inom varje spelare.
        var shuffled = script.Select(tick => tick
            .Select((c, i) => (c, i))
            .OrderByDescending(x => x.c.Player).ThenBy(x => x.i)
            .Select(x => x.c).ToList()).ToArray();

        var a = GameState.NewMatch(CommandScript.Setup(MatchSeed));
        var b = GameState.NewMatch(CommandScript.Setup(MatchSeed));
        CommandScript.Run(a, script);
        CommandScript.Run(b, shuffled);

        Assert.Equal(a.Hash(), b.Hash());
    }

    [Fact]
    public void DifferentSeedsGiveDifferentMatches()
    {
        var script = CommandScript.Generate(ScriptSeed, 2_000, players: 4);
        var a = GameState.NewMatch(CommandScript.Setup(1));
        var b = GameState.NewMatch(CommandScript.Setup(2));
        CommandScript.Run(a, script);
        CommandScript.Run(b, script);

        Assert.NotEqual(a.Hash(), b.Hash());
    }

    [Fact]
    public void HashIsFast()
    {
        var script = CommandScript.Generate(ScriptSeed, 3_000, players: 4);
        var state = GameState.NewMatch(CommandScript.Setup(MatchSeed));
        CommandScript.Run(state, script);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 100; i++) state.Hash();
        sw.Stop();
        Assert.True(sw.Elapsed.TotalMilliseconds / 100 < 1.0, $"Hash() tog {sw.Elapsed.TotalMilliseconds / 100} ms");
    }
}

using Hallonkriget.Sim.Ai;
using Hallonkriget.Sim.Replay;

namespace Hallonkriget.Sim.Tests.Replay;

public class ReplayTests
{
    /// <summary>Brödgårdens byggordning i några minuter, inspelad.</summary>
    private static (Sim.Replay.Replay Replay, GameState State) Record(int minutes)
    {
        var order = TestData.Order("brodgarden");
        var map = TestData.Map(order.MapId);
        var setup = MatchSetup.OnMap(1958, map, TestData.Game, (Faction.Torpet, false));
        var state = GameState.NewMatch(setup);
        var replay = new Sim.Replay.Replay(1958, map, setup.Players, TestData.Game.Fingerprint);
        var player = new BuildOrderPlayer(order, 0);
        for (int t = 0; t < minutes * 600; t++)
        {
            var commands = player.Next(state);
            replay.Record(state.TickCount, commands);
            state.Tick(commands);
            replay.AfterTick(state);
        }
        return (replay, state);
    }

    [Fact]
    public void SavedGameLoadsToTheSameState()
    {
        var (replay, original) = Record(8);
        var bytes = replay.ToBytes();
        var loaded = Sim.Replay.Replay.FromBytes(bytes).Play(TestData.Game);

        Assert.Equal(original.TickCount, loaded.TickCount);
        Assert.Equal(original.Hash(), loaded.Hash());
        Assert.True(bytes.Length < 20_000, $"reprisen är {bytes.Length} byte");
    }

    [Fact]
    public void LoadingCanStopAtAnEarlierTick()
    {
        var (replay, _) = Record(3);
        var state = replay.Play(TestData.Game, untilTick: 1234);
        Assert.Equal(1234, state.TickCount);
    }

    [Fact]
    public void AChangedChecksumIsFoundAtItsTick()
    {
        var (replay, _) = Record(3);
        var bytes = replay.ToBytes();
        // Den sista kontrollsumman ligger precis före sista ticket i filen. Ändra en byte i den.
        int endTickBytes = 2; // 1800 som varint
        bytes[^(endTickBytes + 3)] ^= 0x40;
        var e = Assert.Throws<ReplayMismatchException>(() => Sim.Replay.Replay.FromBytes(bytes).Play(TestData.Game));
        Assert.Equal(1800, e.Tick);
    }

    [Fact]
    public void OtherDataIsRefused()
    {
        var (replay, _) = Record(1);
        var other = new Sim.Replay.Replay(replay.Seed, replay.Map, replay.Players, replay.DataFingerprint + 1);
        Assert.Throws<ReplayMismatchException>(() => other.Play(TestData.Game));
    }

    [Fact]
    public void NotAReplay()
    {
        Assert.Throws<InvalidDataException>(() => Sim.Replay.Replay.FromBytes(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
    }

    /// <summary>
    /// Reprisregressionen (implementationsplanen, Tester): sparade repriser med samma regelversion ska
    /// spelas upp med samma kontrollsummor. Har simuleringen eller speldatan ändrats med flit höjs
    /// Replay.RulesVersion, och en ny reprisfil spelas in med kommandot i sim/Replay/README.md.
    /// </summary>
    [Fact]
    public void SavedReplaysStillGiveTheSameChecksums()
    {
        var dir = Path.Combine(ArchitectureTests.RepoRoot(), "sim.tests", "Replays");
        var files = Directory.GetFiles(dir, "*.hkr");
        Assert.NotEmpty(files);
        int current = 0;
        foreach (var file in files)
        {
            var replay = Sim.Replay.Replay.FromBytes(File.ReadAllBytes(file));
            if (replay.Rules != Sim.Replay.Replay.RulesVersion) continue;
            current++;
            Assert.True(replay.DataFingerprint == TestData.Game.Fingerprint,
                $"{Path.GetFileName(file)}: speldatan har ändrats. Höj Replay.RulesVersion och spela in reprisen igen (sim/Replay/README.md).");
            var state = replay.Play(TestData.Game);
            Assert.Equal(replay.Checksums[^1].Hash, state.Hash());
        }
        Assert.True(current > 0, $"Ingen reprisfil har regelversion {Sim.Replay.Replay.RulesVersion}. Spela in en (sim/Replay/README.md).");
    }
}

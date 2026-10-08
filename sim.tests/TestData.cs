using Hallonkriget.Sim.Data;

namespace Hallonkriget.Sim.Tests;

/// <summary>Speldatan i data/, läst en gång per testkörning.</summary>
public static class TestData
{
    private static readonly Lazy<GameData> Real = new(() => GameData.FromFiles(
        name => File.ReadAllText(Path.Combine(ArchitectureTests.RepoRoot(), "data", name))));

    public static GameData Game => Real.Value;
}

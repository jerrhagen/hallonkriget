using Hallonkriget.Sim.Data;

namespace Hallonkriget.Sim.Tests;

/// <summary>Speldatan i data/, läst en gång per testkörning.</summary>
public static class TestData
{
    private static readonly Lazy<GameData> Real = new(() => GameData.Parse(
        File.ReadAllText(Path.Combine(ArchitectureTests.RepoRoot(), "data", "goods.json")),
        File.ReadAllText(Path.Combine(ArchitectureTests.RepoRoot(), "data", "buildings.json"))));

    public static GameData Game => Real.Value;
}

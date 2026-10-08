using Hallonkriget.Sim.Ai;
using Hallonkriget.Sim.Data;
using Hallonkriget.Sim.Map;

namespace Hallonkriget.Sim.Tests;

/// <summary>Speldatan i data/, läst en gång per testkörning.</summary>
public static class TestData
{
    private static readonly Lazy<GameData> Real = new(() => GameData.FromFiles(Read));

    public static GameData Game => Real.Value;

    public static string Read(string name) => File.ReadAllText(Path.Combine(ArchitectureTests.RepoRoot(), "data", name));

    public static MapDef Map(string id) => MapDef.Parse(Read(Path.Combine("maps", id + ".json")));

    public static BuildOrder Order(string id) => BuildOrder.Parse(Read(Path.Combine("ai", id + ".json")), Game);
}

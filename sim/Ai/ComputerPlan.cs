using System.Collections.Generic;
using System.Text.Json;
using Hallonkriget.Sim.Data;
using Hallonkriget.Sim.Determinism;

namespace Hallonkriget.Sim.Ai;

/// <summary>Det som skiljer svårighetsgraderna åt. Tider i tick.</summary>
public sealed record DifficultyPlan(int StepTicks, int AttackAt, bool Attacks, int Reserve, int Columns);

/// <summary>Ett steg i datorns byggordning: byggnaden och receptet den ska börja med (-1: turas om).</summary>
public sealed record ComputerStep(int Building, int Recipe);

/// <summary>
/// Datorspelarens plan från data/ai/dator.json: byggordningen, armén och svårighetsgraderna.
/// Den ingår i speldatan, så att alla datorer i en match har samma datorspelare.
/// </summary>
public sealed class ComputerPlan
{
    public DifficultyPlan Easy { get; init; } = null!;
    public DifficultyPlan Normal { get; init; } = null!;
    public DifficultyPlan Hard { get; init; } = null!;
    public int Laborers { get; init; }
    public int Carriers { get; init; }
    public int[] Army { get; init; } = System.Array.Empty<int>();
    public int Servers { get; init; }
    /// <summary>Byggnaden som byggs när sängarna tar slut.</summary>
    public int House { get; init; }
    /// <summary>Yrken som utbildas före bärarna och de andra arbetarna: maten.</summary>
    public string[] First { get; init; } = System.Array.Empty<string>();
    public ComputerStep[] Steps { get; init; } = System.Array.Empty<ComputerStep>();

    public DifficultyPlan For(Difficulty d) => d switch
    {
        Difficulty.Easy => Easy,
        Difficulty.Hard => Hard,
        _ => Normal,
    };

    internal static ComputerPlan Parse(string json, IReadOnlyList<BuildingDef> buildings, IReadOnlyList<UnitDef> units)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var d = root.GetProperty("difficulties");
        DifficultyPlan Level(string name)
        {
            var e = d.GetProperty(name);
            return new DifficultyPlan(e.GetProperty("step_seconds").GetInt32() * GameState.TicksPerSecond,
                e.GetProperty("attack_at").GetInt32(), e.GetProperty("attacks").GetBoolean(),
                e.GetProperty("reserve").GetInt32(), e.GetProperty("columns").GetInt32());
        }

        int Building(string id)
        {
            foreach (var b in buildings) if (b.Id == id) return b.Index;
            throw new GameDataException($"ai/dator.json: okänd byggnad {id}");
        }

        var army = new List<int>();
        foreach (var a in root.GetProperty("army").EnumerateArray())
        {
            var id = a.GetString();
            var unit = units is List<UnitDef> list ? list.Find(u => u.Id == id) : null;
            if (unit is null)
                foreach (var u in units) if (u.Id == id) unit = u;
            army.Add(unit?.Index ?? throw new GameDataException($"ai/dator.json: okänd enhet {id}"));
        }

        var steps = new List<ComputerStep>();
        foreach (var s in root.GetProperty("steps").EnumerateArray())
        {
            int b = Building(s.GetProperty("build").GetString() ?? "");
            int recipe = s.TryGetProperty("recipe", out var r) ? r.GetInt32() : -1;
            if (recipe >= buildings[b].Recipes.Length) throw new GameDataException($"ai/dator.json: {buildings[b].Id} har inget recept {recipe}");
            steps.Add(new ComputerStep(b, recipe));
        }

        return new ComputerPlan
        {
            Easy = Level("easy"),
            Normal = Level("normal"),
            Hard = Level("hard"),
            Laborers = root.GetProperty("laborers").GetInt32(),
            Carriers = root.GetProperty("carriers").GetInt32(),
            Army = army.ToArray(),
            Servers = root.GetProperty("servers").GetInt32(),
            First = ParseFirst(root),
            House = Building(root.GetProperty("house").GetString() ?? ""),
            Steps = steps.ToArray(),
        };
    }

    private static string[] ParseFirst(JsonElement root)
    {
        var first = new List<string>();
        foreach (var f in root.GetProperty("first").EnumerateArray()) first.Add(f.GetString() ?? "");
        return first.ToArray();
    }

    internal void AddToHash(ref StateHasher h)
    {
        foreach (var d in new[] { Easy, Normal, Hard })
        {
            h.Add(d.StepTicks);
            h.Add(d.AttackAt);
            h.Add(d.Attacks);
            h.Add(d.Reserve);
            h.Add(d.Columns);
        }
        h.Add(Laborers);
        h.Add(Carriers);
        h.Add(Servers);
        h.Add(House);
        h.Add(First.Length);
        foreach (var f in First)
        {
            h.Add(f.Length);
            foreach (char c in f) h.Add((int)c);
        }
        h.Add(Army.Length);
        foreach (int a in Army) h.Add(a);
        h.Add(Steps.Length);
        foreach (var s in Steps)
        {
            h.Add(s.Building);
            h.Add(s.Recipe);
        }
    }
}

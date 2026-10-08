using System;
using System.Collections.Generic;
using System.Text.Json;
using Hallonkriget.Sim.Buildings;
using Hallonkriget.Sim.Commands;
using Hallonkriget.Sim.Data;
using Hallonkriget.Sim.Map;

namespace Hallonkriget.Sim.Ai;

public enum BuildStepKind : byte
{
    Place,
    Path,
    Recipe,
    Trade,
    Train,
    Block,
    Unblock,
}

/// <summary>
/// Ett steg i en byggordning. Building är namnet ett tidigare Place-steg gav sin byggnad.
/// Steget ges tidigast vid Minute, om WhenDone är satt först när den byggnaden är färdig, och om
/// WhenProducedGood är satt först när spelaren har tillverkat (eller köpt) så många av varan.
/// </summary>
public sealed record BuildStep(
    BuildStepKind Kind,
    string Building = "",
    string Name = "",
    int Index = 0,
    TilePoint At = default,
    TilePoint To = default,
    int Good = -1,
    int Good2 = -1,
    int Count = 1,
    int Minute = 0,
    string WhenDone = "",
    int WhenProducedGood = -1,
    int WhenProducedCount = 0);

/// <summary>
/// En byggordning från data/ai: en lista med steg som ges i tur och ordning, som om en spelare
/// klickade. Används av sim.cli och testerna nu, och av datorspelaren i fas 3.
/// </summary>
public sealed class BuildOrder
{
    public string Id { get; }
    public string MapId { get; }
    public IReadOnlyList<BuildStep> Steps { get; }

    private BuildOrder(string id, string mapId, BuildStep[] steps)
    {
        Id = id;
        MapId = mapId;
        Steps = steps;
    }

    public static BuildOrder Parse(string json, GameData data)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        string id = root.GetProperty("id").GetString() ?? "";
        string map = root.TryGetProperty("map", out var m) ? m.GetString() ?? "" : "";
        var names = new List<string>();
        var steps = new List<BuildStep>();
        int n = 0;
        foreach (var s in root.GetProperty("steps").EnumerateArray())
        {
            n++;
            string where = $"{id}, steg {n}";
            int minute = s.TryGetProperty("minute", out var mi) ? mi.GetInt32() : 0;
            string whenDone = s.TryGetProperty("when_done", out var wd) ? KnownName(wd.GetString(), names, where) : "";
            int producedGood = -1, producedCount = 0;
            if (s.TryGetProperty("when_produced", out var wp))
            {
                foreach (var p in wp.EnumerateObject())
                {
                    producedGood = Lookup(() => data.GoodIndex(p.Name), where);
                    producedCount = p.Value.GetInt32();
                }
            }

            BuildStep step;
            if (s.TryGetProperty("place", out var place))
            {
                var def = Lookup(() => data.Building(place.GetString() ?? ""), where);
                string name = s.TryGetProperty("name", out var nm) ? nm.GetString() ?? "" : "";
                if (name.Length > 0)
                {
                    if (names.Contains(name)) throw new GameDataException($"{where}: namnet {name} finns redan");
                    names.Add(name);
                }
                step = new BuildStep(BuildStepKind.Place, Index: def.Index, Name: name, At: Point(s.GetProperty("at")));
            }
            else if (s.TryGetProperty("path", out var path))
            {
                step = new BuildStep(BuildStepKind.Path, At: Point(path[0]), To: Point(path[1]));
            }
            else if (s.TryGetProperty("recipe", out var recipe))
            {
                step = new BuildStep(BuildStepKind.Recipe, KnownName(recipe.GetString(), names, where), Index: s.GetProperty("index").GetInt32());
            }
            else if (s.TryGetProperty("trade", out var trade))
            {
                step = new BuildStep(BuildStepKind.Trade, KnownName(trade.GetString(), names, where),
                    Good: Lookup(() => data.GoodIndex(s.GetProperty("buy").GetString() ?? ""), where),
                    Good2: Lookup(() => data.GoodIndex(s.GetProperty("pay").GetString() ?? ""), where));
            }
            else if (s.TryGetProperty("train", out var train))
            {
                step = new BuildStep(BuildStepKind.Train, KnownName(train.GetString(), names, where),
                    Index: Lookup(() => data.ProfessionIndex(s.GetProperty("profession").GetString() ?? ""), where),
                    Count: s.TryGetProperty("count", out var c) ? c.GetInt32() : 1);
            }
            else if (s.TryGetProperty("block", out var block) || s.TryGetProperty("unblock", out block))
            {
                bool isBlock = s.TryGetProperty("block", out _);
                step = new BuildStep(isBlock ? BuildStepKind.Block : BuildStepKind.Unblock, KnownName(block.GetString(), names, where),
                    Good: Lookup(() => data.GoodIndex(s.GetProperty("good").GetString() ?? ""), where));
            }
            else
            {
                throw new GameDataException($"{where}: okänt steg");
            }
            steps.Add(step with { Minute = minute, WhenDone = whenDone, WhenProducedGood = producedGood, WhenProducedCount = producedCount });
        }
        return new BuildOrder(id, map, steps.ToArray());
    }

    private static TilePoint Point(JsonElement e) => new(e[0].GetInt32(), e[1].GetInt32());

    private static string KnownName(string? name, List<string> names, string where)
    {
        if (name is null || !names.Contains(name)) throw new GameDataException($"{where}: ingen byggnad heter {name}");
        return name;
    }

    private static T Lookup<T>(Func<T> find, string where)
    {
        try
        {
            return find();
        }
        catch (KeyNotFoundException e)
        {
            throw new GameDataException($"{where}: {e.Message}");
        }
    }
}

/// <summary>
/// Spelar upp en byggordning för en spelare. Varje tick ger Next de kommandon som är på tur;
/// ett steg som väntar (på en minut eller på en byggnad) håller också kvar stegen efter.
/// </summary>
public sealed class BuildOrderPlayer
{
    private readonly BuildOrder _order;
    private readonly byte _player;
    private readonly List<(string Name, int Id)> _named = new();
    private int _next;

    public BuildOrderPlayer(BuildOrder order, byte player)
    {
        _order = order;
        _player = player;
    }

    public bool Finished => _next >= _order.Steps.Count;

    /// <summary>Steg som inte kunde utföras, till exempel en byggnad som inte fick plats.</summary>
    public List<string> Problems { get; } = new();

    /// <summary>Byggnadens id för ett namn i byggordningen, eller -1.</summary>
    public int BuildingId(string name)
    {
        foreach (var (n, id) in _named) if (n == name) return id;
        return -1;
    }

    public List<Command> Next(GameState state)
    {
        var commands = new List<Command>();
        int buildings = state.Buildings.Count;
        while (_next < _order.Steps.Count)
        {
            var step = _order.Steps[_next];
            if (state.TickCount < step.Minute * 60 * GameState.TicksPerSecond) break;
            if (step.WhenDone.Length > 0)
            {
                int id = BuildingId(step.WhenDone);
                if (id < 0 || id >= state.Buildings.Count || state.Buildings[id].Stage != BuildingStage.Done) break;
            }
            // En byggnad som placerades i samma tick finns inte förrän ticket körts.
            if (step.Building.Length > 0 && BuildingId(step.Building) >= state.Buildings.Count) break;
            if (step.WhenProducedGood >= 0 && state.Players[_player].Produced[step.WhenProducedGood] < step.WhenProducedCount) break;
            _next++;
            Issue(state, step, commands, ref buildings);
        }
        return commands;
    }

    private void Issue(GameState state, BuildStep step, List<Command> commands, ref int buildings)
    {
        int tick = state.TickCount;
        int target = step.Building.Length > 0 ? BuildingId(step.Building) : -1;
        if (step.Building.Length > 0 && target < 0)
        {
            Problems.Add($"{step.Kind} {step.Building}: byggnaden placerades aldrig");
            return;
        }

        switch (step.Kind)
        {
            case BuildStepKind.Place:
            {
                var def = state.Data.Buildings[step.Index];
                if (!state.CanPlace(_player, def, step.At))
                {
                    Problems.Add($"{def.Id} på {step.At}: får inte plats");
                    return;
                }
                // Kommandona i samma tick placeras i ordning, så id:t är antalet byggnader före.
                if (step.Name.Length > 0) _named.Add((step.Name, buildings));
                buildings++;
                commands.Add(new Command(tick, _player, CommandType.PlaceBuilding, step.Index, step.At.X, step.At.Y));
                break;
            }
            case BuildStepKind.Path:
            {
                int x = step.At.X, y = step.At.Y;
                commands.Add(new Command(tick, _player, CommandType.PlanPath, x, y));
                while (x != step.To.X)
                {
                    x += x < step.To.X ? 1 : -1;
                    commands.Add(new Command(tick, _player, CommandType.PlanPath, x, y));
                }
                while (y != step.To.Y)
                {
                    y += y < step.To.Y ? 1 : -1;
                    commands.Add(new Command(tick, _player, CommandType.PlanPath, x, y));
                }
                break;
            }
            case BuildStepKind.Recipe:
                commands.Add(new Command(tick, _player, CommandType.SelectRecipe, target, step.Index));
                break;
            case BuildStepKind.Trade:
            {
                var recipes = state.Buildings[target].Def.Recipes;
                int index = -1;
                for (int r = 0; r < recipes.Length && index < 0; r++)
                {
                    if (recipes[r].Out[0].Good != step.Good) continue;
                    foreach (var a in recipes[r].In)
                        if (a.Good == step.Good2) index = r;
                }
                if (index < 0)
                {
                    Problems.Add($"Byte {step.Good} mot {step.Good2}: finns inte");
                    return;
                }
                commands.Add(new Command(tick, _player, CommandType.SelectRecipe, target, index));
                break;
            }
            case BuildStepKind.Train:
                for (int i = 0; i < step.Count; i++)
                    commands.Add(new Command(tick, _player, CommandType.Train, target, step.Index));
                break;
            case BuildStepKind.Block:
            case BuildStepKind.Unblock:
                commands.Add(new Command(tick, _player, CommandType.BlockGood, target, step.Good, step.Kind == BuildStepKind.Block ? 1 : 0));
                break;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text.Json;
using Hallonkriget.Sim.Determinism;
using Hallonkriget.Sim.Map;

namespace Hallonkriget.Sim.Data;

/// <summary>En vara. Numret är platsen i goods.json och används överallt i simuleringen.</summary>
public sealed record GoodDef(int Index, string Id, string Name);

/// <summary>Ett antal av en vara, med varans nummer.</summary>
public readonly record struct GoodAmount(int Good, int Count);

/// <summary>Ett recept: det som går in, det som kommer ut och hur många tick det tar.</summary>
public sealed record Recipe(GoodAmount[] In, GoodAmount[] Out, int Ticks);

/// <summary>Vilket läger som kan bygga en byggnad.</summary>
public enum FactionRule : byte
{
    Both,
    Torpet,
    Storgarden,
}

/// <summary>
/// En byggnadstyp från buildings.json. Storlek i rutor, kostnad i varor, byggtid och cykeltider i tick.
/// Storage större än noll betyder ett förråd (stugan, boden) som tar emot och lämnar ut allt.
/// </summary>
public sealed class BuildingDef
{
    public int Index { get; init; }
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public int Width { get; init; }
    public int Height { get; init; }
    public FactionRule Faction { get; init; }
    public string? Worker { get; init; }
    public bool Buildable { get; init; }
    public GoodAmount[] Cost { get; init; } = Array.Empty<GoodAmount>();
    public int BuildTicks { get; init; }
    public int Storage { get; init; }
    public GoodAmount[] StartStock { get; init; } = Array.Empty<GoodAmount>();
    public Terrain? GathersFrom { get; init; }
    public int GatherRadius { get; init; }
    public Recipe[] Recipes { get; init; } = Array.Empty<Recipe>();

    public bool IsStorage => Storage > 0;

    public bool AllowedFor(Faction faction) => Faction switch
    {
        FactionRule.Torpet => faction == Sim.Faction.Torpet,
        FactionRule.Storgarden => faction == Sim.Faction.Storgarden,
        _ => true,
    };
}

/// <summary>
/// Speldata från data/*.json, läst vid matchstart. Alla datorer i en match ska ha samma data;
/// Fingerprint är en kontrollsumma över innehållet och ingår i GameState.Hash().
/// </summary>
public sealed class GameData
{
    public IReadOnlyList<GoodDef> Goods { get; }
    public IReadOnlyList<BuildingDef> Buildings { get; }
    public ulong Fingerprint { get; }

    public static readonly GameData Empty = new(Array.Empty<GoodDef>(), Array.Empty<BuildingDef>());

    private GameData(GoodDef[] goods, BuildingDef[] buildings)
    {
        Goods = goods;
        Buildings = buildings;
        Fingerprint = ComputeFingerprint();
    }

    public int GoodIndex(string id)
    {
        foreach (var g in Goods) if (g.Id == id) return g.Index;
        throw new KeyNotFoundException($"Okänd vara: {id}");
    }

    public BuildingDef Building(string id)
    {
        foreach (var b in Buildings) if (b.Id == id) return b;
        throw new KeyNotFoundException($"Okänd byggnad: {id}");
    }

    /// <summary>Läser goods.json och buildings.json. Kastar GameDataException med en förklaring om något är fel.</summary>
    public static GameData Parse(string goodsJson, string buildingsJson)
    {
        var goods = new List<GoodDef>();
        using (var doc = JsonDocument.Parse(goodsJson))
        {
            foreach (var g in doc.RootElement.GetProperty("goods").EnumerateArray())
            {
                string id = RequireString(g, "id", "vara");
                if (goods.Exists(x => x.Id == id)) throw new GameDataException($"Varan {id} finns två gånger");
                goods.Add(new GoodDef(goods.Count, id, RequireString(g, "name", id)));
            }
        }

        int Good(string id, string where)
        {
            foreach (var g in goods) if (g.Id == id) return g.Index;
            throw new GameDataException($"{where}: okänd vara {id}");
        }

        GoodAmount[] Amounts(JsonElement parent, string property, string where)
        {
            if (!parent.TryGetProperty(property, out var obj)) return Array.Empty<GoodAmount>();
            var list = new List<GoodAmount>();
            foreach (var p in obj.EnumerateObject())
            {
                int count = p.Value.GetInt32();
                if (count <= 0) throw new GameDataException($"{where}: {p.Name} måste vara minst 1");
                list.Add(new GoodAmount(Good(p.Name, where), count));
            }
            return list.ToArray();
        }

        var buildings = new List<BuildingDef>();
        using (var doc = JsonDocument.Parse(buildingsJson))
        {
            foreach (var b in doc.RootElement.GetProperty("buildings").EnumerateArray())
            {
                string id = RequireString(b, "id", "byggnad");
                if (buildings.Exists(x => x.Id == id)) throw new GameDataException($"Byggnaden {id} finns två gånger");

                var size = b.GetProperty("size");
                int w = size[0].GetInt32(), h = size[1].GetInt32();
                if (w is < 1 or > 3 || h is < 1 or > 3) throw new GameDataException($"{id}: storleken ska vara 1–3 rutor");

                var faction = RequireString(b, "faction", id) switch
                {
                    "both" => FactionRule.Both,
                    "torpet" => FactionRule.Torpet,
                    "storgarden" => FactionRule.Storgarden,
                    var f => throw new GameDataException($"{id}: okänt läger {f}"),
                };

                bool buildable = !b.TryGetProperty("buildable", out var bb) || bb.GetBoolean();
                var cost = Amounts(b, "cost", id);
                if (buildable && cost.Length == 0) throw new GameDataException($"{id}: byggbar men saknar kostnad");

                // Designdokumentet: en liten byggnad tar ungefär en minut när materialet finns.
                int buildSeconds = b.TryGetProperty("build_seconds", out var bs) ? bs.GetInt32() : 30 + 30 * (w > h ? w : h);

                Terrain? gathers = null;
                int radius = 0;
                if (b.TryGetProperty("gathers", out var gt))
                {
                    gathers = ParseTerrain(RequireString(gt, "terrain", id), id);
                    radius = gt.GetProperty("radius").GetInt32();
                }

                var recipes = new List<Recipe>();
                if (b.TryGetProperty("recipes", out var rs))
                {
                    foreach (var r in rs.EnumerateArray())
                    {
                        int seconds = r.GetProperty("seconds").GetInt32();
                        if (seconds <= 0) throw new GameDataException($"{id}: receptets tid måste vara positiv");
                        var output = Amounts(r, "out", id);
                        if (output.Length == 0) throw new GameDataException($"{id}: ett recept måste ge något");
                        recipes.Add(new Recipe(Amounts(r, "in", id), output, seconds * GameState.TicksPerSecond));
                    }
                }

                buildings.Add(new BuildingDef
                {
                    Index = buildings.Count,
                    Id = id,
                    Name = RequireString(b, "name", id),
                    Width = w,
                    Height = h,
                    Faction = faction,
                    Worker = b.TryGetProperty("worker", out var wk) ? wk.GetString() : null,
                    Buildable = buildable,
                    Cost = cost,
                    BuildTicks = buildSeconds * GameState.TicksPerSecond,
                    Storage = b.TryGetProperty("storage", out var st) ? st.GetInt32() : 0,
                    StartStock = Amounts(b, "start_stock", id),
                    GathersFrom = gathers,
                    GatherRadius = radius,
                    Recipes = recipes.ToArray(),
                });
            }
        }
        return new GameData(goods.ToArray(), buildings.ToArray());
    }

    private static string RequireString(JsonElement e, string property, string where)
    {
        if (!e.TryGetProperty(property, out var v) || v.ValueKind != JsonValueKind.String || v.GetString() is not { Length: > 0 } s)
            throw new GameDataException($"{where}: saknar {property}");
        return s;
    }

    private static Terrain ParseTerrain(string name, string where) => name switch
    {
        "clearing" => Terrain.Clearing,
        "stony" => Terrain.Stony,
        "forest" => Terrain.Forest,
        "meadow" => Terrain.Meadow,
        "bog" => Terrain.Bog,
        "water" => Terrain.Water,
        "scrap_heap" => Terrain.ScrapHeap,
        "raspberry_thicket" => Terrain.RaspberryThicket,
        "plum_tree" => Terrain.PlumTree,
        "road" => Terrain.Road,
        _ => throw new GameDataException($"{where}: okänd terräng {name}"),
    };

    private ulong ComputeFingerprint()
    {
        var h = new StateHasher();
        h.Add(Goods.Count);
        foreach (var g in Goods) AddString(ref h, g.Id);
        h.Add(Buildings.Count);
        foreach (var b in Buildings)
        {
            AddString(ref h, b.Id);
            h.Add(b.Width);
            h.Add(b.Height);
            h.Add((byte)b.Faction);
            h.Add(b.Buildable);
            AddAmounts(ref h, b.Cost);
            h.Add(b.BuildTicks);
            h.Add(b.Storage);
            AddAmounts(ref h, b.StartStock);
            h.Add(b.GathersFrom.HasValue ? (int)b.GathersFrom.Value : -1);
            h.Add(b.GatherRadius);
            h.Add(b.Recipes.Length);
            foreach (var r in b.Recipes)
            {
                AddAmounts(ref h, r.In);
                AddAmounts(ref h, r.Out);
                h.Add(r.Ticks);
            }
        }
        return h.Value;
    }

    private static void AddString(ref StateHasher h, string s)
    {
        h.Add(s.Length);
        foreach (char c in s) h.Add((int)c);
    }

    private static void AddAmounts(ref StateHasher h, GoodAmount[] amounts)
    {
        h.Add(amounts.Length);
        foreach (var a in amounts)
        {
            h.Add(a.Good);
            h.Add(a.Count);
        }
    }
}

/// <summary>Fel i datafilerna.</summary>
public sealed class GameDataException : Exception
{
    public GameDataException(string message) : base(message) { }
}

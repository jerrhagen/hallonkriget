using System;
using System.Collections.Generic;
using System.Text.Json;
using Hallonkriget.Sim.Determinism;
using Hallonkriget.Sim.Map;
using Hallonkriget.Sim.People;

namespace Hallonkriget.Sim.Data;

/// <summary>En vara. Numret är platsen i goods.json och används överallt i simuleringen.</summary>
public sealed record GoodDef(int Index, string Id, string Name);

/// <summary>Ett antal av en vara, med varans nummer.</summary>
public readonly record struct GoodAmount(int Good, int Count);

/// <summary>
/// Ett recept: det som går in, det som kommer ut och hur många tick det tar. Lanthandelns byten är
/// också recept, och en del av dem finns bara för ett läger.
/// </summary>
public sealed record Recipe(GoodAmount[] In, GoodAmount[] Out, int Ticks, FactionRule Faction = FactionRule.Both);

/// <summary>Mat på kafferepet: hur mycket humör den ger. With: maten räknas bara ihop med den varan (sylt med pannkakor).</summary>
public sealed record FoodDef(int Good, int Mood, int With, string Bonus, int BonusPercent, int BonusTicks);

/// <summary>Humörets regler från food.json, i tick.</summary>
public sealed record MoodRules(int Max, int TicksPerPoint, int EatAt, int RestTicks)
{
    public static readonly MoodRules Default = new(100, 6 * GameState.TicksPerSecond, 30, 180 * GameState.TicksPerSecond);
}

/// <summary>Ett yrke. Tool är verktyget bygdegården förbrukar, eller -1.</summary>
public sealed record ProfessionDef(int Index, string Id, string Name, PersonRole Role, int Tool);

/// <summary>Ett yrke och hur många.</summary>
public readonly record struct ProfessionAmount(int Profession, int Count);

/// <summary>Bygdegården: tid per utbildning, hur lång kön får vara och vad som betalar (ett av alternativen).</summary>
public sealed record SchoolDef(int Ticks, int QueueLimit, GoodAmount[][] Pay);

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

    /// <summary>Sovplatser när byggnaden är färdig. Summan är befolkningstaket.</summary>
    public int Beds { get; init; }

    /// <summary>De som finns från start, för startbyggnaden.</summary>
    public ProfessionAmount[] StartPeople { get; init; } = Array.Empty<ProfessionAmount>();

    /// <summary>Kafferepet: hur många portioner bordet rymmer. Noll för andra byggnader.</summary>
    public int Table { get; init; }

    /// <summary>Varor byggnaden tar emot utöver recepten: maten på kafferepet, kaffet och verktygen i bygdegården.</summary>
    public int[] Accepts { get; init; } = Array.Empty<int>();

    /// <summary>Varor som är spärrade från början (kaffet på kafferepet). Spelaren kan ändra det.</summary>
    public int[] Blocked { get; init; } = Array.Empty<int>();

    /// <summary>Terräng som ska ligga intill byggnaden, som landsvägen vid lanthandeln.</summary>
    public Terrain? NextTo { get; init; }

    /// <summary>Lanthandeln: recepten är byten, och inget görs förrän spelaren valt ett.</summary>
    public bool IsTrade { get; init; }

    public bool ClosedSundays { get; init; }

    public SchoolDef? School { get; init; }

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

    /// <summary>Maten i den ordning den äts, det bästa först.</summary>
    public IReadOnlyList<FoodDef> Foods { get; }
    public MoodRules Mood { get; }
    public IReadOnlyList<ProfessionDef> Professions { get; }
    public ulong Fingerprint { get; }

    public static readonly GameData Empty = new(Array.Empty<GoodDef>(), Array.Empty<BuildingDef>(),
        Array.Empty<FoodDef>(), MoodRules.Default, Array.Empty<ProfessionDef>());

    private GameData(GoodDef[] goods, BuildingDef[] buildings, FoodDef[] foods, MoodRules mood, ProfessionDef[] professions)
    {
        Goods = goods;
        Buildings = buildings;
        Foods = foods;
        Mood = mood;
        Professions = professions;
        Fingerprint = ComputeFingerprint();
    }

    /// <summary>Läser alla datafiler med readFile("goods.json") och så vidare.</summary>
    public static GameData FromFiles(Func<string, string> readFile) => Parse(
        readFile("goods.json"), readFile("buildings.json"),
        readFile("food.json"), readFile("professions.json"), readFile("trade.json"));

    public int ProfessionIndex(string id)
    {
        foreach (var p in Professions) if (p.Id == id) return p.Index;
        throw new KeyNotFoundException($"Okänt yrke: {id}");
    }

    /// <summary>Hur mycket humör varan ger på kafferepet, eller null om den inte är mat.</summary>
    public FoodDef? Food(int good)
    {
        foreach (var f in Foods) if (f.Good == good) return f;
        return null;
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

    /// <summary>
    /// Läser goods.json, buildings.json och de andra filerna. De som saknas (i tester) blir tomma:
    /// ingen mat, inga yrken att kontrollera mot och inga byten. Kastar GameDataException med en
    /// förklaring om något är fel.
    /// </summary>
    public static GameData Parse(string goodsJson, string buildingsJson,
        string? foodJson = null, string? professionsJson = null, string? tradeJson = null)
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

        var foods = new List<FoodDef>();
        var mood = MoodRules.Default;
        if (foodJson is not null)
        {
            using var doc = JsonDocument.Parse(foodJson);
            var m = doc.RootElement.GetProperty("mood");
            mood = new MoodRules(
                m.GetProperty("max").GetInt32(),
                m.GetProperty("seconds_per_point").GetInt32() * GameState.TicksPerSecond,
                m.GetProperty("eat_at").GetInt32(),
                m.GetProperty("rest_seconds").GetInt32() * GameState.TicksPerSecond);
            if (mood.Max <= 0 || mood.TicksPerPoint <= 0 || mood.EatAt <= 0 || mood.EatAt >= mood.Max)
                throw new GameDataException("food.json: humörets regler går inte ihop");
            foreach (var f in doc.RootElement.GetProperty("food").EnumerateArray())
            {
                string id = RequireString(f, "good", "mat");
                int good = Good(id, "food.json");
                if (foods.Exists(x => x.Good == good)) throw new GameDataException($"food.json: {id} finns två gånger");
                int value = f.GetProperty("mood").GetInt32();
                if (value <= 0) throw new GameDataException($"food.json: {id} måste ge humör");
                foods.Add(new FoodDef(good, value,
                    f.TryGetProperty("with", out var w) ? Good(w.GetString()!, "food.json") : -1,
                    f.TryGetProperty("bonus", out var bo) ? bo.GetString()! : "",
                    f.TryGetProperty("percent", out var pc) ? pc.GetInt32() : 0,
                    f.TryGetProperty("seconds", out var sc) ? sc.GetInt32() * GameState.TicksPerSecond : 0));
            }
        }

        var professions = new List<ProfessionDef>();
        if (professionsJson is not null)
        {
            using var doc = JsonDocument.Parse(professionsJson);
            foreach (var p in doc.RootElement.GetProperty("professions").EnumerateArray())
            {
                string id = RequireString(p, "id", "yrke");
                if (professions.Exists(x => x.Id == id)) throw new GameDataException($"Yrket {id} finns två gånger");
                var role = RequireString(p, "role", id) switch
                {
                    "carrier" => PersonRole.Carrier,
                    "laborer" => PersonRole.Laborer,
                    "worker" => PersonRole.Worker,
                    var r => throw new GameDataException($"{id}: okänd roll {r}"),
                };
                int tool = p.TryGetProperty("tool", out var t) ? Good(t.GetString()!, id) : -1;
                professions.Add(new ProfessionDef(professions.Count, id, RequireString(p, "name", id), role, tool));
            }
        }

        int Profession(string id, string where)
        {
            foreach (var p in professions) if (p.Id == id) return p.Index;
            throw new GameDataException($"{where}: okänt yrke {id}");
        }

        var trade = new List<Recipe>();
        if (tradeJson is not null)
        {
            using var doc = JsonDocument.Parse(tradeJson);
            int ticks = doc.RootElement.GetProperty("seconds").GetInt32() * GameState.TicksPerSecond;
            foreach (var o in doc.RootElement.GetProperty("offers").EnumerateArray())
            {
                int buy = Good(RequireString(o, "buy", "lanthandeln"), "trade.json");
                var faction = o.TryGetProperty("faction", out var fa) ? ParseFaction(fa.GetString()!, "trade.json") : FactionRule.Both;
                foreach (var pay in o.GetProperty("pay").EnumerateArray())
                {
                    var price = new List<GoodAmount>();
                    foreach (var p in pay.EnumerateObject()) price.Add(new GoodAmount(Good(p.Name, "trade.json"), p.Value.GetInt32()));
                    trade.Add(new Recipe(price.ToArray(), new[] { new GoodAmount(buy, 1) }, ticks, faction));
                }
            }
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

                var faction = ParseFaction(RequireString(b, "faction", id), id);

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

                bool isTrade = b.TryGetProperty("trade", out var tr) && tr.GetBoolean();
                if (isTrade) recipes.AddRange(trade);

                string? worker = b.TryGetProperty("worker", out var wk) ? wk.GetString() : null;
                if (worker is not null && professions.Count > 0) Profession(worker, id);

                var startPeople = new List<ProfessionAmount>();
                if (b.TryGetProperty("start_people", out var sp))
                    foreach (var p in sp.EnumerateObject())
                        startPeople.Add(new ProfessionAmount(Profession(p.Name, id), p.Value.GetInt32()));

                int table = b.TryGetProperty("table", out var tb) ? tb.GetInt32() : 0;
                var accepts = new List<int>();
                if (table > 0) foreach (var f in foods) accepts.Add(f.Good);

                SchoolDef? school = null;
                if (b.TryGetProperty("school", out var sc))
                {
                    var pay = new List<GoodAmount[]>();
                    foreach (var p in sc.GetProperty("pay").EnumerateArray())
                    {
                        var price = new List<GoodAmount>();
                        foreach (var a in p.EnumerateObject()) price.Add(new GoodAmount(Good(a.Name, id), a.Value.GetInt32()));
                        pay.Add(price.ToArray());
                        foreach (var a in price) if (!accepts.Contains(a.Good)) accepts.Add(a.Good);
                    }
                    foreach (var p in professions)
                        if (p.Tool >= 0 && !accepts.Contains(p.Tool)) accepts.Add(p.Tool);
                    school = new SchoolDef(sc.GetProperty("seconds").GetInt32() * GameState.TicksPerSecond,
                        sc.GetProperty("queue").GetInt32(), pay.ToArray());
                }

                var blocked = new List<int>();
                if (b.TryGetProperty("blocked", out var bl))
                    foreach (var g in bl.EnumerateArray()) blocked.Add(Good(g.GetString()!, id));

                buildings.Add(new BuildingDef
                {
                    Index = buildings.Count,
                    Id = id,
                    Name = RequireString(b, "name", id),
                    Width = w,
                    Height = h,
                    Faction = faction,
                    Worker = worker,
                    Buildable = buildable,
                    Cost = cost,
                    BuildTicks = buildSeconds * GameState.TicksPerSecond,
                    Storage = b.TryGetProperty("storage", out var st) ? st.GetInt32() : 0,
                    StartStock = Amounts(b, "start_stock", id),
                    GathersFrom = gathers,
                    GatherRadius = radius,
                    Recipes = recipes.ToArray(),
                    Beds = b.TryGetProperty("beds", out var bd) ? bd.GetInt32() : 1,
                    StartPeople = startPeople.ToArray(),
                    Table = table,
                    Accepts = accepts.ToArray(),
                    Blocked = blocked.ToArray(),
                    NextTo = b.TryGetProperty("next_to", out var nt) ? ParseTerrain(nt.GetString()!, id) : null,
                    IsTrade = isTrade,
                    ClosedSundays = b.TryGetProperty("closed_sundays", out var cs) && cs.GetBoolean(),
                    School = school,
                });
            }
        }
        return new GameData(goods.ToArray(), buildings.ToArray(), foods.ToArray(), mood, professions.ToArray());
    }

    private static string RequireString(JsonElement e, string property, string where)
    {
        if (!e.TryGetProperty(property, out var v) || v.ValueKind != JsonValueKind.String || v.GetString() is not { Length: > 0 } s)
            throw new GameDataException($"{where}: saknar {property}");
        return s;
    }

    private static FactionRule ParseFaction(string name, string where) => name switch
    {
        "both" => FactionRule.Both,
        "torpet" => FactionRule.Torpet,
        "storgarden" => FactionRule.Storgarden,
        _ => throw new GameDataException($"{where}: okänt läger {name}"),
    };

    internal static Terrain ParseTerrain(string name, string where) => name switch
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
                h.Add((byte)r.Faction);
            }
            h.Add(b.Beds);
            h.Add(b.StartPeople.Length);
            foreach (var p in b.StartPeople)
            {
                h.Add(p.Profession);
                h.Add(p.Count);
            }
            h.Add(b.Table);
            h.Add(b.Accepts.Length);
            foreach (int g in b.Accepts) h.Add(g);
            h.Add(b.Blocked.Length);
            foreach (int g in b.Blocked) h.Add(g);
            h.Add(b.NextTo.HasValue ? (int)b.NextTo.Value : -1);
            h.Add(b.IsTrade);
            h.Add(b.ClosedSundays);
            h.Add(b.School is not null);
            if (b.School is { } s)
            {
                h.Add(s.Ticks);
                h.Add(s.QueueLimit);
                h.Add(s.Pay.Length);
                foreach (var p in s.Pay) AddAmounts(ref h, p);
            }
            AddString(ref h, b.Worker ?? "");
        }
        h.Add(Mood.Max);
        h.Add(Mood.TicksPerPoint);
        h.Add(Mood.EatAt);
        h.Add(Mood.RestTicks);
        h.Add(Foods.Count);
        foreach (var f in Foods)
        {
            h.Add(f.Good);
            h.Add(f.Mood);
            h.Add(f.With);
            AddString(ref h, f.Bonus);
            h.Add(f.BonusPercent);
            h.Add(f.BonusTicks);
        }
        h.Add(Professions.Count);
        foreach (var p in Professions)
        {
            AddString(ref h, p.Id);
            h.Add((byte)p.Role);
            h.Add(p.Tool);
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

using Hallonkriget.Sim.Data;
using Hallonkriget.Sim.Determinism;
using Hallonkriget.Sim.Map;

namespace Hallonkriget.Sim.Buildings;

public enum BuildingStage : byte
{
    /// <summary>Byggplats: väntar på material och hantlangarnas arbete.</summary>
    Construction,
    Done,
}

/// <summary>
/// En byggnad på kartan: byggplats eller färdig, med inlager, utlager och recept som pågår.
/// Förråd (stugan, boden) har i stället ett gemensamt lager för alla varor.
///
/// Byggnaden vet inget om bärare. Leveranssystemet frågar vad den behöver (InputSpace,
/// MaterialNeeded) och vad den har (OutputCount), och flyttar varorna med Put och Take.
/// </summary>
public sealed class Building
{
    /// <summary>Designdokumentet: inlager och utlager rymmer högst 5 av varje vara.</summary>
    public const int StockLimit = 5;

    public int Id { get; }
    public BuildingDef Def { get; }
    public byte Owner { get; }

    /// <summary>Övre vänstra rutan.</summary>
    public TilePoint Origin { get; }

    /// <summary>Dörren: rutan mitt under byggnaden. Dit går bärare och arbetare.</summary>
    public TilePoint Entrance { get; }

    public BuildingStage Stage { get; private set; }

    /// <summary>Byggarbete i tick, utfört av hantlangare.</summary>
    public int WorkDone { get; private set; }

    /// <summary>Om byggnadens arbetare är på plats. Sätts av personerna (sim/People).</summary>
    public bool HasWorker { get; set; }

    /// <summary>Receptet spelaren valt, eller -1: turas om mellan de recept som går att göra.</summary>
    public int SelectedRecipe { get; private set; } = -1;

    /// <summary>Receptet som pågår, eller -1.</summary>
    public int CurrentRecipe { get; private set; } = -1;

    public int CycleTicksLeft { get; private set; }

    private readonly int[] _delivered;   // material per rad i Def.Cost
    private readonly int[] _input;       // per vara
    private readonly int[] _output;      // per vara; för förråd är det hela lagret
    private int _nextAutoRecipe;

    internal Building(int id, BuildingDef def, byte owner, TilePoint origin, int goodCount)
    {
        Id = id;
        Def = def;
        Owner = owner;
        Origin = origin;
        Entrance = EntranceFor(def, origin);
        _delivered = new int[def.Cost.Length];
        _input = new int[goodCount];
        _output = new int[goodCount];
    }

    public static TilePoint EntranceFor(BuildingDef def, TilePoint origin) =>
        new(origin.X + def.Width / 2, origin.Y + def.Height);

    public bool Covers(TilePoint p) =>
        p.X >= Origin.X && p.Y >= Origin.Y && p.X < Origin.X + Def.Width && p.Y < Origin.Y + Def.Height;

    // ---- Byggande ----

    /// <summary>Hur många av varan som ännu ska levereras till byggplatsen.</summary>
    public int MaterialNeeded(int good)
    {
        if (Stage != BuildingStage.Construction) return 0;
        for (int i = 0; i < Def.Cost.Length; i++)
            if (Def.Cost[i].Good == good) return Def.Cost[i].Count - _delivered[i];
        return 0;
    }

    public bool DeliverMaterial(int good)
    {
        if (MaterialNeeded(good) <= 0) return false;
        for (int i = 0; i < Def.Cost.Length; i++)
            if (Def.Cost[i].Good == good) _delivered[i]++;
        return true;
    }

    /// <summary>
    /// Hur långt bygget kan komma med det material som finns. Hantlangarna bygger medan materialet
    /// kommer, så halva materialet räcker till halva bygget.
    /// </summary>
    public int WorkAllowed
    {
        get
        {
            int total = 0, delivered = 0;
            for (int i = 0; i < Def.Cost.Length; i++)
            {
                total += Def.Cost[i].Count;
                delivered += _delivered[i];
            }
            return total == 0 ? Def.BuildTicks : (int)((long)Def.BuildTicks * delivered / total);
        }
    }

    /// <summary>Ett tick byggarbete. Returnerar true när huset blev färdigt.</summary>
    public bool AddWork()
    {
        if (Stage != BuildingStage.Construction || WorkDone >= WorkAllowed) return false;
        WorkDone++;
        if (WorkDone < Def.BuildTicks) return false;
        Stage = BuildingStage.Done;
        return true;
    }

    internal void CompleteAtOnce(GoodAmount[] stock)
    {
        Stage = BuildingStage.Done;
        WorkDone = Def.BuildTicks;
        foreach (var a in stock) _output[a.Good] += a.Count;
    }

    // ---- Lager ----

    public int StoredTotal
    {
        get
        {
            int sum = 0;
            foreach (int c in _output) sum += c;
            return sum;
        }
    }

    /// <summary>Hur många till av varan byggnaden kan ta emot just nu.</summary>
    public int InputSpace(int good)
    {
        if (Stage != BuildingStage.Done) return 0;
        if (Def.IsStorage) return Def.Storage - StoredTotal;
        if (!Uses(good)) return 0;
        return StockLimit - _input[good];
    }

    public int InputCount(int good) => _input[good];

    /// <summary>Vad byggnaden har färdigt att lämna ut. För förråd: hela lagret.</summary>
    public int OutputCount(int good) => _output[good];

    public bool PutInput(int good)
    {
        if (InputSpace(good) <= 0) return false;
        if (Def.IsStorage) _output[good]++;
        else _input[good]++;
        return true;
    }

    public bool TakeOutput(int good)
    {
        if (_output[good] <= 0) return false;
        _output[good]--;
        return true;
    }

    /// <summary>Används varan i något recept som får köras?</summary>
    private bool Uses(int good)
    {
        for (int r = 0; r < Def.Recipes.Length; r++)
        {
            if (SelectedRecipe >= 0 && r != SelectedRecipe) continue;
            foreach (var a in Def.Recipes[r].In)
                if (a.Good == good) return true;
        }
        return false;
    }

    // ---- Produktion ----

    public void SelectRecipe(int recipe)
    {
        if (recipe < -1 || recipe >= Def.Recipes.Length) return;
        SelectedRecipe = recipe;
    }

    internal void UpdateProduction(GameMap map)
    {
        if (Stage != BuildingStage.Done || Def.Recipes.Length == 0) return;

        if (CurrentRecipe >= 0)
        {
            if (--CycleTicksLeft > 0) return;
            foreach (var a in Def.Recipes[CurrentRecipe].Out) _output[a.Good] += a.Count;
            CurrentRecipe = -1;
            return;
        }

        if (Def.Worker is not null && !HasWorker) return;
        if (Def.GathersFrom is { } terrain && !TerrainNearby(map, terrain, Def.GatherRadius)) return;

        int n = Def.Recipes.Length;
        for (int k = 0; k < n; k++)
        {
            int r = SelectedRecipe >= 0 ? SelectedRecipe : (_nextAutoRecipe + k) % n;
            if (CanStart(Def.Recipes[r]))
            {
                foreach (var a in Def.Recipes[r].In) _input[a.Good] -= a.Count;
                CurrentRecipe = r;
                CycleTicksLeft = Def.Recipes[r].Ticks;
                _nextAutoRecipe = (r + 1) % n;
                return;
            }
            if (SelectedRecipe >= 0) return;
        }
    }

    private bool CanStart(Recipe recipe)
    {
        foreach (var a in recipe.In)
            if (_input[a.Good] < a.Count) return false;
        // Utlagret rymmer 5, men ett recept som ger fler (knäckebröd ×3, hårdkokta ägg ×10)
        // får alltid starta när utlagret är tomt nog.
        foreach (var a in recipe.Out)
            if (_output[a.Good] + a.Count > IntMath.Max(StockLimit, a.Count)) return false;
        return true;
    }

    /// <summary>Finns terrängen inom radien, räknat från dörren?</summary>
    private bool TerrainNearby(GameMap map, Terrain terrain, int radius)
    {
        int r2 = radius * radius;
        for (int dy = -radius; dy <= radius; dy++)
        for (int dx = -radius; dx <= radius; dx++)
        {
            if (dx * dx + dy * dy > r2) continue;
            var p = new TilePoint(Entrance.X + dx, Entrance.Y + dy);
            if (map.Inside(p) && map.TerrainAt(p) == terrain) return true;
        }
        return false;
    }

    internal void AddToHash(ref StateHasher h)
    {
        h.Add(Id);
        h.Add(Def.Index);
        h.Add(Owner);
        h.Add(Origin.X);
        h.Add(Origin.Y);
        h.Add((byte)Stage);
        h.Add(WorkDone);
        h.Add(HasWorker);
        h.Add(SelectedRecipe);
        h.Add(CurrentRecipe);
        h.Add(CycleTicksLeft);
        h.Add(_nextAutoRecipe);
        foreach (int d in _delivered) h.Add(d);
        foreach (int c in _input) h.Add(c);
        foreach (int c in _output) h.Add(c);
    }
}

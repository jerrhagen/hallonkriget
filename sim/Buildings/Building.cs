using System;
using System.Collections.Generic;
using Hallonkriget.Sim.Data;
using Hallonkriget.Sim.Determinism;
using Hallonkriget.Sim.Map;

namespace Hallonkriget.Sim.Buildings;

public enum BuildingStage : byte
{
    /// <summary>Byggplats: väntar på material och hantlangarnas arbete.</summary>
    Construction,
    Done,

    /// <summary>Gärdsgård, staket eller vedtrave som har slagits sönder. Rutan går att gå på igen.</summary>
    Ruined,
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

    /// <summary>Personen som har den här arbetsplatsen, även när hen är ute och äter. -1 när ingen.</summary>
    public int WorkerId { get; set; } = -1;

    /// <summary>Receptet spelaren valt, eller -1: turas om mellan de recept som går att göra.</summary>
    public int SelectedRecipe { get; private set; }

    /// <summary>Receptet som pågår, eller -1.</summary>
    public int CurrentRecipe { get; private set; } = -1;

    public int CycleTicksLeft { get; private set; }

    /// <summary>Spelaren som har tagit byggnaden, eller 255. En tagen byggnad står tom tills fienden har gått.</summary>
    public byte TakenBy { get; internal set; } = Map.GameMap.NoOwner;

    /// <summary>Hur länge fienden har stått vid dörren utan motstånd, eller (för en tagen byggnad) hur länge den varit fri.</summary>
    public int CaptureTicks { get; internal set; }

    /// <summary>Skada på gärdsgården, staketet, vedtraven eller hunden i kojan. Noll är helt.</summary>
    public int Damage { get; internal set; }

    /// <summary>Tick kvar till vedtraven eller hunden slår nästa gång.</summary>
    public int StrikeTimer { get; internal set; }

    /// <summary>Hunden har gett upp och kommer tillbaka det här ticket. Noll när den är hemma.</summary>
    public int AwayUntil { get; internal set; }

    public bool IsTaken => TakenBy != Map.GameMap.NoOwner;

    /// <summary>Varor på väg hit med bärare, per vara. Räknas av när de kommer fram.</summary>
    internal readonly int[] Incoming;

    /// <summary>Varor som en bärare är på väg att hämta härifrån, per vara.</summary>
    internal readonly int[] Outgoing;

    /// <summary>Bygdegårdens kö: yrken som ska utbildas, det första pågår eller väntar på betalning.</summary>
    public IReadOnlyList<ProfessionDef> TrainingQueue => _queue;

    /// <summary>Yrket som utbildas just nu, eller -1.</summary>
    public int Training { get; private set; } = -1;

    /// <summary>Speldagen då ett dagligt recept senast startade, eller -1.</summary>
    private int _lastDay = -1;

    /// <summary>Hur mycket byggnaden har tagit från kartan. Skogshuggaren planterar efter det.</summary>
    internal int Gathered;

    private readonly List<ProfessionDef> _queue = new();
    private readonly bool[] _blocked;    // per vara: spelaren har spärrat den
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
        Incoming = new int[goodCount];
        Outgoing = new int[goodCount];
        _blocked = new bool[goodCount];
        foreach (int g in def.Blocked) _blocked[g] = true;
        SelectedRecipe = def.DefaultRecipe;
    }

    /// <summary>Dörren. En gärdsgård har ingen: materialet lämnas på rutan.</summary>
    public static TilePoint EntranceFor(BuildingDef def, TilePoint origin) =>
        def.IsWall ? origin : new(origin.X + def.Width / 2, origin.Y + def.Height);

    public bool Covers(TilePoint p) =>
        p.X >= Origin.X && p.Y >= Origin.Y && p.X < Origin.X + Def.Width && p.Y < Origin.Y + Def.Height;

    /// <summary>Odlingsrutorna till höger om byggnaden, för åkern.</summary>
    public static IEnumerable<TilePoint> FieldTiles(BuildingDef def, TilePoint origin)
    {
        for (int y = origin.Y; y < origin.Y + def.FieldHeight; y++)
        for (int x = origin.X + def.Width; x < origin.X + def.Width + def.FieldWidth; x++)
            yield return new TilePoint(x, y);
    }

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

    /// <summary>Slagen sönder: står kvar som en ruin.</summary>
    internal void Ruin() => Stage = BuildingStage.Ruined;

    /// <summary>Tagen av fienden: allt i lagren hamnar på marken och är borta, och det som pågår avbryts.</summary>
    internal void Plunder(byte by)
    {
        TakenBy = by;
        CaptureTicks = 0;
        Array.Clear(_input);
        Array.Clear(_output);
        CurrentRecipe = -1;
        HasWorker = false;
        WorkerId = -1;
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
        if (Stage != BuildingStage.Done || IsTaken) return 0;
        if (Def.IsStorage) return Def.Storage - StoredTotal;
        if (!Uses(good)) return 0;
        if (Def.Table > 0) return Def.Table - InputTotal;
        if (Def.School is { } school) return SchoolSpace(school, good);
        return Def.StockLimit - _input[good];
    }

    /// <summary>Hur många till som kan begäras, med det som redan är på väg. Bordet delas av all mat.</summary>
    internal int RequestSpace(int good)
    {
        if (Def.Table == 0) return InputSpace(good) - Incoming[good];
        int incoming = 0;
        foreach (int c in Incoming) incoming += c;
        return InputSpace(good) - incoming;
    }

    public int InputTotal
    {
        get
        {
            int sum = 0;
            foreach (int c in _input) sum += c;
            return sum;
        }
    }

    /// <summary>Tar en vara ur inlagret, som när någon äter på kafferepet.</summary>
    public bool TakeInput(int good)
    {
        if (_input[good] <= 0) return false;
        _input[good]--;
        return true;
    }

    public bool IsBlocked(int good) => _blocked[good];

    /// <summary>Spärrar en vara så att byggnaden inte begär den, eller häver spärren.</summary>
    public void SetBlocked(int good, bool blocked)
    {
        if (good >= 0 && good < _blocked.Length) _blocked[good] = blocked;
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

    /// <summary>
    /// Vill byggnaden ha varan? Den ska ingå i ett recept som får köras, stå på kafferepets bord, eller
    /// betala för utbildningen som står först i bygdegårdens kö. Spärrade varor begärs inte.
    /// </summary>
    internal bool Uses(int good)
    {
        if (_blocked[good]) return false;
        if (Def.School is { } school) return SchoolUses(school, good);
        foreach (int g in Def.Accepts)
            if (g == good) return true;
        if (Def.IsTrade && SelectedRecipe < 0) return false;
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

    /// <summary>
    /// Ett tick produktion. Returnerar receptet om en omgång blev klar.
    /// gather: tar det receptet behöver från kartan, eller svarar false om inget finns inom räckhåll.
    /// closed: söndag för lanthandeln. workBonus: arbetaren har druckit svagdricka och jobbar fortare.
    /// </summary>
    internal Recipe? UpdateProduction(int tick, Func<Building, GatherDef, bool> gather, bool closed = false, int workBonus = 0)
    {
        if (Stage != BuildingStage.Done || Def.Recipes.Length == 0) return null;
        if (Def.IsTrade && SelectedRecipe < 0 && CurrentRecipe < 0) return null;

        if (CurrentRecipe >= 0)
        {
            CycleTicksLeft--;
            // Bonusen i procent: ett extra tick arbete på vart 100/procent:e tick.
            if (workBonus > 0 && HasWorker && tick % (100 / workBonus) == 0) CycleTicksLeft--;
            if (CycleTicksLeft > 0) return null;
            var finished = Def.Recipes[CurrentRecipe];
            foreach (var a in finished.Out) _output[a.Good] += a.Count;
            CurrentRecipe = -1;
            return finished;
        }

        if (Def.Worker is not null && !HasWorker) return null;
        if (closed && Def.ClosedSundays) return null;
        if (Def.Daily && (GameClock.Day(tick) == _lastDay || GameClock.IsSunday(tick))) return null;

        int n = Def.Recipes.Length;
        for (int k = 0; k < n; k++)
        {
            int r = SelectedRecipe >= 0 ? SelectedRecipe : (_nextAutoRecipe + k) % n;
            var recipe = Def.Recipes[r];
            if (CanStart(recipe) && (recipe.Gather ?? Def.Gather) is var g && (g is null || gather(this, g)))
            {
                foreach (var a in recipe.In) _input[a.Good] -= a.Count;
                CurrentRecipe = r;
                CycleTicksLeft = recipe.Ticks;
                _nextAutoRecipe = (r + 1) % n;
                if (Def.Daily) _lastDay = GameClock.Day(tick);
                return null;
            }
            if (SelectedRecipe >= 0) return null;
        }
        return null;
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

    // ---- Bygdegården ----

    /// <summary>Ställer ett yrke i kön. Returnerar false om kön är full.</summary>
    internal bool Enqueue(ProfessionDef profession)
    {
        if (Def.School is not { } school || Stage != BuildingStage.Done || _queue.Count >= school.QueueLimit) return false;
        _queue.Add(profession);
        return true;
    }

    /// <summary>Tar bort det sista i kön som inte har börjat.</summary>
    internal void CancelLast()
    {
        int first = Training >= 0 ? 1 : 0;
        if (_queue.Count > first) _queue.RemoveAt(_queue.Count - 1);
    }

    /// <summary>Kaffe eller surrogat och verktyget för den som står näst på tur, så länge någon väntar.</summary>
    private bool SchoolUses(SchoolDef school, int good)
    {
        int waiting = Training >= 0 ? 1 : 0;
        if (_queue.Count <= waiting) return false;
        return _queue[waiting].Tool == good || IsSchoolPayment(school, good);
    }

    private static bool IsSchoolPayment(SchoolDef school, int good) => PaymentPerPerson(school, good) > 0;

    private static int PaymentPerPerson(SchoolDef school, int good)
    {
        foreach (var pay in school.Pay)
        foreach (var a in pay)
            if (a.Good == good) return a.Count;
        return 0;
    }

    /// <summary>
    /// Bygdegården begär bara det kön behöver: kaffe (eller surrogat) för dem som väntar och ett
    /// verktyg i taget, så att kaffet och verktygen inte samlas där i onödan.
    /// </summary>
    private int SchoolSpace(SchoolDef school, int good)
    {
        int waiting = _queue.Count - (Training >= 0 ? 1 : 0);
        int perPerson = PaymentPerPerson(school, good);
        int wanted = perPerson > 0 ? IntMath.Min(StockLimit, waiting * perPerson) : 1;
        return IntMath.Max(0, wanted - _input[good]);
    }

    /// <summary>
    /// Ett tick i bygdegården. Första i kön börjar när betalningen och verktyget finns och det finns
    /// plats för en till (room). Returnerar yrket när en ny person är klar.
    /// </summary>
    internal ProfessionDef? UpdateSchool(bool room)
    {
        if (Def.School is not { } school || Stage != BuildingStage.Done) return null;

        if (Training >= 0)
        {
            if (--CycleTicksLeft > 0) return null;
            var done = _queue[0];
            Training = -1;
            _queue.RemoveAt(0);
            return done;
        }

        if (_queue.Count == 0 || !room) return null;
        var next = _queue[0];
        if (next.Tool >= 0 && _input[next.Tool] < 1) return null;
        foreach (var pay in school.Pay)
        {
            bool enough = true;
            foreach (var a in pay)
                if (_input[a.Good] < a.Count) enough = false;
            if (!enough) continue;
            foreach (var a in pay) _input[a.Good] -= a.Count;
            if (next.Tool >= 0) _input[next.Tool]--;
            Training = next.Index;
            CycleTicksLeft = school.Ticks;
            return null;
        }
        return null;
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
        h.Add(WorkerId);
        h.Add(Training);
        h.Add(_queue.Count);
        foreach (var q in _queue) h.Add(q.Index);
        h.AddSparse(_blocked);
        h.Add(SelectedRecipe);
        h.Add(CurrentRecipe);
        h.Add(CycleTicksLeft);
        h.Add(_nextAutoRecipe);
        h.Add(_lastDay);
        h.Add(Gathered);
        h.Add(TakenBy);
        h.Add(CaptureTicks);
        h.Add(Damage);
        h.Add(StrikeTimer);
        h.Add(AwayUntil);
        foreach (int d in _delivered) h.Add(d);
        h.AddSparse(_input);
        h.AddSparse(_output);
        h.AddSparse(Incoming);
        h.AddSparse(Outgoing);
    }
}

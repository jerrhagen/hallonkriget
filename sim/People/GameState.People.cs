using System.Collections.Generic;
using Hallonkriget.Sim.Buildings;
using Hallonkriget.Sim.Data;
using Hallonkriget.Sim.Determinism;
using Hallonkriget.Sim.Map;
using Hallonkriget.Sim.People;

namespace Hallonkriget.Sim;

/// <summary>
/// Personerna: hur de går och vad de gör när de inte bär. Bärarnas leveranser ligger i
/// sim/Economy/GameState.Deliveries.cs.
/// </summary>
public sealed partial class GameState
{
    /// <summary>1,2 rutor per sekund på upptrampad stig, i kostnadsenheter × 10 per tick.</summary>
    public const int WalkSpeed = 48;

    /// <summary>Så länge en hantlangare står på en stigruta innan den är upptrampad.</summary>
    public const int TreadTicks = 4 * TicksPerSecond;

    /// <summary>Designdokumentet: två kan mötas på en stigruta, den tredje väntar.</summary>
    public const int PeoplePerPathTile = 2;

    /// <summary>Efter så här lång väntan tränger sig personen förbi ändå, så att ingen fastnar för alltid.</summary>
    public const int MaxWaitTicks = TicksPerSecond;

    /// <summary>Högst så här många hantlangare på samma bygge.</summary>
    public const int LaborersPerSite = 2;

    private readonly List<Person> _people = new();
    private readonly List<(byte Owner, TilePoint Tile)> _plannedPaths = new();
    private int _nextPersonId;
    private Pathfinder? _pathfinder;
    private int[]? _peopleOnTile;

    public IReadOnlyList<Person> People => _people;

    private Pathfinder Paths => _pathfinder ??= new Pathfinder(Map);

    private int[] PeopleOnTile => _peopleOnTile ??= new int[Map.TileCount];

    internal Person SpawnPerson(byte owner, PersonRole role, TilePoint tile, string profession = "")
    {
        var person = new Person(_nextPersonId++, owner, role, profession, tile, WalkSpeed, Data.Mood.Max, Data.Mood.TicksPerPoint);
        _people.Add(person);
        PeopleOnTile[Map.Index(tile)]++;
        return person;
    }

    private Person? FindPerson(int id)
    {
        int i = PersonIndex(id);
        return i >= 0 ? _people[i] : null;
    }

    /// <summary>Platsen i listan. Personerna ligger i id-ordning, så det går att söka binärt.</summary>
    private int PersonIndex(int id)
    {
        int lo = 0, hi = _people.Count - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            int m = _people[mid].Id;
            if (m == id) return mid;
            if (m < id) lo = mid + 1;
            else hi = mid - 1;
        }
        return -1;
    }

    private void PlanPath(byte player, TilePoint tile)
    {
        if (player >= _players.Count || !Map.CanLayPath(tile) || Map.PathAt(tile) != PathState.None) return;
        if (Map.OwnerAt(tile) != GameMap.NoOwner && Map.OwnerAt(tile) != player) return;
        Map.SetPath(tile, PathState.Planned);
        _plannedPaths.Add((player, tile));
    }

    private void UpdatePeople()
    {
        UpdateGroups();
        foreach (var p in _people)
        {
            if (p.Inside)
            {
                if (p.Job == PersonJob.AtWork) LeaveWorkToEat(p);
                else if (p.Job == PersonJob.InBarracks) LeaveBarracksToEat(p);
                continue;
            }
            if (p.Role == PersonRole.Soldier && p.Job is PersonJob.Idle or PersonJob.Soldiering)
            {
                UpdateSoldier(p);
                continue;
            }
            if (p.IsMoving)
            {
                Step(p);
                if (p.IsMoving) continue;
            }
            Act(p);
        }
    }

    /// <summary>Ett tick framåt längs vägen.</summary>
    private void Step(Person p)
    {
        if (p.StepProgress < p.StepTotal)
        {
            p.StepProgress += EffectiveSpeed(p);
            return;
        }

        var next = p.Path[p.PathIndex + 1];
        var goal = p.Path[^1];
        var move = MoveOf(p);
        if (next != goal && !Map.IsWalkable(next, move))
        {
            // Något har byggts i vägen sedan vägen räknades ut.
            if (!WalkTo(p, goal)) StopWalking(p);
            return;
        }

        int nextIndex = Map.Index(next);
        if (Map.PathAt(next) == PathState.Trodden && PeopleOnTile[nextIndex] >= PeoplePerPathTile && p.WaitTicks < MaxWaitTicks)
        {
            p.WaitTicks++;
            return;
        }

        p.WaitTicks = 0;
        PeopleOnTile[Map.Index(p.Tile)]--;
        PeopleOnTile[nextIndex]++;
        bool diagonal = next.X != p.Tile.X && next.Y != p.Tile.Y;
        p.From = p.Tile;
        p.Tile = next;
        p.PathIndex++;
        // Det som blev över från förra steget följer med, så att farten blir rätt över långa sträckor.
        int carry = p.StepProgress - p.StepTotal;
        p.StepTotal = (diagonal ? Pathfinder.Diagonal : Pathfinder.Straight) * Map.StepCost(next, move) * 10;
        p.StepProgress = carry + EffectiveSpeed(p);
    }

    /// <summary>Farten just nu: pannkakor ger 10 procent mer i tre minuter.</summary>
    public int EffectiveSpeed(Person p) =>
        p.SpeedBonusTicks > 0 ? p.Speed * (100 + Data.BonusPercent(FoodBonus.Speed)) / 100 : p.Speed;

    /// <summary>Räknar ut vägen dit. Står personen redan där är den framme direkt.</summary>
    private bool WalkTo(Person p, TilePoint destination)
    {
        p.Path.Clear();
        p.PathIndex = 0;
        p.StepProgress = p.StepTotal = 0;
        p.From = p.Tile;
        if (p.Tile == destination) return true;
        if (!Paths.FindPath(p.Tile, destination, MoveOf(p), p.Path))
        {
            p.Path.Clear();
            return false;
        }
        return true;
    }

    private static void StopWalking(Person p)
    {
        p.Path.Clear();
        p.PathIndex = 0;
        p.StepProgress = p.StepTotal = 0;
    }

    /// <summary>Personen står still: antingen framme, mitt i ett arbete eller ledig.</summary>
    private void Act(Person p)
    {
        if (Hungry(p) && p.Job is PersonJob.Idle or PersonJob.Building or PersonJob.Treading && TryGoEat(p)) return;

        switch (p.Job)
        {
            case PersonJob.Idle:
                if (p.Role == PersonRole.Laborer) FindLaborerJob(p);
                else if (p.Role == PersonRole.Worker) FindWorkplace(p);
                else if (p.Role == PersonRole.Recruit) FindBarracks(p);
                break;

            case PersonJob.ToSite:
                p.Job = PersonJob.Building;
                goto case PersonJob.Building;

            case PersonJob.Building:
            {
                var site = _buildings[p.Target];
                site.AddWork();
                if (site.Stage != BuildingStage.Construction || site.WorkDone >= site.WorkAllowed) MakeIdle(p);
                break;
            }

            case PersonJob.ToPathTile:
                p.Job = PersonJob.Treading;
                p.Timer = TreadTicks;
                break;

            case PersonJob.Treading:
                if (Map.PathAt(p.TargetTile) != PathState.Planned)
                {
                    MakeIdle(p);
                    break;
                }
                if (--p.Timer > 0) break;
                Map.SetPath(p.TargetTile, PathState.Trodden);
                RemovePlannedPath(p.TargetTile);
                MakeIdle(p);
                break;

            case PersonJob.ToWorkplace:
            {
                if (p.Role == PersonRole.Recruit)
                {
                    EnterBarracks(p);
                    break;
                }
                var work = _buildings[p.Target];
                if (work.WorkerId != p.Id || p.Tile != work.Entrance)
                {
                    MakeIdle(p);
                    break;
                }
                work.HasWorker = true;
                p.Job = PersonJob.AtWork;
                p.Inside = true;
                PeopleOnTile[Map.Index(p.Tile)]--;
                break;
            }

            case PersonJob.ToPickup:
                ArriveAtPickup(p);
                break;

            case PersonJob.ToDropoff:
                ArriveAtDropoff(p);
                break;

            case PersonJob.ToEat:
                ArriveAtTable(p);
                break;

            case PersonJob.ToHome:
                LieDown(p);
                break;
        }
    }

    private static void MakeIdle(Person p)
    {
        p.Job = PersonJob.Idle;
        p.Target = -1;
        p.Timer = 0;
    }

    private void RemovePlannedPath(TilePoint tile)
    {
        for (int i = 0; i < _plannedPaths.Count; i++)
        {
            if (_plannedPaths[i].Tile != tile) continue;
            _plannedPaths.RemoveAt(i);
            return;
        }
    }

    /// <summary>Hantlangaren tar närmaste bygge som kan byggas vidare på, annars närmaste planerade stigruta.</summary>
    private void FindLaborerJob(Person p)
    {
        Building? bestSite = null;
        int best = int.MaxValue;
        foreach (var b in _buildings)
        {
            if (b.Owner != p.Owner || b.Stage != BuildingStage.Construction || b.WorkDone >= b.WorkAllowed) continue;
            if (CountWithTarget(PersonJob.ToSite, PersonJob.Building, b.Id) >= LaborersPerSite) continue;
            int d = Distance(p.Tile, b.Entrance);
            if (d < best)
            {
                best = d;
                bestSite = b;
            }
        }
        if (bestSite is not null && WalkTo(p, bestSite.Entrance))
        {
            p.Job = PersonJob.ToSite;
            p.Target = bestSite.Id;
            return;
        }

        int bestTile = -1;
        best = int.MaxValue;
        for (int i = 0; i < _plannedPaths.Count; i++)
        {
            var (owner, tile) = _plannedPaths[i];
            if (owner != p.Owner || TileTaken(tile)) continue;
            int d = Distance(p.Tile, tile);
            if (d < best)
            {
                best = d;
                bestTile = i;
            }
        }
        if (bestTile >= 0 && WalkTo(p, _plannedPaths[bestTile].Tile))
        {
            p.Job = PersonJob.ToPathTile;
            p.TargetTile = _plannedPaths[bestTile].Tile;
        }
    }

    /// <summary>
    /// Arbetaren går tillbaka till sin arbetsplats, eller till närmaste byggnad av eget slag som
    /// saknar arbetare. Platsen är hens också när hen är ute och äter.
    /// </summary>
    private void FindWorkplace(Person p)
    {
        Building? bestWork = null;
        int best = int.MaxValue;
        foreach (var b in _buildings)
        {
            if (b.WorkerId == p.Id)
            {
                bestWork = b;
                break;
            }
            if (b.Owner != p.Owner || b.Stage != BuildingStage.Done || b.WorkerId >= 0 || b.Def.Worker != p.Profession) continue;
            int d = Distance(p.Tile, b.Entrance);
            if (d < best)
            {
                best = d;
                bestWork = b;
            }
        }
        if (bestWork is not null && WalkTo(p, bestWork.Entrance))
        {
            bestWork.WorkerId = p.Id;
            p.Job = PersonJob.ToWorkplace;
            p.Target = bestWork.Id;
        }
    }

    private int CountWithTarget(PersonJob a, PersonJob b, int target)
    {
        int n = 0;
        foreach (var q in _people)
            if ((q.Job == a || q.Job == b) && q.Target == target) n++;
        return n;
    }

    private bool TileTaken(TilePoint tile)
    {
        foreach (var q in _people)
            if ((q.Job == PersonJob.ToPathTile || q.Job == PersonJob.Treading) && q.TargetTile == tile) return true;
        return false;
    }

    /// <summary>Uppskattat avstånd i rutor × 10, utan hänsyn till terräng. Används för att välja närmast.</summary>
    internal static int Distance(TilePoint a, TilePoint b)
    {
        int dx = IntMath.Abs(a.X - b.X), dy = IntMath.Abs(a.Y - b.Y);
        int diag = IntMath.Min(dx, dy);
        return Pathfinder.Straight * (IntMath.Max(dx, dy) - diag) + Pathfinder.Diagonal * diag;
    }

    private void AddPeopleToHash(ref StateHasher h)
    {
        h.Add(_nextPersonId);
        h.Add(_people.Count);
        foreach (var p in _people) p.AddToHash(ref h);
        h.Add(_plannedPaths.Count);
        foreach (var (owner, tile) in _plannedPaths)
        {
            h.Add(owner);
            h.Add(tile.X);
            h.Add(tile.Y);
        }
    }
}

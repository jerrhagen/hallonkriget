using System.Collections.Generic;
using Hallonkriget.Sim.Ai;
using Hallonkriget.Sim.Buildings;
using Hallonkriget.Sim.Data;
using Hallonkriget.Sim.Determinism;
using Hallonkriget.Sim.Map;
using Hallonkriget.Sim.Military;
using Hallonkriget.Sim.People;

namespace Hallonkriget.Sim;

/// <summary>
/// Datorspelaren, designdokumentet (Datorspelaren): en byggordning plus tre reflexer. Den gör samma
/// saker som en spelare kan göra med kommandon, och fuskar inte med varor. Den tänker en gång i sekunden.
///
/// Byggordningen placerar varje byggnad på närmaste lediga plats med en ruta fritt runt om, samlare nära
/// sin terräng, och lägger stig från dörren till stugan. Bygdegården utbildar arbetare till byggnader
/// som saknar, bärare, hantlangare och rekryter. Lanthandeln köper verktyg och kaffe för ved.
/// Reflex 1, mat: under 10 portioner på kafferepen utbildas inga rekryter.
/// Reflex 2, försvar: fiender inom 15 rutor från en egen byggnad anfalls av grupperna som står still.
/// Reflex 3, anfall: när armén är stor nog och kaffe finns anfalls närmaste fiendeförråd, gäss och höns först.
/// </summary>
public sealed partial class GameState
{
    private const int ThinkTicks = TicksPerSecond;
    private const int ThreatRadius = 15;
    private const int HeavyDelayTicks = 30 * TicksPerSecond;

    private readonly List<ComputerState?> _computers = new();

    /// <summary>Datorspelarens tillstånd för spelaren, eller null för en människa.</summary>
    public ComputerState? Computer(int player) => player < _computers.Count ? _computers[player] : null;

    private void RunComputerPlayers()
    {
        if (Data.Computer is not { } plan) return;
        foreach (var player in _players)
        {
            if (!player.IsComputer || player.Defeated || (TickCount + player.Id * 3) % ThinkTicks != 0) continue;
            if (HomeOf(player.Id) is not { } home) continue;
            var ai = _computers[player.Id]!;
            var level = plan.For(player.Difficulty);
            Build(player, ai, plan, level, home);
            Staff(player, plan, level);
            Trade(player);
            Arm(player, plan, level);
            Defend(player, ai);
            Attack(player, ai, plan, level, home);
        }
    }

    // ---- Byggordningen ----

    private void Build(Player player, ComputerState ai, ComputerPlan plan, DifficultyPlan level, Building home)
    {
        if (TickCount < ai.NextStepTick) return;
        // Byggplatserna tar bärarnas tid: en byggplats per tre bärare, högst tre.
        int sites = 0, carriers = 0;
        bool housing = false;
        foreach (var b in _buildings)
        {
            if (b.Owner != player.Id || b.Stage != BuildingStage.Construction) continue;
            sites++;
            if (b.Def.Beds > 1) housing = true;
        }
        foreach (var p in _people)
            if (p.Owner == player.Id && p.Role == PersonRole.Carrier) carriers++;
        if (sites >= IntMath.Clamp(carriers / 3, 1, 3)) return;

        // Fullt i sängarna när bygdegården finns: ett nytt bod före allt annat, annars står bygdegården still.
        if (!housing && Population(player.Id) >= Beds(player.Id) - 2
            && NearestOwn(player.Id, b => b.Def.School is not null && b.Stage == BuildingStage.Done) is not null)
        {
            ai.NextStepTick = TickCount + StepDelay(level);
            Place(player, Data.Buildings[plan.House], -1, home);
            return;
        }

        if (ai.NextStep >= plan.Steps.Length) return;
        // Inte fler hus än folket hinner med: högst två färdiga byggnader får vänta på sin arbetare.
        int unstaffed = 0;
        foreach (var b in _buildings)
            if (b.Owner == player.Id && b.Stage == BuildingStage.Done && b.Def.Worker is not null && b.WorkerId < 0 && !b.IsTaken) unstaffed++;
        if (unstaffed >= 2) return;
        // Står maten kvar i bagarstugor och förråd medan borden är tomma behövs bärarna till den först.
        if (NearestOwn(player.Id, b => b.Def.Table > 0 && b.Stage == BuildingStage.Done) is not null
            && FoodOnTables(player.Id) < 4 && FoodWaiting(player.Id) > 0) return;

        var step = plan.Steps[ai.NextStep++];
        ai.NextStepTick = TickCount + StepDelay(level);
        Place(player, Data.Buildings[step.Building], step.Recipe, home);
    }

    /// <summary>Tiden till nästa bygge, en fjärdedel hit eller dit, så att två datorer inte spelar samma match varje gång.</summary>
    private int StepDelay(DifficultyPlan level) => level.StepTicks * Rng.Range(75, 125) / 100;

    private void Place(Player player, BuildingDef def, int recipe, Building home)
    {
        if (!def.AllowedFor(player.Faction) || FindSpot(player.Id, def, home) is not { } spot) return;
        var building = AddBuilding(player.Id, def, spot);
        if (recipe >= 0) building.SelectRecipe(recipe);
        LayPathHome(player.Id, building, home);
    }

    /// <summary>
    /// Närmaste plats där byggnaden får stå med en ruta fritt runt om, räknat från stugan. En samlare
    /// ska ha sin terräng inom räckhåll, och platsen räknas från närmaste sådan terräng.
    /// </summary>
    internal TilePoint? FindSpot(byte player, BuildingDef def, Building home)
    {
        var center = home.Entrance;
        var prefer = center;
        if (def.Gather is { } g && NearestTile(center, 30, p => Map.TerrainAt(p) == g.Terrain && Map.OwnerAt(p) is var o && (o == player || o == GameMap.NoOwner)) is { } t)
            prefer = t;
        if (def.NextTo is { } next && NearestTile(center, 40, p => Map.TerrainAt(p) == next && Map.OwnerAt(p) is var o && (o == player || o == GameMap.NoOwner)) is { } n)
            prefer = n;

        TilePoint? best = null;
        int bestScore = int.MaxValue, foundAt = -1;
        for (int r = 0; r <= 30; r++)
        {
            if (foundAt >= 0 && r > foundAt + 3) break;
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                if (IntMath.Max(IntMath.Abs(dx), IntMath.Abs(dy)) != r) continue;
                var origin = new TilePoint(prefer.X + dx - def.Width / 2, prefer.Y + dy - def.Height / 2);
                if (!CanPlace(player, def, origin) || !Roomy(def, origin)) continue;
                var door = Building.EntranceFor(def, origin);
                if (def.Gather is { } gather && NearestTile(door, gather.Radius - 1, p => Map.TerrainAt(p) == gather.Terrain) is null) continue;
                int score = Distance(prefer, origin) + Distance(center, origin) / 2;
                if (score < bestScore)
                {
                    (bestScore, best) = (score, origin);
                    if (foundAt < 0) foundAt = r;
                }
            }
        }
        return best;
    }

    /// <summary>En ruta fritt runt byggnaden (och åkerns odlingsrutor), och ingen stig under den. Kartans kant räknas som fri.</summary>
    private bool Roomy(BuildingDef def, TilePoint origin)
    {
        int w = def.Width + def.FieldWidth;
        for (int y = origin.Y - 1; y <= origin.Y + def.Height + 1; y++)
        for (int x = origin.X - 1; x <= origin.X + w; x++)
        {
            var p = new TilePoint(x, y);
            if (!Map.Inside(p)) continue;
            if (Map.OccupantAt(p) != 0 || Map.TerrainAt(p) == Terrain.Field) return false;
            bool inside = x >= origin.X && x < origin.X + w && y >= origin.Y && y < origin.Y + def.Height;
            if (inside && Map.PathAt(p) != PathState.None) return false;
        }
        return true;
    }

    /// <summary>Planerar stig från dörren till stugans dörr. Vägen följer befintliga stigar där de finns.</summary>
    private void LayPathHome(byte player, Building b, Building home)
    {
        var path = new List<TilePoint>();
        if (!Paths.FindPath(b.Entrance, home.Entrance, MoveClass.Foot, path)) return;
        foreach (var t in path)
            if (Map.PathAt(t) == PathState.None && Map.CanLayPath(t)) PlanPath(player, t);
    }

    // ---- Bygdegården ----

    /// <summary>
    /// Bygdegårdens kö hålls kort, så att det viktigaste kommer först: handlaren (som köper kaffet som
    /// betalar utbildningen), sedan arbetare till byggnader som saknar, bärare, hantlangare och rekryter.
    /// </summary>
    private void Staff(Player player, ComputerPlan plan, DifficultyPlan level)
    {
        Building? school = null;
        foreach (var b in _buildings)
            if (b.Owner == player.Id && b.Def.School is not null && b.Stage == BuildingStage.Done && !b.IsTaken) school = b;
        if (school is null || school.TrainingQueue.Count >= 2) return;

        // Handlaren först: hen köper kaffet som betalar alla andra. Kaffet sparas åt hen redan medan handeln byggs.
        if (MissingWorker(player.Id, school, b => b.Def.IsTrade) is { } trader)
        {
            TrainIn(school, trader);
            return;
        }
        if (TradeWaitsForTrader(player.Id, school)) return;

        int buildings = 0, carriers = 0, laborers = 0;
        foreach (var b in _buildings)
            if (b.Owner == player.Id && b.Stage != BuildingStage.Ruined && !b.Def.IsWall) buildings++;
        foreach (var p in _people)
        {
            if (p.Owner != player.Id) continue;
            if (p.Role == PersonRole.Carrier) carriers++;
            if (p.Role == PersonRole.Laborer) laborers++;
        }
        foreach (var q in school.TrainingQueue)
        {
            if (q.Role == PersonRole.Carrier) carriers++;
            if (q.Role == PersonRole.Laborer) laborers++;
        }

        // Maten först: yrkena i planens "first", sedan hälften av bärarna, sedan övriga arbetare.
        if (MissingWorker(player.Id, school, b => System.Array.IndexOf(plan.First, b.Def.Worker) >= 0) is { } cook)
        {
            TrainIn(school, cook);
            return;
        }
        int wantCarriers = IntMath.Min(plan.Carriers, 3 + buildings);
        if (carriers < wantCarriers / 2)
        {
            TrainIn(school, "barare");
            return;
        }
        if (MissingWorker(player.Id, school, _ => true) is { } worker)
        {
            TrainIn(school, worker);
            return;
        }
        // En kopp kaffe sparas till arbetaren som en byggplats snart behöver.
        int sitesNeedingWorkers = 0;
        foreach (var b in _buildings)
            if (b.Owner == player.Id && b.Stage == BuildingStage.Construction && b.Def.Worker is not null) sitesNeedingWorkers++;
        int reserve = sitesNeedingWorkers > 0 ? 2 : 1;
        // Reflex 3 behöver kaffe till rasten: när armén är halvvägs sparas en bricka.
        if (ArmySize(player.Id) >= level.AttackAt / 2) reserve += Data.Combat.Tray;
        if (Available(player.Id, Data.GoodIndex("kaffe")) < reserve) return;
        // Reflex 1: med lite mat på borden utbildas inga rekryter. Annars går rekryterna före resten av bärarna.
        if (FoodOnTables(player.Id) + FoodWaiting(player.Id) >= 10 && RecruitsWanted(player.Id, school))
        {
            TrainIn(school, "rekryt");
            return;
        }
        if (carriers < wantCarriers)
        {
            TrainIn(school, "barare");
            return;
        }
        if (laborers < plan.Laborers)
        {
            TrainIn(school, "hantlangare");
            return;
        }
    }

    /// <summary>Färre än tre rekryter väntar i logen eller är på väg dit.</summary>
    private bool RecruitsWanted(byte player, Building school)
    {
        if (NearestOwn(player, b => b.Def.Barracks && b.Stage == BuildingStage.Done) is not { } barn) return false;
        int recruits = RecruitsIn(barn).Count;
        foreach (var q in school.TrainingQueue)
            if (q.Role == PersonRole.Recruit) recruits++;
        foreach (var p in _people)
            if (p.Owner == player && p.Role == PersonRole.Recruit && p.Job != PersonJob.InBarracks) recruits++;
        return recruits < 3;
    }

    private bool TradeWaitsForTrader(byte player, Building school)
    {
        foreach (var b in _buildings)
        {
            if (b.Owner != player || !b.Def.IsTrade || b.Stage != BuildingStage.Construction || b.Def.Worker is not { } job) continue;
            foreach (var p in _people)
                if (p.Owner == player && p.Profession == job) return false;
            foreach (var q in school.TrainingQueue)
                if (q.Id == job) return false;
            return true;
        }
        return false;
    }

    /// <summary>Yrket för första färdiga byggnaden utan arbetare, om ingen ledig eller i kön redan finns för den.</summary>
    private string? MissingWorker(byte player, Building school, System.Func<Building, bool> which)
    {
        foreach (var b in _buildings)
        {
            if (b.Owner != player || b.Stage != BuildingStage.Done || b.Def.Worker is not { } job || b.WorkerId >= 0 || b.IsTaken || !which(b)) continue;
            int wanted = 0, have = 0;
            foreach (var o in _buildings)
                if (o.Owner == player && o.Stage == BuildingStage.Done && o.Def.Worker == job && o.WorkerId < 0 && !o.IsTaken) wanted++;
            foreach (var p in _people)
                if (p.Owner == player && p.Profession == job && p.Job is PersonJob.Idle or PersonJob.ToEat or PersonJob.ToHome or PersonJob.Resting) have++;
            foreach (var q in school.TrainingQueue)
                if (q.Id == job) have++;
            if (have < wanted) return job;
        }
        return null;
    }

    private void TrainIn(Building school, string profession)
    {
        int index = Data.ProfessionIndex(profession);
        school.Enqueue(Data.Professions[index]);
    }

    /// <summary>Mat som ligger färdig i byggnader och förråd men inte på något bord.</summary>
    private int FoodWaiting(byte player)
    {
        int food = 0;
        foreach (var b in _buildings)
        {
            if (b.Owner != player || b.Stage != BuildingStage.Done || b.IsTaken) continue;
            foreach (var f in Data.Foods)
                if (f.With < 0 && Data.Goods[f.Good].Id != "kaffe") food += b.OutputCount(f.Good);
        }
        return food;
    }

    private int FoodOnTables(byte player)
    {
        int food = 0;
        foreach (var b in _buildings)
        {
            if (b.Owner != player || b.Def.Table == 0 || b.Stage != BuildingStage.Done) continue;
            foreach (var f in Data.Foods)
                if (f.With < 0 && Data.Goods[f.Good].Id != "kaffe") food += b.InputCount(f.Good);
        }
        return food;
    }

    private Building? NearestOwn(byte player, System.Func<Building, bool> match)
    {
        foreach (var b in _buildings)
            if (b.Owner == player && !b.IsTaken && match(b)) return b;
        return null;
    }

    /// <summary>Hur många av varan spelarens förråd har.</summary>
    public int Stored(byte player, int good)
    {
        int n = 0;
        foreach (var b in _buildings)
            if (b.Owner == player && b.Def.IsStorage && b.Stage == BuildingStage.Done && !b.IsTaken) n += b.OutputCount(good);
        return n;
    }

    /// <summary>Varan i förråden, i utlagren där den görs eller köps, och i bygdegården.</summary>
    private int Available(byte player, int good)
    {
        int n = 0;
        foreach (var b in _buildings)
        {
            if (b.Owner != player || b.Stage != BuildingStage.Done || b.IsTaken) continue;
            n += b.OutputCount(good);
            if (b.Def.School is not null) n += b.InputCount(good);
        }
        return n;
    }

    // ---- Lanthandeln ----

    /// <summary>
    /// Turas om som byggordningen brödgården: verktyg när det finns färre än två och inte fler än
    /// kaffe, annars kaffe. Båda betalas med ved. Det som ligger i handeln och bygdegården räknas med.
    /// </summary>
    private void Trade(Player player)
    {
        int tools = Data.GoodIndex("verktyg"), coffee = Data.GoodIndex("kaffe"), wood = Data.GoodIndex("ved");
        int haveTools = Available(player.Id, tools), haveCoffee = Available(player.Id, coffee);
        int buy = haveTools < 2 && haveTools <= haveCoffee ? tools : coffee;
        foreach (var b in _buildings)
        {
            if (b.Owner != player.Id || !b.Def.IsTrade || b.Stage != BuildingStage.Done) continue;
            for (int r = 0; r < b.Def.Recipes.Length; r++)
            {
                var recipe = b.Def.Recipes[r];
                if (recipe.Out[0].Good == buy && recipe.In.Length == 1 && recipe.In[0].Good == wood)
                {
                    if (b.SelectedRecipe != r) b.SelectRecipe(r);
                    break;
                }
            }
        }
    }

    // ---- Armén ----

    /// <summary>Logen gör enheterna i planens ordning. Snickarboden gör brickor tills drängarna finns, annars räfsor.</summary>
    private void Arm(Player player, ComputerPlan plan, DifficultyPlan level)
    {
        int servers = 0;
        foreach (var p in _people)
            if (p.Owner == player.Id && p.Unit >= 0 && Data.Units[p.Unit].Server) servers++;

        int tray = Data.GoodIndex("bricka"), rake = Data.GoodIndex("rafsa");
        foreach (var b in _buildings)
        {
            if (b.Owner != player.Id || b.Def.Id != "snickarboden" || b.Stage != BuildingStage.Done) continue;
            int want = servers < plan.Servers && Stored(player.Id, tray) + BarracksStock(player.Id, tray) == 0 ? tray : rake;
            for (int r = 0; r < b.Def.Recipes.Length; r++)
                if (b.Def.Recipes[r].Out[0].Good == want && b.SelectedRecipe != r) b.SelectRecipe(r);
        }

        foreach (var barn in _buildings)
        {
            if (barn.Owner != player.Id || !barn.Def.Barracks || barn.Stage != BuildingStage.Done || barn.IsTaken) continue;
            foreach (int unit in plan.Army)
            {
                var u = Data.Units[unit];
                if (!u.AllowedFor(player.Faction)) continue;
                // Drängarna först när det finns några att bjuda: fem soldater per dräng.
                if (u.Server && (servers >= plan.Servers || ArmySize(player.Id) < 5 * (servers + 1))) continue;
                if (EquipOne(barn, u) && u.Server) servers++;
            }
        }

        foreach (var g in _groups)
            if (g.Owner == player.Id && !g.IsEmpty && g.Columns != IntMath.Min(level.Columns, Data.Units[g.Unit].GroupMax))
                g.Columns = IntMath.Min(level.Columns, Data.Units[g.Unit].GroupMax);
    }

    private int BarracksStock(byte player, int good)
    {
        int n = 0;
        foreach (var b in _buildings)
            if (b.Owner == player && b.Def.Barracks && b.Stage == BuildingStage.Done) n += b.InputCount(good);
        return n;
    }

    /// <summary>Soldater som kan anfalla: inte gubben och inte kaffedrängarna.</summary>
    public int ArmySize(byte player)
    {
        int n = 0;
        foreach (var p in _people)
            if (p.Owner == player && p.Role == PersonRole.Soldier && IsFighter(p)) n++;
        return n;
    }

    private bool IsFighter(Person p) => p.Unit >= 0 && !Data.Units[p.Unit].Hero && !Data.Units[p.Unit].Server;

    private bool IsFightingGroup(Group g) => !Data.Units[g.Unit].Hero && !Data.Units[g.Unit].Server;

    // ---- Reflex 2: försvar ----

    private void Defend(Player player, ComputerState ai)
    {
        if ((TickCount / ThinkTicks + player.Id) % 2 != 0) return;
        Person? threat = null;
        foreach (var q in _people)
        {
            if (q.Owner == player.Id || q.Role != PersonRole.Soldier || q.Job != PersonJob.Soldiering || q.Group < 0) continue;
            foreach (var b in _buildings)
            {
                if (b.Owner != player.Id || b.Def.IsWall || Chebyshev(b.Entrance, q.Tile) > ThreatRadius) continue;
                threat = q;
                break;
            }
            if (threat is not null) break;
        }
        if (threat is null) return;
        ai.WasAttacked = true;
        foreach (var g in _groups)
        {
            if (g.Owner != player.Id || g.IsEmpty || g.Order != GroupOrder.Idle || !IsFightingGroup(g)) continue;
            AttackGroup(player.Id, g.Id, threat.Group);
        }
    }

    // ---- Reflex 3: anfall ----

    private void Attack(Player player, ComputerState ai, ComputerPlan plan, DifficultyPlan level, Building home)
    {
        int army = ArmySize(player.Id);
        if (!ai.Attacking)
        {
            if (!(level.Attacks || ai.WasAttacked) || army < level.AttackAt || !CoffeeForBreak(player.Id)) return;
            ai.Attacking = true;
            ai.AttackStart = TickCount;
        }
        else if (army < level.AttackAt / 3)
        {
            // Armén är slut: de som är kvar går hem, och nästa anfall väntar tills den är stor igen.
            ai.Attacking = false;
            foreach (var g in _groups)
                if (g.Owner == player.Id && !g.IsEmpty && IsFightingGroup(g)) MoveGroup(player.Id, g.Id, Gathering(home));
            return;
        }

        var target = EnemyStoreToTake(player.Id, home);
        int reserve = level.Reserve;
        Group? biggest = null;
        foreach (var g in _groups)
        {
            if (g.Owner != player.Id || g.IsEmpty || !IsFightingGroup(g)) continue;
            if (reserve > 0 && g.Order == GroupOrder.Idle && Chebyshev(g.Anchor, home.Entrance) < 20)
            {
                reserve--;
                continue;
            }
            if (biggest is null || g.Members.Count > biggest.Members.Count) biggest = g;
            if (g.Order != GroupOrder.Idle) continue;
            bool light = Data.Units[g.Unit].Recruits == 0;
            if (!light && TickCount < ai.AttackStart + HeavyDelayTicks) continue;
            if (target is not null) AttackBuilding(player.Id, g.Id, target.Id);
            else if (NearestEnemyGroup(player.Id, GroupCenter(g)) is { } enemy) AttackGroup(player.Id, g.Id, enemy.Id);
        }

        // Kaffedrängarna följer den största gruppen.
        if (biggest is null) return;
        foreach (var g in _groups)
            if (g.Owner == player.Id && !g.IsEmpty && Data.Units[g.Unit].Server && Chebyshev(g.Anchor, GroupCenter(biggest)) > 6)
                MoveGroup(player.Id, g.Id, GroupCenter(biggest));
    }

    /// <summary>Kaffe för nästa rast: i förråden, i handeln eller på drängarnas brickor.</summary>
    private bool CoffeeForBreak(byte player)
    {
        int coffee = Available(player, Data.GoodIndex("kaffe"));
        foreach (var p in _people)
            if (p.Owner == player) coffee += p.TrayCoffee;
        return coffee >= Data.Combat.Tray;
    }

    /// <summary>Närmaste fiendeförråd som inte redan är taget, räknat från stugan.</summary>
    private Building? EnemyStoreToTake(byte player, Building home)
    {
        Building? best = null;
        int bestDistance = int.MaxValue;
        foreach (var b in _buildings)
        {
            if (b.Owner == player || !b.Def.IsStorage || b.Stage != BuildingStage.Done || b.TakenBy == player || _players[b.Owner].Defeated) continue;
            int d = Distance(home.Entrance, b.Entrance);
            if (d < bestDistance) (bestDistance, best) = (d, b);
        }
        return best;
    }

    private Group? NearestEnemyGroup(byte player, TilePoint from)
    {
        Group? best = null;
        int bestDistance = int.MaxValue;
        foreach (var g in _groups)
        {
            if (g.Owner == player || g.IsEmpty) continue;
            int d = Distance(from, GroupCenter(g));
            if (d < bestDistance) (bestDistance, best) = (d, g);
        }
        return best;
    }

    /// <summary>Samlingsplatsen hemma: några rutor framför stugan.</summary>
    private TilePoint Gathering(Building home) => new(home.Entrance.X, IntMath.Min(MapHeight - 1, home.Entrance.Y + 4));

    private void AddComputersToHash(ref StateHasher h)
    {
        h.Add(_computers.Count);
        foreach (var c in _computers)
        {
            h.Add(c is not null);
            c?.AddToHash(ref h);
        }
    }
}

/// <summary>Det datorspelaren kommer ihåg mellan två tankar.</summary>
public sealed class ComputerState
{
    public int NextStep { get; internal set; }
    public int NextStepTick { get; internal set; }
    public bool WasAttacked { get; internal set; }
    public bool Attacking { get; internal set; }
    public int AttackStart { get; internal set; }

    internal void AddToHash(ref StateHasher h)
    {
        h.Add(NextStep);
        h.Add(NextStepTick);
        h.Add(WasAttacked);
        h.Add(Attacking);
        h.Add(AttackStart);
    }
}

using System.Collections.Generic;
using Hallonkriget.Sim.Buildings;
using Hallonkriget.Sim.Data;
using Hallonkriget.Sim.Determinism;
using Hallonkriget.Sim.Map;
using Hallonkriget.Sim.Military;
using Hallonkriget.Sim.People;

namespace Hallonkriget.Sim;

/// <summary>
/// Försvaret och belägringen, designdokumentet (Strid): gärdsgård och staket som bara katapulten,
/// traktorn och fläskkavalleriet slår sönder och som hantlangarna lagar, vedtraven, hundkojan,
/// råttfällorna, att ta en byggnad och segern.
/// </summary>
public sealed partial class GameState
{
    /// <summary>Högst så här många råttfällor per spelare.</summary>
    public const int MaxTraps = 10;

    /// <summary>Det en råttfälla tar.</summary>
    public const int TrapDamage = 15;

    /// <summary>En hantlangare lagar 10 hållfasthet per 10 sekunder.</summary>
    public const int RepairTicksPerPoint = 10;

    /// <summary>Fiender inom så många rutor från dörren hindrar att byggnaden tas, och håller den tagen.</summary>
    public const int CaptureRadius = 4;

    private readonly List<Trap> _traps = new();

    /// <summary>Utlagda och planerade råttfällor. Fienden ska inte få se dem.</summary>
    public IReadOnlyList<Trap> Traps => _traps;

    /// <summary>Spelaren som har vunnit, eller -1 medan matchen pågår.</summary>
    public int Winner { get; private set; } = -1;

    /// <summary>Hur mycket byggnaden tål: gärdsgårdens hållfasthet eller humöret hos enheten i den.</summary>
    public int MaxStrength(Building b) =>
        b.Def.IsWall ? b.Def.Wall : b.Def.FixedUnit >= 0 ? Data.Units[b.Def.FixedUnit].Mood : 0;

    // ---- Gärdsgård, vedtrave och hundkoja ----

    /// <summary>Torpet har en lånad hund. Storgården får en hundkoja till för varje färdig hundgård.</summary>
    private bool DogHouseAllowed(byte player)
    {
        int houses = 0, kennels = 0;
        foreach (var b in _buildings)
        {
            if (b.Owner != player || b.Stage == BuildingStage.Ruined) continue;
            if (b.Def.FixedUnit >= 0 && Data.Units[b.Def.FixedUnit].Eats >= 0) houses++;
            if (b.Def.Id == "hundgarden" && b.Stage == BuildingStage.Done) kennels++;
        }
        return houses < 1 + (_players[player].Faction == Faction.Storgarden ? kennels : 0);
    }

    /// <summary>Kan soldaten slå på byggnaden? Gärdsgård och vedtrave bara den som slår sönder sånt, hunden alla.</summary>
    private bool CanHit(Person p, UnitDef u, Building b)
    {
        if (b.Owner == p.Owner || b.Stage != BuildingStage.Done || u.Attack == 0) return false;
        if (b.Def.IsWall) return u.BreaksWalls;
        if (b.Def.FixedUnit < 0) return false;
        var fixedUnit = Data.Units[b.Def.FixedUnit];
        if (fixedUnit.Eats >= 0) return b.AwayUntil == 0;
        return u.BreaksWalls;
    }

    /// <summary>Närmaste fiendebyggnad soldaten kan slå på inom räckhåll.</summary>
    private Building? BuildingFoeInReach(Person p, UnitDef u)
    {
        int reach = u.IsRanged ? u.Range : u.Reach;
        if (u.IsRanged && p.Shots <= 0) return null;
        Building? best = null;
        int bestDistance = int.MaxValue;
        foreach (var b in _buildings)
        {
            if (b.Def.Width != 1 || !CanHit(p, u, b)) continue;
            int d = Chebyshev(p.Tile, b.Origin);
            if (d <= reach && d < bestDistance) (bestDistance, best) = (d, b);
        }
        return best;
    }

    /// <summary>Vägen är stängd: den som slår sönder gärdsgårdar går mot närmaste fiendegärdsgård.</summary>
    private bool WalkToNearestWall(Person p, UnitDef u)
    {
        if (!u.BreaksWalls) return false;
        Building? best = null;
        int bestDistance = int.MaxValue;
        foreach (var b in _buildings)
        {
            if (!b.Def.IsWall || !CanHit(p, u, b)) continue;
            int d = Distance(p.Tile, b.Origin);
            if (d < bestDistance) (bestDistance, best) = (d, b);
        }
        return best is not null && Chebyshev(best.Origin, p.Tile) <= 20 && WalkTo(p, best.Origin);
    }

    private void HitBuilding(Person p, UnitDef u, Building b)
    {
        int defence = b.Def.FixedUnit >= 0 ? Data.Units[b.Def.FixedUnit].Defence : 0;
        int damage = IntMath.Max(1, u.Attack + (p.Medal ? 1 : 0) - defence);
        if (u.IsRanged) p.Shots--;
        DamageBuilding(b, damage);
    }

    private void DamageBuilding(Building b, int damage)
    {
        b.Damage += damage;
        if (b.Damage < MaxStrength(b)) return;
        b.Damage = MaxStrength(b);
        var fixedUnit = b.Def.FixedUnit >= 0 ? Data.Units[b.Def.FixedUnit] : null;
        if (fixedUnit is { Eats: >= 0 })
        {
            // Hunden går hem och lägger sig, och kommer tillbaka utvilad.
            b.AwayUntil = TickCount + Data.Mood.RestTicks;
            return;
        }
        RuinBuilding(b);
    }

    private void RuinBuilding(Building b)
    {
        if (b.WorkerId >= 0 && FindPerson(b.WorkerId) is { } worker)
        {
            LeaveBuilding(worker);
            MakeIdle(worker);
        }
        b.WorkerId = -1;
        b.HasWorker = false;
        b.Ruin();
        for (int y = b.Origin.Y; y < b.Origin.Y + b.Def.Height; y++)
        for (int x = b.Origin.X; x < b.Origin.X + b.Def.Width; x++)
            Map.SetOccupant(new TilePoint(x, y), 0);
    }

    /// <summary>Vedtraven kastar ved på närmaste fiende inom räckhåll, hunden biter den som kommer nära.</summary>
    private void FixedDefenders()
    {
        foreach (var b in _buildings)
        {
            if (b.Def.FixedUnit < 0 || b.Stage != BuildingStage.Done || b.IsTaken) continue;
            var u = Data.Units[b.Def.FixedUnit];
            if (u.Eats >= 0)
            {
                if (b.AwayUntil > 0)
                {
                    if (TickCount < b.AwayUntil) continue;
                    b.AwayUntil = 0;
                    b.Damage = 0;
                }
                // Korv ibland: hunden äter när den har tappat mer än korven ger.
                if (Data.Food(u.Eats) is { } food && b.Damage >= food.Mood && b.TakeInput(u.Eats)) b.Damage -= food.Mood;
            }
            else if (b.Def.Worker is not null && !b.HasWorker) continue;
            if (b.StrikeTimer > 0)
            {
                b.StrikeTimer--;
                continue;
            }
            if (u.Ammo is { } ammo && b.InputCount(ammo.Good) == 0) continue;
            var foe = NearestEnemy(b.Owner, b.Origin, u.IsRanged ? u.Range : u.Reach);
            if (foe is null) continue;
            if (u.Ammo is { } used) b.TakeInput(used.Good);
            int defence = foe.Unit >= 0 ? Data.Units[foe.Unit].Defence : 0;
            Hurt(foe, IntMath.Max(1, u.Attack - defence), null);
            b.StrikeTimer = Data.Combat.StrikeTicks;
        }
    }

    private Person? NearestEnemy(byte owner, TilePoint at, int radius)
    {
        Person? best = null;
        int bestDistance = int.MaxValue;
        foreach (var q in _people)
        {
            if (q.Owner == owner || q.Inside || q.Job is PersonJob.Resting or PersonJob.ToHome || q.Mood <= 0) continue;
            int d = Chebyshev(at, q.Tile);
            if (d <= radius && d < bestDistance) (bestDistance, best) = (d, q);
        }
        return best;
    }

    // ---- Hantlangarna lagar ----

    /// <summary>Närmaste skadade egna gärdsgård eller vedtrave som ingen redan lagar.</summary>
    private bool FindRepair(Person p)
    {
        Building? best = null;
        int bestDistance = int.MaxValue;
        foreach (var b in _buildings)
        {
            if (b.Owner != p.Owner || b.Stage != BuildingStage.Done || b.Damage == 0) continue;
            if (!b.Def.IsWall && (b.Def.FixedUnit < 0 || Data.Units[b.Def.FixedUnit].Eats >= 0)) continue;
            if (CountWithTarget(PersonJob.ToRepair, PersonJob.Repairing, b.Id) > 0) continue;
            int d = Distance(p.Tile, b.Origin);
            if (d < bestDistance) (bestDistance, best) = (d, b);
        }
        if (best is null || !WalkTo(p, best.Origin)) return false;
        p.Job = PersonJob.ToRepair;
        p.Target = best.Id;
        return true;
    }

    private void Repair(Person p)
    {
        var b = _buildings[p.Target];
        if (b.Stage != BuildingStage.Done || b.Damage == 0)
        {
            MakeIdle(p);
            return;
        }
        if (++p.Timer < RepairTicksPerPoint) return;
        p.Timer = 0;
        b.Damage--;
    }

    // ---- Råttfällor ----

    private void PlanTrap(byte player, TilePoint tile)
    {
        if (!Map.Inside(tile) || !Map.IsWalkable(tile, MoveClass.Foot)) return;
        if (Map.OwnerAt(tile) != GameMap.NoOwner && Map.OwnerAt(tile) != player) return;
        int mine = 0;
        foreach (var t in _traps)
        {
            if (t.Owner == player) mine++;
            if (t.Tile == tile) return;
        }
        if (mine >= MaxTraps) return;
        _traps.Add(new Trap(player, tile));
    }

    private bool FindTrapToLay(Person p)
    {
        int best = -1, bestDistance = int.MaxValue;
        for (int i = 0; i < _traps.Count; i++)
        {
            var t = _traps[i];
            if (t.Owner != p.Owner || t.Armed || TrapTaken(t.Tile)) continue;
            int d = Distance(p.Tile, t.Tile);
            if (d < bestDistance) (bestDistance, best) = (d, i);
        }
        if (best < 0 || !WalkTo(p, _traps[best].Tile)) return false;
        p.Job = PersonJob.ToTrap;
        p.TargetTile = _traps[best].Tile;
        return true;
    }

    private bool TrapTaken(TilePoint tile)
    {
        foreach (var q in _people)
            if (q.Job == PersonJob.ToTrap && q.TargetTile == tile) return true;
        return false;
    }

    private void ArmTrap(Person p)
    {
        foreach (var t in _traps)
            if (t.Tile == p.TargetTile && t.Owner == p.Owner) t.Armed = true;
        MakeIdle(p);
    }

    /// <summary>Den första fienden som kliver på en gillrad fälla tappar 15 humör, och fällan är förbrukad.</summary>
    private void SpringTraps()
    {
        for (int i = _traps.Count - 1; i >= 0; i--)
        {
            var t = _traps[i];
            if (!t.Armed) continue;
            foreach (var q in _people)
            {
                if (q.Owner == t.Owner || q.Inside || q.Tile != t.Tile || q.Job is PersonJob.Resting or PersonJob.ToHome) continue;
                Hurt(q, TrapDamage, null);
                _traps.RemoveAt(i);
                break;
            }
        }
    }

    // ---- Att ta en byggnad, och segern ----

    /// <summary>
    /// Var sekund: en fiendesoldat vid dörren och ingen försvarare inom fyra rutor räknar upp mot 20
    /// sekunder, och då är byggnaden tagen. En tagen byggnad blir ägarens igen när en egen soldat har
    /// stått vid den i 20 sekunder utan att någon fiende synts.
    /// </summary>
    private void Captures()
    {
        foreach (var b in _buildings)
        {
            if (b.Def.IsWall || b.Stage != BuildingStage.Done) continue;
            byte attacker = GameMap.NoOwner;
            bool defended = false, enemyNear = false, ownNear = false;
            foreach (var q in _people)
            {
                if (q.Role != PersonRole.Soldier || q.Job != PersonJob.Soldiering) continue;
                int d = Chebyshev(q.Tile, b.Entrance);
                if (d > CaptureRadius) continue;
                if (q.Owner == b.Owner) defended = ownNear = true;
                else
                {
                    enemyNear = true;
                    if (d <= 1) attacker = q.Owner;
                }
            }
            if (b.Def.FixedUnit >= 0 && b.AwayUntil == 0 && (b.Def.Worker is null || b.HasWorker)) defended = true;

            if (b.IsTaken)
            {
                // Ägaren återtar den med en egen soldat vid dörren och ingen fiende i närheten.
                b.CaptureTicks = enemyNear || !ownNear ? 0 : b.CaptureTicks + TicksPerSecond;
                if (b.CaptureTicks >= Data.Combat.CaptureTicks)
                {
                    b.TakenBy = GameMap.NoOwner;
                    b.CaptureTicks = 0;
                }
                continue;
            }
            if (attacker == GameMap.NoOwner || defended)
            {
                b.CaptureTicks = 0;
                continue;
            }
            b.CaptureTicks += TicksPerSecond;
            if (b.CaptureTicks >= Data.Combat.CaptureTicks) TakeBuilding(b, attacker);
        }
    }

    /// <summary>Arbetaren går hem sur, lagren ligger på marken och byggnaden står tom.</summary>
    private void TakeBuilding(Building b, byte by)
    {
        if (b.WorkerId >= 0 && FindPerson(b.WorkerId) is { } worker)
        {
            LeaveBuilding(worker);
            GiveUp(worker);
        }
        foreach (var p in _people)
        {
            if (p.Owner != b.Owner || !p.Inside || p.Target != b.Id) continue;
            if (p.Job is PersonJob.InBarracks)
            {
                LeaveBuilding(p);
                GiveUp(p);
            }
        }
        b.Plunder(by);
        _players[by].BuildingsTaken++;
    }

    /// <summary>
    /// En spelare har förlorat när alla förråd (bodarna och stugan) är tagna och inga soldater finns kvar
    /// som inte är på väg hem. När bara en spelare är kvar har den vunnit.
    /// </summary>
    private void CheckVictory()
    {
        if (Winner >= 0) return;
        int alive = 0, last = -1;
        foreach (var player in _players)
        {
            if (player.Defeated) continue;
            bool hasStore = false, allTaken = true, soldiers = false;
            foreach (var b in _buildings)
            {
                if (b.Owner != player.Id || !b.Def.IsStorage || b.Stage != BuildingStage.Done) continue;
                hasStore = true;
                if (!b.IsTaken) allTaken = false;
            }
            foreach (var p in _people)
                if (p.Owner == player.Id && p.Role == PersonRole.Soldier && p.Job is PersonJob.Soldiering or PersonJob.Idle or PersonJob.ToEat)
                {
                    soldiers = true;
                    break;
                }
            if (hasStore && allTaken && !soldiers)
            {
                player.Defeated = true;
                player.DefeatedAt = TickCount;
                continue;
            }
            if (hasStore || soldiers)
            {
                alive++;
                last = player.Id;
            }
        }
        bool someoneLost = false;
        foreach (var player in _players) someoneLost |= player.Defeated;
        if (alive == 1 && someoneLost) Winner = last;
    }

    private void AddDefenceToHash(ref StateHasher h)
    {
        h.Add(Winner);
        h.Add(_traps.Count);
        foreach (var t in _traps)
        {
            h.Add(t.Owner);
            h.Add(t.Tile.X);
            h.Add(t.Tile.Y);
            h.Add(t.Armed);
        }
    }
}

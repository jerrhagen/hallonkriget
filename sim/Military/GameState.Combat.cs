using Hallonkriget.Sim.Buildings;
using Hallonkriget.Sim.Data;
using Hallonkriget.Sim.Determinism;
using Hallonkriget.Sim.Military;
using Hallonkriget.Sim.People;

namespace Hallonkriget.Sim;

/// <summary>
/// Striden, designdokumentet (Strid). Varje soldat slår var 2:a sekund mot sin fiende. Skada är
/// anfall minus försvar, minst 1, och dubbel i flank och rygg. Avståndsvapen träffar slumpvis inom
/// en ruta från målet. Skräms sänker humöret utan att slå, dubbelt för höns och pysslingar.
/// En soldat som överlever tre sammanstötningar får en medalj och +1 i anfall.
/// </summary>
public sealed partial class GameState
{
    /// <summary>Så länge efter ett slag räknas man som i strid.</summary>
    private int CombatHold => Data.Combat.StrikeTicks * 2;

    private void ResolveCombat()
    {
        foreach (var p in _people)
        {
            if (p.Role != PersonRole.Soldier) continue;
            if (p.StrikeTimer > 0) p.StrikeTimer--;
            if (p.CombatTicks > 0 && --p.CombatTicks == 0 && p.Job == PersonJob.Soldiering) SurvivedClash(p);
        }

        foreach (var p in _people)
        {
            if (p.Role != PersonRole.Soldier || p.Job != PersonJob.Soldiering || p.StrikeTimer > 0) continue;
            if (p.StepProgress < p.StepTotal) continue;
            var u = Data.Units[p.Unit];
            if (p.Foe < 0)
            {
                if (p.FoeBuilding < 0) continue;
                var b = _buildings[p.FoeBuilding];
                if (!CanHit(p, u, b) || Chebyshev(p.Tile, b.Origin) > (u.IsRanged ? u.Range : u.Reach) || (u.IsRanged && p.Shots <= 0)) continue;
                HitBuilding(p, u, b);
                p.StrikeTimer = Data.Combat.StrikeTicks;
                BurnFuel(p, u);
                continue;
            }
            if (FindPerson(p.Foe) is not { } foe || !IsTarget(p, foe)) continue;
            int d = Chebyshev(p.Tile, foe.Tile);
            if (u.IsRanged)
            {
                if (p.Shots <= 0 || d > u.Range) continue;
                Shoot(p, u, foe);
            }
            else
            {
                if (d > u.Reach) continue;
                Hurt(foe, Damage(p, u, foe, flank: true), p);
            }
            p.StrikeTimer = Data.Combat.StrikeTicks;
            p.CombatTicks = CombatHold;
            BurnFuel(p, u);
        }

        FixedDefenders();
        SpringTraps();
        if (TickCount % TicksPerSecond == 0)
        {
            Scare();
            Captures();
            CheckVictory();
        }
        RemoveLeavers();
    }

    /// <summary>Anfall minus försvar, minst 1. Medaljen och syltens bonus lägger till. Flank och rygg ger dubbelt.</summary>
    private int Damage(Person attacker, UnitDef u, Person target, bool flank)
    {
        int attack = u.Attack + (attacker.Medal ? 1 : 0) + (attacker.AttackBonusTicks > 0 ? Data.BonusPercent(FoodBonus.Attack) : 0);
        int defence = target.Unit >= 0 ? Data.Units[target.Unit].Defence : 0;
        int damage = IntMath.Max(1, attack - defence);
        if (flank && target.Group >= 0)
        {
            var (fx, fy) = Group.Directions[_groups[target.Group].Facing];
            int dot = (attacker.Tile.X - target.Tile.X) * fx + (attacker.Tile.Y - target.Tile.Y) * fy;
            if (dot <= 0) damage *= 2;
        }
        return damage;
    }

    /// <summary>
    /// Ett skott: träffar en slumpad ruta inom en ruta från målet, och den första fienden som står där.
    /// Salthagelbössan träffar målet en gång av tre, och då ger den träffade upp direkt.
    /// </summary>
    private void Shoot(Person p, UnitDef u, Person foe)
    {
        p.Shots--;
        if (u.HitOneIn > 0)
        {
            if (Rng.Next(u.HitOneIn) == 0) Hurt(foe, foe.Mood, p);
            return;
        }
        var tile = new Map.TilePoint(foe.Tile.X + Rng.Range(-1, 1), foe.Tile.Y + Rng.Range(-1, 1));
        foreach (var q in _people)
        {
            if (q.Tile != tile || !IsTarget(p, q)) continue;
            Hurt(q, Damage(p, u, q, flank: false), p);
            return;
        }
    }

    /// <summary>Drar humör. En soldat som blir slagen slår tillbaka. Vid noll ger man upp.</summary>
    private void Hurt(Person target, int damage, Person? attacker)
    {
        if (target.Mood <= 0 || target.Job is PersonJob.ToHome or PersonJob.Resting) return;
        target.Mood -= damage;
        if (target.Role == PersonRole.Soldier)
        {
            target.CombatTicks = CombatHold;
            if (attacker is not null && target.Foe < 0) target.Foe = attacker.Id;
        }
        if (target.Mood <= 0) GiveUp(target);
    }

    /// <summary>Gäss väser, traktorn bullrar och hunden skäller, en gång per sekund.</summary>
    private void Scare()
    {
        foreach (var p in _people)
        {
            if (p.Role != PersonRole.Soldier || p.Job != PersonJob.Soldiering) continue;
            if (Data.Units[p.Unit].Scare is not { } scare) continue;
            foreach (var q in _people)
            {
                if (!IsTarget(p, q) || Chebyshev(p.Tile, q.Tile) > scare.Radius) continue;
                bool timid = IsTimid(q);
                if (scare.TimidOnly && !timid) continue;
                Hurt(q, timid ? scare.PerSecond * 2 : scare.PerSecond, null);
            }
        }
        ScareFromDogs();
    }

    /// <summary>Hunden i kojan skäller på höns och pysslingar.</summary>
    private void ScareFromDogs()
    {
        foreach (var b in _buildings)
        {
            if (b.Def.FixedUnit < 0 || b.Stage != BuildingStage.Done || b.AwayUntil > 0 || b.IsTaken) continue;
            if (Data.Units[b.Def.FixedUnit].Scare is not { } scare) continue;
            foreach (var q in _people)
            {
                if (q.Owner == b.Owner || q.Inside || q.Job is PersonJob.Resting or PersonJob.ToHome || q.Mood <= 0) continue;
                if (Chebyshev(b.Origin, q.Tile) > scare.Radius) continue;
                bool timid = IsTimid(q);
                if (scare.TimidOnly && !timid) continue;
                Hurt(q, timid ? scare.PerSecond * 2 : scare.PerSecond, null);
            }
        }
    }

    /// <summary>Höns och pysslingar är extra känsliga för skräms.</summary>
    private bool IsTimid(Person q) =>
        q.Unit >= 0 ? Data.Units[q.Unit].Timid : q.Role == PersonRole.Carrier && _players[q.Owner].Faction == Faction.Torpet;

    private void SurvivedClash(Person p)
    {
        p.Clashes++;
        if (p.Clashes >= Data.Combat.MedalAfter) p.Medal = true;
    }

    /// <summary>
    /// En soldat som ger upp lämnar sin grupp. Djur går hem till sitt hus och syns inte mer. Den som
    /// red eller körde blir en rekryt utan utrustning, som först vilar hemma. Gubben kommer tillbaka som gubbe.
    /// </summary>
    private void SoldierGivesUp(Person p)
    {
        var u = Data.Units[p.Unit];
        LeaveGroup(p);
        p.Foe = -1;
        p.CombatTicks = 0;
        _players[p.Owner].SoldiersLost++;
        if (u.Hero) return;
        if (u.Recruits == 0)
        {
            _players[p.Owner].AnimalsLost++;
            RemovePerson(p);
            return;
        }
        p.Role = PersonRole.Recruit;
        p.Unit = -1;
        p.Speed = WalkSpeed;
        p.Shots = 0;
        p.Fuel = 0;
        p.Clashes = 0;
        p.Medal = false;
    }
}

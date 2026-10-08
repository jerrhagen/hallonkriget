using Hallonkriget.Sim.Buildings;
using Hallonkriget.Sim.Data;
using Hallonkriget.Sim.Determinism;
using Hallonkriget.Sim.Map;
using Hallonkriget.Sim.People;

namespace Hallonkriget.Sim;

/// <summary>
/// Humör, mat och bygdegården, som i designdokumentet (Mat och humör, Folk och yrken). Humöret
/// sjunker en enhet var 6:e sekund. Vid 30 går personen till närmaste kafferep och äter när hen har
/// gjort klart det hen håller på med. Vid 0 ger hen upp: släpper det hen bär, går hem och vilar i
/// tre minuter, och kommer sedan tillbaka med fullt humör.
/// </summary>
public sealed partial class GameState
{
    /// <summary>Hungrig vid 30 av 100, och lika stor del av max för soldater.</summary>
    private bool Hungry(Person p) => p.Mood <= MaxMood(p) * Data.Mood.EatAt / Data.Mood.Max;

    /// <summary>Humörets max: 100, eller enhetens för en soldat.</summary>
    public int MaxMood(Person p) => p.Unit >= 0 ? Data.Units[p.Unit].Mood : Data.Mood.Max;

    /// <summary>Tick per humörenhet. Soldater tappar 50 procent fortare, dubbelt i strid.</summary>
    private int MoodTicks(Person p)
    {
        if (p.Role != PersonRole.Soldier) return Data.Mood.TicksPerPoint;
        int percent = p.CombatTicks > 0 ? Data.Combat.CombatHungerPercent : Data.Combat.SoldierHungerPercent;
        return IntMath.Max(1, Data.Mood.TicksPerPoint * 100 / percent);
    }

    private void UpdateMood()
    {
        foreach (var p in _people)
        {
            if (p.SpeedBonusTicks > 0) p.SpeedBonusTicks--;
            if (p.WorkBonusTicks > 0) p.WorkBonusTicks--;
            if (p.AttackBonusTicks > 0) p.AttackBonusTicks--;
            if (p.Job == PersonJob.Resting)
            {
                if (--p.Timer > 0) continue;
                if (p.Inside) PeopleOnTile[Map.Index(p.Tile)]++;
                p.Inside = false;
                p.Mood = MaxMood(p);
                p.MoodTimer = MoodTicks(p);
                if (IsHero(p) && HomeOf(p.Owner) is { } home) TopUpShots(p, Data.Units[p.Unit], home);
                MakeIdle(p);
                continue;
            }
            if (p.Job == PersonJob.ToHome || IsHero(p)) continue;
            if (--p.MoodTimer > 0) continue;
            p.MoodTimer = MoodTicks(p);
            if (--p.Mood <= 0) GiveUp(p);
        }
    }

    /// <summary>Närmaste färdiga kafferep med något att äta på bordet.</summary>
    private Building? FindTable(Person p)
    {
        Building? best = null;
        int bestDistance = int.MaxValue;
        foreach (var b in _buildings)
        {
            if (b.Owner != p.Owner || b.Stage != BuildingStage.Done || b.Def.Table == 0 || !HasFood(b)) continue;
            int d = Distance(p.Tile, b.Entrance);
            if (d < bestDistance) (bestDistance, best) = (d, b);
        }
        return best;
    }

    private bool HasFood(Building table)
    {
        foreach (var f in Data.Foods)
            if (f.With < 0 && table.InputCount(f.Good) > 0) return true;
        return false;
    }

    /// <summary>Släpper det personen gör och går till kafferepet, om det finns mat någonstans.</summary>
    private bool TryGoEat(Person p)
    {
        var table = FindTable(p);
        if (table is null || !WalkTo(p, table.Entrance)) return false;
        p.Job = PersonJob.ToEat;
        p.Target = table.Id;
        p.Timer = 0;
        return true;
    }

    /// <summary>Arbetaren går ut och äter mellan två omgångar, aldrig mitt i en.</summary>
    private void LeaveWorkToEat(Person p)
    {
        if (!Hungry(p)) return;
        var work = _buildings[p.Target];
        if (work.CurrentRecipe >= 0 || FindTable(p) is null) return;
        LeaveBuilding(p);
        MakeIdle(p);
        TryGoEat(p);
    }

    private void LeaveBuilding(Person p)
    {
        if (!p.Inside) return;
        p.Inside = false;
        PeopleOnTile[Map.Index(p.Tile)]++;
        if (p.Job == PersonJob.AtWork) _buildings[p.Target].HasWorker = false;
    }

    /// <summary>
    /// Äter det bästa som står på bordet, en portion av varje sort, tills humöret är fullt.
    /// Mat som bara räknas ihop med en annan (sylt med pannkakor) äts bara efter den.
    /// </summary>
    private void ArriveAtTable(Person p)
    {
        var table = _buildings[p.Target];
        if (p.Tile != table.Entrance)
        {
            if (!WalkTo(p, table.Entrance)) MakeIdle(p);
            return;
        }

        int ateWith = -1;
        foreach (var f in Data.Foods)
        {
            if (p.Mood >= MaxMood(p)) break;
            if (f.With >= 0 && f.With != ateWith) continue;
            if (!table.TakeInput(f.Good)) continue;
            p.Mood = IntMath.Min(MaxMood(p), p.Mood + f.Mood);
            ateWith = f.Good;
            _players[p.Owner].Eaten[f.Good]++;
            switch (f.Bonus)
            {
                case FoodBonus.Speed: p.SpeedBonusTicks = IntMath.Max(p.SpeedBonusTicks, f.BonusTicks); break;
                case FoodBonus.Work: p.WorkBonusTicks = IntMath.Max(p.WorkBonusTicks, f.BonusTicks); break;
                case FoodBonus.Attack: p.AttackBonusTicks = IntMath.Max(p.AttackBonusTicks, f.BonusTicks); break;
            }
        }
        MakeIdle(p);
    }

    /// <summary>Humöret tog slut. Det personen bär är borta, och hen går hem till stugan.</summary>
    private void GiveUp(Person p)
    {
        p.Mood = 0;
        _players[p.Owner].GaveUp++;
        if (p.Role == PersonRole.Soldier) SoldierGivesUp(p);
        if (_leaving.Contains(p.Id)) return;
        LeaveBuilding(p);
        // Leveransen släpps, men varan följer med hem och läggs i stugan (beslut 2026-10-08).
        if (p.Job is PersonJob.ToPickup or PersonJob.ToDropoff && FindDelivery(p.Target) is { } d) CancelDelivery(d, p);
        MakeIdle(p);
        StopWalking(p);

        var home = HomeOf(p.Owner);
        if (home is not null && WalkTo(p, home.Entrance))
        {
            p.Job = PersonJob.ToHome;
            p.Target = home.Id;
            return;
        }
        LieDown(p);
    }

    private void LieDown(Person p)
    {
        if (p.Carrying >= 0)
        {
            if (p.Target >= 0 && p.Tile == _buildings[p.Target].Entrance) _buildings[p.Target].PutInput(p.Carrying);
            p.Carrying = -1; // utan plats i stugan är varan borta
        }
        p.Job = PersonJob.Resting;
        p.Timer = Data.Mood.RestTicks;
        if (p.Target >= 0 && p.Tile == _buildings[p.Target].Entrance && !p.Inside)
        {
            p.Inside = true;
            PeopleOnTile[Map.Index(p.Tile)]--;
        }
    }

    /// <summary>Stugan eller mangårdsbyggnaden.</summary>
    private Building? HomeOf(byte owner)
    {
        foreach (var b in _buildings)
            if (b.Owner == owner && !b.Def.Buildable && b.Def.IsStorage) return b;
        return null;
    }

    // ---- Befolkningen och bygdegården ----

    /// <summary>Sovplatserna i spelarens färdiga byggnader.</summary>
    public int Beds(byte owner)
    {
        int beds = 0;
        foreach (var b in _buildings)
            if (b.Owner == owner && b.Stage == BuildingStage.Done) beds += b.Def.Beds;
        return beds;
    }

    /// <summary>Spelarens personer, och de som utbildas just nu.</summary>
    public int Population(byte owner)
    {
        int n = 0;
        foreach (var p in _people) if (p.Owner == owner && !NeedsNoBed(p)) n++;
        foreach (var b in _buildings) if (b.Owner == owner && b.Training >= 0) n++;
        return n;
    }

    private void UpdateSchools()
    {
        foreach (var b in _buildings)
        {
            if (b.Def.School is null || b.IsTaken) continue;
            var done = b.UpdateSchool(room: Population(b.Owner) < Beds(b.Owner));
            if (done is not null) SpawnProfession(b.Owner, done, b.Entrance);
        }
    }

    private Person SpawnProfession(byte owner, ProfessionDef profession, TilePoint tile) =>
        SpawnPerson(owner, profession.Role, tile, profession.Role == PersonRole.Worker ? profession.Id : "");

    private void Train(byte player, int building, int profession)
    {
        if (building < 0 || building >= _buildings.Count || _buildings[building].Owner != player) return;
        if (profession < 0 || profession >= Data.Professions.Count) return;
        _buildings[building].Enqueue(Data.Professions[profession]);
    }
}

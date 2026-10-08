using System.Collections.Generic;
using Hallonkriget.Sim.Buildings;
using Hallonkriget.Sim.Data;
using Hallonkriget.Sim.Determinism;
using Hallonkriget.Sim.Map;
using Hallonkriget.Sim.Military;
using Hallonkriget.Sim.People;

namespace Hallonkriget.Sim;

/// <summary>
/// Logen, rekryterna, grupperna och hur soldaterna rör sig, designdokumentet (Enheter, Strid).
/// Rekryter utbildas i bygdegården och väntar inne i logen. Spelaren beställer en enhet i logen,
/// som förbrukar rekryter och utrustning ur sitt lager. Soldaten ställer sig i en grupp utanför.
/// Slagen, skotten och skrämseln ligger i GameState.Combat.cs.
/// </summary>
public sealed partial class GameState
{
    private readonly List<Group> _groups = new();

    /// <summary>Alla grupper som funnits. Id är platsen i listan; tomma grupper ligger kvar.</summary>
    public IReadOnlyList<Group> Groups => _groups;

    /// <summary>Standard bredd på en ny grupp.</summary>
    public const int DefaultColumns = 5;

    // ---- Logen ----

    private void Equip(byte player, int building, int unit, int count)
    {
        if (OwnBuilding(player, building) is not { Def.Barracks: true, Stage: BuildingStage.Done } b) return;
        if (unit < 0 || unit >= Data.Units.Count) return;
        var u = Data.Units[unit];
        if (u.Fixed is not null || u.Hero || !u.AllowedFor(_players[player].Faction)) return;
        for (int k = 0; k < count; k++)
            if (!EquipOne(b, u)) break;
    }

    /// <summary>Rekryterna som väntar inne i logen, äldst först.</summary>
    public List<Person> RecruitsIn(Building barracks)
    {
        var list = new List<Person>();
        foreach (var p in _people)
            if (p.Role == PersonRole.Recruit && p.Job == PersonJob.InBarracks && p.Target == barracks.Id) list.Add(p);
        return list;
    }

    /// <summary>Kan logen göra en beställning av enheten just nu?</summary>
    public bool CanEquip(Building barracks, UnitDef u)
    {
        if (RecruitsIn(barracks).Count < u.Recruits) return false;
        foreach (var a in u.Gear)
            if (barracks.InputCount(a.Good) < a.Count * u.Squad) return false;
        return true;
    }

    private bool EquipOne(Building b, UnitDef u)
    {
        if (!CanEquip(b, u)) return false;
        var recruits = RecruitsIn(b);
        foreach (var a in u.Gear)
            for (int i = 0; i < a.Count * u.Squad; i++) b.TakeInput(a.Good);

        if (u.Recruits > 0)
        {
            // Katapultens andra man sitter i maskinen och syns inte mer.
            for (int i = 1; i < u.Recruits; i++) RemovePerson(recruits[i]);
            MakeSoldier(recruits[0], u, b);
        }
        else
        {
            for (int i = 0; i < u.Squad; i++)
            {
                var animal = SpawnPerson(b.Owner, PersonRole.Soldier, b.Entrance);
                MakeSoldier(animal, u, b);
            }
        }
        _players[b.Owner].Equipped[u.Index] += u.Squad;
        return true;
    }

    /// <summary>Gör personen till en soldat av enheten och ställer hen utanför logen.</summary>
    private void MakeSoldier(Person p, UnitDef u, Building? at)
    {
        if (p.Inside)
        {
            p.Inside = false;
            PeopleOnTile[Map.Index(p.Tile)]++;
        }
        p.Role = PersonRole.Soldier;
        p.Unit = u.Index;
        p.Speed = u.Speed;
        p.Mood = u.Mood;
        p.MoodTimer = MoodTicks(p);
        p.Group = -1;
        p.Foe = -1;
        p.Clashes = 0;
        p.Medal = false;
        p.Shots = 0;
        if (u.Ammo is { } ammo)
        {
            foreach (var a in u.Gear)
                if (a.Good == ammo.Good) p.Shots = IntMath.Min(u.MaxShots, a.Count * ammo.ShotsPerGood);
            if (at is null) p.Shots = u.MaxShots;
            else TopUpShots(p, u, at);
        }
        if (u.Fuel is { } fuel)
        {
            foreach (var a in u.Gear)
                if (a.Good == fuel.Good) p.Fuel = a.Count * fuel.Ticks;
        }
        MakeIdle(p);
        StopWalking(p);
    }

    /// <summary>Fyller på skotten ur byggnadens lager: logen, eller stugan för gubben.</summary>
    private void TopUpShots(Person p, UnitDef u, Building at)
    {
        if (u.Ammo is not { } ammo) return;
        while (p.Shots + ammo.ShotsPerGood <= u.MaxShots || p.Shots == 0)
        {
            bool took = at.Def.IsStorage ? at.TakeOutput(ammo.Good) : at.TakeInput(ammo.Good);
            if (!took) break;
            p.Shots = IntMath.Min(u.MaxShots, p.Shots + ammo.ShotsPerGood);
        }
    }

    /// <summary>Rekryten går till närmaste färdiga loge och väntar där.</summary>
    private void FindBarracks(Person p)
    {
        Building? best = null;
        int bestDistance = int.MaxValue;
        foreach (var b in _buildings)
        {
            if (b.Owner != p.Owner || !b.Def.Barracks || b.Stage != BuildingStage.Done || b.IsTaken) continue;
            int d = Distance(p.Tile, b.Entrance);
            if (d < bestDistance) (bestDistance, best) = (d, b);
        }
        if (best is null || !WalkTo(p, best.Entrance)) return;
        p.Job = PersonJob.ToWorkplace;
        p.Target = best.Id;
    }

    private void EnterBarracks(Person p)
    {
        var b = _buildings[p.Target];
        if (p.Tile != b.Entrance || !b.Def.Barracks)
        {
            MakeIdle(p);
            return;
        }
        p.Job = PersonJob.InBarracks;
        p.Inside = true;
        PeopleOnTile[Map.Index(p.Tile)]--;
    }

    /// <summary>En hungrig rekryt går ut ur logen och äter, och kommer sedan tillbaka.</summary>
    private void LeaveBarracksToEat(Person p)
    {
        if (!Hungry(p) || FindTable(p) is null) return;
        p.Inside = false;
        PeopleOnTile[Map.Index(p.Tile)]++;
        MakeIdle(p);
        TryGoEat(p);
    }

    // ---- Grupper och order ----

    private Group? OwnGroup(byte player, int id) =>
        id >= 0 && id < _groups.Count && _groups[id].Owner == player && !_groups[id].IsEmpty ? _groups[id] : null;

    private void MoveGroup(byte player, int id, TilePoint to)
    {
        if (OwnGroup(player, id) is not { } g || !Map.Inside(to)) return;
        g.Facing = Group.DirectionTo(GroupCenter(g), to, g.Facing);
        g.Anchor = to;
        g.Order = GroupOrder.Move;
        g.Target = -1;
    }

    private void AttackGroup(byte player, int id, int target)
    {
        if (OwnGroup(player, id) is not { } g || target < 0 || target >= _groups.Count) return;
        var t = _groups[target];
        if (t.IsEmpty || t.Owner == player) return;
        g.Order = GroupOrder.AttackGroup;
        g.Target = target;
        AimAtTarget(g);
    }

    private void AttackBuilding(byte player, int id, int building)
    {
        if (OwnGroup(player, id) is not { } g || building < 0 || building >= _buildings.Count) return;
        var b = _buildings[building];
        if (b.Owner == player) return;
        g.Order = GroupOrder.AttackBuilding;
        g.Target = building;
        AimAtTarget(g);
    }

    private void SetFormation(byte player, int id, int columns)
    {
        if (OwnGroup(player, id) is { } g) g.Columns = IntMath.Clamp(columns, 1, Data.Units[g.Unit].GroupMax);
    }

    private void TurnGroup(byte player, int id, int facing)
    {
        if (OwnGroup(player, id) is { } g && facing is >= 0 and < 8) g.Facing = facing;
    }

    private void HaltGroup(byte player, int id)
    {
        if (OwnGroup(player, id) is not { } g) return;
        g.Anchor = GroupCenter(g);
        g.Order = GroupOrder.Idle;
        g.Target = -1;
    }

    /// <summary>Delar gruppen: den bakre halvan blir en ny grupp bredvid.</summary>
    private void SplitGroup(byte player, int id)
    {
        if (OwnGroup(player, id) is not { } g || g.Members.Count < 2) return;
        var (fx, fy) = Group.Directions[g.Facing];
        var anchor = new TilePoint(g.Anchor.X - fy * (g.Columns + 1), g.Anchor.Y + fx * (g.Columns + 1));
        if (!Map.Inside(anchor)) anchor = g.Anchor;
        var split = NewGroup(g.Owner, g.Unit, anchor, g.Facing);
        split.Columns = g.Columns;
        int keep = (g.Members.Count + 1) / 2;
        while (g.Members.Count > keep)
        {
            int m = g.Members[keep];
            g.Members.RemoveAt(keep);
            split.Members.Add(m);
            if (FindPerson(m) is { } p) p.Group = split.Id;
        }
        split.Order = GroupOrder.Move;
    }

    /// <summary>Slår ihop: den andra gruppen ställer sig bakom den första, så många som får plats.</summary>
    private void MergeGroups(byte player, int id, int other)
    {
        if (id == other || OwnGroup(player, id) is not { } g || OwnGroup(player, other) is not { } o || o.Unit != g.Unit) return;
        int max = Data.Units[g.Unit].GroupMax;
        while (o.Members.Count > 0 && g.Members.Count < max)
        {
            int m = o.Members[0];
            o.Members.RemoveAt(0);
            g.Members.Add(m);
            if (FindPerson(m) is { } p) p.Group = g.Id;
        }
    }

    private Group NewGroup(byte owner, int unit, TilePoint anchor, int facing)
    {
        var g = new Group(_groups.Count, owner, unit, anchor, facing, IntMath.Min(DefaultColumns, Data.Units[unit].GroupMax));
        _groups.Add(g);
        return g;
    }

    /// <summary>
    /// En soldat utan grupp går in i en stillastående grupp av samma slag i närheten med plats kvar,
    /// annars blir hen en ny grupp några rutor framför där hen står.
    /// </summary>
    private void JoinGroup(Person p)
    {
        var u = Data.Units[p.Unit];
        foreach (var g in _groups)
        {
            if (g.Owner != p.Owner || g.Unit != p.Unit || g.IsEmpty || g.Order != GroupOrder.Idle) continue;
            if (g.Members.Count >= u.GroupMax || Chebyshev(g.Anchor, p.Tile) > 8) continue;
            g.Members.Add(p.Id);
            p.Group = g.Id;
            return;
        }
        var start = new TilePoint(p.Tile.X, p.Tile.Y + 3);
        var anchor = NearestTile(start, 8, t => CanStand(t, p) && !GroupAnchorNear(t, 4)) ?? p.Tile;
        var group = NewGroup(p.Owner, p.Unit, anchor, 4);
        group.Members.Add(p.Id);
        p.Group = group.Id;
    }

    private bool GroupAnchorNear(TilePoint t, int radius)
    {
        foreach (var g in _groups)
            if (!g.IsEmpty && Chebyshev(g.Anchor, t) <= radius) return true;
        return false;
    }

    private void LeaveGroup(Person p)
    {
        if (p.Group < 0) return;
        _groups[p.Group].Members.Remove(p.Id);
        p.Group = -1;
    }

    /// <summary>Där ledaren står, eller gruppens främsta ruta om ledaren inte finns.</summary>
    public TilePoint GroupCenter(Group g) =>
        g.Members.Count > 0 && FindPerson(g.Members[0]) is { } leader ? leader.Tile : g.Anchor;

    private void AimAtTarget(Group g)
    {
        var target = g.Order switch
        {
            GroupOrder.AttackGroup => GroupCenter(_groups[g.Target]),
            GroupOrder.AttackBuilding => _buildings[g.Target].Entrance,
            _ => g.Anchor,
        };
        g.Facing = Group.DirectionTo(GroupCenter(g), target, g.Facing);
        g.Anchor = target;
    }

    private void UpdateGroups()
    {
        foreach (var g in _groups)
        {
            if (g.IsEmpty) continue;
            switch (g.Order)
            {
                case GroupOrder.AttackGroup:
                    if (_groups[g.Target].IsEmpty) HaltAfterFight(g);
                    else if ((TickCount + g.Id) % 10 == 0) AimAtTarget(g);
                    break;
                case GroupOrder.AttackBuilding:
                    if (_buildings[g.Target].TakenBy == g.Owner) HaltAfterFight(g);
                    break;
                case GroupOrder.Move:
                    if (AllInPlace(g)) g.Order = GroupOrder.Idle;
                    break;
            }
        }
    }

    private void HaltAfterFight(Group g)
    {
        g.Order = GroupOrder.Idle;
        g.Target = -1;
        g.Anchor = GroupCenter(g);
    }

    private bool AllInPlace(Group g)
    {
        for (int i = 0; i < g.Members.Count; i++)
            if (FindPerson(g.Members[i]) is { } p && p.Job == PersonJob.Soldiering && p.Foe < 0 && p.Tile != SlotFor(g, i, p) ) return false;
        return true;
    }

    /// <summary>Platsen i formationen, eller närmaste ruta som går att stå på.</summary>
    private TilePoint SlotFor(Group g, int index, Person p)
    {
        var slot = g.SlotTile(index);
        if (Map.Inside(slot) && CanStand(slot, p)) return slot;
        return NearestTile(slot, 2, t => CanStand(t, p)) ?? NearestTile(slot, 2, t => Map.IsWalkable(t, MoveOf(p))) ?? g.Anchor;
    }

    /// <summary>Soldater ställer sig inte på stigar och dörrar, så att bärarna kommer fram.</summary>
    private bool CanStand(TilePoint t, Person p)
    {
        if (!Map.IsWalkable(t, MoveOf(p)) || Map.PathAt(t) != PathState.None) return false;
        foreach (var b in _buildings)
            if (b.Entrance == t) return false;
        return true;
    }

    // ---- Soldaten ----

    internal static int Chebyshev(TilePoint a, TilePoint b) => IntMath.Max(IntMath.Abs(a.X - b.X), IntMath.Abs(a.Y - b.Y));

    private MoveClass MoveOf(Person p) => p.Unit >= 0 && Data.Units[p.Unit].Vehicle ? MoveClass.Vehicle : MoveClass.Foot;

    /// <summary>
    /// Ett tick för en soldat i fält. Mitt i ett steg går hen klart. Annars: stå still under rasten,
    /// slåss med fienden inom räckhåll (slagen räknas i ResolveCombat), jaga fiender inom synhåll om
    /// gruppen inte marscherar, hämta skott eller äta, och annars gå till sin plats i formationen.
    /// </summary>
    private void UpdateSoldier(Person p)
    {
        var u = Data.Units[p.Unit];
        if (p.Job == PersonJob.Idle)
        {
            p.Job = PersonJob.Soldiering;
            p.Target = -1;
            if (p.Group < 0) JoinGroup(p);
        }
        if (p.StepProgress < p.StepTotal)
        {
            p.StepProgress += EffectiveSpeed(p);
            BurnFuel(p, u);
            return;
        }

        var g = _groups[p.Group];
        bool canMove = u.Fuel is null || p.Fuel > 0 || TryRefuel(p, u);

        if (u.Attack > 0)
        {
            var foe = CurrentFoe(p, u);
            p.Foe = foe?.Id ?? -1;
            if (foe is not null)
            {
                int reach = u.IsRanged ? u.Range : u.Reach;
                bool armed = !u.IsRanged || p.Shots > 0;
                if (armed && Chebyshev(p.Tile, foe.Tile) <= reach)
                {
                    StopWalking(p);
                    return;
                }
                if (armed && canMove && g.Order != GroupOrder.Move && g.StillUntil <= TickCount)
                {
                    if (PathGoal(p) != foe.Tile) WalkTo(p, foe.Tile);
                    FollowPath(p, u);
                    return;
                }
            }
        }

        p.FoeBuilding = -1;
        if (BuildingFoeInReach(p, u) is { } wall)
        {
            p.FoeBuilding = wall.Id;
            StopWalking(p);
            return;
        }

        if (g.StillUntil > TickCount || !canMove)
        {
            StopWalking(p);
            return;
        }

        if (u.Ammo is not null && p.Shots == 0)
        {
            GoForAmmo(p, u);
            return;
        }

        if (Hungry(p) && p.Foe < 0 && g.Order != GroupOrder.Move && !u.Hero && TryGoEat(p)) return;

        var slot = SlotFor(g, g.Members.IndexOf(p.Id), p);
        if (p.Tile == slot)
        {
            StopWalking(p);
            return;
        }
        // På väg mot en gärdsgård som står i vägen: fortsätt dit.
        bool towardWall = PathGoal(p) is { } goal && Map.OccupantAt(goal) > 0 && CanHit(p, u, _buildings[Map.OccupantAt(goal) - 1]);
        if (PathGoal(p) != slot && !towardWall && !WalkTo(p, slot) && !WalkToNearestWall(p, u))
        {
            StopWalking(p);
            return;
        }
        FollowPath(p, u);
    }

    private static TilePoint? PathGoal(Person p) => p.Path.Count > 0 ? p.Path[^1] : null;

    private void FollowPath(Person p, UnitDef u)
    {
        if (p.PathIndex >= p.Path.Count - 1) return;
        Step(p);
        BurnFuel(p, u);
    }

    private static void BurnFuel(Person p, UnitDef u)
    {
        if (u.Fuel is not null && p.Fuel > 0) p.Fuel--;
    }

    /// <summary>Traktorn tankar i en egen loge inom tre rutor, en dunk i taget, högst tre dunkar.</summary>
    private bool TryRefuel(Person p, UnitDef u)
    {
        if (u.Fuel is not { } fuel) return true;
        foreach (var b in _buildings)
        {
            if (b.Owner != p.Owner || !b.Def.Barracks || b.Stage != BuildingStage.Done || Chebyshev(b.Entrance, p.Tile) > 3) continue;
            while (p.Fuel <= 2 * fuel.Ticks && b.TakeInput(fuel.Good)) p.Fuel += fuel.Ticks;
        }
        return p.Fuel > 0;
    }

    /// <summary>Skotten är slut: gubben går hem till stugan, de andra till närmaste loge, och fyller på där.</summary>
    private void GoForAmmo(Person p, UnitDef u)
    {
        var place = u.Hero ? HomeOf(p.Owner) : NearestBarracks(p);
        if (place is null)
        {
            StopWalking(p);
            return;
        }
        if (p.Tile == place.Entrance)
        {
            StopWalking(p);
            if ((TickCount + p.Id) % 50 == 0) TopUpShots(p, u, place);
            return;
        }
        if (PathGoal(p) != place.Entrance && !WalkTo(p, place.Entrance)) return;
        FollowPath(p, u);
    }

    private Building? NearestBarracks(Person p)
    {
        Building? best = null;
        int bestDistance = int.MaxValue;
        foreach (var b in _buildings)
        {
            if (b.Owner != p.Owner || !b.Def.Barracks || b.Stage != BuildingStage.Done) continue;
            int d = Distance(p.Tile, b.Entrance);
            if (d < bestDistance) (bestDistance, best) = (d, b);
        }
        return best;
    }

    /// <summary>Fienden soldaten redan slåss med om hen fortfarande går att nå, annars den närmaste inom synhåll.</summary>
    private Person? CurrentFoe(Person p, UnitDef u)
    {
        int sight = IntMath.Max(Data.Combat.Sight, u.Range);
        if (p.Foe >= 0 && FindPerson(p.Foe) is { } old && IsTarget(p, old) && Chebyshev(p.Tile, old.Tile) <= sight + 2) return old;
        if (p.Foe < 0)
        {
            // Att leta är dyrt: var femte gång soldaten står mellan två steg.
            if (p.Timer > 0)
            {
                p.Timer--;
                return null;
            }
            p.Timer = 4;
        }
        return FindFoe(p, sight);
    }

    /// <summary>En fiende som går att slå: ute på kartan, och inte på väg hem för att lägga sig.</summary>
    private bool IsTarget(Person p, Person q) =>
        q.Owner != p.Owner && !q.Inside && q.Job is not (PersonJob.Resting or PersonJob.ToHome) && q.Mood > 0;

    /// <summary>Närmaste fiendesoldat inom radien, annars närmaste fiende över huvud taget.</summary>
    private Person? FindFoe(Person p, int radius)
    {
        Person? soldier = null, civilian = null;
        int bestSoldier = int.MaxValue, bestCivilian = int.MaxValue;
        foreach (var q in _people)
        {
            if (!IsTarget(p, q)) continue;
            int d = Chebyshev(p.Tile, q.Tile);
            if (d > radius) continue;
            if (q.Role == PersonRole.Soldier)
            {
                if (d < bestSoldier) (bestSoldier, soldier) = (d, q);
            }
            else if (d < bestCivilian) (bestCivilian, civilian) = (d, q);
        }
        return soldier ?? civilian;
    }

    /// <summary>För tester: en soldat av enheten, direkt på rutan, utan logen.</summary>
    internal Person SpawnSoldier(byte owner, UnitDef u, TilePoint tile)
    {
        var p = SpawnPerson(owner, PersonRole.Soldier, tile);
        MakeSoldier(p, u, null);
        return p;
    }

    private bool IsHero(Person p) => p.Unit >= 0 && Data.Units[p.Unit].Hero;

    /// <summary>Gubben bor i stugan och djuren i sina hus, så de räknas inte in i befolkningen.</summary>
    private bool NeedsNoBed(Person p) => p.Unit >= 0 && (Data.Units[p.Unit].Hero || Data.Units[p.Unit].Recruits == 0);

    private void SpawnHero(byte player, Building home)
    {
        foreach (var u in Data.Units)
        {
            if (!u.Hero || !u.AllowedFor(_players[player].Faction)) continue;
            var hero = SpawnPerson(player, PersonRole.Soldier, home.Entrance);
            MakeSoldier(hero, u, null);
            return;
        }
    }

    /// <summary>Tar bort personen ur spelet, i slutet av ticket så att ingen lista ändras medan den gås igenom.</summary>
    private void RemovePerson(Person p)
    {
        LeaveGroup(p);
        if (!_leaving.Contains(p.Id)) _leaving.Add(p.Id);
    }

    private readonly List<int> _leaving = new();

    private void RemoveLeavers()
    {
        foreach (int id in _leaving)
        {
            int i = PersonIndex(id);
            if (i < 0) continue;
            var p = _people[i];
            if (!p.Inside) PeopleOnTile[Map.Index(p.Tile)]--;
            _people.RemoveAt(i);
        }
        _leaving.Clear();
    }

    private void AddMilitaryToHash(ref StateHasher h)
    {
        h.Add(_groups.Count);
        foreach (var g in _groups) g.AddToHash(ref h);
    }
}

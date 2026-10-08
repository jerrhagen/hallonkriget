using System;
using System.Collections.Generic;
using Hallonkriget.Sim.Buildings;
using Hallonkriget.Sim.Commands;
using Hallonkriget.Sim.Data;
using Hallonkriget.Sim.Determinism;
using Hallonkriget.Sim.Map;
using Hallonkriget.Sim.People;

namespace Hallonkriget.Sim;

/// <summary>
/// Hela spelets tillstånd. Kärnan är en funktion: Tick(kommandon). Samma tillstånd och samma
/// kommandon ger exakt samma nya tillstånd på alla datorer. Se determinismreglerna i CLAUDE.md.
/// </summary>
public sealed partial class GameState
{
    public const int TicksPerSecond = 10;

    // Fas 0: tak för vandrare så att testerna går fort. Försvinner med Walker.
    public const int MaxWalkersPerPlayer = 256;

    private readonly List<Player> _players = new();
    private readonly List<Walker> _walkers = new();
    private readonly List<Building> _buildings = new();
    private int _nextWalkerId;

    public int TickCount { get; private set; }
    public Rng Rng { get; }
    public GameData Data { get; }
    public GameMap Map { get; }
    public int MapWidth => Map.Width;
    public int MapHeight => Map.Height;
    public IReadOnlyList<Player> Players => _players;
    public IReadOnlyList<Walker> Walkers => _walkers;

    /// <summary>Alla byggnader i den ordning de placerades. Id är platsen i listan.</summary>
    public IReadOnlyList<Building> Buildings => _buildings;

    private GameState(ulong seed, int mapWidth, int mapHeight, GameData data)
    {
        Rng = new Rng(seed);
        Data = data;
        Map = new GameMap(mapWidth, mapHeight);
    }

    public static GameState NewMatch(MatchSetup setup)
    {
        if (setup.MapWidth <= 0 || setup.MapHeight <= 0) throw new ArgumentException("Kartan måste ha en storlek");
        if (setup.Players.Count is 0 or > 8) throw new ArgumentException("1–8 spelare");

        if (setup.Map is { } mapDef && (mapDef.Width != setup.MapWidth || mapDef.Height != setup.MapHeight))
            throw new ArgumentException("Kartans storlek stämmer inte");
        var state = new GameState(setup.Seed, setup.MapWidth, setup.MapHeight, setup.Data ?? GameData.Empty);
        setup.Map?.Apply(state.Map);
        for (int i = 0; i < setup.Players.Count; i++)
        {
            var p = setup.Players[i];
            state._players.Add(new Player((byte)i, p.Faction, p.IsComputer, state.Data.Goods.Count, state.Data.Units.Count));
        }
        for (int i = 0; i < setup.Players.Count; i++)
        {
            if (setup.Players[i].Start is not { } start) continue;
            var def = state.StartBuildingFor(setup.Players[i].Faction);
            if (!state.CanPlace((byte)i, def, start, ignoreBuildable: true))
                throw new ArgumentException($"Spelare {i} kan inte börja på {start}");
            var home = state.AddBuilding((byte)i, def, start);
            home.CompleteAtOnce(def.StartStock);
            if (def.StartPeople.Length == 0)
            {
                // Designdokumentet: tre bärare och två hantlangare från start.
                for (int k = 0; k < 3; k++) state.SpawnPerson((byte)i, PersonRole.Carrier, home.Entrance);
                for (int k = 0; k < 2; k++) state.SpawnPerson((byte)i, PersonRole.Laborer, home.Entrance);
            }
            foreach (var group in def.StartPeople)
                for (int k = 0; k < group.Count; k++)
                    state.SpawnProfession((byte)i, state.Data.Professions[group.Profession], home.Entrance);
            state.SpawnHero((byte)i, home);
        }
        return state;
    }

    /// <summary>
    /// Kör ett tick. Kommandona ska alla gälla det här ticket. De tillämpas sorterade per spelare,
    /// och i given ordning inom samma spelare, så att ankomstordningen i nätverket inte spelar roll.
    /// </summary>
    public void Tick(IReadOnlyList<Command> commands)
    {
        ApplyCommands(commands);
        MoveWalkers();
        UpdatePeople();
        UpdateProduction();
        MatchDeliveries();
        UpdateMood();
        ResolveCombat();
        RunComputerPlayers();
        TickCount++;
    }

    /// <summary>Kontrollsumma över hela tillståndet. Nya fält i tillståndet ska läggas till här.</summary>
    public ulong Hash()
    {
        var h = new StateHasher();
        h.Add(TickCount);
        h.Add(Rng.State);
        Map.AddToHash(ref h);
        h.Add(_players.Count);
        foreach (var p in _players) p.AddToHash(ref h);
        h.Add(Data.Fingerprint);
        h.Add(_buildings.Count);
        foreach (var b in _buildings) b.AddToHash(ref h);
        AddPeopleToHash(ref h);
        AddDeliveriesToHash(ref h);
        AddMilitaryToHash(ref h);
        AddDefenceToHash(ref h);
        h.Add(_nextWalkerId);
        h.Add(_walkers.Count);
        foreach (var w in _walkers) w.AddToHash(ref h);
        return h.Value;
    }

    private void ApplyCommands(IReadOnlyList<Command> commands)
    {
        var ordered = SortByPlayer(commands);
        foreach (var c in ordered)
        {
            if (c.Tick != TickCount)
                throw new ArgumentException($"Kommando för tick {c.Tick} kom till tick {TickCount}");
            if (c.Player >= _players.Count) continue;

            switch (c.Type)
            {
                case CommandType.SpawnWalker:
                    SpawnWalker(c.Player, c.A, c.B);
                    break;
                case CommandType.MoveWalker:
                    MoveWalker(c.Player, c.A, c.B, c.C);
                    break;
                case CommandType.PlaceBuilding:
                    if (c.A >= 0 && c.A < Data.Buildings.Count)
                        TryPlaceBuilding(c.Player, Data.Buildings[c.A], new TilePoint(c.B, c.C));
                    break;
                case CommandType.PlanPath:
                    PlanPath(c.Player, new TilePoint(c.A, c.B));
                    break;
                case CommandType.SelectRecipe:
                    SelectRecipe(c.Player, c.A, c.B);
                    break;
                case CommandType.Train:
                    Train(c.Player, c.A, c.B);
                    break;
                case CommandType.CancelTraining:
                    if (OwnBuilding(c.Player, c.A) is { } school) school.CancelLast();
                    break;
                case CommandType.BlockGood:
                    if (OwnBuilding(c.Player, c.A) is { } blocked) blocked.SetBlocked(c.B, c.C != 0);
                    break;
                case CommandType.Equip:
                    Equip(c.Player, c.A, c.B, c.C);
                    break;
                case CommandType.MoveGroup:
                    MoveGroup(c.Player, c.A, new TilePoint(c.B, c.C));
                    break;
                case CommandType.AttackGroup:
                    AttackGroup(c.Player, c.A, c.B);
                    break;
                case CommandType.AttackBuilding:
                    AttackBuilding(c.Player, c.A, c.B);
                    break;
                case CommandType.SetFormation:
                    SetFormation(c.Player, c.A, c.B);
                    break;
                case CommandType.TurnGroup:
                    TurnGroup(c.Player, c.A, c.B);
                    break;
                case CommandType.SplitGroup:
                    SplitGroup(c.Player, c.A);
                    break;
                case CommandType.MergeGroups:
                    MergeGroups(c.Player, c.A, c.B);
                    break;
                case CommandType.HaltGroup:
                    HaltGroup(c.Player, c.A);
                    break;
                case CommandType.PlaceTrap:
                    PlanTrap(c.Player, new TilePoint(c.A, c.B));
                    break;
            }
        }
    }

    private Building? OwnBuilding(byte player, int id) =>
        id >= 0 && id < _buildings.Count && _buildings[id].Owner == player ? _buildings[id] : null;

    /// <summary>Välj recept, eller byte i lanthandeln. Byten som bara finns för det andra lägret går inte att välja.</summary>
    private void SelectRecipe(byte player, int building, int recipe)
    {
        if (OwnBuilding(player, building) is not { } b) return;
        if (recipe >= 0 && recipe < b.Def.Recipes.Length)
        {
            var rule = b.Def.Recipes[recipe].Faction;
            if (rule == FactionRule.Torpet && _players[player].Faction != Faction.Torpet) return;
            if (rule == FactionRule.Storgarden && _players[player].Faction != Faction.Storgarden) return;
        }
        b.SelectRecipe(recipe);
    }

    /// <summary>Stabil sortering på spelare. Egen insättningssortering: List.Sort är inte stabil.</summary>
    private static Command[] SortByPlayer(IReadOnlyList<Command> commands)
    {
        var arr = new Command[commands.Count];
        for (int i = 0; i < arr.Length; i++)
        {
            var c = commands[i];
            int j = i - 1;
            while (j >= 0 && arr[j].Player > c.Player)
            {
                arr[j + 1] = arr[j];
                j--;
            }
            arr[j + 1] = c;
        }
        return arr;
    }

    private bool InsideMap(int x, int y) => Map.Inside(new TilePoint(x, y));

    private BuildingDef StartBuildingFor(Faction faction)
    {
        foreach (var b in Data.Buildings)
            if (!b.Buildable && b.IsStorage && b.AllowedFor(faction) && b.Faction != FactionRule.Both) return b;
        throw new ArgumentException($"Datan saknar startbyggnad för {faction}");
    }

    /// <summary>
    /// Kan spelaren placera byggnaden här? Hela fotavtrycket ska vara glänta som ingen annan äger
    /// och där inget står, och dörren ska gå att nå. Ingen byggnad får stå på en annans dörr.
    /// </summary>
    public bool CanPlace(byte player, BuildingDef def, TilePoint origin, bool ignoreBuildable = false)
    {
        if (player >= _players.Count || !def.AllowedFor(_players[player].Faction)) return false;
        if (!def.Buildable && !ignoreBuildable) return false;

        for (int y = origin.Y; y < origin.Y + def.Height; y++)
        for (int x = origin.X; x < origin.X + def.Width; x++)
            if (!FreeToBuild(player, new TilePoint(x, y))) return false;
        foreach (var p in Building.FieldTiles(def, origin))
            if (!FreeToBuild(player, p)) return false;

        if (def.FixedUnit >= 0 && Data.Units[def.FixedUnit].Eats >= 0 && !DogHouseAllowed(player)) return false;
        var door = Building.EntranceFor(def, origin);
        if (!Map.Inside(door) || !Map.IsWalkable(door, MoveClass.Foot)) return false;
        if (def.NextTo is { } terrain && !TerrainAround(def, origin, terrain)) return false;
        return true;
    }

    /// <summary>Glänta som ingen annan äger, där inget står och som inte är någons dörr.</summary>
    private bool FreeToBuild(byte player, TilePoint p)
    {
        if (!Map.Inside(p) || !TerrainRules.IsBuildable(Map.TerrainAt(p)) || Map.OccupantAt(p) != 0) return false;
        if (Map.OwnerAt(p) != GameMap.NoOwner && Map.OwnerAt(p) != player) return false;
        foreach (var other in _buildings)
            if (other.Entrance == p && other.Stage != BuildingStage.Ruined) return false;
        return true;
    }

    /// <summary>Ligger terrängen på någon ruta runt fotavtrycket, diagonalt också?</summary>
    private bool TerrainAround(BuildingDef def, TilePoint origin, Terrain terrain)
    {
        for (int y = origin.Y - 1; y <= origin.Y + def.Height; y++)
        for (int x = origin.X - 1; x <= origin.X + def.Width; x++)
        {
            var p = new TilePoint(x, y);
            if (Map.Inside(p) && Map.TerrainAt(p) == terrain) return true;
        }
        return false;
    }

    private void TryPlaceBuilding(byte player, BuildingDef def, TilePoint origin)
    {
        if (CanPlace(player, def, origin)) AddBuilding(player, def, origin);
    }

    private Building AddBuilding(byte player, BuildingDef def, TilePoint origin)
    {
        var building = new Building(_buildings.Count, def, player, origin, Data.Goods.Count);
        _buildings.Add(building);
        for (int y = origin.Y; y < origin.Y + def.Height; y++)
        for (int x = origin.X; x < origin.X + def.Width; x++)
        {
            var p = new TilePoint(x, y);
            if (Map.PathAt(p) != PathState.None) Map.SetPath(p, PathState.None);
            Map.SetOccupant(p, building.Id + 1);
            Map.SetOwner(p, player);
        }
        foreach (var p in Building.FieldTiles(def, origin))
        {
            if (Map.PathAt(p) != PathState.None) Map.SetPath(p, PathState.None);
            Map.SetTerrain(p, Terrain.Field);
            Map.SetOwner(p, player);
        }
        return building;
    }

    private void SpawnWalker(byte owner, int tileX, int tileY)
    {
        if (!InsideMap(tileX, tileY)) return;
        int owned = 0;
        foreach (var w in _walkers) if (w.Owner == owner) owned++;
        if (owned >= MaxWalkersPerPlayer) return;

        // En pysslings fart: 1,2 rutor per sekund.
        var speed = Fixed.FromRatio(12, 10 * TicksPerSecond);
        var center = Fixed.FromRatio(1, 2);
        _walkers.Add(new Walker(_nextWalkerId++, owner, Fixed.FromInt(tileX) + center, Fixed.FromInt(tileY) + center, speed));
    }

    private void MoveWalker(byte owner, int walkerId, int tileX, int tileY)
    {
        if (!InsideMap(tileX, tileY)) return;
        var w = FindWalker(walkerId);
        if (w is null || w.Owner != owner) return;
        var center = Fixed.FromRatio(1, 2);
        w.TargetX = Fixed.FromInt(tileX) + center;
        w.TargetY = Fixed.FromInt(tileY) + center;
        w.HasTarget = true;
    }

    private Walker? FindWalker(int id)
    {
        foreach (var w in _walkers) if (w.Id == id) return w;
        return null;
    }

    private void MoveWalkers()
    {
        foreach (var w in _walkers)
        {
            if (!w.HasTarget)
            {
                // Lediga vandrare strövar ibland iväg, så att slumpen påverkar tillståndet.
                if (Rng.Chance(2))
                {
                    int tx = IntMath.Clamp(w.X.Floor + Rng.Range(-5, 5), 0, MapWidth - 1);
                    int ty = IntMath.Clamp(w.Y.Floor + Rng.Range(-5, 5), 0, MapHeight - 1);
                    w.TargetX = Fixed.FromInt(tx) + Fixed.FromRatio(1, 2);
                    w.TargetY = Fixed.FromInt(ty) + Fixed.FromRatio(1, 2);
                    w.HasTarget = true;
                }
                continue;
            }

            var dx = w.TargetX - w.X;
            var dy = w.TargetY - w.Y;
            var dist = Fixed.Length(dx, dy);
            if (dist <= w.Speed)
            {
                w.X = w.TargetX;
                w.Y = w.TargetY;
                w.HasTarget = false;
                continue;
            }
            w.X = Fixed.FromRaw(w.X.Raw + (int)((long)dx.Raw * w.Speed.Raw / dist.Raw));
            w.Y = Fixed.FromRaw(w.Y.Raw + (int)((long)dy.Raw * w.Speed.Raw / dist.Raw));
        }
    }

    // Stegen nedan fylls i under fas 1 och 3. Ordningen är fast och står i CLAUDE.md.
    private void UpdateProduction()
    {
        bool sunday = GameClock.IsSunday(TickCount);
        UpdateSchools();
        foreach (var b in _buildings)
        {
            if (b.IsTaken) continue;
            int bonus = b.HasWorker && FindPerson(b.WorkerId) is { WorkBonusTicks: > 0 } ? Data.BonusPercent(FoodBonus.Work) : 0;
            var done = b.UpdateProduction(TickCount, _gather ??= TryGather, closed: sunday, workBonus: bonus);
            if (done is null) continue;
            foreach (var a in done.Out) _players[b.Owner].Produced[a.Good] += a.Count;
        }
    }
    private Func<Building, GatherDef, bool>? _gather;

    /// <summary>
    /// En samlare börjar en omgång: närmaste ruta med terrängen inom räckhåll, räknat från dörren.
    /// Det som tar slut (sten, träd, skrot) räknas av, och en tom ruta blir glänta. Skogshuggaren
    /// planterar en gran per PlantEvery fällda: på en glesnad ruta i skogen, annars på en glänta intill.
    /// </summary>
    private bool TryGather(Building b, GatherDef g)
    {
        var tile = NearestTile(b.Entrance, g.Radius, p => Map.TerrainAt(p) == g.Terrain);
        if (tile is not { } t) return false;
        if (g.PerTile == 0) return true;

        if (Map.TakenAt(t) + 1 >= g.PerTile) Map.SetTerrain(t, Terrain.Clearing);
        else Map.SetTaken(t, Map.TakenAt(t) + 1);

        b.Gathered++;
        if (g.PlantEvery > 0 && b.Gathered % g.PlantEvery == 0) Plant(b, g);
        return true;
    }

    private void Plant(Building b, GatherDef g)
    {
        var thinned = NearestTile(b.Entrance, g.Radius, p => Map.TerrainAt(p) == g.Terrain && Map.TakenAt(p) > 0);
        if (thinned is { } t)
        {
            Map.SetTaken(t, Map.TakenAt(t) - 1);
            return;
        }
        // En ny gran på en glänta intill skogen, eller intill platsen där den sista fälldes.
        var clearing = NearestTile(b.Entrance, g.Radius, p => CanPlantOn(b.Owner, p) && NextToTerrainOrClearedForest(p, g.Terrain));
        if (clearing is not { } c) return;
        Map.SetTerrain(c, g.Terrain);
        Map.SetTaken(c, g.PerTile - 1);
    }

    private bool CanPlantOn(byte owner, TilePoint p)
    {
        if (Map.TerrainAt(p) != Terrain.Clearing || Map.OccupantAt(p) != 0 || Map.PathAt(p) != PathState.None) return false;
        if (Map.OwnerAt(p) != GameMap.NoOwner && Map.OwnerAt(p) != owner) return false;
        foreach (var other in _buildings)
            if (Distance(other.Entrance, p) <= Pathfinder.Diagonal) return false;
        return true;
    }

    private bool NextToTerrainOrClearedForest(TilePoint p, Terrain terrain)
    {
        for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
        {
            var q = new TilePoint(p.X + dx, p.Y + dy);
            if (Map.Inside(q) && Map.TerrainAt(q) == terrain) return true;
        }
        // Skogen är borta: planteras där huggaren står och tittar, närmast dörren.
        return !AnyTerrainNear(p, terrain, 8);
    }

    private bool AnyTerrainNear(TilePoint p, Terrain terrain, int radius) =>
        NearestTile(p, radius, q => Map.TerrainAt(q) == terrain) is not null;

    /// <summary>Närmaste ruta inom radien som uppfyller villkoret. Lika nära: den första i radordning.</summary>
    private TilePoint? NearestTile(TilePoint center, int radius, Func<TilePoint, bool> match)
    {
        TilePoint? best = null;
        int bestDistance = int.MaxValue, r2 = radius * radius;
        for (int dy = -radius; dy <= radius; dy++)
        for (int dx = -radius; dx <= radius; dx++)
        {
            if (dx * dx + dy * dy > r2) continue;
            var p = new TilePoint(center.X + dx, center.Y + dy);
            if (!Map.Inside(p) || !match(p)) continue;
            int d = dx * dx + dy * dy;
            if (d < bestDistance) (bestDistance, best) = (d, p);
        }
        return best;
    }

    private void RunComputerPlayers() { }
}

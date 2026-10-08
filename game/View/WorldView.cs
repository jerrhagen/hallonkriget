using System.Collections.Generic;
using Godot;
using Hallonkriget.Game.Net;
using Hallonkriget.Sim;
using Hallonkriget.Sim.Buildings;
using Hallonkriget.Sim.Map;
using Hallonkriget.Sim.People;

namespace Hallonkriget.Game.View;

/// <summary>
/// Ritar spelet från GameState: marken, stigarna, byggnaderna och personerna. Vyn läser bara;
/// allt spelaren gör går som kommandon genom LocalMatch. Personerna ritas mittemellan två tick.
/// En ruta är 128 världspixlar (1×-grafiken); kameran zoomar.
/// </summary>
public partial class WorldView : Node2D
{
    public const int Tile = 128;

    private LocalMatch _match = null!;
    private TileMapLayer _terrain = null!;
    private TileMapLayer _paths = null!;
    private PlannedPaths _planned = null!;
    private Node2D _objects = null!;
    private int _terrainSource, _pathSource;
    private int _mapVersion = -1;

    private readonly List<BuildingView> _buildings = new();
    private readonly Dictionary<int, PersonView> _people = new();

    public LocalMatch Match => _match;

    public void Attach(LocalMatch match)
    {
        _match = match;
        foreach (var child in GetChildren()) child.QueueFree();
        _buildings.Clear();
        _people.Clear();
        _mapVersion = -1;

        _terrain = NewLayer("Mark", "res://art/terrain/terrang_atlas@2x.png", 22, out _terrainSource);
        AddTone();
        _paths = NewLayer("Stigar", "res://art/terrain/stigar_atlas@2x.png", 16, out _pathSource);
        _planned = new PlannedPaths { Name = "PlaneradeStigar" };
        AddChild(_planned);
        _objects = new Node2D { Name = "Objekt", YSortEnabled = true };
        AddChild(_objects);

        _match.Ticked += OnTicked;
        SyncAll(snap: true);
    }

    public override void _ExitTree()
    {
        if (_match is not null) _match.Ticked -= OnTicked;
    }

    private void OnTicked() => SyncAll(snap: false);

    public override void _Process(double delta)
    {
        if (_match is null) return;
        float alpha = Mathf.Clamp(_match.Alpha, 0, 1);
        foreach (var p in _people.Values) p.Interpolate(alpha);
    }

    /// <summary>Världskoordinat för mitten av en ruta.</summary>
    public static Vector2 TileCenter(TilePoint p) => new((p.X + 0.5f) * Tile, (p.Y + 0.5f) * Tile);

    public static TilePoint TileAt(Vector2 world) => new(Mathf.FloorToInt(world.X / Tile), Mathf.FloorToInt(world.Y / Tile));

    private void SyncAll(bool snap)
    {
        var state = _match.State;
        if (state.Map.Version != _mapVersion) SyncMap();

        while (_buildings.Count < state.Buildings.Count)
        {
            var view = new BuildingView(state.Buildings[_buildings.Count], _match.Data);
            _buildings.Add(view);
            _objects.AddChild(view);
        }
        foreach (var b in _buildings) b.Sync();

        foreach (var person in state.People)
        {
            if (!_people.TryGetValue(person.Id, out var view))
            {
                view = new PersonView(person, _match.Data);
                _people[person.Id] = view;
                _objects.AddChild(view);
                view.Sync(snap: true);
                continue;
            }
            view.Sync(snap);
        }
    }

    private void SyncMap()
    {
        var map = _match.State.Map;
        _mapVersion = map.Version;
        _planned.Tiles.Clear();
        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
        {
            var p = new TilePoint(x, y);
            int column = (int)map.TerrainAt(p) * 2 + Variant(x, y);
            _terrain.SetCell(new Vector2I(x, y), _terrainSource, new Vector2I(column, 0));
            var path = map.PathAt(p);
            if (path == PathState.Trodden) _paths.SetCell(new Vector2I(x, y), _pathSource, new Vector2I(PathMask(p), 0));
            else _paths.EraseCell(new Vector2I(x, y));
            if (path == PathState.Planned) _planned.Tiles.Add(p);
        }
        _planned.QueueRedraw();
    }

    /// <summary>Vilka grannar som också är stig, eller en dörr som stigen leder till: 1 norr, 2 öster, 4 söder, 8 väster.</summary>
    private int PathMask(TilePoint p)
    {
        var map = _match.State.Map;
        int mask = 0;
        (int dx, int dy, int bit)[] dirs = { (0, -1, 1), (1, 0, 2), (0, 1, 4), (-1, 0, 8) };
        foreach (var (dx, dy, bit) in dirs)
        {
            var q = new TilePoint(p.X + dx, p.Y + dy);
            if (!map.Inside(q)) continue;
            if (map.PathAt(q) == PathState.Trodden) mask |= bit;
            else if (dy == -1 && map.OccupantAt(q) is > 0 and var id && _match.State.Buildings[id - 1].Entrance == p) mask |= bit;
        }
        return mask;
    }

    /// <summary>Samma variant på samma ruta varje gång, men utan synligt mönster.</summary>
    private static int Variant(int x, int y)
    {
        uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663);
        h ^= h >> 13;
        h *= 0x5bd1e995;
        return (int)((h >> 9) % 2);
    }

    private TileMapLayer NewLayer(string name, string texture, int columns, out int sourceId)
    {
        // 2×-grafiken skalas ner till hälften, så att den är skarp även inzoomad.
        var tileSet = new TileSet { TileSize = new Vector2I(Tile * 2, Tile * 2) };
        var source = new TileSetAtlasSource
        {
            Texture = GD.Load<Texture2D>(texture),
            TextureRegionSize = new Vector2I(Tile * 2, Tile * 2),
        };
        for (int v = 0; v < columns; v++) source.CreateTile(new Vector2I(v, 0));
        sourceId = tileSet.AddSource(source);
        var layer = new TileMapLayer { Name = name, TileSet = tileSet, Scale = new Vector2(0.5f, 0.5f) };
        AddChild(layer);
        return layer;
    }

    /// <summary>Storskalig ton som multipliceras över marken, så att den inte blir en tapet.</summary>
    private void AddTone()
    {
        var tex = GD.Load<Texture2D>("res://art/terrain/ang_ton.png");
        var map = _match.State.Map;
        AddChild(new Sprite2D
        {
            Name = "Ton",
            Texture = tex,
            Centered = false,
            Scale = new Vector2(map.Width * Tile / (float)tex.GetWidth(), map.Height * Tile / (float)tex.GetHeight()),
            Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Mul },
        });
    }

    /// <summary>Planerade stigar: en prickad linje i rutans mitt tills en hantlangare trampat upp dem.</summary>
    private sealed partial class PlannedPaths : Node2D
    {
        public readonly List<TilePoint> Tiles = new();

        public override void _Draw()
        {
            var ink = new Color(0.36f, 0.27f, 0.18f, 0.75f);
            foreach (var p in Tiles)
            {
                var c = TileCenter(p);
                for (int i = 0; i < 4; i++)
                    DrawCircle(c + new Vector2(-36 + i * 24, 0), 6, ink);
                DrawArc(c, 30, 0, Mathf.Tau, 24, new Color(ink, 0.35f), 3);
            }
        }
    }
}

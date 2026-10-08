using System;
using System.Collections.Generic;
using Hallonkriget.Sim.Determinism;

namespace Hallonkriget.Sim.Map;

/// <summary>
/// Rutnätet: terräng, stig, ägare och vad som står på varje ruta. Lagret per egenskap är en
/// array med en plats per ruta, i radordning, så att kontrollsumman går fort.
/// </summary>
public sealed class GameMap
{
    public const byte NoOwner = 255;

    private readonly Terrain[] _terrain;
    private readonly PathState[] _path;
    private readonly byte[] _owner;
    private readonly int[] _occupant;

    public int Width { get; }
    public int Height { get; }
    public int TileCount => _terrain.Length;

    /// <summary>
    /// Ökar varje gång något ändras som påverkar var man kan gå och hur fort.
    /// Sparade sökvägar som räknats på en äldre version ska räknas om.
    /// </summary>
    public int Version { get; private set; }

    public GameMap(int width, int height)
    {
        if (width <= 0 || height <= 0) throw new ArgumentException("Kartan måste ha en storlek");
        Width = width;
        Height = height;
        _terrain = new Terrain[width * height];
        _path = new PathState[width * height];
        _owner = new byte[width * height];
        _occupant = new int[width * height];
        Array.Fill(_owner, NoOwner);
    }

    public bool Inside(TilePoint p) => p.X >= 0 && p.Y >= 0 && p.X < Width && p.Y < Height;

    public int Index(TilePoint p) => p.Y * Width + p.X;

    public TilePoint PointOf(int index) => new(index % Width, index / Width);

    public IEnumerable<TilePoint> AllTiles()
    {
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
            yield return new TilePoint(x, y);
    }

    public Terrain TerrainAt(TilePoint p) => _terrain[Index(p)];
    public PathState PathAt(TilePoint p) => _path[Index(p)];
    public byte OwnerAt(TilePoint p) => _owner[Index(p)];

    /// <summary>Vad som står på rutan: 0 är inget, annars byggnadens id + 1.</summary>
    public int OccupantAt(TilePoint p) => _occupant[Index(p)];

    public void SetTerrain(TilePoint p, Terrain terrain)
    {
        int i = Index(p);
        if (_terrain[i] == terrain) return;
        _terrain[i] = terrain;
        if (!TerrainRules.IsBuildable(terrain)) _path[i] = PathState.None;
        Version++;
    }

    public void SetPath(TilePoint p, PathState state)
    {
        int i = Index(p);
        if (state != PathState.None && !CanLayPath(p))
            throw new InvalidOperationException($"Stig kan inte läggas på {p}");
        if (_path[i] == state) return;
        _path[i] = state;
        Version++;
    }

    public void SetOwner(TilePoint p, byte owner) => _owner[Index(p)] = owner;

    public void SetOccupant(TilePoint p, int occupant)
    {
        int i = Index(p);
        if (_occupant[i] == occupant) return;
        _occupant[i] = occupant;
        Version++;
    }

    /// <summary>Stigar läggs bara på glänta, och inte där något står.</summary>
    public bool CanLayPath(TilePoint p) =>
        Inside(p) && TerrainRules.IsBuildable(_terrain[Index(p)]) && _occupant[Index(p)] == 0;

    /// <summary>Kan man gå in på rutan? Byggnader stänger rutan, utom som mål (se Pathfinder).</summary>
    public bool IsWalkable(TilePoint p, MoveClass move) => IsWalkable(Index(p), move);

    internal bool IsWalkable(int index, MoveClass move) =>
        _occupant[index] == 0 && TerrainRules.IsPassable(_terrain[index], move);

    /// <summary>Kostnaden för att gå in på rutan, rakt. Upptrampad stig går före terrängen.</summary>
    public int StepCost(TilePoint p, MoveClass move) => StepCost(Index(p), move);

    internal int StepCost(int index, MoveClass move) =>
        _path[index] == PathState.Trodden ? TerrainRules.PathStepCost : TerrainRules.StepCost(_terrain[index], move);

    public void AddToHash(ref StateHasher h)
    {
        h.Add(Width);
        h.Add(Height);
        for (int i = 0; i < _terrain.Length; i++)
        {
            h.Add((uint)_terrain[i] | (uint)_path[i] << 8 | (uint)_owner[i] << 16);
            h.Add(_occupant[i]);
        }
    }
}

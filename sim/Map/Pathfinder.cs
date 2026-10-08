using System;
using System.Collections.Generic;
using Hallonkriget.Sim.Determinism;

namespace Hallonkriget.Sim.Map;

/// <summary>
/// A* på rutnätet, åtta riktningar, med kostnad per terräng och stig. Diagonaler över hörn
/// som inte går att gå på är stängda. Samma fråga ger alltid samma väg: grannar prövas i fast
/// ordning och lika billiga rutor tas i den ordning de hittades.
///
/// Arrayerna återanvänds mellan sökningar, så en Pathfinder per karta räcker.
/// Den är inte en del av tillståndet och behöver inte vara med i Hash().
/// </summary>
public sealed class Pathfinder
{
    /// <summary>Faktor på stegkostnaden rakt och diagonalt (ungefär 10 och 10·√2).</summary>
    public const int Straight = 10, Diagonal = 14;

    private static readonly int[] Dx = { 1, 0, -1, 0, 1, -1, -1, 1 };
    private static readonly int[] Dy = { 0, 1, 0, -1, 1, 1, -1, -1 };

    private readonly GameMap _map;
    private readonly int[] _g;
    private readonly int[] _parent;
    private readonly int[] _seen;    // sökningens nummer när rutan senast fick ett g-värde
    private readonly int[] _closed;  // sökningens nummer när rutan senast stängdes
    private int _search;

    private Node[] _heap = new Node[256];
    private int _heapCount;
    private int _sequence;

    /// <summary>Kostnaden för den senast hittade vägen, i stegkostnad × Straight/Diagonal.</summary>
    public int LastCost { get; private set; }

    public Pathfinder(GameMap map)
    {
        _map = map;
        _g = new int[map.TileCount];
        _parent = new int[map.TileCount];
        _seen = new int[map.TileCount];
        _closed = new int[map.TileCount];
    }

    /// <summary>
    /// Letar en väg från start till mål och lägger den i result, start och mål inräknade.
    /// Målet får vara en ruta man annars inte kan gå på, till exempel en byggnad.
    /// </summary>
    public bool FindPath(TilePoint start, TilePoint goal, MoveClass move, List<TilePoint> result)
    {
        result.Clear();
        LastCost = 0;
        if (!_map.Inside(start) || !_map.Inside(goal)) return false;

        _search++;
        _heapCount = 0;
        _sequence = 0;
        int startIndex = _map.Index(start), goalIndex = _map.Index(goal);
        _g[startIndex] = 0;
        _parent[startIndex] = -1;
        _seen[startIndex] = _search;
        Push(startIndex, Heuristic(start, goal));

        while (_heapCount > 0)
        {
            int current = Pop();
            if (_closed[current] == _search) continue;
            _closed[current] = _search;

            if (current == goalIndex)
            {
                LastCost = _g[current];
                for (int i = current; i >= 0; i = _parent[i]) result.Add(_map.PointOf(i));
                result.Reverse();
                return true;
            }

            int cx = current % _map.Width, cy = current / _map.Width;
            for (int d = 0; d < 8; d++)
            {
                int nx = cx + Dx[d], ny = cy + Dy[d];
                if (nx < 0 || ny < 0 || nx >= _map.Width || ny >= _map.Height) continue;
                int next = ny * _map.Width + nx;
                if (_closed[next] == _search) continue;
                if (next != goalIndex && !_map.IsWalkable(next, move)) continue;

                bool diagonal = d >= 4;
                if (diagonal && (!_map.IsWalkable(cy * _map.Width + nx, move) ||
                                 !_map.IsWalkable(ny * _map.Width + cx, move))) continue;

                int g = _g[current] + (diagonal ? Diagonal : Straight) * _map.StepCost(next, move);
                if (_seen[next] == _search && g >= _g[next]) continue;
                _g[next] = g;
                _parent[next] = current;
                _seen[next] = _search;
                Push(next, g + Heuristic(new TilePoint(nx, ny), goal));
            }
        }
        return false;
    }

    /// <summary>Oktilavstånd med den billigaste stegkostnaden. Överskattar aldrig.</summary>
    private static int Heuristic(TilePoint a, TilePoint b)
    {
        int dx = IntMath.Abs(a.X - b.X), dy = IntMath.Abs(a.Y - b.Y);
        int diag = dx < dy ? dx : dy, straight = (dx > dy ? dx : dy) - diag;
        return (Straight * straight + Diagonal * diag) * TerrainRules.MinStepCost;
    }

    private struct Node
    {
        public int F;
        public int Sequence;
        public int Index;
    }

    private static bool Before(in Node a, in Node b) => a.F != b.F ? a.F < b.F : a.Sequence < b.Sequence;

    private void Push(int index, int f)
    {
        if (_heapCount == _heap.Length) Array.Resize(ref _heap, _heap.Length * 2);
        var node = new Node { F = f, Sequence = _sequence++, Index = index };
        int i = _heapCount++;
        while (i > 0)
        {
            int parent = (i - 1) / 2;
            if (!Before(node, _heap[parent])) break;
            _heap[i] = _heap[parent];
            i = parent;
        }
        _heap[i] = node;
    }

    private int Pop()
    {
        int top = _heap[0].Index;
        var last = _heap[--_heapCount];
        int i = 0;
        while (true)
        {
            int child = 2 * i + 1;
            if (child >= _heapCount) break;
            if (child + 1 < _heapCount && Before(_heap[child + 1], _heap[child])) child++;
            if (!Before(_heap[child], last)) break;
            _heap[i] = _heap[child];
            i = child;
        }
        _heap[i] = last;
        return top;
    }
}

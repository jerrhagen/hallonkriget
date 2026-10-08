using System.Diagnostics;
using Hallonkriget.Sim.Map;

namespace Hallonkriget.Sim.Tests.Map;

public class PathfinderTests
{
    private static List<TilePoint> Find(string ascii, MoveClass move = MoveClass.Foot)
    {
        var map = MapAscii.Parse(ascii, out var start, out var goal);
        var path = new List<TilePoint>();
        Assert.True(new Pathfinder(map).FindPath(start, goal, move, path), "ingen väg hittades");
        return path;
    }

    private static bool Exists(string ascii, MoveClass move = MoveClass.Foot)
    {
        var map = MapAscii.Parse(ascii, out var start, out var goal);
        return new Pathfinder(map).FindPath(start, goal, move, new List<TilePoint>());
    }

    /// <summary>Varje steg i vägen går till en granne, och vägen börjar och slutar rätt.</summary>
    private static void AssertConnected(List<TilePoint> path, TilePoint start, TilePoint goal)
    {
        Assert.Equal(start, path[0]);
        Assert.Equal(goal, path[^1]);
        for (int i = 1; i < path.Count; i++)
        {
            int dx = Math.Abs(path[i].X - path[i - 1].X), dy = Math.Abs(path[i].Y - path[i - 1].Y);
            Assert.True(dx <= 1 && dy <= 1 && dx + dy > 0, $"hopp mellan {path[i - 1]} och {path[i]}");
        }
    }

    [Fact]
    public void StartIsGoal()
    {
        var map = new GameMap(3, 3);
        var path = new List<TilePoint>();
        Assert.True(new Pathfinder(map).FindPath(new TilePoint(1, 1), new TilePoint(1, 1), MoveClass.Foot, path));
        Assert.Equal(new[] { new TilePoint(1, 1) }, path);
    }

    [Fact]
    public void WalksStraightAcrossOpenGround()
    {
        var path = Find("""
            S.....G
            """);
        Assert.Equal(7, path.Count);
        AssertConnected(path, new TilePoint(0, 0), new TilePoint(6, 0));
    }

    [Fact]
    public void WalksDiagonally()
    {
        var path = Find("""
            S...
            ....
            ....
            ...G
            """);
        Assert.Equal(4, path.Count);
    }

    [Fact]
    public void GoesAroundWater()
    {
        var path = Find("""
            ......
            S.~~.G
            ..~~..
            ......
            """);
        AssertConnected(path, new TilePoint(0, 1), new TilePoint(5, 1));
        Assert.DoesNotContain(path, p => p.X is 2 or 3 && p.Y is 1 or 2);
    }

    [Fact]
    public void NoPathThroughAWallOfWater()
    {
        Assert.False(Exists("""
            S.~..
            ..~.G
            ..~..
            """));
    }

    [Fact]
    public void DoesNotCutCornersBetweenObstacles()
    {
        // Diagonalen mellan två vattenrutor är stängd.
        Assert.False(Exists("""
            S~
            ~G
            """));
    }

    [Fact]
    public void PrefersALongerPathOverForest()
    {
        // Rakt genom skogen är 5 steg á 4× stigkostnaden. Omvägen på stig är billigare.
        var path = Find("""
            #######
            S#TTT#G
            .......
            """);
        Assert.DoesNotContain(path, p => p.Y == 1 && p.X is >= 2 and <= 4);
    }

    [Fact]
    public void PrefersTroddenPathOverGrass()
    {
        // Stigen gör en omväg på en rad men är dubbelt så snabb.
        var path = Find("""
            .#######.
            S.......G
            """);
        Assert.Contains(path, p => p.Y == 0 && p.X == 4);
    }

    [Fact]
    public void PlannedPathIsNotFasterThanGrass()
    {
        var path = Find("""
            .+++++++.
            S.......G
            """);
        Assert.DoesNotContain(path, p => p.Y == 0);
    }

    [Fact]
    public void VehiclesCannotCrossBog()
    {
        const string map = """
            mmm
            SmG
            mmm
            """;
        Assert.True(Exists(map, MoveClass.Foot));
        Assert.False(Exists(map, MoveClass.Vehicle));
    }

    [Fact]
    public void BuildingsBlockButCanBeTheGoal()
    {
        Assert.False(Exists("""
            ~~~~~
            S.B.G
            ~~~~~
            """));

        var map = MapAscii.Parse("""
            S..B
            """, out var start, out _);
        var path = new List<TilePoint>();
        Assert.True(new Pathfinder(map).FindPath(start, new TilePoint(3, 0), MoveClass.Foot, path));
        Assert.Equal(new TilePoint(3, 0), path[^1]);
    }

    [Fact]
    public void ReportsCostAlongTheWay()
    {
        var map = MapAscii.Parse("""
            S##G
            """, out var start, out var goal);
        var pf = new Pathfinder(map);
        Assert.True(pf.FindPath(start, goal, MoveClass.Foot, new List<TilePoint>()));
        // Kostnaden är för rutorna man går in på: två stigrutor och målrutan på gräs.
        int expected = 2 * Pathfinder.Straight * TerrainRules.PathStepCost +
                       Pathfinder.Straight * TerrainRules.StepCost(Terrain.Clearing, MoveClass.Foot);
        Assert.Equal(expected, pf.LastCost);
    }

    [Fact]
    public void OutsideTheMapIsNoPath()
    {
        var map = new GameMap(5, 5);
        var pf = new Pathfinder(map);
        Assert.False(pf.FindPath(new TilePoint(0, 0), new TilePoint(5, 0), MoveClass.Foot, new List<TilePoint>()));
        Assert.False(pf.FindPath(new TilePoint(-1, 0), new TilePoint(2, 2), MoveClass.Foot, new List<TilePoint>()));
    }

    [Fact]
    public void SameQueryGivesSamePathEveryTime()
    {
        // Många vägar är lika billiga på öppen mark. Valet mellan dem ska vara fast.
        var map = new GameMap(40, 40);
        var a = new List<TilePoint>();
        var b = new List<TilePoint>();
        var pf = new Pathfinder(map);
        pf.FindPath(new TilePoint(1, 3), new TilePoint(37, 30), MoveClass.Foot, a);
        pf.FindPath(new TilePoint(5, 5), new TilePoint(6, 30), MoveClass.Foot, new List<TilePoint>());
        new Pathfinder(map).FindPath(new TilePoint(1, 3), new TilePoint(37, 30), MoveClass.Foot, b);
        Assert.Equal(a, b);
    }

    [Fact]
    public void FindsTheCheapestPathOnARandomMap()
    {
        // Jämför A* mot en enkel Dijkstra på slumpade kartor.
        var rng = new Hallonkriget.Sim.Determinism.Rng(77);
        var terrains = new[] { Terrain.Clearing, Terrain.Clearing, Terrain.Forest, Terrain.Stony, Terrain.Water, Terrain.Road, Terrain.Bog };
        for (int round = 0; round < 30; round++)
        {
            var map = new GameMap(24, 18);
            foreach (var p in map.AllTiles())
            {
                map.SetTerrain(p, terrains[rng.Range(0, terrains.Length - 1)]);
                if (map.CanLayPath(p) && rng.Chance(20)) map.SetPath(p, PathState.Trodden);
            }
            var start = new TilePoint(rng.Range(0, 23), rng.Range(0, 17));
            var goal = new TilePoint(rng.Range(0, 23), rng.Range(0, 17));
            map.SetTerrain(start, Terrain.Clearing);
            map.SetTerrain(goal, Terrain.Clearing);

            var pf = new Pathfinder(map);
            bool found = pf.FindPath(start, goal, MoveClass.Foot, new List<TilePoint>());
            int best = Dijkstra(map, start, goal);
            Assert.Equal(best >= 0, found);
            if (found) Assert.Equal(best, pf.LastCost);
        }
    }

    [Fact]
    public void LongSearchOnAFullSizeMapIsFast()
    {
        // 128×128 är den största kartan. En sökväg tvärs över med en lång mur ska gå på några millisekunder.
        var map = new GameMap(128, 128);
        for (int y = 0; y < 120; y++) map.SetTerrain(new TilePoint(64, y), Terrain.Water);
        var pf = new Pathfinder(map);
        var path = new List<TilePoint>();
        pf.FindPath(new TilePoint(0, 0), new TilePoint(127, 0), MoveClass.Foot, path); // uppvärmning
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 20; i++)
            Assert.True(pf.FindPath(new TilePoint(0, 0), new TilePoint(127, 0), MoveClass.Foot, path));
        Assert.True(sw.Elapsed.TotalMilliseconds / 20 < 20, $"{sw.Elapsed.TotalMilliseconds / 20:F1} ms per sökning");
    }

    private static int Dijkstra(GameMap map, TilePoint start, TilePoint goal)
    {
        var dist = new int[map.TileCount];
        Array.Fill(dist, int.MaxValue);
        var queue = new PriorityQueue<TilePoint, int>();
        dist[map.Index(start)] = 0;
        queue.Enqueue(start, 0);
        while (queue.TryDequeue(out var p, out int d))
        {
            if (d > dist[map.Index(p)]) continue;
            if (p == goal) return d;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                var n = new TilePoint(p.X + dx, p.Y + dy);
                if (!map.Inside(n) || !map.IsWalkable(n, MoveClass.Foot) && n != goal) continue;
                if (dx != 0 && dy != 0 &&
                    (!map.IsWalkable(new TilePoint(p.X + dx, p.Y), MoveClass.Foot) ||
                     !map.IsWalkable(new TilePoint(p.X, p.Y + dy), MoveClass.Foot))) continue;
                int step = (dx != 0 && dy != 0 ? Pathfinder.Diagonal : Pathfinder.Straight) * map.StepCost(n, MoveClass.Foot);
                int nd = d + step;
                if (nd < dist[map.Index(n)])
                {
                    dist[map.Index(n)] = nd;
                    queue.Enqueue(n, nd);
                }
            }
        }
        return -1;
    }
}

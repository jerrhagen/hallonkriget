using Hallonkriget.Sim.Determinism;
using Hallonkriget.Sim.Map;

namespace Hallonkriget.Sim.Tests.Map;

public class GameMapTests
{
    [Fact]
    public void NewMapIsUnownedClearingWithoutPaths()
    {
        var map = new GameMap(4, 3);
        Assert.Equal(12, map.TileCount);
        foreach (var p in map.AllTiles())
        {
            Assert.Equal(Terrain.Clearing, map.TerrainAt(p));
            Assert.Equal(PathState.None, map.PathAt(p));
            Assert.Equal(GameMap.NoOwner, map.OwnerAt(p));
            Assert.Equal(0, map.OccupantAt(p));
        }
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(3, 2, true)]
    [InlineData(-1, 0, false)]
    [InlineData(4, 0, false)]
    [InlineData(0, 3, false)]
    public void InsideKnowsTheEdges(int x, int y, bool inside)
    {
        Assert.Equal(inside, new GameMap(4, 3).Inside(new TilePoint(x, y)));
    }

    [Fact]
    public void RejectsMapsWithoutSize()
    {
        Assert.Throws<ArgumentException>(() => new GameMap(0, 5));
        Assert.Throws<ArgumentException>(() => new GameMap(5, -1));
    }

    [Fact]
    public void TerrainRulesFollowTheDesignDocument()
    {
        // Designdokumentet, Kartan och Stigar: stig 2× så fort som gräs, 4× så fort som skog.
        Assert.Equal(2 * TerrainRules.PathStepCost, TerrainRules.StepCost(Terrain.Clearing, MoveClass.Foot));
        Assert.Equal(4 * TerrainRules.PathStepCost, TerrainRules.StepCost(Terrain.Forest, MoveClass.Foot));
        Assert.True(TerrainRules.StepCost(Terrain.Road, MoveClass.Foot) < TerrainRules.PathStepCost);

        Assert.True(TerrainRules.IsBuildable(Terrain.Clearing));
        foreach (var t in Enum.GetValues<Terrain>())
            if (t != Terrain.Clearing) Assert.False(TerrainRules.IsBuildable(t), t.ToString());

        Assert.False(TerrainRules.IsPassable(Terrain.Water, MoveClass.Foot));
        Assert.True(TerrainRules.IsPassable(Terrain.Bog, MoveClass.Foot));
        Assert.False(TerrainRules.IsPassable(Terrain.Bog, MoveClass.Vehicle));
    }

    [Fact]
    public void PathsCanOnlyBeLaidOnClearing()
    {
        var map = MapAscii.Parse("""
            .T~
            """);
        Assert.True(map.CanLayPath(new TilePoint(0, 0)));
        Assert.False(map.CanLayPath(new TilePoint(1, 0)));
        Assert.False(map.CanLayPath(new TilePoint(2, 0)));
    }

    [Fact]
    public void OnlyTroddenPathsAreFast()
    {
        var map = MapAscii.Parse("""
            .+#
            """);
        Assert.Equal(TerrainRules.StepCost(Terrain.Clearing, MoveClass.Foot), map.StepCost(new TilePoint(0, 0), MoveClass.Foot));
        Assert.Equal(TerrainRules.StepCost(Terrain.Clearing, MoveClass.Foot), map.StepCost(new TilePoint(1, 0), MoveClass.Foot));
        Assert.Equal(TerrainRules.PathStepCost, map.StepCost(new TilePoint(2, 0), MoveClass.Foot));
    }

    [Fact]
    public void ChangesThatAffectWalkingBumpTheVersion()
    {
        var map = new GameMap(3, 3);
        var p = new TilePoint(1, 1);
        int v = map.Version;

        map.SetOwner(p, 0);
        Assert.Equal(v, map.Version); // ägaren påverkar inte vägarna

        map.SetPath(p, PathState.Trodden);
        Assert.True(map.Version > v);
        v = map.Version;

        map.SetOccupant(p, 7);
        Assert.True(map.Version > v);
        v = map.Version;

        map.SetTerrain(p, Terrain.Forest);
        Assert.True(map.Version > v);
    }

    [Fact]
    public void HashCoversEveryLayer()
    {
        static ulong HashOf(GameMap m)
        {
            var h = new StateHasher();
            m.AddToHash(ref h);
            return h.Value;
        }

        var p = new TilePoint(2, 1);
        var baseline = HashOf(new GameMap(4, 4));
        var changes = new Action<GameMap>[]
        {
            m => m.SetTerrain(p, Terrain.Meadow),
            m => m.SetPath(p, PathState.Planned),
            m => m.SetOwner(p, 1),
            m => m.SetOccupant(p, 3),
        };
        var seen = new List<ulong> { baseline };
        foreach (var change in changes)
        {
            var m = new GameMap(4, 4);
            change(m);
            var h = HashOf(m);
            Assert.DoesNotContain(h, seen);
            seen.Add(h);
        }
        Assert.NotEqual(HashOf(new GameMap(4, 4)), HashOf(new GameMap(2, 8)));
    }
}

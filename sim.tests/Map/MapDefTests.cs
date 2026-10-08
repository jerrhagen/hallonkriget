using Hallonkriget.Sim.Data;
using Hallonkriget.Sim.Map;

namespace Hallonkriget.Sim.Tests.Map;

public class MapDefTests
{
    [Fact]
    public void TerrainIsLaidOutInOrder()
    {
        var map = MapDef.Parse("""
            { "id": "liten", "size": [12, 10], "starts": [[1, 1]],
              "terrain": [ { "terrain": "forest", "rect": [5, 5, 8, 7] }, { "terrain": "water", "rect": [8, 7, 9, 9] } ] }
            """);
        var state = GameState.NewMatch(MatchSetup.OnMap(1, map, TestData.Game, (Faction.Torpet, false)));
        Assert.Equal(Terrain.Forest, state.Map.TerrainAt(new TilePoint(5, 5)));
        Assert.Equal(Terrain.Water, state.Map.TerrainAt(new TilePoint(8, 7))); // den senare vinner
        Assert.Equal(Terrain.Clearing, state.Map.TerrainAt(new TilePoint(4, 5)));
        Assert.Equal(new TilePoint(1, 1), state.Buildings[0].Origin);
    }

    [Fact]
    public void ARectangleOutsideTheMapIsAnError()
    {
        Assert.Throws<GameDataException>(() => MapDef.Parse("""
            { "id": "fel", "size": [12, 10], "starts": [], "terrain": [ { "terrain": "forest", "rect": [5, 5, 12, 7] } ] }
            """));
    }

    [Fact]
    public void TheRealMapsLoad()
    {
        var map = TestData.Map("brodgarden");
        Assert.Equal((44, 26), (map.Width, map.Height));
        Assert.Contains(map.Areas, a => a.Terrain == Terrain.Road);
    }
}

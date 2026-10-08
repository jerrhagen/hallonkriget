using Hallonkriget.Sim.Map;

namespace Hallonkriget.Sim.Tests.Map;

/// <summary>
/// Bygger kartor för tester ur text, en rad per kartrad:
/// <c>.</c> glänta, <c>s</c> stenig mark, <c>T</c> granskog, <c>,</c> äng, <c>m</c> myr,
/// <c>~</c> vatten, <c>x</c> skrothög, <c>h</c> hallonsnår, <c>p</c> plommonträd, <c>=</c> landsväg,
/// <c>#</c> upptrampad stig på glänta, <c>+</c> planerad stig på glänta, <c>B</c> en byggnad på glänta,
/// <c>S</c> och <c>G</c> start och mål på glänta.
/// </summary>
public static class MapAscii
{
    public static GameMap Parse(string text, out TilePoint start, out TilePoint goal)
    {
        var rows = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var map = new GameMap(rows[0].Length, rows.Length);
        start = goal = new TilePoint(-1, -1);
        for (int y = 0; y < rows.Length; y++)
        for (int x = 0; x < rows[y].Length; x++)
        {
            var p = new TilePoint(x, y);
            char c = rows[y][x];
            map.SetTerrain(p, c switch
            {
                's' => Terrain.Stony,
                'T' => Terrain.Forest,
                ',' => Terrain.Meadow,
                'm' => Terrain.Bog,
                '~' => Terrain.Water,
                'x' => Terrain.ScrapHeap,
                'h' => Terrain.RaspberryThicket,
                'p' => Terrain.PlumTree,
                '=' => Terrain.Road,
                _ => Terrain.Clearing,
            });
            if (c == '#') map.SetPath(p, PathState.Trodden);
            if (c == '+') map.SetPath(p, PathState.Planned);
            if (c == 'B') map.SetOccupant(p, 1);
            if (c == 'S') start = p;
            if (c == 'G') goal = p;
        }
        return map;
    }

    public static GameMap Parse(string text) => Parse(text, out _, out _);
}

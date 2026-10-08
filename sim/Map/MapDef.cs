using System.Collections.Generic;
using System.Text.Json;
using Hallonkriget.Sim.Data;

namespace Hallonkriget.Sim.Map;

/// <summary>Ett rektangulärt område med en terräng, från och med (X0, Y0) till och med (X1, Y1).</summary>
public readonly record struct TerrainArea(Terrain Terrain, int X0, int Y0, int X1, int Y1);

/// <summary>
/// En karta från data/maps: storlek, startplatser och terräng som rektanglar ovanpå glänta.
/// Rektanglarna läggs i ordning, så en senare skriver över en tidigare.
/// </summary>
public sealed record MapDef(string Id, int Width, int Height, TilePoint[] Starts, TerrainArea[] Areas)
{
    /// <summary>Spelarnas tomter, en rektangel per startplats och i samma ordning. Tomt för kartor utan tomter.</summary>
    public TerrainArea[] Territories { get; init; } = System.Array.Empty<TerrainArea>();

    public static MapDef Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        string id = root.GetProperty("id").GetString() ?? "";
        int w = root.GetProperty("size")[0].GetInt32(), h = root.GetProperty("size")[1].GetInt32();
        if (w is < 8 or > 128 || h is < 8 or > 128) throw new GameDataException($"{id}: kartan ska vara 8–128 rutor");

        var starts = new List<TilePoint>();
        foreach (var s in root.GetProperty("starts").EnumerateArray())
            starts.Add(new TilePoint(s[0].GetInt32(), s[1].GetInt32()));

        var areas = new List<TerrainArea>();
        if (root.TryGetProperty("terrain", out var ts))
        {
            foreach (var t in ts.EnumerateArray())
            {
                var terrain = GameData.ParseTerrain(t.GetProperty("terrain").GetString() ?? "", id);
                var r = t.GetProperty("rect");
                var area = new TerrainArea(terrain, r[0].GetInt32(), r[1].GetInt32(), r[2].GetInt32(), r[3].GetInt32());
                if (area.X0 < 0 || area.Y0 < 0 || area.X1 >= w || area.Y1 >= h || area.X0 > area.X1 || area.Y0 > area.Y1)
                    throw new GameDataException($"{id}: rektangeln {area} ligger utanför kartan");
                areas.Add(area);
            }
        }
        var territories = new List<TerrainArea>();
        if (root.TryGetProperty("territories", out var tr))
        {
            foreach (var r in tr.EnumerateArray())
            {
                var area = new TerrainArea(Terrain.Clearing, r[0].GetInt32(), r[1].GetInt32(), r[2].GetInt32(), r[3].GetInt32());
                if (area.X0 < 0 || area.Y0 < 0 || area.X1 >= w || area.Y1 >= h || area.X0 > area.X1 || area.Y0 > area.Y1)
                    throw new GameDataException($"{id}: tomten {area} ligger utanför kartan");
                territories.Add(area);
            }
            if (territories.Count != starts.Count) throw new GameDataException($"{id}: en tomt per startplats");
        }
        return new MapDef(id, w, h, starts.ToArray(), areas.ToArray()) { Territories = territories.ToArray() };
    }

    internal void Apply(GameMap map)
    {
        foreach (var a in Areas)
        for (int y = a.Y0; y <= a.Y1; y++)
        for (int x = a.X0; x <= a.X1; x++)
            map.SetTerrain(new TilePoint(x, y), a.Terrain);
    }

    /// <summary>Lägger ut tomterna för de spelare som finns i matchen.</summary>
    internal void ApplyTerritories(GameMap map, int players)
    {
        for (int i = 0; i < Territories.Length && i < players; i++)
        {
            var a = Territories[i];
            for (int y = a.Y0; y <= a.Y1; y++)
            for (int x = a.X0; x <= a.X1; x++)
                map.SetOwner(new TilePoint(x, y), (byte)i);
        }
    }
}

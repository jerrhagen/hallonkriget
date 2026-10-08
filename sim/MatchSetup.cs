using System.Collections.Generic;
using Hallonkriget.Sim.Data;
using Hallonkriget.Sim.Map;

namespace Hallonkriget.Sim;

/// <summary>
/// Allt som behövs för att starta en match. Samma uppsättning ger samma match på alla datorer.
/// Utan data blir det en match utan byggnader, som i determinismtestets äldsta del. Med en karta
/// läggs dess terräng ut innan startbyggnaderna placeras; storleken ska då stämma med kartans.
/// </summary>
public sealed record MatchSetup(ulong Seed, int MapWidth, int MapHeight, IReadOnlyList<PlayerSetup> Players,
    GameData? Data = null, MapDef? Map = null)
{
    /// <summary>En match på kartan, med spelarna på kartans startplatser i tur och ordning.</summary>
    public static MatchSetup OnMap(ulong seed, MapDef map, GameData data, params (Faction Faction, bool IsComputer)[] players)
    {
        var list = new List<PlayerSetup>();
        for (int i = 0; i < players.Length; i++)
            list.Add(new PlayerSetup(players[i].Faction, players[i].IsComputer, i < map.Starts.Length ? map.Starts[i] : null));
        return new MatchSetup(seed, map.Width, map.Height, list, data, map);
    }

    /// <summary>En match där spelarnas uppsättning anges helt, med startplatserna från kartan.</summary>
    public static MatchSetup OnMap(ulong seed, MapDef map, GameData data, params PlayerSetup[] players)
    {
        var list = new List<PlayerSetup>();
        for (int i = 0; i < players.Length; i++)
            list.Add(players[i] with { Start = i < map.Starts.Length ? map.Starts[i] : null });
        return new MatchSetup(seed, map.Width, map.Height, list, data, map);
    }
}

/// <summary>
/// En spelare. Start är övre vänstra rutan för stugan eller mangårdsbyggnaden, om den ska stå där från början.
/// Difficulty gäller datorspelaren.
/// </summary>
public sealed record PlayerSetup(Faction Faction, bool IsComputer, TilePoint? Start = null, Difficulty Difficulty = Difficulty.Normal);

/// <summary>Datorspelarens svårighetsgrad, designdokumentet (Datorspelaren).</summary>
public enum Difficulty : byte
{
    Easy,
    Normal,
    Hard,
}

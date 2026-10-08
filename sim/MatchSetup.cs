using System.Collections.Generic;
using Hallonkriget.Sim.Data;
using Hallonkriget.Sim.Map;

namespace Hallonkriget.Sim;

/// <summary>
/// Allt som behövs för att starta en match. Samma uppsättning ger samma match på alla datorer.
/// Utan data blir det en match utan byggnader, som i determinismtestets äldsta del.
/// </summary>
public sealed record MatchSetup(ulong Seed, int MapWidth, int MapHeight, IReadOnlyList<PlayerSetup> Players, GameData? Data = null);

/// <summary>En spelare. Start är övre vänstra rutan för stugan eller mangårdsbyggnaden, om den ska stå där från början.</summary>
public sealed record PlayerSetup(Faction Faction, bool IsComputer, TilePoint? Start = null);

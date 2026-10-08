using System.Collections.Generic;

namespace Hallonkriget.Sim;

/// <summary>Allt som behövs för att starta en match. Samma uppsättning ger samma match på alla datorer.</summary>
public sealed record MatchSetup(ulong Seed, int MapWidth, int MapHeight, IReadOnlyList<PlayerSetup> Players);

public sealed record PlayerSetup(Faction Faction, bool IsComputer);

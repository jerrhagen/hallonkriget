namespace Hallonkriget.Sim.Map;

/// <summary>Terrängen på en ruta. Se tabellen under Kartan i designdokumentet.</summary>
public enum Terrain : byte
{
    Clearing,          // glänta (gräs): allt kan byggas här
    Stony,             // stenig mark: sten, blir glänta när den är röjd
    Forest,            // granskog: timmer
    Meadow,            // äng: hö och honung
    Bog,               // myr: bara gående
    Water,             // sjö och bäck
    ScrapHeap,         // skrothög
    RaspberryThicket,  // hallonsnår
    PlumTree,          // plommonträd
    Road,              // landsväg
    Field,             // odlad åker: hör till en åker, går att gå över men inte att bygga på
}

/// <summary>Hur något tar sig fram. Fläskkavalleri och traktor kan inte gå i myr.</summary>
public enum MoveClass : byte
{
    Foot,
    Vehicle,
}

/// <summary>En stig planeras av spelaren och trampas upp av en hantlangare. Bara upptrampad stig går fort.</summary>
public enum PathState : byte
{
    None,
    Planned,
    Trodden,
}

/// <summary>
/// Reglerna för terrängen: vad som går att bygga på, var man kan gå och hur fort.
/// Stegkostnaden är relativ: stig 4, gräs 8, skog 16, som designdokumentets
/// "2× så fort på stig som på gräs, 4× så fort som i skog".
/// </summary>
public static class TerrainRules
{
    public const int PathStepCost = 4;

    /// <summary>Den lägsta stegkostnaden som finns. A* behöver den för att uppskatta avstånd.</summary>
    public const int MinStepCost = 3;

    public static bool IsBuildable(Terrain t) => t == Terrain.Clearing;

    public static bool IsPassable(Terrain t, MoveClass move) => t switch
    {
        Terrain.Water => false,
        Terrain.Bog => move == MoveClass.Foot,
        _ => true,
    };

    /// <summary>Kostnaden för att gå in på en ruta med den här terrängen, utan stig.</summary>
    public static int StepCost(Terrain t, MoveClass move) => t switch
    {
        Terrain.Road => 3,
        Terrain.Stony => 12,
        Terrain.Forest => 16,
        Terrain.Bog => 16,
        _ => 8,
    };
}

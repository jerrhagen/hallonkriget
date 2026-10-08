namespace Hallonkriget.Sim.Commands;

/// <summary>
/// Allt en spelare kan göra är ett kommando. Gränssnittet, nätverket och datorspelaren
/// skapar kommandon; bara GameState.Tick ändrar spelet.
/// </summary>
public enum CommandType : byte
{
    None = 0,

    // Fas 0: kommandon för vandrare, så att determinismtestet har något att göra.
    // Ersätts av riktiga kommandon (bygg, lägg stig, utbilda ...) från fas 1.
    SpawnWalker = 1,
    MoveWalker = 2,

    /// <summary>Placera en byggplats. A: byggnadstyp (nummer i buildings.json), B och C: övre vänstra rutan.</summary>
    PlaceBuilding = 3,

    /// <summary>Välj recept. A: byggnadens id, B: receptets nummer, eller -1 för att turas om.</summary>
    SelectRecipe = 4,

    /// <summary>Planera en stig på en ruta. A och B: rutan. En hantlangare trampar upp den.</summary>
    PlanPath = 5,
}

/// <summary>Ett kommando: vem, när, vad och upp till tre heltalsparametrar.</summary>
public readonly record struct Command(int Tick, byte Player, CommandType Type, int A = 0, int B = 0, int C = 0);

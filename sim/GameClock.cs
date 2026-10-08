namespace Hallonkriget.Sim;

/// <summary>
/// Spelets klocka: en vecka per speltimme (designdokumentet, Kaffeekonomin och lanthandeln).
/// Matchen börjar en måndag klockan noll. En speldag är 1/7 timme, cirka 8 minuter och 34 sekunder.
/// </summary>
public static class GameClock
{
    public const int WeekTicks = 60 * 60 * GameState.TicksPerSecond;

    /// <summary>0 är måndag, 6 är söndag.</summary>
    public static int Weekday(int tick) => (int)((long)(tick % WeekTicks) * 7 / WeekTicks);

    /// <summary>Söndag: lanthandeln är stängd.</summary>
    public static bool IsSunday(int tick) => Weekday(tick) == 6;
}

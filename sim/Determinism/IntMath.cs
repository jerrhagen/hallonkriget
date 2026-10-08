namespace Hallonkriget.Sim.Determinism;

/// <summary>Heltalsmatematik som annars skulle ha gått via Math och double.</summary>
public static class IntMath
{
    /// <summary>Kvadratroten av ett icke-negativt tal, avrundad nedåt.</summary>
    public static long Sqrt(long value)
    {
        if (value < 0) throw new System.ArgumentOutOfRangeException(nameof(value));
        if (value < 2) return value;

        // Newtons metod med heltal. Startar över roten och går nedåt.
        long x = value;
        long y = (x >> 1) + (x & 1);
        while (y < x)
        {
            x = y;
            y = (x + value / x) / 2;
        }
        return x;
    }

    public static int Max(int a, int b) => a > b ? a : b;

    public static int Min(int a, int b) => a < b ? a : b;

    public static int Abs(int value) => value < 0 ? -value : value;

    public static int Clamp(int value, int min, int max) =>
        value < min ? min : value > max ? max : value;
}

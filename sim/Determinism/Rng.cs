namespace Hallonkriget.Sim.Determinism;

/// <summary>
/// Spelets enda slumpgenerator: xorshift64. Tillståndet ingår i GameState och i Hash(),
/// så alla datorer drar samma tal i samma ordning. System.Random är förbjuden i sim/.
/// </summary>
public sealed class Rng
{
    public ulong State { get; private set; }

    public Rng(ulong seed)
    {
        // xorshift fastnar på noll, så nollfröet byts mot en fast konstant.
        State = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
    }

    public ulong NextULong()
    {
        ulong x = State;
        x ^= x << 13;
        x ^= x >> 7;
        x ^= x << 17;
        State = x;
        return x;
    }

    /// <summary>Ett tal i [0, maxExclusive). Lemires metod: multiplikation i stället för modulo.</summary>
    public int Next(int maxExclusive)
    {
        if (maxExclusive <= 0) throw new System.ArgumentOutOfRangeException(nameof(maxExclusive));
        ulong r = NextULong() >> 32;
        return (int)((r * (ulong)maxExclusive) >> 32);
    }

    /// <summary>Ett tal i [min, max].</summary>
    public int Range(int min, int max) => min + Next(max - min + 1);

    /// <summary>Sant med sannolikheten percent/100.</summary>
    public bool Chance(int percent) => Next(100) < percent;
}

using System;

namespace Hallonkriget.Sim.Determinism;

/// <summary>
/// Fast punkt med 8 bråkbitar: ett heltal i 1/256 ruta. Ersätter flyttal i hela kärnan.
/// Multiplikation och division går via long och avrundar mot minus oändligheten,
/// så resultatet är exakt detsamma på alla processorer.
/// </summary>
public readonly struct Fixed : IEquatable<Fixed>, IComparable<Fixed>
{
    public const int FractionBits = 8;
    public const int One = 1 << FractionBits;

    public readonly int Raw;

    private Fixed(int raw) => Raw = raw;

    public static readonly Fixed Zero = new(0);
    public static readonly Fixed Unit = new(One);

    public static Fixed FromRaw(int raw) => new(raw);
    public static Fixed FromInt(int value) => new(value << FractionBits);

    /// <summary>Bråk som täljare/nämnare, t.ex. 12/10 för 1,2 rutor per sekund.</summary>
    public static Fixed FromRatio(int numerator, int denominator) =>
        new((int)(((long)numerator << FractionBits) / denominator));

    /// <summary>Heltalsdelen, avrundad nedåt.</summary>
    public int Floor => Raw >> FractionBits;

    public static Fixed operator +(Fixed a, Fixed b) => new(a.Raw + b.Raw);
    public static Fixed operator -(Fixed a, Fixed b) => new(a.Raw - b.Raw);
    public static Fixed operator -(Fixed a) => new(-a.Raw);
    public static Fixed operator *(Fixed a, Fixed b) => new((int)(((long)a.Raw * b.Raw) >> FractionBits));
    public static Fixed operator *(Fixed a, int b) => new(a.Raw * b);
    public static Fixed operator /(Fixed a, int b) => new(a.Raw / b);

    public static Fixed operator /(Fixed a, Fixed b)
    {
        if (b.Raw == 0) throw new DivideByZeroException();
        return new((int)(((long)a.Raw << FractionBits) / b.Raw));
    }

    public static bool operator ==(Fixed a, Fixed b) => a.Raw == b.Raw;
    public static bool operator !=(Fixed a, Fixed b) => a.Raw != b.Raw;
    public static bool operator <(Fixed a, Fixed b) => a.Raw < b.Raw;
    public static bool operator >(Fixed a, Fixed b) => a.Raw > b.Raw;
    public static bool operator <=(Fixed a, Fixed b) => a.Raw <= b.Raw;
    public static bool operator >=(Fixed a, Fixed b) => a.Raw >= b.Raw;

    public static Fixed Min(Fixed a, Fixed b) => a.Raw <= b.Raw ? a : b;
    public static Fixed Max(Fixed a, Fixed b) => a.Raw >= b.Raw ? a : b;
    public static Fixed Abs(Fixed a) => a.Raw < 0 ? new(-a.Raw) : a;

    /// <summary>Avståndet sqrt(dx² + dy²), exakt nedåtavrundat.</summary>
    public static Fixed Length(Fixed dx, Fixed dy)
    {
        long sq = (long)dx.Raw * dx.Raw + (long)dy.Raw * dy.Raw;
        return new((int)IntMath.Sqrt(sq));
    }

    public bool Equals(Fixed other) => Raw == other.Raw;
    public override bool Equals(object? obj) => obj is Fixed f && f.Raw == Raw;
    public override int GetHashCode() => Raw;
    public int CompareTo(Fixed other) => Raw.CompareTo(other.Raw);

    /// <summary>Visas som heltal plus 256-delar, t.ex. "3+64/256". Ingen flyttalsformatering.</summary>
    public override string ToString()
    {
        int whole = Raw >> FractionBits;
        int frac = Raw & (One - 1);
        return frac == 0 ? whole.ToString() : $"{whole}+{frac}/{One}";
    }
}

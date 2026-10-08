namespace Hallonkriget.Sim.Determinism;

/// <summary>
/// Kontrollsumma över tillståndet: FNV-1a 64 bitar, matat med ett 32-bitars heltal åt gången
/// (inte en byte åt gången, för att Hash() ska hinna med hela kartan på under en millisekund).
/// Varje del av GameState skriver sina fält hit i en fast ordning.
/// </summary>
public struct StateHasher
{
    private const ulong OffsetBasis = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;

    private ulong _hash;
    private bool _started;

    public ulong Value => _started ? _hash : OffsetBasis;

    public void Add(byte value) => Add((uint)value);

    public void Add(bool value) => Add(value ? (byte)1 : (byte)0);

    public void Add(int value) => Add(unchecked((uint)value));

    public void Add(uint value)
    {
        if (!_started) { _hash = OffsetBasis; _started = true; }
        _hash ^= value;
        _hash *= Prime;
    }

    /// <summary>
    /// En array som mest innehåller nollor (lager per vara): bara platserna som inte är noll, och
    /// sedan längden. Samma innehåll ger samma bidrag, och tomma lager kostar nästan ingenting.
    /// </summary>
    public void AddSparse(int[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] == 0) continue;
            Add(i);
            Add(values[i]);
        }
        Add(values.Length);
    }

    public void AddSparse(bool[] values)
    {
        for (int i = 0; i < values.Length; i++)
            if (values[i]) Add(i);
        Add(values.Length);
    }

    public void Add(long value) => Add(unchecked((ulong)value));

    public void Add(ulong value)
    {
        Add((uint)value);
        Add((uint)(value >> 32));
    }

    public void Add(Fixed value) => Add(value.Raw);
}

namespace Hallonkriget.Sim.Determinism;

/// <summary>
/// Kontrollsumma över tillståndet: FNV-1a 64 bitar, matat med heltal i fast byteordning.
/// Varje del av GameState skriver sina fält hit i en fast ordning.
/// </summary>
public struct StateHasher
{
    private const ulong OffsetBasis = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;

    private ulong _hash;
    private bool _started;

    public ulong Value => _started ? _hash : OffsetBasis;

    public void Add(byte value)
    {
        if (!_started) { _hash = OffsetBasis; _started = true; }
        _hash ^= value;
        _hash *= Prime;
    }

    public void Add(bool value) => Add(value ? (byte)1 : (byte)0);

    public void Add(int value) => Add(unchecked((uint)value));

    public void Add(uint value)
    {
        Add((byte)value);
        Add((byte)(value >> 8));
        Add((byte)(value >> 16));
        Add((byte)(value >> 24));
    }

    public void Add(long value) => Add(unchecked((ulong)value));

    public void Add(ulong value)
    {
        Add((uint)value);
        Add((uint)(value >> 32));
    }

    public void Add(Fixed value) => Add(value.Raw);
}

using Hallonkriget.Sim.Determinism;

namespace Hallonkriget.Sim;

public enum Faction : byte
{
    Torpet = 0,
    Storgarden = 1,
}

/// <summary>En spelare: läger och om det är en människa eller datorn.</summary>
public sealed class Player
{
    public byte Id { get; }
    public Faction Faction { get; }
    public bool IsComputer { get; }

    /// <summary>Hur många av varje vara spelarens byggnader har tillverkat under matchen.</summary>
    public int[] Produced { get; }

    /// <summary>Hur många gånger någon av spelarens personer har gett upp av hunger.</summary>
    public int GaveUp { get; internal set; }

    public Player(byte id, Faction faction, bool isComputer, int goodCount)
    {
        Id = id;
        Faction = faction;
        IsComputer = isComputer;
        Produced = new int[goodCount];
    }

    internal void AddToHash(ref StateHasher h)
    {
        h.Add(Id);
        h.Add((byte)Faction);
        h.Add(IsComputer);
        h.AddSparse(Produced);
        h.Add(GaveUp);
    }
}

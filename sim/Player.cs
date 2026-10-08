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

    public Player(byte id, Faction faction, bool isComputer)
    {
        Id = id;
        Faction = faction;
        IsComputer = isComputer;
    }
}

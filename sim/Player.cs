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

    /// <summary>Hur många av varje mat spelarens folk har ätit på kafferepet. Till Sixtens dagbok.</summary>
    public int[] Eaten { get; }

    /// <summary>Hur många gånger någon av spelarens personer har gett upp av hunger.</summary>
    public int GaveUp { get; internal set; }

    /// <summary>Soldater och djur som har gett upp i fält.</summary>
    public int SoldiersLost { get; internal set; }

    /// <summary>Av dem: djuren (höns och gäss).</summary>
    public int AnimalsLost { get; internal set; }

    /// <summary>Fiendebyggnader spelaren har tagit.</summary>
    public int BuildingsTaken { get; internal set; }

    /// <summary>Alla förråd är tagna och inga soldater finns kvar.</summary>
    public bool Defeated { get; internal set; }
    public int DefeatedAt { get; internal set; }

    /// <summary>Hur många figurer av varje enhet logen har gjort.</summary>
    public int[] Equipped { get; }

    public Player(byte id, Faction faction, bool isComputer, int goodCount, int unitCount = 0)
    {
        Equipped = new int[unitCount];
        Id = id;
        Faction = faction;
        IsComputer = isComputer;
        Produced = new int[goodCount];
        Eaten = new int[goodCount];
    }

    internal void AddToHash(ref StateHasher h)
    {
        h.Add(Id);
        h.Add((byte)Faction);
        h.Add(IsComputer);
        h.AddSparse(Produced);
        h.AddSparse(Eaten);
        h.Add(GaveUp);
        h.Add(SoldiersLost);
        h.Add(AnimalsLost);
        h.Add(BuildingsTaken);
        h.Add(Defeated);
        h.Add(DefeatedAt);
        h.AddSparse(Equipped);
    }
}

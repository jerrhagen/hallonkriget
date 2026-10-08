using Hallonkriget.Sim.Determinism;

namespace Hallonkriget.Sim;

/// <summary>
/// Fas 0: en figur som går mot ett mål och ibland vandrar iväg på egen hand.
/// Den finns för att determinismtestet ska röra fast punkt, slump och kommandon.
/// Ersätts av Person i sim/People under fas 1.
/// </summary>
public sealed class Walker
{
    public int Id { get; }
    public byte Owner { get; }
    public Fixed X { get; internal set; }
    public Fixed Y { get; internal set; }
    public Fixed TargetX { get; internal set; }
    public Fixed TargetY { get; internal set; }
    public bool HasTarget { get; internal set; }

    /// <summary>Rutor per tick.</summary>
    public Fixed Speed { get; }

    internal Walker(int id, byte owner, Fixed x, Fixed y, Fixed speed)
    {
        Id = id;
        Owner = owner;
        X = x;
        Y = y;
        Speed = speed;
    }

    internal void AddToHash(ref StateHasher h)
    {
        h.Add(Id);
        h.Add(Owner);
        h.Add(X);
        h.Add(Y);
        h.Add(TargetX);
        h.Add(TargetY);
        h.Add(HasTarget);
        h.Add(Speed);
    }
}

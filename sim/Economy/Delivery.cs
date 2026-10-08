using Hallonkriget.Sim.Determinism;

namespace Hallonkriget.Sim.Economy;

/// <summary>
/// En leverans: en vara från en byggnad till en annan, med en bärare. Skapas när leveranssystemet
/// parar ihop en begäran med ett erbjudande, och försvinner när varan är framme.
/// </summary>
public sealed class Delivery
{
    public int Id { get; }
    public int Good { get; }
    public int From { get; }
    public int To { get; internal set; }
    public int Carrier { get; }

    /// <summary>Varan är hämtad och på väg till To.</summary>
    public bool PickedUp { get; internal set; }

    internal Delivery(int id, int good, int from, int to, int carrier)
    {
        Id = id;
        Good = good;
        From = from;
        To = to;
        Carrier = carrier;
    }

    internal void AddToHash(ref StateHasher h)
    {
        h.Add(Id);
        h.Add(Good);
        h.Add(From);
        h.Add(To);
        h.Add(Carrier);
        h.Add(PickedUp);
    }
}

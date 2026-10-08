using System.Collections.Generic;
using Hallonkriget.Sim.Buildings;
using Hallonkriget.Sim.Determinism;
using Hallonkriget.Sim.Economy;
using Hallonkriget.Sim.Map;
using Hallonkriget.Sim.People;

namespace Hallonkriget.Sim;

/// <summary>
/// Leveranssystemet, som i designdokumentet: byggnader med plats i inlagret (och byggplatser som
/// saknar material) begär varor, byggnader med något i utlagret erbjuder dem. Var 5:e tick paras
/// de ihop: för varje begäran, närmaste erbjudande och den lediga bärare som står närmast varan.
/// Producenter går före förråd. Det som ingen begär bärs till närmaste förråd med plats.
/// </summary>
public sealed partial class GameState
{
    public const int DeliveryInterval = 5;

    /// <summary>
    /// Ett förråd tar emot överskott av en vara bara upp till en fjärdedel av sin plats (boden 50,
    /// stugan 7). Annars fyller det som görs i överflöd, som vatten, upp förrådet så att inget annat får plats.
    /// Det som någon begär går alltid direkt dit.
    /// </summary>
    public const int SurplusShareDivisor = 4;

    private readonly List<Delivery> _deliveries = new();
    private int _nextDeliveryId;

    public IReadOnlyList<Delivery> Deliveries => _deliveries;

    private void MatchDeliveries()
    {
        if (TickCount % DeliveryInterval != 0) return;
        for (byte owner = 0; owner < _players.Count; owner++)
        {
            if (!MatchRequests(owner)) continue;
            MoveSurplusToStorage(owner);
        }
    }

    /// <summary>
    /// Byggplatser först, i den ordning de placerades: hela materialet till den första innan nästa
    /// får något, annars kan brädorna spridas ut så att inget bygge blir klart (och sågboden aldrig
    /// kommer igång). Sedan färdiga byggnader i omgångar, en vara per byggnad och omgång, så att två
    /// som vill ha samma knappa vara (sågboden och vedboden vill båda ha timmer) delar på den.
    /// Returnerar false när det tog slut på lediga bärare.
    /// </summary>
    private bool MatchRequests(byte owner)
    {
        var goods = new List<int>();
        foreach (var site in _buildings)
        {
            if (site.Owner != owner || site.Stage != BuildingStage.Construction) continue;
            RequestedGoods(site, goods);
            foreach (int good in goods)
            {
                for (int want = Wanted(site, good); want > 0; want--)
                {
                    var from = FindOffer(owner, good, site);
                    if (from is null) break;
                    var carrier = FindIdleCarrier(owner, from);
                    if (carrier is null) return false;
                    StartDelivery(carrier, good, from, site);
                }
            }
        }

        int n = _buildings.Count;
        if (n == 0) return true;
        int first = (TickCount / DeliveryInterval) % n;
        bool progress = true;
        while (progress)
        {
            progress = false;
            for (int k = 0; k < n; k++)
            {
                var to = _buildings[(first + k) % n];
                if (to.Owner != owner || to.Stage != BuildingStage.Done) continue;
                RequestedGoods(to, goods);
                foreach (int good in goods)
                {
                    if (Wanted(to, good) <= 0) continue;
                    var from = FindOffer(owner, good, to);
                    if (from is null) continue;
                    var carrier = FindIdleCarrier(owner, from);
                    if (carrier is null) return false;
                    StartDelivery(carrier, good, from, to);
                    progress = true;
                }
            }
        }
        return true;
    }

    private void MoveSurplusToStorage(byte owner)
    {
        foreach (var from in _buildings)
        {
            if (from.Owner != owner || from.Stage != BuildingStage.Done || from.Def.IsStorage) continue;
            for (int good = 0; good < Data.Goods.Count; good++)
            {
                while (Available(from, good) > 0)
                {
                    var to = FindStorageWithSpace(owner, from.Entrance, except: -1, surplusOf: good);
                    if (to is null) break; // inget förråd vill ha mer av den här varan
                    var carrier = FindIdleCarrier(owner, from);
                    if (carrier is null) return;
                    StartDelivery(carrier, good, from, to);
                }
            }
        }
    }

    private void RequestedGoods(Building b, List<int> goods)
    {
        goods.Clear();
        if (b.Stage == BuildingStage.Construction)
        {
            foreach (var c in b.Def.Cost) goods.Add(c.Good);
            return;
        }
        if (b.Def.IsStorage) return;
        // En byggnad som ingen arbetare har tagit begär inget, så att varorna inte fastnar där.
        if (b.Def.Worker is not null && b.WorkerId < 0) return;
        for (int good = 0; good < Data.Goods.Count; good++)
            if (b.Uses(good)) goods.Add(good);
    }

    private static int Wanted(Building b, int good) =>
        b.Stage == BuildingStage.Construction ? b.MaterialNeeded(good) - b.Incoming[good] : b.RequestSpace(good);

    private static int Available(Building b, int good) =>
        b.Stage == BuildingStage.Done ? b.OutputCount(good) - b.Outgoing[good] : 0;

    private static int StorageFree(Building b)
    {
        int incoming = 0;
        foreach (int c in b.Incoming) incoming += c;
        return b.Def.Storage - b.StoredTotal - incoming;
    }

    /// <summary>Närmaste producent som har varan, annars närmaste förråd som har den.</summary>
    private Building? FindOffer(byte owner, int good, Building to)
    {
        Building? producer = null, storage = null;
        int bestProducer = int.MaxValue, bestStorage = int.MaxValue;
        foreach (var b in _buildings)
        {
            if (b.Owner != owner || b == to || Available(b, good) <= 0) continue;
            int d = Distance(b.Entrance, to.Entrance);
            if (b.Def.IsStorage)
            {
                if (d < bestStorage) (bestStorage, storage) = (d, b);
            }
            else if (d < bestProducer)
            {
                (bestProducer, producer) = (d, b);
            }
        }
        return producer ?? storage;
    }

    private Building? FindStorageWithSpace(byte owner, TilePoint near, int except, int surplusOf = -1)
    {
        Building? best = null;
        int bestDistance = int.MaxValue;
        foreach (var b in _buildings)
        {
            if (b.Owner != owner || b.Id == except || !b.Def.IsStorage || b.Stage != BuildingStage.Done || StorageFree(b) <= 0) continue;
            if (surplusOf >= 0 && b.OutputCount(surplusOf) + b.Incoming[surplusOf] >= b.Def.Storage / SurplusShareDivisor) continue;
            int d = Distance(near, b.Entrance);
            if (d < bestDistance) (bestDistance, best) = (d, b);
        }
        return best;
    }

    private Person? FindIdleCarrier(byte owner, Building near)
    {
        Person? best = null;
        int bestDistance = int.MaxValue;
        foreach (var p in _people)
        {
            if (p.Owner != owner || p.Role != PersonRole.Carrier || p.Job != PersonJob.Idle || p.IsMoving) continue;
            int d = Distance(p.Tile, near.Entrance);
            if (d < bestDistance) (bestDistance, best) = (d, p);
        }
        return best;
    }

    private void StartDelivery(Person carrier, int good, Building from, Building to)
    {
        var d = new Delivery(_nextDeliveryId++, good, from.Id, to.Id, carrier.Id);
        _deliveries.Add(d);
        from.Outgoing[good]++;
        to.Incoming[good]++;
        carrier.Job = PersonJob.ToPickup;
        carrier.Target = d.Id;
        if (!WalkTo(carrier, from.Entrance)) CancelDelivery(d, carrier);
    }

    private Delivery? FindDelivery(int id)
    {
        foreach (var d in _deliveries) if (d.Id == id) return d;
        return null;
    }

    private void CancelDelivery(Delivery d, Person carrier)
    {
        if (!d.PickedUp) _buildings[d.From].Outgoing[d.Good]--;
        _buildings[d.To].Incoming[d.Good]--;
        _deliveries.Remove(d);
        MakeIdle(carrier);
    }

    private void ArriveAtPickup(Person carrier)
    {
        var d = FindDelivery(carrier.Target)!;
        var from = _buildings[d.From];
        if (carrier.Tile != from.Entrance)
        {
            // Vägen blev stängd på vägen dit.
            if (!WalkTo(carrier, from.Entrance)) CancelDelivery(d, carrier);
            return;
        }
        from.Outgoing[d.Good]--;
        if (!from.TakeOutput(d.Good))
        {
            d.PickedUp = true; // reservationen är redan släppt
            CancelDelivery(d, carrier);
            return;
        }
        d.PickedUp = true;
        carrier.Carrying = d.Good;
        carrier.Job = PersonJob.ToDropoff;
        if (!WalkTo(carrier, _buildings[d.To].Entrance)) Redirect(d, carrier);
    }

    private void ArriveAtDropoff(Person carrier)
    {
        var d = FindDelivery(carrier.Target)!;
        var to = _buildings[d.To];
        if (carrier.Tile != to.Entrance)
        {
            if (!WalkTo(carrier, to.Entrance)) Redirect(d, carrier);
            return;
        }
        bool ok = to.Stage == BuildingStage.Construction ? to.DeliverMaterial(d.Good) : to.PutInput(d.Good);
        if (!ok)
        {
            Redirect(d, carrier);
            return;
        }
        to.Incoming[d.Good]--;
        carrier.Carrying = -1;
        _deliveries.Remove(d);
        MakeIdle(carrier);
    }

    /// <summary>Mottagaren tar inte emot varan längre. Bär den till närmaste förråd, eller vänta och försök igen.</summary>
    private void Redirect(Delivery d, Person carrier)
    {
        var storage = FindStorageWithSpace(carrier.Owner, carrier.Tile, except: d.To);
        if (storage is null) return; // står kvar och försöker igen nästa tick
        _buildings[d.To].Incoming[d.Good]--;
        d.To = storage.Id;
        storage.Incoming[d.Good]++;
        WalkTo(carrier, storage.Entrance);
    }

    private void AddDeliveriesToHash(ref StateHasher h)
    {
        h.Add(_nextDeliveryId);
        h.Add(_deliveries.Count);
        foreach (var d in _deliveries) d.AddToHash(ref h);
    }
}

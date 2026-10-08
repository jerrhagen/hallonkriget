using Hallonkriget.Sim.Buildings;
using Hallonkriget.Sim.Data;
using Hallonkriget.Sim.Determinism;
using Hallonkriget.Sim.Map;
using Hallonkriget.Sim.People;

namespace Hallonkriget.Sim;

/// <summary>
/// Kafferasten och kaffedrängen, designdokumentet (Mat och humör). Var tionde minut tar alla soldater
/// rast där de står i en minut. Den som inte har fått kaffe när rasten är slut tappar 20 humör, och
/// gruppen står still en minut till. Kaffet kommer med kaffedrängen, som bär 4 koppar och 4 portioner
/// mat, eller från ett eget kafferep inom 8 rutor. Utanför rasten bjuder drängen hungriga soldater på mat.
/// </summary>
public sealed partial class GameState
{
    /// <summary>Soldater så här nära ett eget kafferep med kaffe på bordet hämtar sin kopp själva.</summary>
    public const int TableReach = 8;

    /// <summary>Pågår kafferasten?</summary>
    public bool IsCoffeeBreak => BreakStarted(TickCount) >= 0;

    /// <summary>Ticket då rasten som pågår började, eller -1.</summary>
    private int BreakStarted(int tick)
    {
        int every = Data.Combat.BreakEveryTicks;
        if (every <= 0 || tick < every) return -1;
        int start = tick - tick % every;
        return tick - start < Data.Combat.BreakTicks ? start : -1;
    }

    /// <summary>Djur, gubben och drängen själv dricker inget kaffe.</summary>
    private bool DrinksCoffee(Person p)
    {
        if (p.Role != PersonRole.Soldier || p.Job != PersonJob.Soldiering || p.Group < 0) return false;
        var u = Data.Units[p.Unit];
        return u.Recruits > 0 && !u.Hero && !u.Server;
    }

    private void CoffeeBreak()
    {
        int every = Data.Combat.BreakEveryTicks;
        if (every <= 0 || TickCount < every) return;
        int phase = TickCount % every;
        if (phase == 0)
        {
            foreach (var p in _people)
            {
                if (!DrinksCoffee(p)) continue;
                p.CoffeeDue = true;
                var g = _groups[p.Group];
                g.StillUntil = IntMath.Max(g.StillUntil, TickCount + Data.Combat.BreakTicks);
            }
        }
        else if (phase < Data.Combat.BreakTicks - 1 && phase % TicksPerSecond == 0)
        {
            foreach (var p in _people)
                if (p.CoffeeDue && NearTableWithCoffee(p) is { } table && table.TakeInput(Data.GoodIndex("kaffe"))) Drink(p);
        }
        else if (phase == Data.Combat.BreakTicks - 1)
        {
            // Sista ticket i rasten, så att gruppen inte hinner ta ett steg innan den sätter sig.
            foreach (var p in _people)
            {
                if (!p.CoffeeDue) continue;
                p.CoffeeDue = false;
                if (p.Group < 0) continue;
                var g = _groups[p.Group];
                g.StillUntil = IntMath.Max(g.StillUntil, TickCount + 1 + Data.Combat.BreakTicks);
                Hurt(p, Data.Combat.NoCoffeeMood, null);
            }
        }
    }

    private Building? NearTableWithCoffee(Person p)
    {
        int coffee = Data.GoodIndex("kaffe");
        foreach (var b in _buildings)
            if (b.Owner == p.Owner && b.Def.Table > 0 && b.Stage == BuildingStage.Done && !b.IsTaken
                && b.InputCount(coffee) > 0 && Chebyshev(b.Entrance, p.Tile) <= TableReach) return b;
        return null;
    }

    private void Drink(Person p)
    {
        p.CoffeeDue = false;
        int coffee = Data.GoodIndex("kaffe");
        int mood = Data.Food(coffee)?.Mood ?? 0;
        p.Mood = IntMath.Min(MaxMood(p), p.Mood + mood);
        _players[p.Owner].Eaten[coffee]++;
    }

    // ---- Kaffedrängen ----

    /// <summary>
    /// Under rasten går drängen till närmaste soldat som väntar på kaffe. Annars till närmaste hungriga
    /// soldat inom 20 rutor. Är brickan tom hämtar han kaffe och den bästa maten i närmaste förråd.
    /// </summary>
    private void UpdateServer(Person p, UnitDef u)
    {
        if (p.StepProgress < p.StepTotal)
        {
            p.StepProgress += EffectiveSpeed(p);
            return;
        }

        // Drängen äter själv ur brickan, eller på kafferepet när den är tom.
        if (Hungry(p))
        {
            if (p.TrayFood > 0)
            {
                Serve(p, p);
                return;
            }
            if (TryGoEat(p)) return;
        }

        Person? guest = null;
        if (p.TrayCoffee > 0) guest = NearestOwnSoldier(p, q => q.CoffeeDue, 40);
        if (guest is null && p.TrayFood > 0) guest = NearestOwnSoldier(p, q => Hungry(q) && q.Job == PersonJob.Soldiering && Data.Units[q.Unit].Recruits > 0, 20);
        if (guest is not null)
        {
            if (Chebyshev(guest.Tile, p.Tile) <= 1)
            {
                StopWalking(p);
                Serve(p, guest);
                return;
            }
            if (PathGoal(p) != guest.Tile) WalkTo(p, guest.Tile);
            FollowPath(p, u);
            return;
        }

        if ((p.TrayCoffee < Data.Combat.Tray || p.TrayFood < Data.Combat.Tray) && NearestStoreWithTray(p) is { } store)
        {
            if (p.Tile == store.Entrance)
            {
                StopWalking(p);
                FillTray(p, store);
                return;
            }
            if (PathGoal(p) != store.Entrance && !WalkTo(p, store.Entrance)) return;
            FollowPath(p, u);
            return;
        }

        var g = _groups[p.Group];
        var slot = SlotFor(g, g.Members.IndexOf(p.Id), p);
        if (p.Tile == slot) StopWalking(p);
        else if (PathGoal(p) == slot || WalkTo(p, slot)) FollowPath(p, u);
    }

    private Person? NearestOwnSoldier(Person p, System.Func<Person, bool> wants, int radius)
    {
        Person? best = null;
        int bestDistance = int.MaxValue;
        foreach (var q in _people)
        {
            if (q.Owner != p.Owner || q.Id == p.Id || q.Role != PersonRole.Soldier || !wants(q)) continue;
            int d = Chebyshev(p.Tile, q.Tile);
            if (d <= radius && d < bestDistance) (bestDistance, best) = (d, q);
        }
        return best;
    }

    private void Serve(Person server, Person guest)
    {
        if (guest.CoffeeDue && server.TrayCoffee > 0 && guest != server)
        {
            server.TrayCoffee--;
            Drink(guest);
            return;
        }
        if (server.TrayFood > 0 && Data.Food(server.TrayFoodGood) is { } food)
        {
            server.TrayFood--;
            guest.Mood = IntMath.Min(MaxMood(guest), guest.Mood + food.Mood);
            _players[guest.Owner].Eaten[food.Good]++;
            if (server.TrayFood == 0) server.TrayFoodGood = -1;
        }
    }

    /// <summary>Närmaste egna förråd som har kaffe eller mat som får plats på brickan.</summary>
    private Building? NearestStoreWithTray(Person p)
    {
        int coffee = Data.GoodIndex("kaffe");
        Building? best = null;
        int bestDistance = int.MaxValue;
        foreach (var b in _buildings)
        {
            if (b.Owner != p.Owner || !b.Def.IsStorage || b.Stage != BuildingStage.Done || b.IsTaken) continue;
            bool useful = (p.TrayCoffee < Data.Combat.Tray && b.OutputCount(coffee) > 0) || (p.TrayFood < Data.Combat.Tray && TrayFoodIn(b, p) >= 0);
            if (!useful) continue;
            int d = Distance(p.Tile, b.Entrance);
            if (d < bestDistance) (bestDistance, best) = (d, b);
        }
        return best;
    }

    /// <summary>Den bästa maten i förrådet, eller den som redan ligger på brickan. Kaffe räknas inte som mat här.</summary>
    private int TrayFoodIn(Building store, Person p)
    {
        if (p.TrayFoodGood >= 0) return store.OutputCount(p.TrayFoodGood) > 0 ? p.TrayFoodGood : -1;
        int coffee = Data.GoodIndex("kaffe");
        foreach (var f in Data.Foods)
            if (f.Good != coffee && f.With < 0 && store.OutputCount(f.Good) > 0) return f.Good;
        return -1;
    }

    private void FillTray(Person p, Building store)
    {
        int coffee = Data.GoodIndex("kaffe");
        while (p.TrayCoffee < Data.Combat.Tray && store.TakeOutput(coffee)) p.TrayCoffee++;
        int food = TrayFoodIn(store, p);
        if (food < 0) return;
        p.TrayFoodGood = food;
        while (p.TrayFood < Data.Combat.Tray && store.TakeOutput(food)) p.TrayFood++;
    }
}

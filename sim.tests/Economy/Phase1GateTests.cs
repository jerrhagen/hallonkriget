using Hallonkriget.Sim.Ai;
using Xunit.Abstractions;

namespace Hallonkriget.Sim.Tests.Economy;

/// <summary>
/// Fas 1:s kontrollfråga, samma körning som <c>dotnet run --project sim.cli</c>: byggordningen
/// data/ai/brodgarden.json på sin karta i 90 minuter. Gården utbildar sitt folk i bygdegården, köper
/// kaffe och verktyg för ved i lanthandeln, och ska de sista 30 minuterna baka knäckebröd i jämn takt
/// utan att någon ger upp av hunger och utan att bärarna fastnar.
/// </summary>
public class Phase1GateTests
{
    private readonly ITestOutputHelper _out;

    public Phase1GateTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void TheBreadFarmBakesSteadilyForThirtyMinutes()
    {
        var order = TestData.Order("brodgarden");
        var map = TestData.Map(order.MapId);
        var state = GameState.NewMatch(MatchSetup.OnMap(1958, map, TestData.Game, (Faction.Torpet, false)));
        var player = new BuildOrderPlayer(order, 0);
        int bread = TestData.Game.GoodIndex("knackebrod");

        for (int t = 0; t < 60 * 600; t++) state.Tick(player.Next(state));
        Assert.True(player.Finished, "byggordningen ska vara klar efter en timme");
        Assert.Empty(player.Problems);

        var windows = new List<int>();
        // En bärare har fastnat om samma leverans pågår länge. Räkna den längsta.
        int gaveUp = state.Players[0].GaveUp, longest = 0;
        var since = new Dictionary<int, (int Delivery, int Tick)>();
        for (int w = 0; w < 6; w++)
        {
            int before = state.Players[0].Produced[bread];
            for (int t = 0; t < 5 * 600; t++)
            {
                state.Tick(player.Next(state));
                foreach (var d in state.Deliveries)
                {
                    if (!since.TryGetValue(d.Carrier, out var s) || s.Delivery != d.Id) since[d.Carrier] = (d.Id, state.TickCount);
                    else longest = Math.Max(longest, state.TickCount - s.Tick);
                }
            }
            windows.Add(state.Players[0].Produced[bread] - before);
        }
        _out.WriteLine($"Knäckebröd per 5 min: {string.Join(", ", windows)}; längsta leverans {longest / 10} s");

        Assert.All(windows, n => Assert.True(n >= 20, $"för lite knäckebröd: {string.Join(", ", windows)}"));
        Assert.True(windows.Min() * 2 >= windows.Max(), $"ojämn takt: {string.Join(", ", windows)}");
        Assert.Equal(gaveUp, state.Players[0].GaveUp);
        Assert.True(longest < 2 * 600, $"en leverans tog {longest / 10} sekunder");
    }
}

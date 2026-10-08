using Hallonkriget.Sim.Commands;
using Hallonkriget.Sim.Map;

namespace Hallonkriget.Sim.Tests.Military;

/// <summary>
/// Ett slag mellan två arméer med alla sorters enheter, två gånger. Kontrollsumman är inskriven så
/// att Windows i CI räknar likadant: avståndsvapnen och salthagelbössan använder slumpgeneratorn.
/// </summary>
public class CombatDeterminismTests
{
    private static ulong Battle()
    {
        var farm = new Farm(width: 60, height: 40);
        string[] torpet = { "rafsbrigaden", "hogaffelgardet", "slangbellepojkarna", "honschocktruppen", "gasskvadronen", "gubben" };
        string[] storgarden = { "rafsbrigaden", "hogaffelgardet", "flaskkavalleriet", "slangbellepojkarna", "traktorn", "gubben" };
        for (int i = 0; i < torpet.Length; i++)
            for (int k = 0; k < (torpet[i] == "gubben" ? 1 : 6); k++)
                farm.State.SpawnSoldier(0, farm.Data.Unit(torpet[i]), new TilePoint(5 + k % 3, 3 + i * 6 + k / 3));
        for (int i = 0; i < storgarden.Length; i++)
            for (int k = 0; k < (storgarden[i] is "gubben" or "traktorn" ? 1 : 6); k++)
                farm.State.SpawnSoldier(1, farm.Data.Unit(storgarden[i]), new TilePoint(52 + k % 3, 3 + i * 6 + k / 3));
        farm.Run(5);
        var commands = new List<Command>();
        foreach (var g in farm.State.Groups)
        {
            var enemy = farm.State.Groups.First(o => o.Owner != g.Owner && !o.IsEmpty);
            commands.Add(new Command(farm.State.TickCount, g.Owner, CommandType.AttackGroup, g.Id, enemy.Id));
        }
        farm.State.Tick(commands);
        farm.Run(3000);
        Assert.True(farm.State.Players[0].SoldiersLost + farm.State.Players[1].SoldiersLost > 20, "slaget ska ha blivit av");
        return farm.State.Hash();
    }

    [Fact]
    public void TheSameBattleTwiceEndsTheSame() => Assert.Equal(Battle(), Battle());

    [Fact]
    public void BattleHashMatchesRecordedValueOnEveryPlatform() => Assert.Equal(GoldenHash, Battle());

    private const ulong GoldenHash = 2393219691382396010UL;
}

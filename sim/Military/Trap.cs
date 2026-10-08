using Hallonkriget.Sim.Map;

namespace Hallonkriget.Sim.Military;

/// <summary>En råttfälla: planerad av spelaren, gillrad av en hantlangare. Osynlig för fienden.</summary>
public sealed class Trap
{
    public byte Owner { get; }
    public TilePoint Tile { get; }
    public bool Armed { get; internal set; }

    internal Trap(byte owner, TilePoint tile)
    {
        Owner = owner;
        Tile = tile;
    }
}

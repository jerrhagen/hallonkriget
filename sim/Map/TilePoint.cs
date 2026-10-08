namespace Hallonkriget.Sim.Map;

/// <summary>En ruta på kartan.</summary>
public readonly record struct TilePoint(int X, int Y)
{
    public override string ToString() => $"({X},{Y})";
}

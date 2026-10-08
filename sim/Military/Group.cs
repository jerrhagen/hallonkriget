using System.Collections.Generic;
using Hallonkriget.Sim.Determinism;
using Hallonkriget.Sim.Map;

namespace Hallonkriget.Sim.Military;

public enum GroupOrder : byte
{
    /// <summary>Står i formation. Fiender inom synhåll anfalls.</summary>
    Idle,

    /// <summary>Marscherar till Anchor och struntar i fiender som inte står intill.</summary>
    Move,

    /// <summary>Anfaller gruppen Target.</summary>
    AttackGroup,

    /// <summary>Går till byggnaden Target och tar den.</summary>
    AttackBuilding,
}

/// <summary>
/// En grupp soldater av samma slag, som i KaM: en formation med rader och kolumner, en riktning
/// och en främsta ruta (Anchor) som formationen ställer upp bakom. Members[0] är ledaren.
/// Id är platsen i GameState.Groups; en tom grupp ligger kvar så att numren inte ändras.
/// </summary>
public sealed class Group
{
    public int Id { get; }
    public byte Owner { get; }
    public int Unit { get; }
    public List<int> Members { get; } = new();

    /// <summary>Hur många som står i bredd.</summary>
    public int Columns { get; internal set; }

    /// <summary>Riktningen, 0 är norr och sedan medurs i åttondelar: 2 öster, 4 söder, 6 väster.</summary>
    public int Facing { get; internal set; }

    /// <summary>Mitten av främsta raden.</summary>
    public TilePoint Anchor { get; internal set; }

    public GroupOrder Order { get; internal set; }

    /// <summary>Gruppen eller byggnaden som anfalls, eller -1.</summary>
    public int Target { get; internal set; } = -1;

    /// <summary>Gruppen står still till det här ticket (kafferasten).</summary>
    public int StillUntil { get; internal set; }

    public bool IsEmpty => Members.Count == 0;

    internal Group(int id, byte owner, int unit, TilePoint anchor, int facing, int columns)
    {
        Id = id;
        Owner = owner;
        Unit = unit;
        Anchor = anchor;
        Facing = facing;
        Columns = columns;
    }

    public static readonly (int X, int Y)[] Directions =
    {
        (0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1),
    };

    /// <summary>Riktningen från a mot b, närmast av de åtta. Samma ruta: oförändrad.</summary>
    public static int DirectionTo(TilePoint a, TilePoint b, int fallback)
    {
        int dx = b.X - a.X, dy = b.Y - a.Y;
        if (dx == 0 && dy == 0) return fallback;
        // Diagonalt om den kortare sidan är minst hälften av den längre.
        int sx = IntMath.Abs(dx) * 2 >= IntMath.Abs(dy) ? IntMath.Sign(dx) : 0;
        int sy = IntMath.Abs(dy) * 2 >= IntMath.Abs(dx) ? IntMath.Sign(dy) : 0;
        for (int i = 0; i < Directions.Length; i++)
            if (Directions[i] == (sx, sy)) return i;
        return fallback;
    }

    /// <summary>Platsen i formationen för medlem nummer slot: rad för rad bakåt, mitten först i bredd.</summary>
    public TilePoint SlotTile(int slot)
    {
        int cols = IntMath.Max(1, IntMath.Min(Columns, Members.Count));
        int row = slot / cols, col = slot % cols;
        int offset = col - (cols - 1) / 2;
        var (fx, fy) = Directions[Facing];
        int rx = -fy, ry = fx;
        return new TilePoint(Anchor.X + offset * rx - row * fx, Anchor.Y + offset * ry - row * fy);
    }

    internal void AddToHash(ref StateHasher h)
    {
        h.Add(Id);
        h.Add(Owner);
        h.Add(Unit);
        h.Add(Members.Count);
        foreach (int m in Members) h.Add(m);
        h.Add(Columns);
        h.Add(Facing);
        h.Add(Anchor.X);
        h.Add(Anchor.Y);
        h.Add((byte)Order);
        h.Add(Target);
        h.Add(StillUntil);
    }
}

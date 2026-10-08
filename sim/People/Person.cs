using System.Collections.Generic;
using Hallonkriget.Sim.Determinism;
using Hallonkriget.Sim.Map;

namespace Hallonkriget.Sim.People;

public enum PersonRole : byte
{
    /// <summary>Bär en vara i taget mellan byggnaderna.</summary>
    Carrier,

    /// <summary>Hantlangare: bygger hus och trampar upp stigar. Bär inte.</summary>
    Laborer,

    /// <summary>Arbetar i en byggnad. Yrket står i Profession och ska stämma med byggnadens "worker".</summary>
    Worker,
}

public enum PersonJob : byte
{
    Idle,
    ToPickup,
    ToDropoff,
    ToSite,
    Building,
    ToPathTile,
    Treading,
    ToWorkplace,
    AtWork,
}

/// <summary>
/// En person på kartan. Rör sig ruta för ruta längs en väg från Pathfinder. Tile är rutan personen
/// står på eller är på väg in i; From är rutan den kom från, så att vyn kan rita den mittemellan.
/// </summary>
public sealed class Person
{
    public int Id { get; }
    public byte Owner { get; }
    public PersonRole Role { get; }
    public string Profession { get; }

    /// <summary>Kostnadsenheter × 10 per tick. 48 är 1,2 rutor per sekund på upptrampad stig.</summary>
    public int Speed { get; }

    public TilePoint Tile { get; internal set; }
    public TilePoint From { get; internal set; }

    /// <summary>Hur långt in på steget från From till Tile, av StepTotal. Kan gå över StepTotal när steget är klart.</summary>
    public int StepProgress { get; internal set; }
    public int StepTotal { get; internal set; }

    public PersonJob Job { get; internal set; }

    /// <summary>Jobbets mål: leveransens, byggets eller arbetsplatsens id. -1 när inget.</summary>
    public int Target { get; internal set; } = -1;

    /// <summary>Stigrutan en hantlangare trampar.</summary>
    public TilePoint TargetTile { get; internal set; }

    public int Timer { get; internal set; }

    /// <summary>Varan personen bär, eller -1.</summary>
    public int Carrying { get; internal set; } = -1;

    /// <summary>Hur länge personen väntat på en full stigruta.</summary>
    public int WaitTicks { get; internal set; }

    /// <summary>Inne i en byggnad: syns inte och tar ingen plats på stigen.</summary>
    public bool Inside { get; internal set; }

    internal readonly List<TilePoint> Path = new();
    internal int PathIndex;

    internal Person(int id, byte owner, PersonRole role, string profession, TilePoint tile, int speed)
    {
        Id = id;
        Owner = owner;
        Role = role;
        Profession = profession;
        Tile = tile;
        From = tile;
        Speed = speed;
    }

    public bool IsMoving => PathIndex < Path.Count - 1 || StepProgress < StepTotal;

    internal void AddToHash(ref StateHasher h)
    {
        h.Add(Id);
        h.Add(Owner);
        h.Add((byte)Role);
        h.Add(Profession.Length);
        foreach (char c in Profession) h.Add((int)c);
        h.Add(Speed);
        h.Add(Tile.X);
        h.Add(Tile.Y);
        h.Add(From.X);
        h.Add(From.Y);
        h.Add(StepProgress);
        h.Add(StepTotal);
        h.Add((byte)Job);
        h.Add(Target);
        h.Add(TargetTile.X);
        h.Add(TargetTile.Y);
        h.Add(Timer);
        h.Add(Carrying);
        h.Add(WaitTicks);
        h.Add(Inside);
        h.Add(PathIndex);
        h.Add(Path.Count);
        foreach (var p in Path)
        {
            h.Add(p.X);
            h.Add(p.Y);
        }
    }
}

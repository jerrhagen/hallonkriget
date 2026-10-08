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

    /// <summary>Går till logen och väntar där på utrustning.</summary>
    Recruit,

    /// <summary>Soldat, eller djur i strid. Enheten står i Unit.</summary>
    Soldier,
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

    /// <summary>På väg till kafferepet för att äta.</summary>
    ToEat,

    /// <summary>Har gett upp och går hem till stugan.</summary>
    ToHome,

    /// <summary>Ligger hemma i stugan och vilar.</summary>
    Resting,

    /// <summary>Rekryten väntar inne i logen.</summary>
    InBarracks,

    /// <summary>Soldaten är i fält med sin grupp.</summary>
    Soldiering,
}

/// <summary>
/// En person på kartan. Rör sig ruta för ruta längs en väg från Pathfinder. Tile är rutan personen
/// står på eller är på väg in i; From är rutan den kom från, så att vyn kan rita den mittemellan.
/// </summary>
public sealed class Person
{
    public int Id { get; }
    public byte Owner { get; }
    public PersonRole Role { get; internal set; }
    public string Profession { get; }

    /// <summary>Kostnadsenheter × 10 per tick. 48 är 1,2 rutor per sekund på upptrampad stig.</summary>
    public int Speed { get; internal set; }

    /// <summary>Soldatens enhet (nummer i units.json), eller -1.</summary>
    public int Unit { get; internal set; } = -1;

    /// <summary>Gruppen soldaten hör till, eller -1.</summary>
    public int Group { get; internal set; } = -1;

    /// <summary>Skott kvar för avståndsvapen.</summary>
    public int Shots { get; internal set; }

    /// <summary>Tick kvar till nästa slag eller skott.</summary>
    public int StrikeTimer { get; internal set; }

    /// <summary>Tick kvar av striden: så länge tappar soldaten humör dubbelt så fort. Noll utanför strid.</summary>
    public int CombatTicks { get; internal set; }

    /// <summary>Sammanstötningar soldaten har överlevt. Efter tre får hen medaljen.</summary>
    public int Clashes { get; internal set; }

    /// <summary>Korken på snöret: +1 i anfall.</summary>
    public bool Medal { get; internal set; }

    /// <summary>Bränsle kvar i tick (traktorn).</summary>
    public int Fuel { get; internal set; }

    /// <summary>Fienden soldaten slåss mot just nu: personens id, eller -1.</summary>
    public int Foe { get; internal set; } = -1;

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

    /// <summary>Humöret, 0–100. Ersätter både hunger och hälsa.</summary>
    public int Mood { get; internal set; }

    /// <summary>Tick kvar tills humöret sjunker en enhet.</summary>
    public int MoodTimer { get; internal set; }

    /// <summary>Tick kvar av bonusarna från maten: fart (pannkakor), arbete (svagdricka) och anfall (sylt).</summary>
    public int SpeedBonusTicks { get; internal set; }
    public int WorkBonusTicks { get; internal set; }
    public int AttackBonusTicks { get; internal set; }

    internal readonly List<TilePoint> Path = new();
    internal int PathIndex;

    internal Person(int id, byte owner, PersonRole role, string profession, TilePoint tile, int speed, int mood, int moodTimer)
    {
        Mood = mood;
        MoodTimer = moodTimer;
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
        h.Add(Mood);
        h.Add(MoodTimer);
        h.Add(SpeedBonusTicks);
        h.Add(WorkBonusTicks);
        h.Add(AttackBonusTicks);
        h.Add(Unit);
        h.Add(Group);
        h.Add(Shots);
        h.Add(StrikeTimer);
        h.Add(CombatTicks);
        h.Add(Clashes);
        h.Add(Medal);
        h.Add(Fuel);
        h.Add(Foe);
        h.Add(PathIndex);
        h.Add(Path.Count);
        foreach (var p in Path)
        {
            h.Add(p.X);
            h.Add(p.Y);
        }
    }
}

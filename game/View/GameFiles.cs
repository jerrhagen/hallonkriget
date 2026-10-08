using System.IO;
using Godot;
using Hallonkriget.Sim.Ai;
using Hallonkriget.Sim.Data;
using Hallonkriget.Sim.Map;

namespace Hallonkriget.Game.View;

/// <summary>
/// Speldatan i data/ och sparfilerna. Från editorn ligger data/ bredvid game/; i ett exporterat
/// spel ligger den bredvid programfilen.
/// </summary>
public static class GameFiles
{
    public static string DataDir
    {
        get
        {
            string[] candidates =
            {
                Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "data"),
                Path.Combine(Path.GetDirectoryName(OS.GetExecutablePath()) ?? ".", "data"),
            };
            foreach (var dir in candidates)
                if (File.Exists(Path.Combine(dir, "goods.json"))) return Path.GetFullPath(dir);
            throw new DirectoryNotFoundException("Hittar inte speldatan (data/goods.json)");
        }
    }

    public static GameData LoadData()
    {
        string dir = DataDir;
        return GameData.FromFiles(name => File.ReadAllText(Path.Combine(dir, name)));
    }

    public static MapDef LoadMap(string id) => MapDef.Parse(File.ReadAllText(Path.Combine(DataDir, "maps", id + ".json")));

    public static BuildOrder LoadBuildOrder(string id, GameData data) =>
        BuildOrder.Parse(File.ReadAllText(Path.Combine(DataDir, "ai", id + ".json")), data);

    /// <summary>Sparfilerna ligger i användarens Godot-mapp, user://spara/.</summary>
    public static string SaveDir
    {
        get
        {
            var dir = ProjectSettings.GlobalizePath("user://spara");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}

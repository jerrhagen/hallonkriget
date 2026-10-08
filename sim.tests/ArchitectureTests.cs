using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Hallonkriget.Sim.Tests;

/// <summary>
/// Kontrollerar determinismreglerna i CLAUDE.md: inga Godot-beroenden, inga flyttal,
/// ingen System.Random, ingen klocka, inga trådar i sim/.
/// </summary>
public class ArchitectureTests
{
    private static readonly Assembly SimAssembly = typeof(GameState).Assembly;

    [Fact]
    public void SimProjectHasNoReferences()
    {
        var csproj = XDocument.Load(Path.Combine(RepoRoot(), "sim", "Hallonkriget.Sim.csproj"));
        var refs = csproj.Descendants()
            .Where(e => e.Name.LocalName is "PackageReference" or "ProjectReference" or "Reference" or "FrameworkReference")
            .Select(e => e.Attribute("Include")?.Value)
            .ToList();
        Assert.Empty(refs);
    }

    [Fact]
    public void SimAssemblyDoesNotReferenceGodot()
    {
        var names = SimAssembly.GetReferencedAssemblies().Select(a => a.Name ?? "").ToList();
        Assert.DoesNotContain(names, n => n.StartsWith("Godot", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NoFloatingPointInSimTypes()
    {
        var floating = new[] { typeof(float), typeof(double), typeof(decimal) };
        var found = new List<string>();
        const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                 BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        foreach (var type in SimAssembly.GetTypes())
        {
            foreach (var f in type.GetFields(all))
                if (floating.Contains(f.FieldType)) found.Add($"{type.Name}.{f.Name}");
            foreach (var m in type.GetMethods(all))
            {
                if (floating.Contains(m.ReturnType)) found.Add($"{type.Name}.{m.Name}()");
                foreach (var p in m.GetParameters())
                    if (floating.Contains(p.ParameterType)) found.Add($"{type.Name}.{m.Name}({p.Name})");
            }
        }
        Assert.Empty(found);
    }

    /// <summary>
    /// Ord som inte får förekomma i koden under sim/ (kommentarer och strängar räknas inte).
    /// En rad kan undantas med kommentaren "determinism-ok:" följt av ett skäl,
    /// till exempel en Dictionary som bara används för uppslag och aldrig itereras.
    /// </summary>
    private static readonly (string Pattern, string Why)[] Forbidden =
    {
        (@"\bfloat\b", "flyttal"),
        (@"\bdouble\b", "flyttal"),
        (@"\bdecimal\b", "flyttal"),
        (@"\bSingle\b", "flyttal"),
        (@"\bDouble\b", "flyttal"),
        (@"\bDecimal\b", "flyttal"),
        (@"\bMathF?\.", "Math räknar med flyttal, använd IntMath"),
        (@"\bRandom\b", "System.Random, använd Rng"),
        (@"\bDateTime(Offset)?\b", "klocka"),
        (@"\bStopwatch\b", "klocka"),
        (@"\bEnvironment\.TickCount", "klocka"),
        (@"\bThread(ing|Pool)?\b", "trådar"),
        (@"\bTask\b", "trådar"),
        (@"\bParallel\b", "trådar"),
        (@"\basync\b", "trådar"),
        (@"\bawait\b", "trådar"),
        (@"\bDictionary\b", "oordnad iteration, använd listor med fasta id"),
        (@"\bHashSet\b", "oordnad iteration, använd listor med fasta id"),
        (@"\bGodot\b", "Godot får inte användas i sim/"),
    };

    [Fact]
    public void SimSourceHasNoForbiddenConstructs()
    {
        var simDir = Path.Combine(RepoRoot(), "sim");
        var files = Directory.GetFiles(simDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                        !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .ToList();
        Assert.NotEmpty(files);

        var problems = new List<string>();
        foreach (var file in files)
        {
            var lines = File.ReadAllLines(file);
            var code = StripCommentsAndStrings(string.Join("\n", lines)).Split('\n');
            for (int i = 0; i < code.Length; i++)
            {
                if (lines[i].Contains("determinism-ok:")) continue;
                foreach (var (pattern, why) in Forbidden)
                    if (Regex.IsMatch(code[i], pattern))
                        problems.Add($"{Path.GetRelativePath(simDir, file)}:{i + 1}: {why}: {lines[i].Trim()}");
            }
        }
        Assert.True(problems.Count == 0, "Förbjudet i sim/:\n" + string.Join("\n", problems));
    }

    [Fact]
    public void ScannerCatchesForbiddenWords()
    {
        // Testar skannern själv, så att den inte tyst slutar fungera.
        var code = StripCommentsAndStrings("var x = 1.5f; // double\nstring s = \"float\"; double y;").Split('\n');
        Assert.DoesNotMatch(@"\bdouble\b", code[0]);
        Assert.DoesNotMatch(@"\bfloat\b", code[1]);
        Assert.Matches(@"\bdouble\b", code[1]);
    }

    /// <summary>Ersätter kommentarer och strängar med blanksteg men behåller radbrytningar.</summary>
    private static string StripCommentsAndStrings(string src)
    {
        var sb = new System.Text.StringBuilder(src.Length);
        int i = 0;
        while (i < src.Length)
        {
            if (src[i] == '/' && i + 1 < src.Length && src[i + 1] == '/')
            {
                while (i < src.Length && src[i] != '\n') { sb.Append(' '); i++; }
            }
            else if (src[i] == '/' && i + 1 < src.Length && src[i + 1] == '*')
            {
                while (i < src.Length && !(src[i] == '*' && i + 1 < src.Length && src[i + 1] == '/'))
                { sb.Append(src[i] == '\n' ? '\n' : ' '); i++; }
                sb.Append("  "); i += 2;
            }
            else if (src[i] == '"' || src[i] == '\'')
            {
                char q = src[i];
                sb.Append(' '); i++;
                while (i < src.Length && src[i] != q && src[i] != '\n')
                {
                    if (src[i] == '\\') { sb.Append(' '); i++; }
                    sb.Append(' '); i++;
                }
                sb.Append(' '); i++;
            }
            else { sb.Append(src[i]); i++; }
        }
        return sb.ToString();
    }

    internal static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Hallonkriget.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Hittar inte repots rot");
    }
}

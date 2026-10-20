using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

// AGENTS.md rules 1, 4 and 6, checked against the repo's own files on every `dotnet test shared/core-dotnet`: locally,
// in Definition of Done step 3, and in the `core` job of .github/workflows/guard.yml. So a commit that breaks one fails
// for everyone, agent or human, hook or no hook. tools/guard/check.mjs scans the text (R1, R4, R7); these tests add what
// a text scan cannot see: the assembly references that decide what app code can compile against at all.
//
// Scope: apps/<app>/Assets and shared/packages. Imported third-party folders (TextMesh Pro, Package Manager Samples)
// are skipped, as in check.mjs R7.
public class ArchitectureRuleTests
{
    const string InputAsmdef = "shared/packages/com.gardenvr.input/Runtime/GardenVR.Input.asmdef";
    const string CoreAsmdef = "shared/packages/com.gardenvr.core/Runtime/GardenVR.Core.asmdef";
    const string CorePackage = "shared/packages/com.gardenvr.core/package.json";
    const string Provider = "shared/packages/com.gardenvr.input/Runtime/Providers/";
    const string ProviderSource = Provider + "KeyboardMouseIntentSource.cs";
    // The Input System package's own asmdef GUID; Unity writes a reference this way when "Use GUIDs" is on.
    const string InputSystemGuid = "GUID:75469ad4d38634e559750d17036d5f7c";

    static readonly string Root = FindRoot();
    static readonly JsonDocumentOptions Lenient = new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

    // Device reads that must stay inside the keyboard/mouse provider (rule 1). Wider than check.mjs R1: every Input
    // System device, its device queries, and the legacy touch, scroll and sensor reads.
    static readonly Regex DeviceRead = new Regex(
        @"\b(Keyboard|Mouse|Gamepad|Joystick|Touchscreen|Pointer|Pen|Accelerometer|Gyroscope)\.current\b" +
        @"|\bInputSystem\.(GetDevice\w*|devices|onEvent|onAnyButtonPress)\b" +
        @"|(?<![\w.])Input\.(GetKey\w*|GetMouse\w*|GetAxis\w*|GetButton\w*|GetTouch|touch\w*|mousePosition|mouseScrollDelta|anyKey\w*|inputString|acceleration|gyro|compass)\b",
        RegexOptions.CultureInvariant);

    // Rule 6: no medical claims. The vocabulary the PlayMode narration tests already ban (BoxBreathingPlaybackTests,
    // SundialAudioTests), matched as whole words inside string literals only, so "secure" or "diagnostics" never trip it.
    static readonly Regex Medical = new Regex(
        @"\b(therap\w*|medical\w*|anxiety|anxious|cures?|cured|diagnos(e|es|ed|is)|treatments?|clinical\w*|insomnia|heal|heals|healed|healing)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    static readonly Regex Literal = new Regex(@"""(?:[^""\\]|\\.)*""", RegexOptions.CultureInvariant);

    [Fact]
    public void Only_the_input_package_compiles_against_the_input_system()
    {
        // Rule 1, by construction: an assembly that does not reference Unity.InputSystem cannot compile Keyboard.current.
        // Test assemblies are exempt, for the Input System's own test fixture.
        var asmdefs = AppAssets("*.asmdef").Concat(Packages("*.asmdef")).ToList();
        Assert.Contains(InputAsmdef, asmdefs);
        Assert.True(ReferencesInputSystem(Asmdef(InputAsmdef)), "the check must see the input package's own reference, or it checks nothing");

        var offenders = asmdefs
            .Where(p => !string.Equals(p, InputAsmdef, StringComparison.Ordinal) && !IsTest(p))
            .Where(p => ReferencesInputSystem(Asmdef(p)))
            .ToList();
        Assert.True(offenders.Count == 0,
            "only GardenVR.Input may reference Unity.InputSystem; go through com.gardenvr.input intents (AGENTS rule 1): " + string.Join(", ", offenders));
    }

    [Fact]
    public void App_runtime_code_never_falls_into_the_default_assembly()
    {
        // A script outside every asmdef compiles into Assembly-CSharp, which references every package, Input System
        // included, so the reference check above would not hold for it. Editor folders are editor-only code.
        var sources = AppAssets("*.cs").Where(p => !IsEditor(p)).ToList();
        Assert.NotEmpty(sources);

        var loose = sources.Where(p => !HasAssemblyDefinition(p)).ToList();
        Assert.True(loose.Count == 0,
            "app runtime code must sit under an .asmdef (apps/<app>/Assets/Scripts or Tests), not in Assembly-CSharp: " + string.Join(", ", loose));
    }

    [Fact]
    public void Core_assembly_stays_an_engine_free_leaf()
    {
        // Rule 4. noEngineReferences keeps the engine out; an empty reference list keeps it from arriving through
        // another assembly or a precompiled DLL; an empty package dependency list keeps it from arriving as a package.
        var core = Asmdef(CoreAsmdef);
        Assert.True(core.TryGetProperty("noEngineReferences", out var flag) && flag.ValueKind == JsonValueKind.True,
            "GardenVR.Core.asmdef must keep \"noEngineReferences\": true (AGENTS rule 4)");
        Assert.True(Strings(core, "references").Count == 0,
            "GardenVR.Core.asmdef must reference no other assembly (AGENTS rule 4): " + string.Join(", ", Strings(core, "references")));
        Assert.True(Strings(core, "precompiledReferences").Count == 0,
            "GardenVR.Core.asmdef must reference no precompiled DLL (AGENTS rule 4): " + string.Join(", ", Strings(core, "precompiledReferences")));

        using var package = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, CorePackage)), Lenient);
        bool noDependencies = !package.RootElement.TryGetProperty("dependencies", out var deps)
            || (deps.ValueKind == JsonValueKind.Object && !deps.EnumerateObject().Any());
        Assert.True(noDependencies, "com.gardenvr.core must declare no package dependencies (AGENTS rule 4)");
    }

    [Fact]
    public void No_device_read_outside_the_keyboard_mouse_provider()
    {
        Assert.Contains(Lines(ProviderSource), l => DeviceRead.IsMatch(l.Text));

        var hits = AppAssets("*.cs").Concat(Packages("*.cs"))
            .Where(p => !p.StartsWith(Provider, StringComparison.Ordinal))
            .SelectMany(Lines)
            .Where(l => !IsComment(l.Text) && DeviceRead.IsMatch(l.Text))
            .Select(l => l.Where)
            .ToList();
        Assert.True(hits.Count == 0,
            "raw device read outside " + Provider + "; go through com.gardenvr.input intents (AGENTS rule 1): " + string.Join(", ", hits));
    }

    [Fact]
    public void No_medical_claim_in_user_facing_text()
    {
        // Strings in app and package runtime code (tests skipped, as in check.mjs R7) and the narration and cue files.
        var code = AppAssets("*.cs").Where(p => !IsTest(p))
            .Concat(Packages("*.cs").Where(p => p.Split('/').Length > 4 && p.Split('/')[3] == "Runtime"));
        var narration = AppAssets("*.json").Where(p => p.Split('/').Contains("Audio"));
        var sources = code.Concat(narration).ToList();
        Assert.NotEmpty(sources);

        // The coach's refusal list and its prompt rules name the words the coach must never say (CoachGuards.MedicalStems,
        // "never give medical ... advice"). They are the check itself, not user-facing text, so these two files are out.
        string[] refusalSources =
        {
            "shared/packages/com.gardenvr.core/Runtime/Coach/CoachGuards.cs",
            "shared/packages/com.gardenvr.core/Runtime/Coach/CoachPrompts.cs"
        };
        var hits = sources.Where(p => !refusalSources.Contains(p)).SelectMany(Lines)
            .Where(l => MedicalClaim(l.Text)).Select(l => l.Where).ToList();
        Assert.True(hits.Count == 0, "medical or therapeutic wording in user-facing text (AGENTS rule 6): " + string.Join(", ", hits));
    }

    [Theory]
    [InlineData("label.text = \"A breathing therapy for the evening\";", true)]
    [InlineData("Say(\"Nothing here cures anything\");", true)]
    [InlineData("Say(\"ok\", \"eases anxiety\");", true)]
    [InlineData("\"line\": \"a clinical calm\",", true)]
    [InlineData("Say(\"Keep the jar secure\");", false)]
    [InlineData("Log(\"diagnostics written\");", false)]
    [InlineData("var therapyCount = 0;", false)]
    [InlineData("// no therapy claims in a comment", false)]
    public void The_medical_word_check_reads_string_literals_only(string line, bool flagged)
    {
        Assert.Equal(flagged, MedicalClaim(line));
    }

    [Theory]
    [InlineData("var k = Keyboard.current;", true)]
    [InlineData("var g = Gamepad.current.leftStick.ReadValue();", true)]
    [InlineData("var t = Touchscreen.current;", true)]
    [InlineData("var d = InputSystem.GetDevice<Mouse>();", true)]
    [InlineData("if (Input.touchCount > 0) Tend();", true)]
    [InlineData("var s = Input.mouseScrollDelta;", true)]
    [InlineData("using GardenVR.Input;", false)]
    [InlineData("var s = GardenVR.Input.HandIntent.Pinch;", false)]
    [InlineData("var c = Camera.current;", false)]
    public void The_device_read_check_matches_raw_reads_only(string line, bool flagged)
    {
        Assert.Equal(flagged, DeviceRead.IsMatch(line));
    }

    static bool MedicalClaim(string line) =>
        !IsComment(line) && Literal.Matches(line).Any(m => Medical.IsMatch(m.Value));

    static bool ReferencesInputSystem(JsonElement asmdef) =>
        Strings(asmdef, "references").Any(r => r.StartsWith("Unity.InputSystem", StringComparison.Ordinal)
            || string.Equals(r, InputSystemGuid, StringComparison.OrdinalIgnoreCase))
        || Strings(asmdef, "precompiledReferences").Any(r => r.StartsWith("Unity.InputSystem", StringComparison.Ordinal));

    // True when the file's folder, or one above it up to apps/<app>/Assets, holds an .asmdef or .asmref.
    static bool HasAssemblyDefinition(string rel)
    {
        string[] parts = rel.Split('/');
        for (int end = parts.Length - 1; end >= 3; end--)
        {
            string dir = Path.Combine(Root, string.Join("/", parts.Take(end)));
            if (Directory.EnumerateFiles(dir, "*.asmdef").Any() || Directory.EnumerateFiles(dir, "*.asmref").Any())
                return true;
        }
        return false;
    }

    static bool IsTest(string p) => p.Split('/').Contains("Tests");
    static bool IsEditor(string p) => p.Split('/').Contains("Editor");
    static bool Skipped(string p) =>
        p.Contains("/TextMesh Pro/", StringComparison.Ordinal) || p.Contains("/Samples/", StringComparison.Ordinal);

    static bool IsComment(string line)
    {
        string t = line.TrimStart();
        return t.StartsWith("//", StringComparison.Ordinal) || t.StartsWith("/*", StringComparison.Ordinal) || t.StartsWith("*", StringComparison.Ordinal);
    }

    // Repo-relative paths with forward slashes, under each apps/<app>/Assets (never Library/ or Temp/).
    static IEnumerable<string> AppAssets(string pattern) =>
        Directory.GetDirectories(Path.Combine(Root, "apps"))
            .Select(app => Path.Combine(app, "Assets"))
            .Where(Directory.Exists)
            .SelectMany(assets => Directory.EnumerateFiles(assets, pattern, SearchOption.AllDirectories))
            .Select(Rel)
            .Where(p => !Skipped(p))
            .OrderBy(p => p, StringComparer.Ordinal);

    static IEnumerable<string> Packages(string pattern) =>
        Directory.EnumerateFiles(Path.Combine(Root, "shared", "packages"), pattern, SearchOption.AllDirectories)
            .Select(Rel)
            .Where(p => !Skipped(p))
            .OrderBy(p => p, StringComparer.Ordinal);

    static IEnumerable<(string Text, string Where)> Lines(string rel) =>
        File.ReadAllLines(Path.Combine(Root, rel)).Select((text, i) => (text, rel + ":" + (i + 1)));

    static string Rel(string full) => Path.GetRelativePath(Root, full).Replace('\\', '/');

    static JsonElement Asmdef(string rel)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, rel)), Lenient);
        return doc.RootElement.Clone();
    }

    static List<string> Strings(JsonElement obj, string name)
    {
        var list = new List<string>();
        if (obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var array) && array.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in array.EnumerateArray())
            {
                if (e.ValueKind == JsonValueKind.String)
                    list.Add(e.GetString());
            }
        }
        return list;
    }

    // The checkout root: the first folder above the test binaries that holds AGENTS.md and shared/packages.
    static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !(File.Exists(Path.Combine(dir.FullName, "AGENTS.md")) && Directory.Exists(Path.Combine(dir.FullName, "shared", "packages"))))
            dir = dir.Parent;
        if (dir == null)
            throw new InvalidOperationException("ArchitectureRuleTests: no checkout root (AGENTS.md and shared/packages) above " + AppContext.BaseDirectory);
        return dir.FullName;
    }
}

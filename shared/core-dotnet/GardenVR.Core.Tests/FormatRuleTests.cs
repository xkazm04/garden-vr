using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

// The C# layout rules of the repo-root .editorconfig, checked on every C# file in the repo on every
// `dotnet test shared/core-dotnet`: locally, in Definition of Done step 3, and in the `core` job of
// .github/workflows/guard.yml. So agent, host and human code that drifts in layout fails for everyone, hook or no hook.
//
// The analyzers' IDE0055 sees only the core build, and stays a warning until it is measured; Unity compiles the apps
// and reads none of it. This test holds the part of the format a line can show, everywhere a .cs file lives: spaces
// for indentation, no trailing whitespace, a final newline, Allman braces, `else`/`catch`/`finally` on their own line,
// brace lines on the 4-space grid, and a space between a control-flow keyword and its parenthesis. The single-line
// blocks and statements that .editorconfig preserves (`try { Kill(); } catch (Exception) { }`) stay allowed, and so
// does an array initializer opened after `=` (com.gardenvr.capture BitmapFont.Data).
//
// Scope: apps/<app>/Assets, shared/packages and shared/core-dotnet (not bin/ or obj/). Imported third-party folders
// (TextMesh Pro, Package Manager Samples) are skipped, as in ArchitectureRuleTests.
public class FormatRuleTests
{
    const string Self = "shared/core-dotnet/GardenVR.Core.Tests/FormatRuleTests.cs";

    static readonly string Root = FindRoot();

    // String and char literals, removed before a code-shape rule reads the line, so "if(x) {" in a string never trips it.
    static readonly Regex Literal = new Regex(
        @"@""(?:[^""]|"""")*""|""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)+'",
        RegexOptions.CultureInvariant);

    static readonly Regex KeywordParen = new Regex(
        @"(?<![\w.@#])(if|for|foreach|while|switch|catch|using|lock|fixed)\(",
        RegexOptions.CultureInvariant);

    static readonly Regex CloseThenContinue = new Regex(@"^\}\s*(else|catch|finally)\b", RegexOptions.CultureInvariant);

    // The [*.cs] settings this test mirrors. Changing one in .editorconfig means changing the rule here with it.
    static readonly (string Key, string Value)[] Mirrored =
    {
        ("indent_style", "space"),
        ("indent_size", "4"),
        ("trim_trailing_whitespace", "true"),
        ("insert_final_newline", "true"),
        ("csharp_new_line_before_open_brace", "all"),
        ("csharp_new_line_before_else", "true"),
        ("csharp_new_line_before_catch", "true"),
        ("csharp_new_line_before_finally", "true"),
        ("csharp_preserve_single_line_blocks", "true"),
        ("csharp_preserve_single_line_statements", "true"),
        ("csharp_space_after_keywords_in_control_flow_statements", "true"),
    };

    [Fact]
    public void Every_csharp_file_keeps_the_editorconfig_whitespace()
    {
        var sources = Sources().ToList();
        AssertScope(sources);

        var hits = new List<string>();
        foreach (string rel in sources)
        {
            string text = File.ReadAllText(Path.Combine(Root, rel));
            if (text.Length > 0 && text[text.Length - 1] != '\n')
                hits.Add(rel + " final-newline");
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                foreach (string rule in LineFindings(lines[i]).Where(IsWhitespaceRule))
                    hits.Add(rel + ":" + (i + 1) + " " + rule);
            }
        }
        Assert.True(hits.Count == 0, Report("C# whitespace drifts from .editorconfig ([*.cs]: spaces, no trailing whitespace, final newline)", hits));
    }

    [Fact]
    public void Every_csharp_file_keeps_allman_braces_and_keyword_spacing()
    {
        var sources = Sources().ToList();
        AssertScope(sources);

        var hits = new List<string>();
        foreach (string rel in sources)
        {
            string[] lines = File.ReadAllText(Path.Combine(Root, rel)).Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                foreach (string rule in LineFindings(lines[i]).Where(r => !IsWhitespaceRule(r)))
                    hits.Add(rel + ":" + (i + 1) + " " + rule);
            }
        }
        Assert.True(hits.Count == 0, Report("C# layout drifts from .editorconfig ([*.cs]: Allman braces, keyword spacing, 4-space brace lines)", hits));
    }

    [Fact]
    public void The_editorconfig_still_declares_the_rules_this_test_holds()
    {
        var settings = CsharpSettings(File.ReadAllText(Path.Combine(Root, ".editorconfig")));
        var drift = Mirrored
            .Where(m => !settings.TryGetValue(m.Key, out string v) || !string.Equals(v, m.Value, StringComparison.OrdinalIgnoreCase))
            .Select(m => m.Key + " = " + m.Value)
            .ToList();
        Assert.True(drift.Count == 0,
            ".editorconfig [*.cs] no longer says what FormatRuleTests checks; change the test and the setting together: " + string.Join(", ", drift));
    }

    [Theory]
    [InlineData("    var x = 1;", "")]
    [InlineData("\tvar x = 1;", "tab-indent")]
    [InlineData("    var x = 1; ", "trailing-whitespace")]
    [InlineData("    // a comment\t", "trailing-whitespace")]
    [InlineData("    if (ready) {", "brace-same-line")]
    [InlineData("    public void Run() {", "brace-same-line")]
    [InlineData("    xs.ForEach(x => {", "brace-same-line")]
    [InlineData("    static readonly byte[] Data = {", "")]
    [InlineData("    {", "")]
    [InlineData("      }", "brace-indent")]
    [InlineData("    } else", "close-brace-continues")]
    [InlineData("    } catch (Exception)", "close-brace-continues")]
    [InlineData("    try { Kill(); } catch (Exception) { }", "")]
    [InlineData("    if (a) { Run(); }", "")]
    [InlineData("    if(ready) Run();", "keyword-paren")]
    [InlineData("    foreach(var x in xs)", "keyword-paren")]
    [InlineData("    var t = typeof(Foo); Lock(x); obj.lock(y);", "")]
    [InlineData("    Log(\"if(x) {\");", "")]
    [InlineData("    var path = @\"C:\\dir\\\"; if (a) Run();", "")]
    [InlineData("    var c = '{';", "")]
    [InlineData("    if (a) Run(); // done {", "")]
    [InlineData("    // if(x) {", "")]
    public void The_line_rules_flag_drift_only(string line, string expected)
    {
        Assert.Equal(expected, string.Join(",", LineFindings(line)));
    }

    // The rules one line can break. Whitespace rules read every line; code-shape rules skip comment lines and read the
    // line with its literals and trailing comment removed.
    static IEnumerable<string> LineFindings(string line)
    {
        string indent = line.Substring(0, line.Length - line.TrimStart(' ', '\t').Length);
        if (indent.Contains('\t'))
            yield return "tab-indent";
        if (line.Length > 0 && (line[line.Length - 1] == ' ' || line[line.Length - 1] == '\t'))
            yield return "trailing-whitespace";

        string trimmed = line.Trim();
        if ((trimmed == "{" || trimmed == "}") && !indent.Contains('\t') && indent.Length % 4 != 0)
            yield return "brace-indent";
        if (IsComment(line))
            yield break;

        string code = Code(line).Trim();
        if (code.Length > 1 && code[code.Length - 1] == '{')
        {
            string before = code.Substring(0, code.Length - 1).TrimEnd();
            if (before.Length > 0 && before[before.Length - 1] != '=')
                yield return "brace-same-line";
        }
        if (CloseThenContinue.IsMatch(code))
            yield return "close-brace-continues";
        if (KeywordParen.IsMatch(code))
            yield return "keyword-paren";
    }

    static bool IsWhitespaceRule(string rule) => rule == "tab-indent" || rule == "trailing-whitespace";

    // The line with string and char literals emptied and a trailing // comment cut off.
    static string Code(string line)
    {
        string bare = Literal.Replace(line, "\"\"");
        int comment = bare.IndexOf("//", StringComparison.Ordinal);
        return comment >= 0 ? bare.Substring(0, comment) : bare;
    }

    static bool IsComment(string line)
    {
        string t = line.TrimStart();
        return t.StartsWith("//", StringComparison.Ordinal) || t.StartsWith("/*", StringComparison.Ordinal) || t.StartsWith("*", StringComparison.Ordinal);
    }

    // The settings of the [*.cs] section, last one wins, as editorconfig reads them.
    static Dictionary<string, string> CsharpSettings(string editorconfig)
    {
        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        bool inCs = false;
        foreach (string raw in editorconfig.Replace("\r\n", "\n").Split('\n'))
        {
            string l = raw.Trim();
            if (l.StartsWith("[", StringComparison.Ordinal))
            {
                inCs = string.Equals(l, "[*.cs]", StringComparison.Ordinal);
                continue;
            }
            int eq = l.IndexOf('=');
            if (!inCs || eq <= 0 || l.StartsWith("#", StringComparison.Ordinal) || l.StartsWith(";", StringComparison.Ordinal))
                continue;
            settings[l.Substring(0, eq).Trim()] = l.Substring(eq + 1).Trim();
        }
        return settings;
    }

    // The scan must reach the core tests, a shared package and both apps, or it checks less than it claims.
    static void AssertScope(List<string> sources)
    {
        Assert.Contains(Self, sources);
        Assert.Contains(sources, p => p.StartsWith("shared/packages/", StringComparison.Ordinal));
        Assert.Contains(sources, p => p.StartsWith("apps/terrarium/Assets/", StringComparison.Ordinal));
        Assert.Contains(sources, p => p.StartsWith("apps/sundial/Assets/", StringComparison.Ordinal));
    }

    static string Report(string what, List<string> hits) =>
        what + "; fix the layout by hand or with `dotnet format whitespace`, " + hits.Count + " hit(s): "
        + string.Join(", ", hits.Take(40)) + (hits.Count > 40 ? ", ..." : "");

    // Repo-relative paths with forward slashes: every .cs under apps/<app>/Assets, shared/packages and
    // shared/core-dotnet, never a build folder or an imported third-party folder.
    static IEnumerable<string> Sources()
    {
        var roots = Directory.GetDirectories(Path.Combine(Root, "apps"))
            .Select(app => Path.Combine(app, "Assets"))
            .Append(Path.Combine(Root, "shared", "packages"))
            .Append(Path.Combine(Root, "shared", "core-dotnet"))
            .Where(Directory.Exists);
        return roots
            .SelectMany(dir => Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            .Select(Rel)
            .Where(p => !Skipped(p))
            .OrderBy(p => p, StringComparer.Ordinal);
    }

    static bool Skipped(string p)
    {
        string[] parts = p.Split('/');
        return parts.Contains("bin") || parts.Contains("obj") || parts.Contains("TextMesh Pro") || parts.Contains("Samples");
    }

    static string Rel(string full) => Path.GetRelativePath(Root, full).Replace('\\', '/');

    // The checkout root: the first folder above the test binaries that holds AGENTS.md and shared/packages.
    static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !(File.Exists(Path.Combine(dir.FullName, "AGENTS.md")) && Directory.Exists(Path.Combine(dir.FullName, "shared", "packages"))))
            dir = dir.Parent;
        if (dir == null)
            throw new InvalidOperationException("FormatRuleTests: no checkout root (AGENTS.md and shared/packages) above " + AppContext.BaseDirectory);
        return dir.FullName;
    }
}

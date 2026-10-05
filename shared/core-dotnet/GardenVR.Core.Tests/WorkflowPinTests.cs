using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

// Every action a GitHub workflow runs is pinned to a full commit SHA, checked on every `dotnet test shared/core-dotnet`:
// locally, in Definition of Done step 3, and in the `core` job of .github/workflows/guard.yml. A tag such as `@v4` can be
// moved upstream, and the job that runs it checks out the repo with a token, so a moved tag runs foreign code here.
//
// A pin is `owner/repo[/path]@<40 lowercase hex>` followed by a `# vX.Y.Z` comment, which Dependabot
// (.github/dependabot.yml, github-actions) keeps in step when it bumps the SHA. A local action (`./...`) needs no pin; a
// container (`docker://`) needs an `@sha256:` digest.
//
// Ratchet: TagPinned lists the tag refs that were in the workflows when this rule went in, as "file: action". They stay
// until someone who can resolve each tag to its SHA pins them; guard.yml is a gate file, which is the owner's to edit.
// A new tag ref fails, and an entry that is pinned or gone fails as stale, so the list only shrinks.
public class WorkflowPinTests
{
    static readonly string[] TagPinned =
    {
        "claude-hook.yml: actions/checkout",
        "claude-hook.yml: actions/setup-node",
        "guard.yml: actions/checkout",
        "guard.yml: actions/setup-dotnet",
        "guard.yml: actions/setup-node",
        "merge-gate.yml: actions/checkout",
        "merge-gate.yml: actions/setup-node",
    };

    static readonly string Root = FindRoot();

    // A step's `uses:` key, as a list item or not, with an optional quoted value and trailing comment.
    static readonly Regex Uses = new Regex(
        @"^\s*(?:-\s+)?uses:\s*[""']?(?<ref>[^\s""'#]+)[""']?\s*(?:#\s*(?<note>.*))?$",
        RegexOptions.CultureInvariant);

    static readonly Regex ShaPin = new Regex(@"^[\w.-]+/[\w.-]+(?:/[\w./-]+)?@[0-9a-f]{40}$", RegexOptions.CultureInvariant);
    static readonly Regex VersionNote = new Regex(@"^v\d+(?:\.\d+)*\b", RegexOptions.CultureInvariant);
    static readonly Regex ImageDigest = new Regex(@"^docker://[^@\s]+@sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant);

    [Fact]
    public void Every_action_is_pinned_to_a_commit_sha()
    {
        var refs = WorkflowRefs().ToList();
        Assert.Contains(refs, r => r.File == "guard.yml" && r.Action == "actions/checkout");

        var hits = refs
            .Where(r => r.Finding.Length > 0)
            .Where(r => !(r.Finding == "tag-pinned" && TagPinned.Contains(r.File + ": " + r.Action, StringComparer.Ordinal)))
            .Select(r => r.Where + " " + r.Finding)
            .ToList();
        Assert.True(hits.Count == 0,
            "a workflow runs an action from a movable ref; pin it as owner/repo@<full commit sha> # vX.Y.Z: " + string.Join(", ", hits));
    }

    [Fact]
    public void The_tag_pinned_list_only_shrinks()
    {
        var tagged = new HashSet<string>(
            WorkflowRefs().Where(r => r.Finding == "tag-pinned").Select(r => r.File + ": " + r.Action),
            StringComparer.Ordinal);
        var stale = TagPinned.Where(t => !tagged.Contains(t)).ToList();
        Assert.True(stale.Count == 0,
            "TagPinned names an action that is now pinned or gone; remove it from WorkflowPinTests: " + string.Join(", ", stale));
    }

    [Theory]
    [InlineData("      - uses: actions/checkout@v4", "tag-pinned")]
    [InlineData("      - uses: actions/checkout@main", "tag-pinned")]
    [InlineData("      - uses: 'actions/setup-node@v4'", "tag-pinned")]
    [InlineData("      - uses: actions/checkout@11BD71901BBE5B1630CEEA73D27597364C9AF683 # v4.2.2", "tag-pinned")]
    [InlineData("      - uses: actions/checkout@11bd71901bbe5b1630ceea73d27597364c9af683 # v4.2.2", "")]
    [InlineData("        uses: \"actions/checkout@11bd71901bbe5b1630ceea73d27597364c9af683\" # v4", "")]
    [InlineData("      - uses: github/codeql-action/init@11bd71901bbe5b1630ceea73d27597364c9af683 # v3.26.0", "")]
    [InlineData("      - uses: actions/checkout@11bd71901bbe5b1630ceea73d27597364c9af683", "no-version-comment")]
    [InlineData("      - uses: actions/checkout@11bd71901bbe5b1630ceea73d27597364c9af683 # latest", "no-version-comment")]
    [InlineData("      - uses: ./.github/actions/setup", "")]
    [InlineData("      - uses: docker://alpine:3.20", "unpinned-image")]
    [InlineData("      - uses: docker://alpine@sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", "")]
    [InlineData("      - run: echo uses: actions/checkout@v4", "-")]
    [InlineData("      # - uses: actions/checkout@v4", "-")]
    public void The_pin_rule_reads_one_line(string line, string expected)
    {
        Assert.Equal(expected, LineFinding(line) ?? "-");
    }

    // The finding for one workflow line: null when it is not a `uses:` step, "" when the ref is pinned.
    static string LineFinding(string line)
    {
        if (line.TrimStart().StartsWith("#", StringComparison.Ordinal))
            return null;
        var m = Uses.Match(line);
        if (!m.Success)
            return null;
        string reference = m.Groups["ref"].Value;
        string note = m.Groups["note"].Success ? m.Groups["note"].Value.Trim() : "";
        if (reference.StartsWith("./", StringComparison.Ordinal))
            return "";
        if (reference.StartsWith("docker://", StringComparison.Ordinal))
            return ImageDigest.IsMatch(reference) ? "" : "unpinned-image";
        if (!ShaPin.IsMatch(reference))
            return "tag-pinned";
        return VersionNote.IsMatch(note) ? "" : "no-version-comment";
    }

    static IEnumerable<(string File, string Action, string Finding, string Where)> WorkflowRefs()
    {
        string dir = Path.Combine(Root, ".github", "workflows");
        var files = Directory.EnumerateFiles(dir)
            .Where(f => f.EndsWith(".yml", StringComparison.Ordinal) || f.EndsWith(".yaml", StringComparison.Ordinal))
            .OrderBy(f => f, StringComparer.Ordinal);
        foreach (string full in files)
        {
            string name = Path.GetFileName(full);
            string[] lines = File.ReadAllText(full).Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string finding = LineFinding(lines[i]);
                if (finding == null)
                    continue;
                string reference = Uses.Match(lines[i]).Groups["ref"].Value;
                int at = reference.IndexOf('@', StringComparison.Ordinal);
                string action = at >= 0 ? reference.Substring(0, at) : reference;
                yield return (name, action, finding, ".github/workflows/" + name + ":" + (i + 1));
            }
        }
    }

    // The checkout root: the first folder above the test binaries that holds AGENTS.md and shared/packages.
    static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !(File.Exists(Path.Combine(dir.FullName, "AGENTS.md")) && Directory.Exists(Path.Combine(dir.FullName, "shared", "packages"))))
            dir = dir.Parent;
        if (dir == null)
            throw new InvalidOperationException("WorkflowPinTests: no checkout root (AGENTS.md and shared/packages) above " + AppContext.BaseDirectory);
        return dir.FullName;
    }
}

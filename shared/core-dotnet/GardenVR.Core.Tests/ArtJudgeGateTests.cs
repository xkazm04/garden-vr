using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

// The art judge's trust bar (docs/decisions/0004-art-judge-gate-criteria.md), checked on the committed calibration
// records and ladders on every `dotnet test shared/core-dotnet`: locally, in Definition of Done step 3, and in the `core`
// job of .github/workflows/guard.yml. The judge itself runs only on the host (tools/fidelity/lab is untracked), so these
// tests hold what a clone can see:
//
// - A calibration record may say "advisory": false only when it carries the owner-agreement evidence the decision
//   names: at least kappa_min_labels owner labels and kappa at the packet's floor, a 95% lower bound, raw agreement,
//   labels per frame and the inversion rate. Until the lab writes those fields, no record can leave advisory.
// - Every image the judge and the ladders read is either in the checkout or on the HostOnly list below. That list is
//   the gap that keeps the judge from re-running in a clone; it may only shrink, so a new host-only input fails.
public class ArtJudgeGateTests
{
    const string JudgeDir = "tools/fidelity/judge/";
    const string LadderDir = "tools/fidelity/ladders/";
    const string Controls = "tools/fidelity/judge/controls.json";

    // The bar beyond the packet's own kappa floor and label minimum (decision 0004).
    const int MinLabelsPerFrame = 12;
    const double MinKappaCi95Low = 0.4;
    const double MinAgreement = 0.8;
    const double MaxInversionRate = 0.1;

    // Folders .gitignore keeps out of every clone. An input under one of them exists only on the host.
    static readonly string[] Untracked =
    {
        "orchestration/",
        "tools/fidelity/lab/",
        "docs/contest/",
        "docs/research/",
    };

    // The host-only judge inputs on 2026-10-05: the two locked gate frames and the six re-renders the ladders take
    // their noise band from. Decision 0004 proposes tracking them by sha256; remove an entry when its file is tracked.
    static readonly string[] HostOnly =
    {
        "orchestration/runs/sundial/T-SUN-031/dial-g1-nohalo.png",
        "orchestration/runs/sundial/T-SUN-040/rerenders/dial-1.png",
        "orchestration/runs/sundial/T-SUN-040/rerenders/dial-2.png",
        "orchestration/runs/sundial/T-SUN-040/rerenders/dial-3.png",
        "orchestration/runs/sundial/T-SUN-040/rerenders/jar-1.png",
        "orchestration/runs/sundial/T-SUN-040/rerenders/jar-2.png",
        "orchestration/runs/sundial/T-SUN-040/rerenders/jar-3.png",
        "orchestration/runs/terrarium/T-TER-032/jar-g1.png",
    };

    static readonly string Root = FindRoot();
    static readonly JsonDocumentOptions Lenient = new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

    [Fact]
    public void Every_calibration_record_stays_advisory_until_owner_agreement()
    {
        var records = CalibrationRecords().ToList();
        Assert.NotEmpty(records);

        var hits = new List<string>();
        foreach (string rel in records)
        {
            var record = Json(rel);
            string packet = record.TryGetProperty("packet", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
            string manifest = JudgeDir + packet + "/manifest.json";
            if (packet == null || !File.Exists(Path.Combine(Root, manifest)))
            {
                hits.Add(rel + " names no packet with a manifest.json");
                continue;
            }
            var calibration = Json(manifest).GetProperty("calibration");
            int minLabels = calibration.GetProperty("kappa_min_labels").GetInt32();
            double floor = calibration.GetProperty("kappa_floor").GetDouble();
            hits.AddRange(CalibrationFindings(record, minLabels, floor).Select(f => rel + " " + f));
        }
        Assert.True(hits.Count == 0,
            "an art judge calibration claims more trust than its owner labels show (docs/decisions/0004-art-judge-gate-criteria.md): " + string.Join("; ", hits));
    }

    [Fact]
    public void Every_judge_input_is_in_the_checkout_or_listed_host_only()
    {
        var inputs = JudgeInputs();
        Assert.Contains("shared/assets/art-reference/A1-03-night-moss-1.png", inputs);
        Assert.Contains("tools/fidelity/rungs/DialG1-control-blur-dial.png", inputs);

        var hits = new List<string>();
        foreach (string p in inputs)
        {
            if (IsUntracked(p))
            {
                if (!HostOnly.Contains(p, StringComparer.Ordinal))
                    hits.Add(p + " is a new host-only input; track it, or the judge reads one more file a clone cannot");
            }
            else if (!File.Exists(Path.Combine(Root, p)))
            {
                hits.Add(p + " is missing from the checkout");
            }
        }
        Assert.True(hits.Count == 0, "the art judge reads files a clone does not have: " + string.Join("; ", hits));
    }

    [Fact]
    public void The_host_only_list_only_shrinks()
    {
        var inputs = JudgeInputs();
        var stale = HostOnly
            .Where(p => !inputs.Contains(p) || !IsUntracked(p))
            .ToList();
        Assert.True(stale.Count == 0,
            "HostOnly lists a path the judge no longer reads from an untracked folder; remove it from ArtJudgeGateTests: " + string.Join(", ", stale));
    }

    const string Gated =
        "{'advisory': false, 'gate_pass': true," +
        " 'frames': {'JarG1': {'inversion': {'rate': 0.0}}, 'DialG1': {'inversion': {'rate': 0.05}}}," +
        " 'kappa': {'n': 30, 'kappa': 0.7, 'pass': true, 'ci95_low': 0.45, 'agreement': 0.85, 'per_frame': {'JarG1': 15, 'DialG1': 15}}}";

    [Theory]
    [InlineData("{'advisory': true, 'advisory_reason': 'need 30 owner labels, have 0', 'gate_pass': true, 'kappa': {'n': 0, 'kappa': null, 'pass': false}}", "")]
    [InlineData(Gated, "")]
    [InlineData("{'gate_pass': true, 'kappa': {'n': 0}}", "advisory-flag")]
    [InlineData("{'advisory': true, 'gate_pass': true, 'kappa': {'n': 0}}", "advisory-reason")]
    [InlineData("{'advisory': false, 'gate_pass': true, 'kappa': {'n': 0, 'kappa': null, 'pass': false}}", "labels,kappa,kappa-pass,ci95-low,agreement,per-frame")]
    [InlineData("{'advisory': false, 'gate_pass': false, 'frames': {}, 'kappa': {'n': 30, 'kappa': 0.7, 'pass': true, 'ci95_low': 0.45, 'agreement': 0.85, 'per_frame': {}}}", "gate-pass")]
    public void The_calibration_rule_holds_the_decision_bar(string json, string expected)
    {
        using var doc = JsonDocument.Parse(json.Replace('\'', '"'));
        Assert.Equal(expected, string.Join(",", CalibrationFindings(doc.RootElement, 30, 0.6).Select(Code)));
    }

    [Theory]
    [InlineData("'ci95_low': 0.45", "'ci95_low': 0.3", "ci95-low")]
    [InlineData("'agreement': 0.85", "'agreement': 0.7", "agreement")]
    [InlineData("'n': 30", "'n': 29", "labels")]
    [InlineData("'kappa': 0.7", "'kappa': 0.5", "kappa")]
    [InlineData("'per_frame': {'JarG1': 15, 'DialG1': 15}", "'per_frame': {'JarG1': 18, 'DialG1': 8}", "per-frame")]
    [InlineData("'per_frame': {'JarG1': 15, 'DialG1': 15}", "'per_frame': {'JarG1': 30}", "per-frame")]
    [InlineData("'rate': 0.05", "'rate': 0.2", "inversion")]
    public void One_missing_piece_of_evidence_keeps_a_record_advisory(string from, string to, string expected)
    {
        using var doc = JsonDocument.Parse(Gated.Replace(from, to).Replace('\'', '"'));
        Assert.Equal(expected, string.Join(",", CalibrationFindings(doc.RootElement, 30, 0.6).Select(Code)));
    }

    // Findings as "code: message". An advisory record needs only its flag and a reason; a gating one needs every piece
    // of evidence decision 0004 names, each read from the record itself.
    static List<string> CalibrationFindings(JsonElement record, int minLabels, double floor)
    {
        var found = new List<string>();
        if (!record.TryGetProperty("advisory", out var flag) || (flag.ValueKind != JsonValueKind.True && flag.ValueKind != JsonValueKind.False))
        {
            found.Add("advisory-flag: no \"advisory\": true or false");
            return found;
        }
        if (flag.ValueKind == JsonValueKind.True)
        {
            if (!(record.TryGetProperty("advisory_reason", out var why) && why.ValueKind == JsonValueKind.String && why.GetString().Trim().Length > 0))
                found.Add("advisory-reason: an advisory record says why it is advisory");
            return found;
        }

        var kappa = Child(record, "kappa");
        if (!AtLeast(Number(kappa, "n"), minLabels))
            found.Add("labels: fewer owner labels than the packet's kappa_min_labels");
        if (!AtLeast(Number(kappa, "kappa"), floor))
            found.Add("kappa: missing or below the packet's kappa_floor");
        if (!IsTrue(kappa, "pass"))
            found.Add("kappa-pass: kappa.pass is not true");
        if (!IsTrue(record, "gate_pass"))
            found.Add("gate-pass: the control gate did not pass");
        if (!AtLeast(Number(kappa, "ci95_low"), MinKappaCi95Low))
            found.Add("ci95-low: kappa.ci95_low is missing or below the decision's bound");
        if (!AtLeast(Number(kappa, "agreement"), MinAgreement))
            found.Add("agreement: kappa.agreement (raw owner agreement) is missing or below the decision's bound");

        var frames = Child(record, "frames");
        var names = frames.ValueKind == JsonValueKind.Object ? frames.EnumerateObject().Select(f => f.Name).ToList() : new List<string>();
        var perFrame = Child(kappa, "per_frame");
        if (perFrame.ValueKind != JsonValueKind.Object || names.Any(n => !AtLeast(Number(perFrame, n), MinLabelsPerFrame)))
            found.Add("per-frame: kappa.per_frame must give every frame its own minimum of owner labels");
        if (names.Any(n => !AtLeast(MaxInversionRate, Number(Child(Child(frames, n), "inversion"), "rate"))))
            found.Add("inversion: a frame's inversion rate is missing or above the decision's bound");
        return found;
    }

    // False for a missing value (NaN), so absent evidence never passes a bound.
    static bool AtLeast(double value, double bound) => value >= bound;

    static string Code(string finding) => finding.Substring(0, finding.IndexOf(':', StringComparison.Ordinal));

    static JsonElement Child(JsonElement obj, string name) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var v) ? v : default;

    // A missing or non-numeric value reads as NaN, which AtLeast never accepts on either side.
    static double Number(JsonElement obj, string name)
    {
        var v = Child(obj, name);
        return v.ValueKind == JsonValueKind.Number ? v.GetDouble() : double.NaN;
    }

    static bool IsTrue(JsonElement obj, string name) => Child(obj, name).ValueKind == JsonValueKind.True;

    static bool IsUntracked(string p) => Untracked.Any(u => p.StartsWith(u, StringComparison.Ordinal));

    static IEnumerable<string> CalibrationRecords() =>
        Directory.EnumerateFiles(Path.Combine(Root, "tools", "fidelity", "judge"))
            .Select(Rel)
            .Where(p => p.StartsWith(JudgeDir + "calibration-", StringComparison.Ordinal) && p.EndsWith(".json", StringComparison.Ordinal))
            .OrderBy(p => p, StringComparer.Ordinal);

    // Every "path" value in the calibration records, the ladders and the control list.
    static HashSet<string> JudgeInputs()
    {
        var sources = CalibrationRecords()
            .Concat(Directory.EnumerateFiles(Path.Combine(Root, "tools", "fidelity", "ladders")).Select(Rel)
                .Where(p => p.StartsWith(LadderDir, StringComparison.Ordinal) && p.EndsWith(".json", StringComparison.Ordinal)))
            .Append(Controls);
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (string rel in sources)
            CollectPaths(Json(rel), paths);
        return paths;
    }

    static void CollectPaths(JsonElement e, HashSet<string> into)
    {
        if (e.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in e.EnumerateObject())
            {
                if (prop.Name == "path" && prop.Value.ValueKind == JsonValueKind.String)
                    into.Add(prop.Value.GetString().Replace('\\', '/'));
                CollectPaths(prop.Value, into);
            }
        }
        else if (e.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in e.EnumerateArray())
                CollectPaths(item, into);
        }
    }

    static JsonElement Json(string rel)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, rel)), Lenient);
        return doc.RootElement.Clone();
    }

    static string Rel(string full) => Path.GetRelativePath(Root, full).Replace('\\', '/');

    // The checkout root: the first folder above the test binaries that holds AGENTS.md and shared/packages.
    static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !(File.Exists(Path.Combine(dir.FullName, "AGENTS.md")) && Directory.Exists(Path.Combine(dir.FullName, "shared", "packages"))))
            dir = dir.Parent;
        if (dir == null)
            throw new InvalidOperationException("ArtJudgeGateTests: no checkout root (AGENTS.md and shared/packages) above " + AppContext.BaseDirectory);
        return dir.FullName;
    }
}

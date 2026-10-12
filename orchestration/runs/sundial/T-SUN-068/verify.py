"""Check content-free evidence and summarize the Windows player's CLI sample."""
import csv
import json
import math
import statistics
from pathlib import Path
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parent
rows = list(csv.DictReader((root / "timings.csv").open()))
assert len(rows) == 20
assert [int(r["sample"]) for r in rows] == list(range(1, 21))
assert all(r["backend"] == "cli" for r in rows)
assert sum(r["kind"] == "Onboarding" for r in rows) == 10
assert sum(r["kind"] == "Reflection" for r in rows) == 10
health = json.loads((root / "health.json").read_text(encoding="utf-8-sig"))
assert health == {"ok": True, "backend": "cli"}
entries = [json.loads(line) for line in (root / "relay.log").read_text(encoding="utf-8-sig").splitlines()]
assert set(entries[0]) == {"listening", "backend", "model", "systems"}
assert entries[0]["backend"] == "cli"
allowed = {"t", "kind", "status", "failure", "ms", "in_tokens", "out_tokens", "upstream"}
assert len(entries) == 23, f"expected startup + two UI calls + 20 samples, got {len(entries)}"
for entry in entries[1:]:
    assert set(entry) <= allowed
    assert entry["kind"] in {"Onboarding", "Reflection"}
    assert entry["failure"] in {None, "Error", "Refused", "RateLimited", "Rejected"}
    if "upstream" in entry:
        assert entry["upstream"] in {"cli-timeout", "cli-error", "error", "max-tokens", "empty"}
    assert isinstance(entry["ms"], (int, float))
assert not (root / "relay-stderr.log").read_text().strip()
(root / "relay-lines.jsonl").write_text("\n".join(json.dumps(e) for e in entries) + "\n")
player = (root / "player.log").read_text(encoding="utf-8-sig")
lines = [line for line in player.splitlines() if line.startswith("[RelayLive]")]
assert any("live_onboarding from_model=true packet_text_matches=true" in x for x in lines)
assert any("live_reflection from_model=true card_text_matches=true journal_matches=true" in x for x in lines)
assert lines[-1] == "[RelayLive] complete samples=20 backend=cli"
assert len(lines) == 24
assert not any("Exception:" in line for line in player.splitlines())
(root / "player-excerpts.txt").write_text("\n".join(lines) + "\n")

def stats(data):
    values = sorted(float(r["elapsed_ms"]) for r in data)
    return statistics.median(values), values[math.ceil(.95 * len(values)) - 1]

table = ["# Windows player latency, backend=cli", "",
         "20 sequential calls, 10 per kind; two preceding live UI calls excluded. One install key, starts at least 11 seconds apart.",
         "Elapsed time covers SundialCoach/CoachFlow, UnityWebRequest, guards, and the player's completion observation (frame granularity). Budget: 15 seconds.",
         "p50 is the median (average of the middle pair); p95 uses nearest rank. Includes every call, including fallbacks.", "",
         "| Kind | n | Model replies | p50 ms | p95 ms |", "|---|---:|---:|---:|---:|"]
for kind in ("All", "Onboarding", "Reflection"):
    selected = rows if kind == "All" else [r for r in rows if r["kind"] == kind]
    p50, p95 = stats(selected)
    table.append(f"| {kind} | {len(selected)} | {sum(r['from_model'] == 'true' for r in selected)} | {p50:.3f} | {p95:.3f} |")
table += ["", "| Sample | Kind | Player ms | Relay ms | From model | Failure |", "|---:|---|---:|---:|---|---|"]
for row, entry in zip(rows, entries[3:]):
    assert row["kind"] == entry["kind"]
    table.append(f"| {row['sample']} | {row['kind']} | {row['elapsed_ms']} | {entry['ms']} | {row['from_model']} | {row['outcome']} |")
(root / "timing-table.md").write_text("\n".join(table) + "\n")
summary = []
for name in ("editmode", "playmode-initial", "playmode-recheck"):
    result = ET.parse(root / f"{name}.xml").getroot()
    assert int(result.attrib["total"]) > 0
    assert result.attrib["failed"] == ("1" if name == "playmode-initial" else "0")
    summary.append(name + " " + " ".join(f"{k}={result.attrib[k]}" for k in ("result", "total", "passed", "failed", "skipped")))
summary += ["PASS relay log schema: no prompt, reply, transcript, or install-key fields; stderr empty",
            "PASS live model proposals match rendered packets; live model line matches rendered card and journal",
            "PASS backend=cli, budget=15000ms, 20 timed player calls, 10 per kind", "p50_ms=%.3f p95_ms=%.3f" % stats(rows)]
(root / "verification.txt").write_text("\n".join(summary) + "\n")
print("\n".join(summary))

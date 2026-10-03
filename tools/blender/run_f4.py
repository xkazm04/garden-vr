"""Reproduce the F4 checks and the moss-repaint variant.

    python tools/blender/run_f4.py

Runs, in order: the headless probe, finish_generated --self-test, the checker
projection, the JarG1 passes, a Nano Banana repaint of each pass (tools/agy/image.sh),
the Canny gate, then project_bake. The moss material is not reassigned.

Pass --skip-repaint to stop after the checker. Pass --skip-probe or --skip-finish
when those artifacts are already in the out directory and you are iterating.
"""
from __future__ import annotations

import json
import os
import shutil
import subprocess
import sys
import uuid

import numpy as np
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
BLENDER = r"C:\Program Files\Blender Foundation\Blender 4.2\blender.exe"
GIT_BASH = r"C:\Program Files\Git\bin\bash.exe"
HERE = os.path.dirname(os.path.abspath(__file__))
MACRO = os.path.join(ROOT, "apps", "terrarium", "Assets", "Art", "Textures", "moss_macro.png")
STYLE = os.path.join(ROOT, "apps", "terrarium", "Assets", "Art", "Source", "moss_macro.png")

PROMPT = (
    "Repaint only the moss in this flat render. Keep every silhouette and every edge exactly where it is. "
    "Do not move, scale, crop, or redraw the outline. Pure black pixels stay pure black. "
    "Style sheet, for colour and clump shape only, not for silhouette: an approved cushion-moss macro, "
    "soft velvety clumps of tiny overlapping leaves, deep teal-green crevices, brighter yellow-green tips, damp and even. "
    "Palette roles: moss body #376222, crevice near #0D231D, tip #8FF0C8 kept gentle, nothing brighter than #E8FFF4. "
    "Finish is flat albedo. No lighting, no shadows, no highlights, no rim light, no text, no glass, no jar, no fern, no desk. "
    "Do not use a finished jar photograph as the style source."
)


def say(line):
    print(line, flush=True)


def run(cmd, log_path):
    say("[f4] " + " ".join(cmd))
    os.makedirs(os.path.dirname(log_path), exist_ok=True)
    with open(log_path, "w", encoding="utf-8") as handle:
        proc = subprocess.run(cmd, cwd=ROOT, stdout=handle, stderr=subprocess.STDOUT)
    say("[f4] exit %s log %s" % (proc.returncode, log_path))
    with open(log_path, "r", encoding="utf-8", errors="replace") as handle:
        log_text = handle.read()
    if proc.returncode != 0 or "Traceback (most recent call last)" in log_text or "SELF_TEST_FAIL" in log_text or "CHECKER_FAIL" in log_text:
        with open(log_path, "r", encoding="utf-8", errors="replace") as handle:
            tail = handle.read()[-2000:]
        say(tail)
        raise SystemExit(proc.returncode or 1)
    return proc.returncode


def bash_exe():
    """PATH `bash` from this Python is WSL, which cannot see C:/ paths. Git bash can."""
    if os.path.isfile(GIT_BASH):
        return GIT_BASH
    found = shutil.which("bash")
    return found or "bash"


def blender(script, args, log_path):
    cmd = [BLENDER, "-b", "-P", os.path.join(HERE, script), "--", *args]
    return run(cmd, log_path)


def srgb_to_linear(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def linear_to_srgb(c):
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * np.power(np.clip(c, 0, None), 1.0 / 2.4) - 0.055)


def fit_repaint(raw_path, flat_path):
    """Nano Banana may change the canvas. The projection expects the flat render's pixel grid."""
    flat = Image.open(flat_path)
    raw = Image.open(raw_path).convert("RGB")
    if raw.size == flat.size:
        return raw_path, False
    sized = os.path.splitext(raw_path)[0] + "-sized.png"
    raw.resize(flat.size, Image.Resampling.LANCZOS).save(sized)
    return sized, True


def palette_lock(repaint_path, mask_path, out_path, strength=0.45):
    """Pull moss pixels toward the approved macro. Edges are not moved on purpose.

    color-matcher is not installed. This is a Reinhard mean/std match in RGB,
    mixed back with the repaint so the paint is not replaced.
    """
    src = np.asarray(Image.open(repaint_path).convert("RGB"), dtype=np.float32) / 255.0
    mask = np.asarray(Image.open(mask_path).convert("L"), dtype=np.float32) / 255.0 > 0.5
    ref = np.asarray(Image.open(STYLE).convert("RGB"), dtype=np.float32) / 255.0
    src_l = srgb_to_linear(src)
    ref_l = srgb_to_linear(ref)
    sample = src_l[mask]
    sm = sample.mean(axis=0)
    ss = sample.std(axis=0) + 1e-5
    rm = ref_l.reshape(-1, 3).mean(axis=0)
    rs = ref_l.reshape(-1, 3).std(axis=0) + 1e-5
    mapped = (src_l - sm) / ss * rs + rm
    mixed = src_l.copy()
    mixed[mask] = (1.0 - strength) * src_l[mask] + strength * np.clip(mapped[mask], 0.0, 1.0)
    out = np.clip(linear_to_srgb(mixed), 0.0, 1.0)
    Image.fromarray((out * 255.0).astype(np.uint8), "RGB").save(out_path)
    return {"strength": strength, "src_linear_mean": sm.tolist(), "macro_linear_mean": rm.tolist()}


def dinov2_status():
    """Borrowed-pixel check. Do not download a model and do not open the reference frame."""
    return {
        "measured": False,
        "reason": "No local DINOv2 weights were loaded. The distance was not measured. The parity reference frame was not a generation input.",
    }


def _swap_guid(text):
    guid = uuid.uuid4().hex
    lines = []
    for line in text.splitlines(True):
        if line.startswith("guid:"):
            lines.append("guid: %s\n" % guid)
        else:
            lines.append(line)
    return "".join(lines)


def write_unity_metas(source_png, texture_png, texture_note):
    """New guids. Texture settings match the approved moss macro (sRGB, mipmaps)."""
    tex_template = os.path.join(ROOT, "apps", "terrarium", "Assets", "Art", "Textures", "moss_macro.png.meta")
    text_template = os.path.join(ROOT, "apps", "terrarium", "Assets", "Art", "Source", "moss_macro.prompt.txt.meta")
    with open(tex_template, "r", encoding="utf-8") as handle:
        tex = handle.read()
    with open(text_template, "r", encoding="utf-8") as handle:
        text = handle.read()
    for png in (source_png, texture_png):
        with open(png + ".meta", "w", encoding="utf-8", newline="\n") as handle:
            handle.write(_swap_guid(tex))
    for sidecar in (source_png + ".prompt.txt", texture_note):
        if os.path.isfile(sidecar):
            with open(sidecar + ".meta", "w", encoding="utf-8", newline="\n") as handle:
                handle.write(_swap_guid(text))


def main():
    out = os.path.join(ROOT, "orchestration", "runs", "terrarium", "T-TER-041")
    skip_probe = "--skip-probe" in sys.argv
    skip_finish = "--skip-finish" in sys.argv
    skip_repaint = "--skip-repaint" in sys.argv
    os.makedirs(out, exist_ok=True)
    if not skip_probe:
        blender("probe_f4.py", ["--out", os.path.join(out, "probe")], os.path.join(out, "probe", "run.log"))
    if not skip_finish:
        blender(
            "finish_generated.py",
            ["--self-test", "--out", os.path.join(out, "finish")],
            os.path.join(out, "finish", "run.log"),
        )
    checker = os.path.join(out, "checker")
    blender("project_bake.py", ["--op", "checker", "--out", checker], os.path.join(checker, "run.log"))
    subprocess.run(
        [sys.executable, os.path.join(HERE, "f4_align.py"), "diff",
         "--render", os.path.join(checker, "render.png"),
         "--checker", os.path.join(checker, "checker.png"),
         "--mask", os.path.join(checker, "mask.png"),
         "--out", os.path.join(checker, "diff.png")],
        cwd=ROOT, check=True,
    )
    if skip_repaint:
        say("[f4] skip repaint")
        return
    passes = os.path.join(out, "passes")
    blender(
        "project_bake.py",
        ["--op", "passes", "--out", passes, "--macro", MACRO],
        os.path.join(passes, "run.log"),
    )
    views = []
    align = []
    for view in ("gate", "yaw-pos", "yaw-neg", "pitch-up"):
        flat = os.path.join(passes, "flat-%s.png" % view)
        mask = os.path.join(passes, "mask-%s.png" % view)
        raw = os.path.join(out, "repaint", "raw-%s.png" % view)
        os.makedirs(os.path.dirname(raw), exist_ok=True)
        run(
            [bash_exe(), os.path.join(ROOT, "tools", "agy", "image.sh").replace("\\", "/"),
             PROMPT, raw.replace("\\", "/"), flat.replace("\\", "/")],
            os.path.join(out, "repaint", "agy-%s.log" % view),
        )
        sized, did_resize = fit_repaint(raw, flat)
        # Gate on the repaint before the palette lock, so the colour match cannot move edges.
        iou_json = os.path.join(out, "repaint", "iou-%s.json" % view)
        subprocess.run(
            [sys.executable, os.path.join(HERE, "f4_align.py"), "iou",
             "--flat", flat, "--repaint", sized, "--mask", mask, "--json", iou_json],
            cwd=ROOT,
        )
        with open(iou_json, "r", encoding="utf-8") as handle:
            iou = json.load(handle)
        locked = os.path.join(out, "repaint", "locked-%s.png" % view)
        stats = palette_lock(sized, mask, locked)
        stats["resized"] = did_resize
        locked_json = os.path.join(out, "repaint", "iou-locked-%s.json" % view)
        subprocess.run(
            [sys.executable, os.path.join(HERE, "f4_align.py"), "iou",
             "--flat", flat, "--repaint", locked, "--mask", mask, "--json", locked_json],
            cwd=ROOT,
        )
        with open(locked_json, "r", encoding="utf-8") as handle:
            locked_iou = json.load(handle)
        iou["iou_after_palette"] = locked_iou["iou"]
        iou["palette"] = stats
        iou["view"] = view
        iou["gate_image"] = "sized-before-palette"
        iou["used"] = bool(iou.get("pass"))
        with open(iou_json, "w", encoding="utf-8") as handle:
            json.dump(iou, handle, indent=2)
        align.append(iou)
        say("[f4] iou %s %.4f pass %s" % (view, iou["iou"], iou["pass"]))
        if iou["used"]:
            views.append({
                "id": view,
                "yaw": {"gate": 0, "yaw-pos": 18, "yaw-neg": -18, "pitch-up": 0}[view],
                "pitch": {"gate": 0, "yaw-pos": 0, "yaw-neg": 0, "pitch-up": 12}[view],
                "image": locked,
            })
    if not any(item["id"] == "gate" for item in views):
        say("[f4] gate repaint failed the 0.6 IoU gate. Baking is blocked.")
        with open(os.path.join(out, "repaint", "align.json"), "w", encoding="utf-8") as handle:
            json.dump({"align": align, "dinov2": dinov2_status()}, handle, indent=2)
        raise SystemExit(2)
    views_path = os.path.join(out, "repaint", "views.json")
    with open(views_path, "w", encoding="utf-8") as handle:
        json.dump({"views": views, "align": align, "dinov2": dinov2_status()}, handle, indent=2)
    bake_out = os.path.join(out, "bake")
    blender(
        "project_bake.py",
        ["--op", "bake", "--views", views_path, "--previous", MACRO, "--out", bake_out, "--res", "1024"],
        os.path.join(bake_out, "run.log"),
    )
    # Ship the gate repaint and the baked variant. Do not touch Jar_Moss.mat.
    source = os.path.join(ROOT, "apps", "terrarium", "Assets", "Art", "Source", "moss-repaint.png")
    texture = os.path.join(ROOT, "apps", "terrarium", "Assets", "Art", "Textures", "moss-repaint.png")
    shutil.copyfile(os.path.join(out, "repaint", "raw-gate.png"), source)
    prompt_src = os.path.join(out, "repaint", "raw-gate.png.prompt.txt")
    if os.path.isfile(prompt_src):
        shutil.copyfile(prompt_src, source + ".prompt.txt")
    shutil.copyfile(os.path.join(bake_out, "moss-repaint.png"), texture)
    note = os.path.join(ROOT, "apps", "terrarium", "Assets", "Art", "Textures", "moss-repaint.prompt.txt")
    with open(note, "w", encoding="utf-8") as handle:
        handle.write(
            "variant: moss-repaint\n"
            "generator: project_bake.py from a Nano Banana repaint of the JarG1 flat mound\n"
            "style: docs/art/terrarium-style.md palette, apps/terrarium/Assets/Art/Source/moss_macro.png\n"
            "not conditioned on shared/assets/art-reference/\n"
            "previous texels: apps/terrarium/Assets/Art/Textures/moss_macro.png\n"
            "Jar_Moss.mat was not changed. The moss look stays locked.\n"
        )
    write_unity_metas(source, texture, note)
    say("[f4] DONE")


if __name__ == "__main__":
    main()

#!/usr/bin/env python
r"""Local image-to-3D driver for ComfyUI + TRELLIS.2 (T-TER-046, dossier F5).

  python tools/gen3d/gen.py <image.png> <out.glb> [--seed N] [--res 512|1024|1024_cascade|1536_cascade]

POSTs an API-format workflow to a running ComfyUI (default http://127.0.0.1:8188, started from C:\ai\ComfyUI with the
C:\ai\venv python), polls /history, copies the GLB out of ComfyUI's output folder and writes <out>.gen.json
(model id, seed, workflow sha256, wall time, VRAM peak sampled with nvidia-smi). Exit 1 on failure, with the node error.
"""
import argparse, hashlib, json, shutil, subprocess, sys, threading, time, urllib.request, uuid
from pathlib import Path

HERE = Path(__file__).resolve().parent
COMFY = "http://127.0.0.1:8188"
COMFY_DIR = Path(r"C:\ai\ComfyUI")
MODEL_ID = "microsoft/TRELLIS.2-4B (ComfyUI-TRELLIS2 PozzettiAndrea, MIT)"


def http(path, data=None, raw=False):
    req = urllib.request.Request(COMFY + path, data=data, headers={"Content-Type": "application/json"} if data and not raw else {})
    with urllib.request.urlopen(req, timeout=1800) as r:
        return json.loads(r.read())


def upload(img: Path) -> str:
    b = uuid.uuid4().hex
    body = (f"--{b}\r\nContent-Disposition: form-data; name=\"image\"; filename=\"{img.name}\"\r\nContent-Type: image/png\r\n\r\n").encode() \
        + img.read_bytes() + f"\r\n--{b}\r\nContent-Disposition: form-data; name=\"overwrite\"\r\n\r\ntrue\r\n--{b}--\r\n".encode()
    req = urllib.request.Request(COMFY + "/upload/image", data=body, headers={"Content-Type": f"multipart/form-data; boundary={b}"})
    return json.loads(urllib.request.urlopen(req, timeout=60).read())["name"]


class VramSampler(threading.Thread):
    def __init__(self):
        super().__init__(daemon=True); self.peak = 0; self.base = None; self.stop = False

    def run(self):
        while not self.stop:
            try:
                v = int(subprocess.check_output(["nvidia-smi", "--query-gpu=memory.used", "--format=csv,noheader,nounits"], text=True).split()[0])
                self.base = v if self.base is None else self.base
                self.peak = max(self.peak, v)
            except Exception:
                pass
            time.sleep(1)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("image"); ap.add_argument("out")
    ap.add_argument("--seed", type=int, default=42); ap.add_argument("--res", default="1024_cascade")
    ap.add_argument("--workflow", default=str(HERE / "workflows" / "trellis2_textured_api.json"))
    ap.add_argument("--timeout", type=int, default=1800)
    a = ap.parse_args()
    raw = Path(a.workflow).read_text()
    wf_hash = hashlib.sha256(raw.encode()).hexdigest()
    name = upload(Path(a.image))
    prefix = "gen3d/" + Path(a.out).stem
    wf = json.loads(raw.replace('"__SEED__"', str(a.seed)).replace("__IMAGE__", name).replace("__RES__", a.res).replace("__PREFIX__", prefix))
    s = VramSampler(); s.start(); t0 = time.time()
    pid = http("/prompt", json.dumps({"prompt": wf, "client_id": uuid.uuid4().hex}).encode())["prompt_id"]
    while True:
        h = http("/history/" + pid)
        if pid in h and h[pid].get("status", {}).get("completed") is not None and (h[pid]["status"].get("completed") or h[pid]["status"].get("status_str") == "error"):
            break
        if time.time() - t0 > a.timeout:
            s.stop = True; sys.exit("timeout")
        time.sleep(2)
    s.stop = True; wall = time.time() - t0; h = h[pid]
    if h["status"].get("status_str") == "error":
        sys.exit("comfy error: " + json.dumps([m for m in h["status"]["messages"] if m[0] == "execution_error"], indent=1)[:4000])
    fp = None
    for nid, o in h["outputs"].items():
        for v in o.values():
            for x in (v if isinstance(v, list) else [v]):
                if isinstance(x, str) and x.lower().endswith(".glb"):
                    fp = x
    if fp is None:  # Trellis2ExportTrimesh reports no ui output; take the newest GLB under the prefix
        c = sorted((COMFY_DIR / "output" / "gen3d").glob(Path(a.out).stem + "_*.glb"), key=lambda x: x.stat().st_mtime)
        if not c or c[-1].stat().st_mtime < t0 - 5:
            sys.exit("no glb produced: " + json.dumps(h["outputs"])[:1000])
        fp = str(c[-1])
    src = Path(fp) if Path(fp).is_absolute() else COMFY_DIR / "output" / fp
    Path(a.out).parent.mkdir(parents=True, exist_ok=True); shutil.copyfile(src, a.out)
    Path(a.out + ".gen.json").write_text(json.dumps({
        "generator": "comfyui+trellis2", "model": MODEL_ID, "resolution": a.res, "seed": a.seed, "workflow": Path(a.workflow).name,
        "workflow_sha256": wf_hash, "input": str(a.image), "wall_s": round(wall, 1),
        "vram_mib_before": s.base, "vram_mib_peak": s.peak, "vram_mib_delta": (s.peak - s.base) if s.base else None,
        "glb_bytes": Path(a.out).stat().st_size}, indent=1))
    print("ok", a.out, f"{wall:.0f}s", "vram_peak_mib", s.peak)


if __name__ == "__main__":
    main()

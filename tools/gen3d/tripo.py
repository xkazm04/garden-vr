#!/usr/bin/env python
"""Tripo image-to-model arm (T-TER-046). NOT RUN in T-TER-046: no TRIPO_API_KEY exists in .env or the environment.

  python tools/gen3d/tripo.py <image.png> <out.glb> [--face-limit N] [--allow-p2]

Reads TRIPO_API_KEY from the repo .env (git-ignored). Credit guard: logs balance before/after every call to
tools/gen3d/tripo_ledger.jsonl, refuses to start when spent-so-far + 130 would exceed BUDGET (300), P2 at most once.
Endpoints follow Tripo's v2 openapi (upload -> task -> poll); unverified against a live key.
"""
import argparse, json, os, sys, time, urllib.request, uuid
from pathlib import Path

BASE = "https://api.tripo3d.ai/v2/openapi"
LEDGER = Path(__file__).with_name("tripo_ledger.jsonl")
BUDGET = 300
MODEL = "v3.1-20260211"


def key():
    k = os.environ.get("TRIPO_API_KEY")
    env = Path(__file__).resolve().parents[2] / ".env"
    if not k and env.exists():
        for ln in env.read_text().splitlines():
            if ln.startswith("TRIPO_API_KEY="):
                k = ln.split("=", 1)[1].strip().strip('"')
    if not k:
        sys.exit("TRIPO_API_KEY missing (.env / environment); Tripo arm blocked")
    return k


def call(path, k, data=None, body=None, ctype="application/json"):
    req = urllib.request.Request(BASE + path, data=body if body is not None else (json.dumps(data).encode() if data else None),
                                 headers={"Authorization": "Bearer " + k, "Content-Type": ctype})
    return json.loads(urllib.request.urlopen(req, timeout=120).read())


def balance(k):
    return call("/user/balance", k)["data"]["balance"]


def spent():
    return sum(r["before"] - r["after"] for r in map(json.loads, LEDGER.read_text().splitlines())) if LEDGER.exists() else 0


def main():
    ap = argparse.ArgumentParser(); ap.add_argument("image"); ap.add_argument("out")
    ap.add_argument("--face-limit", type=int, default=8000); ap.add_argument("--allow-p2", action="store_true")
    a = ap.parse_args(); k = key()
    if spent() + (130 if a.allow_p2 else 40) > BUDGET:
        sys.exit("credit budget would be exceeded")
    before = balance(k)
    b = uuid.uuid4().hex; img = Path(a.image)
    body = (f"--{b}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"{img.name}\"\r\nContent-Type: image/png\r\n\r\n").encode() \
        + img.read_bytes() + f"\r\n--{b}--\r\n".encode()
    tok = call("/upload", k, body=body, ctype=f"multipart/form-data; boundary={b}")["data"]["image_token"]
    task = {"type": "image_to_model", "file": {"type": "png", "file_token": tok}, "model_version": "P2-20260801" if a.allow_p2 else MODEL,
            "texture": True, "pbr": True, "smart_low_poly": not a.allow_p2, "face_limit": a.face_limit}
    tid = call("/task", k, data=task)["data"]["task_id"]
    while True:
        d = call("/task/" + tid, k)["data"]
        if d["status"] in ("success", "failed", "cancelled", "unknown"):
            break
        time.sleep(5)
    after = balance(k)
    with LEDGER.open("a") as f:
        f.write(json.dumps({"task": tid, "model": task["model_version"], "image": str(img), "before": before, "after": after, "status": d["status"]}) + "\n")
    if d["status"] != "success":
        sys.exit("tripo task " + d["status"])
    url = d["output"].get("pbr_model") or d["output"].get("model")
    Path(a.out).write_bytes(urllib.request.urlopen(url, timeout=300).read())
    print("ok", a.out, "credits", before - after)


if __name__ == "__main__":
    main()

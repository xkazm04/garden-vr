"""Alpha-cut droplet normal map for the jar glass.

Sphere-cap beads on black. RGB is a tangent-space normal, A is a hard cut.
Not sampled from the reference frames. Run from the repo root:

    python apps/terrarium/Art/Scripts/droplet_normal.py
"""
import os
import random
import struct
import zlib

W = 512
H = 512
SEED = 33
OUT = os.path.join("apps", "terrarium", "Assets", "Art", "Textures", "droplet_normal.png")


def beads():
    rng = random.Random(SEED)
    placed = []
    for _ in range(800):
        if len(placed) >= 36:
            break
        r = rng.uniform(22.0, 40.0)
        x = rng.uniform(r + 2, W - r - 2)
        y = rng.uniform(r + 2, H - r - 2)
        if any((x - px) ** 2 + (y - py) ** 2 < (r + pr + 10) ** 2 for px, py, pr in placed):
            continue
        placed.append((x, y, r))
    return placed


def write_png(path, rgba):
    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    raw = bytearray()
    stride = W * 4
    for y in range(H):
        raw.append(0)
        raw.extend(rgba[y * stride:(y + 1) * stride])
    png = b"\x89PNG\r\n\x1a\n"
    png += chunk(b"IHDR", struct.pack(">IIBBBBB", W, H, 8, 6, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(bytes(raw), 9))
    png += chunk(b"IEND", b"")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as handle:
        handle.write(png)


def main():
    height = [0.0] * (W * H)
    for cx, cy, r in beads():
        x0 = max(0, int(cx - r))
        x1 = min(W, int(cx + r + 1))
        y0 = max(0, int(cy - r))
        y1 = min(H, int(cy + r + 1))
        for y in range(y0, y1):
            for x in range(x0, x1):
                d = ((x + 0.5 - cx) ** 2 + (y + 0.5 - cy) ** 2) ** 0.5
                if d >= r:
                    continue
                cap = (1.0 - (d / r) ** 2) ** 0.5
                i = y * W + x
                if cap > height[i]:
                    height[i] = cap
    rgba = bytearray(W * H * 4)
    for y in range(H):
        y0 = y * W
        for x in range(W):
            i = y0 + x
            h = height[i]
            o = i * 4
            if h <= 0.28:
                rgba[o] = 128
                rgba[o + 1] = 128
                rgba[o + 2] = 255
                rgba[o + 3] = 0
                continue
            left = height[i - 1] if x > 0 else 0.0
            right = height[i + 1] if x + 1 < W else 0.0
            down = height[i - W] if y > 0 else 0.0
            up = height[i + W] if y + 1 < H else 0.0
            dx = (right - left) * 2.2
            dy = (up - down) * 2.2
            nx, ny, nz = -dx, -dy, 1.0
            length = (nx * nx + ny * ny + nz * nz) ** 0.5
            rgba[o] = int(max(0, min(255, round((nx / length) * 127.5 + 127.5))))
            rgba[o + 1] = int(max(0, min(255, round((ny / length) * 127.5 + 127.5))))
            rgba[o + 2] = int(max(0, min(255, round((nz / length) * 127.5 + 127.5))))
            rgba[o + 3] = 255
    write_png(OUT, rgba)
    cuts = sum(1 for i in range(3, len(rgba), 4) if rgba[i] > 0)
    print("wrote", OUT, "beads", "cut-px", cuts)


if __name__ == "__main__":
    main()

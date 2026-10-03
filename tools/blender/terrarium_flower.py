"""Small 6-petal bell bloom for the night jar. Blender 4.2, headless.

    blender.exe -b -P tools/blender/terrarium_flower.py -- --out apps/terrarium/Assets/Art/Models

One mesh named Flower, under 1500 triangles. Blender Z is up; the FBX exporter
writes Unity Y-up. Each petal's UV is 0..1 so one petal texture covers every petal.
The stem base is the origin. Units are metres.
"""
import math
import os
import sys

import bpy
import bmesh
from mathutils import Vector


def resolve_out():
    out = os.path.join("apps", "terrarium", "Assets", "Art", "Models")
    if "--" in sys.argv:
        tail = sys.argv[sys.argv.index("--") + 1:]
        if "--out" in tail:
            i = tail.index("--out")
            if i + 1 >= len(tail):
                raise SystemExit("missing value for --out")
            out = tail[i + 1]
    if not os.path.isabs(out):
        out = os.path.abspath(out)
    os.makedirs(out, exist_ok=True)
    return out


OUT = resolve_out()
PETALS = 6
COLS = 6
ROWS = 9


def obj_from_bm(bm, name):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    for poly in me.polygons:
        poly.use_smooth = True
    return ob


def add_grid(bm, uv, ccount, rcount, vert_fn):
    grid = []
    for r in range(rcount):
        row = []
        for c in range(ccount):
            row.append(bm.verts.new(vert_fn(c, r)))
        grid.append(row)
    for r in range(rcount - 1):
        for c in range(ccount - 1):
            face = bm.faces.new((grid[r][c], grid[r][c + 1], grid[r + 1][c + 1], grid[r + 1][c]))
            coords = (
                (c / (ccount - 1), r / (rcount - 1)),
                ((c + 1) / (ccount - 1), r / (rcount - 1)),
                ((c + 1) / (ccount - 1), (r + 1) / (rcount - 1)),
                (c / (ccount - 1), (r + 1) / (rcount - 1)),
            )
            for loop, pair in zip(face.loops, coords):
                loop[uv].uv = pair
    return grid


def build():
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    sector = math.tau / PETALS

    for petal in range(PETALS):
        yaw = petal * sector

        def vert(c, r, yaw=yaw):
            span_s = r / (ROWS - 1)
            across = (c / (COLS - 1) - 0.5) * 2.0
            half = 0.0011 + 0.0044 * math.sin(span_s * math.pi) ** 0.85
            # Bell: narrow at the stem, opening, with the tip curling out and down.
            radius = 0.0016 + half + 0.0068 * math.sin(span_s * math.pi * 0.55)
            height = 0.0065 + 0.0220 * span_s - 0.0055 * span_s * span_s
            cup = 0.0028 * span_s * abs(across)
            # Overlap the next petal so the bell reads as one bloom, not six separated shards.
            ang = yaw + across * sector * 0.62
            return (
                math.sin(ang) * radius,
                math.cos(ang) * radius,
                height - cup,
            )

        add_grid(bm, uv, COLS, ROWS, vert)

    # A small closed bud in the throat. UVs sit on the petal's warm base.
    rings = 4
    bud = []
    for ring in range(rings):
        span_s = ring / (rings - 1)
        radius = 0.0025 * (1.0 - span_s * 0.82)
        height = 0.0082 + 0.0042 * span_s
        row = []
        for k in range(PETALS):
            ang = k * sector
            row.append(bm.verts.new((math.sin(ang) * radius, math.cos(ang) * radius, height)))
        bud.append(row)
    for ring in range(rings - 1):
        for k in range(PETALS):
            nxt = (k + 1) % PETALS
            face = bm.faces.new((bud[ring][k], bud[ring][nxt], bud[ring + 1][nxt], bud[ring + 1][k]))
            for loop in face.loops:
                loop[uv].uv = (0.5, 0.08)

    stem_rows = 6
    stem_segs = 6
    stem = []
    for row_i in range(stem_rows):
        span_s = row_i / (stem_rows - 1)
        radius = 0.0012 * (1.08 - 0.28 * span_s)
        height = 0.0082 * span_s
        row = []
        for k in range(stem_segs):
            ang = k * math.tau / stem_segs
            row.append(bm.verts.new((math.sin(ang) * radius, math.cos(ang) * radius, height)))
        stem.append(row)
    for row_i in range(stem_rows - 1):
        for k in range(stem_segs):
            nxt = (k + 1) % stem_segs
            face = bm.faces.new((stem[row_i][k], stem[row_i][nxt], stem[row_i + 1][nxt], stem[row_i + 1][k]))
            for loop, uu in zip(face.loops, (k / stem_segs, (k + 1) / stem_segs, (k + 1) / stem_segs, k / stem_segs)):
                loop[uv].uv = (uu, row_i / (stem_rows - 1) * 0.25)

    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.normal_update()
    score = 0.0
    for face in bm.faces:
        center = face.calc_center_median()
        score += face.normal.x * center.x + face.normal.y * center.y
    if score < 0.0:
        bmesh.ops.reverse_faces(bm, faces=list(bm.faces))
        print("[flower] flipped normals")

    tris = sum(len(face.verts) - 2 for face in bm.faces)
    zs = [v.co.z for v in bm.verts]
    radii = [math.hypot(v.co.x, v.co.y) for v in bm.verts]
    print("[flower] tris", tris, "z", "{:.4f}".format(min(zs)), "{:.4f}".format(max(zs)), "r", "{:.4f}".format(max(radii)))
    if tris >= 1500:
        raise SystemExit("flower is over the 1500 tri budget: %d" % tris)
    return obj_from_bm(bm, "Flower")


def export(path):
    bpy.ops.object.select_all(action="DESELECT")
    bpy.data.objects["Flower"].select_set(True)
    bpy.context.view_layer.objects.active = bpy.data.objects["Flower"]
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        object_types={"MESH"},
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        bake_anim=False,
        add_leaf_bones=False,
    )
    print("[flower] exported", path)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    build()
    export(os.path.join(OUT, "flower.fbx"))


if __name__ == "__main__":
    main()

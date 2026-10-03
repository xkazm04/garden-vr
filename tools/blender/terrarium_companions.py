"""One sprig card for the terrarium companions. Blender 4.2, headless.

    blender.exe -b -P tools/blender/terrarium_companions.py -- --out apps/terrarium/Assets/Art/Models

One mesh named Sprig, under 600 triangles, longest side under 8 cm.
Modelled in Blender's XZ plane (stem along +Z, thin in Y) so the Unity
Y-up import stands the card up. Extra kept-day leaves are added in the app.
"""
import math
import os
import shutil
import sys

import bpy
import bmesh


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
COLS = 8
ROWS = 12
WIDTH = 0.026
HEIGHT = 0.046


def obj_from_bm(bm, name):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    for poly in me.polygons:
        poly.use_smooth = True
    return ob


def build():
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    grid = []
    for r in range(ROWS):
        row = []
        v = r / (ROWS - 1)
        for c in range(COLS):
            u = c / (COLS - 1)
            x = (u - 0.5) * WIDTH
            z = v * HEIGHT
            y = 0.0011 * math.sin(u * math.pi) * math.sin(v * math.pi)
            row.append(bm.verts.new((x, y, z)))
        grid.append(row)
    for r in range(ROWS - 1):
        for c in range(COLS - 1):
            face = bm.faces.new((
                grid[r][c],
                grid[r][c + 1],
                grid[r + 1][c + 1],
                grid[r + 1][c],
            ))
            coords = (
                (c / (COLS - 1), r / (ROWS - 1)),
                ((c + 1) / (COLS - 1), r / (ROWS - 1)),
                ((c + 1) / (COLS - 1), (r + 1) / (ROWS - 1)),
                (c / (COLS - 1), (r + 1) / (ROWS - 1)),
            )
            for loop, pair in zip(face.loops, coords):
                loop[uv].uv = pair

    tris = sum(len(face.verts) - 2 for face in bm.faces)
    xs = [v.co.x for v in bm.verts]
    ys = [v.co.y for v in bm.verts]
    zs = [v.co.z for v in bm.verts]
    extent = max(max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs))
    print("[sprig] tris", tris, "extent", "{:.4f}".format(extent))
    if tris >= 600:
        raise SystemExit("sprig is over the 600 tri budget: %d" % tris)
    if extent >= 0.08:
        raise SystemExit("sprig is over 8 cm: %.4f" % extent)
    return obj_from_bm(bm, "Sprig")


def export(path):
    bpy.ops.object.select_all(action="DESELECT")
    bpy.data.objects["Sprig"].select_set(True)
    bpy.context.view_layer.objects.active = bpy.data.objects["Sprig"]
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
    print("[sprig] exported", path)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    build()
    dest = os.path.join(OUT, "sprig.fbx")
    export(dest)
    resources = os.path.abspath(os.path.join(
        "apps", "terrarium", "Assets", "Resources", "Companions"))
    os.makedirs(resources, exist_ok=True)
    copy = os.path.join(resources, "sprig.fbx")
    shutil.copyfile(dest, copy)
    print("[sprig] copied", copy)


if __name__ == "__main__":
    main()

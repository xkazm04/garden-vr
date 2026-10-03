"""Clump moss and a silhouette skirt for the night jar.

Imported by terrarium_jar.py after the scene reset. Also runnable alone:

    blender.exe -b -P tools/blender/terrarium_moss.py -- --out apps/terrarium/Assets/Art/Models

The mound is one low carpet plus a few coplanar patches. Each patch takes its
own sheared crop of the macro, so the kaleidoscope cannot line up into one
honeycomb and a sphere cannot repeat the plate's round cushions. UV2.x is a
0..1 blend toward the second macro. MossSkirt is a rim of tuft cards, joined
into one mesh so the draw stays one. Cards carry a 4x4 tuft atlas.
"""
import math
import os
import random
import sys

import bpy
import bmesh
from mathutils import Euler, Matrix, Vector, noise

# One joined mesh. BudgetMeasure counts a renderer as a draw, and the cap is 40.
# The card material has GPU instancing on, so this batch is one instanced draw.
TUFT_CARDS = 400
ATLAS = 4


def probe():
    """Exercise every bpy operator this module relies on. Prints [probe] lines."""
    print("[probe] blender", bpy.app.version_string)
    bm = bmesh.new()
    try:
        bmesh.ops.create_icosphere(
            bm, subdivisions=1, radius=0.01, matrix=Matrix.Identity(4), calc_uvs=False
        )
        print("[probe] bmesh.ops.create_icosphere ok verts", len(bm.verts))
    except Exception as exc:
        print("[probe] bmesh.ops.create_icosphere FAIL", exc)
        raise
    finally:
        bm.free()
    try:
        sample = noise.noise(Vector((0.2, 0.4, 0.6)))
        print("[probe] mathutils.noise.noise ok", "{:.4f}".format(sample))
    except Exception as exc:
        print("[probe] mathutils.noise.noise FAIL", exc)
        raise

    made = []
    try:
        for i, loc in enumerate(((0.0, 0.0, -1.0), (0.02, 0.0, -1.0))):
            temp = bmesh.new()
            bmesh.ops.create_icosphere(
                temp, subdivisions=1, radius=0.008, matrix=Matrix.Identity(4), calc_uvs=False
            )
            me = bpy.data.meshes.new("_probeMesh%d" % i)
            temp.to_mesh(me)
            temp.free()
            ob = bpy.data.objects.new("_probe%d" % i, me)
            bpy.context.collection.objects.link(ob)
            ob.location = Vector(loc)
            made.append(ob)
        bpy.ops.object.select_all(action="DESELECT")
        for ob in made:
            ob.select_set(True)
        bpy.context.view_layer.objects.active = made[0]
        bpy.ops.object.join()
        joined = bpy.context.view_layer.objects.active
        print("[probe] bpy.ops.object.join ok", joined.name if joined else "none")
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        print("[probe] bpy.ops.object.transform_apply ok")
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.mesh.normals_make_consistent(inside=False)
        bpy.ops.object.mode_set(mode="OBJECT")
        print("[probe] bpy.ops.mesh.normals_make_consistent ok")
        bpy.ops.object.select_all(action="DESELECT")
        joined.select_set(True)
        bpy.context.view_layer.objects.active = joined
        bpy.ops.object.delete()
        print("[probe] bpy.ops.object.delete ok")
    except Exception as exc:
        print("[probe] bpy.ops FAIL", type(exc).__name__, exc)
        raise


def finish(bm, name):
    bmesh.ops.dissolve_degenerate(bm, dist=1e-8, edges=bm.edges)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.validate(verbose=False)
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    for poly in me.polygons:
        poly.use_smooth = True
    return ob


def activate(obj):
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def consistent_normals(obj):
    activate(obj)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    for poly in obj.data.polygons:
        poly.use_smooth = True


def tri_count(obj):
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def safe_crop(rng, crop):
    """A window that stays inside one quadrant of the mirrored macro.

    moss_macro is a 2x2 kaleidoscope. The mirror crosses sit at 0, 0.5 and 1.
    A crop that includes a cross reads as one cell of the honeycomb.
    """
    span = max(0.08, min(0.46, crop))
    for _ in range(24):
        u = rng.uniform(0.02, 0.98 - span)
        if (u + span < 0.48) or (u > 0.52 and u + span < 0.98):
            return u, span
    return 0.58, span


def make_clump(center, radius, seed, flatten, subdiv, blend, rng, span=None, lump=0.10):
    """One flat patch. Vertices are in world space. UV is one sheared crop, not a tiled dome."""
    bm = bmesh.new()
    bmesh.ops.create_icosphere(
        bm, subdivisions=subdiv, radius=1.0, matrix=Matrix.Identity(4), calc_uvs=False
    )
    uv = bm.loops.layers.uv.new("UVMap")
    uv2 = bm.loops.layers.uv.new("UV2")
    spin = Euler((rng.uniform(-0.15, 0.15), rng.uniform(-0.12, 0.12), rng.uniform(0.0, math.tau)), "XYZ")
    center_v = Vector(center)
    for vert in bm.verts:
        p = vert.co.copy()
        p.rotate(spin)
        n1 = noise.noise(p * 2.1 + Vector((seed * 0.13, 0.2, 0.4)))
        n2 = noise.noise(p * 5.4 + Vector((0.3, seed * 0.2, 1.1)))
        deform = max(0.90, 0.97 + lump * n1 + 0.04 * n2)
        p.z *= flatten
        vert.co = center_v + p * (deform * radius)
        ripple = noise.noise(Vector((vert.co.x * 46.0, vert.co.y * 46.0, seed * 0.31)))
        vert.co.z += ripple * 0.0011
    if span is None:
        span = rng.uniform(0.18, 0.34)
    crop_u, span_u = safe_crop(rng, span)
    crop_v, span_v = safe_crop(rng, span * rng.uniform(0.85, 1.15))
    ang = rng.uniform(0.0, math.tau)
    cu, su = math.cos(ang), math.sin(ang)
    reach = max(radius * 2.0, 1e-4)
    for face in bm.faces:
        for loop in face.loops:
            p = loop.vert.co
            lx = (p.x - center_v.x) / reach
            ly = (p.y - center_v.y) / reach
            rx = lx * cu - ly * su
            ry = lx * su + ly * cu
            # Shear inside the crop. A round kaleidoscope centre cannot survive as a circle.
            nu = noise.noise(Vector((lx * 3.2, ly * 3.2, seed * 0.17 + 1.7)))
            nv = noise.noise(Vector((lx * 3.2 + 5.0, ly * 3.2, seed * 0.17 + 2.3)))
            rx = max(-0.48, min(0.48, rx + nu * 0.12 + ry * 0.08))
            ry = max(-0.48, min(0.48, ry + nv * 0.12 - rx * 0.05))
            loop[uv].uv = (crop_u + (rx + 0.5) * span_u, crop_v + (ry + 0.5) * span_v)
            loop[uv2].uv = (blend, ang / math.tau)
    return finish(bm, "Clump%d" % seed)


def build_mound():
    """One low carpet plus flat overlapping patches.

    create_icosphere subdivisions=1 is the bare icosahedron. 5 is a dense dome.
    Round swells read as a honeycomb of balls even when each UV is a single crop,
    because the Nano Banana plates are themselves round cushions. Stay flat.
    """
    rng = random.Random(11)
    # (radius from axis, angle, clump radius, flatten, z, seed, subdiv, blend, span, lump)
    specs = [(0.0, 0.0, 0.037, 0.08, 0.0270, 1, 5, 0.35, 0.38, 0.10)]
    blends = (0.15, 0.40, 0.65, 0.90)
    for i in range(6):
        if rng.random() < 0.7:
            ang = -math.pi * 0.5 + rng.uniform(-1.0, 1.0)
        else:
            ang = rng.random() * math.tau
        rad = rng.uniform(0.0, 0.014)
        specs.append((rad, ang, rng.uniform(0.016, 0.026), rng.uniform(0.05, 0.09),
                      0.0272, i + 2, 3, blends[i % 4],
                      rng.uniform(0.22, 0.36), 0.08))
    objs = []
    for rad, ang, radius, flat, z0, seed, subdiv, blend, span, lump in specs:
        cx = rad * math.cos(ang)
        cy = rad * math.sin(ang)
        limit = glass_inner(z0) - radius * 0.55
        dist = math.hypot(cx, cy)
        if dist > limit and dist > 1e-6:
            cx *= limit / dist
            cy *= limit / dist
        objs.append(make_clump((cx, cy, z0), radius, seed, flat, subdiv, blend, rng, span, lump))
    activate(objs[0])
    for ob in objs:
        ob.select_set(True)
    bpy.ops.object.join()
    moss = bpy.context.view_layer.objects.active
    moss.name = "Moss"
    moss.data.name = "Moss"
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    pull_inside(moss)
    consistent_normals(moss)
    return moss


def grid_mesh(name, width, height, cols, rows, fold, arch, cell):
    """Card whose UV sits inside one atlas cell (cell is x, y with origin at the bottom-left)."""
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    cx, cy = cell
    pad = 0.02 / ATLAS
    u0 = cx / ATLAS + pad
    v0 = cy / ATLAS + pad
    u1 = (cx + 1) / ATLAS - pad
    v1 = (cy + 1) / ATLAS - pad
    grid = []
    for j in range(rows + 1):
        v = j / rows
        row = []
        for i in range(cols + 1):
            u = i / cols
            x = (u - 0.5) * width
            y = -abs(u - 0.5) * 2.0 * fold - arch * (v ** 1.3)
            z = v * height
            row.append(bm.verts.new((x, y, z)))
        grid.append(row)
    for j in range(rows):
        for i in range(cols):
            face = bm.faces.new((grid[j][i], grid[j + 1][i], grid[j + 1][i + 1], grid[j][i + 1]))
            coords = (
                (u0 + (i / cols) * (u1 - u0), v0 + (j / rows) * (v1 - v0)),
                (u0 + (i / cols) * (u1 - u0), v0 + ((j + 1) / rows) * (v1 - v0)),
                (u0 + ((i + 1) / cols) * (u1 - u0), v0 + ((j + 1) / rows) * (v1 - v0)),
                (u0 + ((i + 1) / cols) * (u1 - u0), v0 + (j / rows) * (v1 - v0)),
            )
            for loop, uvw in zip(face.loops, coords):
                loop[uv].uv = uvw
    return finish(bm, name)


def align_card(obj, loc, ang, tilt):
    z_axis = Vector((
        math.sin(tilt) * math.cos(ang),
        math.sin(tilt) * math.sin(ang),
        math.cos(tilt),
    )).normalized()
    radial = Vector((math.cos(ang), math.sin(ang), 0.0))
    x_axis = radial.cross(Vector((0.0, 0.0, 1.0)))
    if x_axis.length < 1e-6:
        x_axis = Vector((1.0, 0.0, 0.0))
    x_axis.normalize()
    y_axis = z_axis.cross(x_axis).normalized()
    x_axis = y_axis.cross(z_axis).normalized()
    rot = Matrix((x_axis, y_axis, z_axis)).transposed().to_4x4()
    obj.matrix_world = Matrix.Translation(Vector(loc)) @ rot


def glass_inner(z):
    """Inside the mason wall. The heel is narrower than the body."""
    if z < 0.008:
        return 0.030
    if z < 0.016:
        return 0.036
    if z < 0.024:
        return 0.040
    return 0.0412


def pull_inside(obj):
    """Pull vertices that stick through the glass back inside the wall at that height."""
    mesh = obj.data
    moved = 0
    for vert in mesh.vertices:
        limit = glass_inner(vert.co.z)
        radius = math.hypot(vert.co.x, vert.co.y)
        if radius > limit:
            vert.co.x *= limit / radius
            vert.co.y *= limit / radius
            moved += 1
    if moved:
        mesh.update()
    print("[hero] pull_inside", obj.name, moved)


def build_skirt():
    """Flat overlapping tuft cards over the carpet. Joined into MossSkirt (one draw).

    Upright cards face the camera as a ring of round pom-poms, which is the
    honeycomb again. Small cards lying on the mound overlap several deep, so
    the circular alpha does not read as a separate cushion. Camera is Blender -Y.
    """
    rng = random.Random(19)
    cards = []
    for i in range(TUFT_CARDS):
        if rng.random() < 0.72:
            ang = -math.pi * 0.5 + rng.uniform(-1.2, 1.2)
        else:
            ang = rng.random() * math.tau
        rad = (rng.random() ** 0.75) * 0.032
        z = 0.0275 + rng.uniform(0.0, 0.0035)
        # Near pi/2 the card lies on the mound instead of standing up at the camera.
        tilt = rng.uniform(1.20, 1.52)
        cell = ((i * 3 + 1) % ATLAS, (i * 5 + 2) % ATLAS)
        card = grid_mesh(
            "Skirt%d" % i,
            rng.uniform(0.010, 0.016),
            rng.uniform(0.008, 0.013),
            2, 2, 0.0002, 0.0001, cell,
        )
        align_card(card, (rad * math.cos(ang), rad * math.sin(ang), z), rng.uniform(0.0, math.tau), tilt)
        s = rng.uniform(0.75, 1.25)
        card.scale = (s, s * rng.uniform(0.85, 1.15), s * rng.uniform(0.70, 1.25))
        cards.append(card)
    bpy.context.view_layer.update()
    activate(cards[0])
    for card in cards:
        card.select_set(True)
    bpy.ops.object.join()
    skirt = bpy.context.view_layer.objects.active
    skirt.name = "MossSkirt"
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    pull_inside(skirt)
    consistent_normals(skirt)
    skirt.data.name = "MossSkirt"
    return skirt


def bounds(obj):
    zs, rs = [], []
    for vert in obj.data.vertices:
        w = obj.matrix_world @ vert.co
        zs.append(w.z)
        rs.append(math.hypot(w.x, w.y))
    return min(zs), max(zs), max(rs) if rs else 0.0


def build():
    """Add Moss and MossSkirt to the current scene. Returns their names."""
    probe()
    moss = build_mound()
    skirt = build_skirt()
    for ob in (moss, skirt):
        z0, z1, rad = bounds(ob)
        print("[hero] tris", ob.name, tri_count(ob), "z", "{:.4f}".format(z0), "{:.4f}".format(z1), "r", "{:.4f}".format(rad))
    print("[probe] moss cards", TUFT_CARDS)
    return ["Moss", "MossSkirt"]


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


if __name__ == "__main__":
    bpy.ops.wm.read_factory_settings(use_empty=True)
    names = build()
    out = resolve_out()
    bpy.ops.object.select_all(action="DESELECT")
    for name in names:
        bpy.data.objects[name].select_set(True)
    path = os.path.join(out, "moss_only.fbx")
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z", axis_up="Y", object_types={"MESH"}, use_mesh_modifiers=True,
        mesh_smooth_type="FACE", bake_anim=False, add_leaf_bones=False,
    )
    print("[hero] exported", path, names)

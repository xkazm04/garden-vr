"""Low moss mound and a dense tuft carpet for the night jar.

Imported by terrarium_jar.py after the scene reset. Also runnable alone:

    blender.exe -b -P tools/blender/terrarium_moss.py -- --out apps/terrarium/Assets/Art/Models

The mound is one low irregular sheet. It is not a field of round cushions.
MossSkirt is hundreds of small tuft cards over that sheet, authored as
instances and joined into one mesh. BudgetMeasure counts a renderer as a
draw, so a card per object would blow the 40-draw cap. The card material
has GPU instancing on, and the shader compiles instancing. Each card's UV
sits in one cell of the 8x8 tuft atlas.
"""
import math
import os
import random
import sys

import bpy
import bmesh
from mathutils import Euler, Matrix, Vector, noise

# One joined mesh. BudgetMeasure counts a renderer as a draw, and the cap is 40.
# The cards are instances welded into that mesh so the draw stays one.
TUFT_CARDS = 980
ATLAS = 8
MOUND_R = 0.040


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


def soil_top(radius):
    """Match the Soil lathe top in terrarium_jar.py. The carpet has to sit on it."""
    if radius <= 0.030:
        return 0.024 - 0.001 * (radius / 0.030)
    t = min((radius - 0.030) / 0.0096, 1.0)
    return 0.023 - 0.003 * t


def mound_z(x, y):
    """Low irregular carpet on the soil, draped over the dark soil wall.

    Wavelengths stay clear of a cobblestone (about 1 cm).
    """
    radius = math.hypot(x, y)
    if radius > 0.0365:
        t = min((radius - 0.0365) / 0.0065, 1.0)
        fall = t * t * (3.0 - 2.0 * t)
        rim_z = soil_top(0.036) + 0.0032
        return rim_z + (0.0115 - rim_z) * fall
    t = min(radius / MOUND_R, 1.0)
    dome = 0.0048 * ((1.0 - t * t) ** 1.15)
    point = Vector((x, y, 0.2))
    # About 3.5 cm and 1.4 cm. Amplitudes are a couple of millimetres, not spheres.
    low = 0.0016 * noise.noise(point * 28.0)
    fine = 0.0007 * noise.noise(point * 72.0 + Vector((3.0, 1.0, 0.0)))
    edge = 1.0 if t < 0.82 else max(0.0, (1.0 - t) / 0.18)
    lift = 0.0022 + (dome + max(low, -0.0008) + fine) * (0.25 + 0.75 * edge)
    return soil_top(radius) + lift


def force_up(obj):
    total = 0.0
    for poly in obj.data.polygons:
        total += poly.normal.z
    if total >= 0.0:
        return
    activate(obj)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.flip_normals()
    bpy.ops.object.mode_set(mode="OBJECT")
    for poly in obj.data.polygons:
        poly.use_smooth = True


def build_mound():
    """One sheet. Round icospheres read as a honeycomb, so they are not used."""
    n = 52
    extent = 0.044
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    grid = []
    for j in range(n + 1):
        y = -extent + 2.0 * extent * j / n
        row = []
        for i in range(n + 1):
            x = -extent + 2.0 * extent * i / n
            radius = math.hypot(x, y)
            z = mound_z(x, y)
            if radius > 0.043 and radius > 1e-8:
                scale = 0.043 / radius
                x *= scale
                y *= scale
                z = mound_z(x, y)
            row.append(bm.verts.new((x, y, z)))
        grid.append(row)
    for j in range(n):
        for i in range(n):
            verts = (grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i])
            cx = sum(vert.co.x for vert in verts) * 0.25
            cy = sum(vert.co.y for vert in verts) * 0.25
            if math.hypot(cx, cy) > 0.0432:
                continue
            face = bm.faces.new(verts)
            for loop in face.loops:
                point = loop.vert.co
                # One carpet photo across the sheet. The rim stays inside 0..1, so the texture does not tile.
                loop[uv].uv = (point.x / 0.092 + 0.5, point.y / 0.092 + 0.5)
    moss = finish(bm, "Moss")
    pull_inside(moss, 0.0430)
    consistent_normals(moss)
    force_up(moss)
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


# Glass shell is about 0.046 m. Cards stay inside it and on the soil bed.
GLASS_INNER = 0.0430


def pull_inside(obj, limit):
    """Pull vertices that stick through the glass back to the inner wall."""
    mesh = obj.data
    moved = 0
    for vert in mesh.vertices:
        radius = math.hypot(vert.co.x, vert.co.y)
        if radius > limit:
            vert.co.x *= limit / radius
            vert.co.y *= limit / radius
            moved += 1
    if moved:
        mesh.update()
    print("[hero] pull_inside", obj.name, moved, "limit", "{:.4f}".format(limit))


def card_matrix(loc, yaw, tilt, scale):
    """Same basis as align_card, with scale baked in. Local +Z is the tuft tip."""
    z_axis = Vector((
        math.sin(tilt) * math.cos(yaw),
        math.sin(tilt) * math.sin(yaw),
        math.cos(tilt),
    )).normalized()
    radial = Vector((math.cos(yaw), math.sin(yaw), 0.0))
    x_axis = radial.cross(Vector((0.0, 0.0, 1.0)))
    if x_axis.length < 1e-6:
        x_axis = Vector((1.0, 0.0, 0.0))
    x_axis.normalize()
    y_axis = z_axis.cross(x_axis).normalized()
    x_axis = y_axis.cross(z_axis).normalized()
    rot = Matrix((x_axis * scale[0], y_axis * scale[1], z_axis * scale[2])).transposed().to_4x4()
    return Matrix.Translation(Vector(loc)) @ rot


def build_skirt():
    """Hundreds of small tuft cards, welded into one mesh so the draw stays one."""
    rng = random.Random(19)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    placed = []
    min_dist = 0.0011
    cols = 2
    rows = 2
    for i in range(TUFT_CARDS):
        x = y = yaw = 0.0
        on_rim = rng.random() < 0.24
        for _try in range(24):
            yaw = rng.random() * math.tau
            if on_rim:
                rad = rng.uniform(0.036, 0.0415)
            else:
                rad = math.sqrt(rng.random()) * 0.036
            x = rad * math.cos(yaw)
            y = rad * math.sin(yaw)
            if all((x - px) * (x - px) + (y - py) * (y - py) > min_dist * min_dist for px, py in placed[-64:]):
                break
        placed.append((x, y))
        z = mound_z(x, y) - 0.0003
        if on_rim:
            tilt = rng.uniform(1.15, 1.55)
            width = rng.uniform(0.0030, 0.0050)
            height = rng.uniform(0.0035, 0.0060)
        # Most cards are a short nap. A few lie flatter so the gaps are leaves, not holes.
        elif rng.random() < 0.72:
            tilt = rng.uniform(0.15, 0.62)
            width = rng.uniform(0.0024, 0.0042)
            height = rng.uniform(0.0028, 0.0050)
        else:
            tilt = rng.uniform(0.85, 1.15)
            width = rng.uniform(0.0036, 0.0055)
            height = rng.uniform(0.0026, 0.0044)
        cell_x = (i * 3 + 1) % ATLAS
        cell_y = (i * 5 + 2) % ATLAS
        pad = 0.02 / ATLAS
        u0 = cell_x / ATLAS + pad
        v0 = cell_y / ATLAS + pad
        u1 = (cell_x + 1) / ATLAS - pad
        v1 = (cell_y + 1) / ATLAS - pad
        fold = 0.00025
        arch = 0.00012
        scale = (
            rng.uniform(0.85, 1.15),
            rng.uniform(0.85, 1.15),
            rng.uniform(0.85, 1.15),
        )
        matrix = card_matrix((x, y, z), yaw + rng.uniform(-0.6, 0.6), tilt, scale)
        grid = []
        for j in range(rows + 1):
            v = j / rows
            row = []
            for k in range(cols + 1):
                u = k / cols
                local = Vector((
                    (u - 0.5) * width,
                    -abs(u - 0.5) * 2.0 * fold - arch * (v ** 1.3),
                    v * height,
                ))
                point = matrix @ Vector((local.x, local.y, local.z, 1.0))
                row.append(bm.verts.new((point.x, point.y, point.z)))
            grid.append(row)
        for j in range(rows):
            for k in range(cols):
                face = bm.faces.new((grid[j][k], grid[j + 1][k], grid[j + 1][k + 1], grid[j][k + 1]))
                coords = (
                    (u0 + (k / cols) * (u1 - u0), v0 + (j / rows) * (v1 - v0)),
                    (u0 + (k / cols) * (u1 - u0), v0 + ((j + 1) / rows) * (v1 - v0)),
                    (u0 + ((k + 1) / cols) * (u1 - u0), v0 + ((j + 1) / rows) * (v1 - v0)),
                    (u0 + ((k + 1) / cols) * (u1 - u0), v0 + (j / rows) * (v1 - v0)),
                )
                for loop, uvw in zip(face.loops, coords):
                    loop[uv].uv = uvw
    skirt = finish(bm, "MossSkirt")
    pull_inside(skirt, GLASS_INNER)
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

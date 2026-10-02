"""Night Moss hero assets for Terrarium. Blender 4.2, headless:

    blender.exe -b -P tools/blender/terrarium_hero.py

Models are metres, Z-up in Blender, exported FBX with Y-up for Unity.
Jar height including cork is 14.0 cm and the outer diameter is 8.7 cm
(docs/art/terrarium-style.md). Set HERO_SKIP_RENDER=1 to export only.
"""
import math
import os
import sys
import traceback

import bpy
import bmesh
from mathutils import Euler, Matrix, Vector, noise

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
TEX = os.path.join(ROOT, "apps", "terrarium", "Assets", "Art", "Textures")
MOD = os.path.join(ROOT, "apps", "terrarium", "Assets", "Art", "Models")
RUN = os.path.join(ROOT, "orchestration", "runs", "terrarium", "T-TER-013")
SKIP_RENDER = os.environ.get("HERO_SKIP_RENDER", "") == "1"
SAMPLES = int(os.environ.get("HERO_SAMPLES", "40"))

# Style-bible envelope. Total height is the cork top.
JAR_HEIGHT = 0.140
JAR_RADIUS = 0.0435


def lin(r, g, b, a=1.0):
    def c(u):
        return u / 12.92 if u <= 0.04045 else ((u + 0.055) / 1.055) ** 2.4
    return (c(r), c(g), c(b), a)


def hex_lin(h, a=1.0):
    h = h.lstrip("#")
    return lin(int(h[0:2], 16) / 255, int(h[2:4], 16) / 255, int(h[4:6], 16) / 255, a)


def smoothstep(x):
    x = max(0.0, min(1.0, x))
    return x * x * (3.0 - 2.0 * x)


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    return scene


def finish(bm, name):
    bmesh.ops.dissolve_degenerate(bm, dist=1e-8, edges=bm.edges)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.validate(verbose=False)
    obj = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(obj)
    for p in me.polygons:
        p.use_smooth = True
    return obj


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
    for p in obj.data.polygons:
        p.use_smooth = True


def ensure_outward(obj):
    """Flip a closed lathe if the outermost vertex points toward the axis."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.normal_update()
    best = max(bm.verts, key=lambda v: v.co.x * v.co.x + v.co.y * v.co.y)
    radial = Vector((best.co.x, best.co.y, 0.0))
    if radial.length > 1e-6 and best.normal.dot(radial) < 0:
        bmesh.ops.reverse_faces(bm, faces=bm.faces)
        bm.to_mesh(obj.data)
        print("[hero] flipped", obj.name)
    bm.free()


def tri_count(obj):
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def bounds(obj):
    mw = obj.matrix_world
    zs, rs = [], []
    for v in obj.data.vertices:
        w = mw @ v.co
        zs.append(w.z)
        rs.append(math.hypot(w.x, w.y))
    return min(zs), max(zs), max(rs) if rs else 0.0


def duplicate(obj, linked=True):
    dup = obj.copy()
    dup.data = obj.data if linked else obj.data.copy()
    bpy.context.collection.objects.link(dup)
    return dup


def load_image(filename):
    path = os.path.join(TEX, filename)
    if not os.path.exists(path):
        print("[hero] missing texture", path)
        return None
    img = bpy.data.images.load(path)
    try:
        img.colorspace_settings.name = "sRGB"
    except (TypeError, ValueError):
        pass
    return img


# ---------------------------------------------------------------- geometry
def lathe(name, profile, segs=96):
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    rows = []
    n = len(profile)
    for r, z in profile:
        row = []
        for s in range(segs + 1):
            a = 2.0 * math.pi * s / segs
            row.append(bm.verts.new((r * math.cos(a), -r * math.sin(a), z)))
        rows.append(row)
    for i in range(n - 1):
        for s in range(segs):
            try:
                f = bm.faces.new((rows[i][s], rows[i][s + 1], rows[i + 1][s + 1], rows[i + 1][s]))
            except ValueError:
                continue
            v0 = i / (n - 1)
            v1 = (i + 1) / (n - 1)
            uvs = (s / segs, (s + 1) / segs, (s + 1) / segs, s / segs)
            vs = (v0, v0, v1, v1)
            for loop, uu, vv in zip(f.loops, uvs, vs):
                loop[uv].uv = (uu, vv)
    poles = [v for v in bm.verts if math.hypot(v.co.x, v.co.y) < 1e-7]
    if poles:
        bmesh.ops.remove_doubles(bm, verts=poles, dist=1e-6)
    return finish(bm, name)


def jar_profile():
    # Closed shell: up the outside, over a rounded lip, down the inside, across the floor.
    return [
        (0.0000, 0.0000),
        (0.0260, 0.0000),
        (0.0340, 0.0012),
        (0.0395, 0.0036),
        (0.0424, 0.0080),
        (0.0435, 0.0160),
        (0.0435, 0.0780),
        (0.0432, 0.0900),
        (0.0410, 0.1000),
        (0.0376, 0.1080),
        (0.0358, 0.1145),
        (0.0356, 0.1172),
        (0.0374, 0.1200),
        (0.0402, 0.1226),
        (0.0410, 0.1240),
        (0.0398, 0.1252),
        (0.0372, 0.1258),
        (0.0348, 0.1254),
        (0.0338, 0.1232),
        (0.0334, 0.1160),
        (0.0346, 0.1080),
        (0.0376, 0.0980),
        (0.0404, 0.0860),
        (0.0412, 0.0400),
        (0.0412, 0.0120),
        (0.0396, 0.0060),
        (0.0280, 0.0038),
        (0.0000, 0.0036),
    ]


def cork_profile():
    # Dome, chamfered cap, tapered plug. Top is the 14.0 cm line.
    return [
        (0.0000, 0.1400),
        (0.0200, 0.1404),
        (0.0320, 0.1396),
        (0.0368, 0.1384),
        (0.0388, 0.1368),
        (0.0396, 0.1352),
        (0.0394, 0.1304),
        (0.0382, 0.1270),
        (0.0352, 0.1260),
        (0.0336, 0.1244),
        (0.0334, 0.1180),
        (0.0328, 0.1125),
        (0.0000, 0.1125),
    ]


def soil_profile():
    return [
        (0.0000, 0.0300),
        (0.0180, 0.0292),
        (0.0320, 0.0274),
        (0.0385, 0.0248),
        (0.0406, 0.0220),
        (0.0408, 0.0100),
        (0.0388, 0.0052),
        (0.0000, 0.0042),
    ]


def pit_cork(obj):
    """Broad pits the vertex density can hold. Fine pores stay in the texture."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.normal_update()
    for v in bm.verts:
        p = v.co
        if p.z < 0.1265:
            continue
        radial = math.hypot(p.x, p.y)
        if radial < 0.004 and p.z < 0.138:
            continue
        coarse = noise.noise(p * 70.0 + Vector((2.0, 0.4, 0.2)))
        mid = noise.noise(p * 150.0 + Vector((0.2, 1.4, 0.7)))
        depth = max(0.0, coarse) ** 2 * 0.00055 + max(0.0, mid) * 0.00022
        v.co -= v.normal * depth
    bm.to_mesh(obj.data)
    bm.free()


def roughen_soil(obj):
    me = obj.data
    for v in me.vertices:
        if v.co.z < 0.020:
            continue
        n = noise.noise(Vector((v.co.x * 90.0, v.co.y * 90.0, 0.4)))
        v.co.z += n * 0.0011


def make_clump(center, radius, seed, flatten, subdiv=2):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(
        bm, subdivisions=subdiv, radius=1.0, matrix=Matrix.Identity(4), calc_uvs=False
    )
    spin = Euler((seed * 0.37, seed * 0.17, seed * 0.11), "XYZ")
    for v in bm.verts:
        p = v.co.copy()
        p.rotate(spin)
        n1 = noise.noise(p * 2.1 + Vector((seed * 0.13, 0.2, 0.4)))
        n2 = noise.noise(p * 5.4 + Vector((0.3, seed * 0.2, 1.1)))
        deform = max(0.55, 0.80 + 0.30 * n1 + 0.10 * n2)
        p.z *= flatten
        v.co = Vector(center) + p * (deform * radius)
    obj = finish(bm, f"Clump{seed}")
    consistent_normals(obj)
    return obj


def build_moss():
    import random
    rng = random.Random(11)
    specs = []
    # Large cushions, some stacked so the mound is not a dome.
    for i in range(5):
        ang = rng.random() * math.tau
        rad = rng.uniform(0.0, 0.012)
        radius = rng.uniform(0.012, 0.017)
        specs.append((rad, ang, radius, rng.uniform(0.50, 0.66), 0.030 + rng.uniform(0.0, 0.004), i + 1, 3))
    specs.append((0.004, 0.4, 0.011, 0.58, 0.040, 21, 3))
    specs.append((0.008, 2.2, 0.010, 0.52, 0.038, 22, 3))
    for i in range(6):
        ang = rng.random() * math.tau
        rad = rng.uniform(0.012, 0.024)
        radius = rng.uniform(0.008, 0.012)
        specs.append((rad, ang, radius, rng.uniform(0.48, 0.64), 0.028 + rng.uniform(0.0, 0.003), 30 + i, 3))
    for i in range(6):
        ang = (i / 6.0) * math.tau + rng.uniform(-0.25, 0.25)
        rad = rng.uniform(0.026, 0.034)
        radius = rng.uniform(0.005, 0.008)
        specs.append((rad, ang, radius, rng.uniform(0.45, 0.60), 0.026, 50 + i, 2))
    objs = []
    for rad, ang, radius, flat, z0, seed, subdiv in specs:
        cx = rad * math.cos(ang)
        cy = rad * math.sin(ang)
        # Keep the cushion inside the glass.
        limit = 0.0395 - radius * 0.85
        if math.hypot(cx, cy) > limit:
            scale = limit / max(1e-6, math.hypot(cx, cy))
            cx *= scale
            cy *= scale
        objs.append(make_clump((cx, cy, z0), radius, seed, flat, subdiv))
    activate(objs[0])
    for o in objs:
        o.select_set(True)
    bpy.ops.object.join()
    moss = bpy.context.view_layer.objects.active
    moss.name = "Moss"
    moss.data.name = "Moss"
    return moss


def grid_mesh(name, width, height, cols, rows, fold=0.0, arch=0.0, twist=0.0):
    """V-folded card. UV 0..1 carries an alpha-cut texture. Normal faces +Y."""
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    grid = []
    for j in range(rows + 1):
        v = j / rows
        row = []
        for i in range(cols + 1):
            u = i / cols
            x = (u - 0.5) * width
            y = -abs(u - 0.5) * 2.0 * fold - arch * (v ** 1.35)
            z = v * height
            ang = twist * v
            xr = x * math.cos(ang) - y * math.sin(ang)
            yr = x * math.sin(ang) + y * math.cos(ang)
            row.append(bm.verts.new((xr, yr, z)))
        grid.append(row)
    for j in range(rows):
        for i in range(cols):
            f = bm.faces.new((grid[j][i], grid[j + 1][i], grid[j + 1][i + 1], grid[j][i + 1]))
            u0, u1 = i / cols, (i + 1) / cols
            v0, v1 = j / rows, (j + 1) / rows
            coords = ((u0, v0), (u0, v1), (u1, v1), (u1, v0))
            for loop, uvw in zip(f.loops, coords):
                loop[uv].uv = uvw
    return finish(bm, name)


def moss_card_proto():
    return grid_mesh("MossCard", 0.007, 0.008, 3, 4, fold=0.0006, arch=0.0008, twist=0.2)


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


def build_skirt(proto):
    """A few small tufts lying on the mound. Upright cards read as a flat wall."""
    import random
    rng = random.Random(19)
    cards = []
    for i in range(8):
        ang = rng.uniform(0.0, math.tau)
        rad = rng.uniform(0.002, 0.016)
        z = 0.030 + rng.uniform(0.0, 0.006)
        tilt = rng.uniform(1.15, 1.45)
        card = duplicate(proto, linked=False)
        card.name = f"Skirt{i}"
        align_card(card, (rad * math.cos(ang), rad * math.sin(ang), z), ang, tilt)
        s = rng.uniform(0.45, 0.85)
        card.scale = (s * rng.uniform(0.8, 1.25), s, s * rng.uniform(0.75, 1.1))
        cards.append(card)
    bpy.context.view_layer.update()
    activate(cards[0])
    for c in cards:
        c.select_set(True)
    bpy.ops.object.join()
    skirt = bpy.context.view_layer.objects.active
    skirt.name = "MossSkirt"
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    skirt.data.name = "MossSkirt"
    return skirt


def fiddle_points(uncoil, stem_n=42, coil_n=70):
    pts = []
    stem_len = 0.040 + 0.034 * uncoil
    lean = 0.007 * (1.0 - 0.45 * uncoil)
    for i in range(stem_n):
        t = i / (stem_n - 1)
        z = stem_len * t
        x = lean * math.sin(t * math.pi)
        y = 0.0012 * math.sin(t * 2.4)
        pts.append(Vector((x, y, z)))
    top = pts[-1]
    turns = 2.50 * ((1.0 - uncoil) ** 0.90) + 0.16
    r0 = 0.0058 + 0.0126 * uncoil
    decay = 0.58
    for i in range(1, coil_n + 1):
        t = i / coil_n
        th = t * turns * 2.0 * math.pi
        r = r0 * math.exp(-decay * th / (2.0 * math.pi))
        ang = th + 0.35
        cx = top.x - r0 * 0.20
        cz = top.z + r0 * 0.62
        # A millimetre of drift keeps successive turns from occupying one surface.
        spiral = Vector((cx + r * math.sin(ang), top.y + 0.0016 * t, cz + r * math.cos(ang)))
        opened = Vector((
            top.x + 0.012 * math.sin(t * math.pi),
            top.y,
            top.z + (0.026 + 0.020 * uncoil) * t,
        ))
        # The stem end of the coil opens first. The tip stays curled.
        open_amt = smoothstep(uncoil * 1.35 - t)
        pts.append(spiral.lerp(opened, open_amt))
    return pts


def tube(name, pts, r_base=0.0017, r_tip=0.00075, ring=10):
    frames = []
    up = Vector((0.0, 1.0, 0.0))
    for i in range(len(pts)):
        if i == 0:
            tang = (pts[1] - pts[0]).normalized()
        elif i == len(pts) - 1:
            tang = (pts[i] - pts[i - 1]).normalized()
        else:
            tang = (pts[i + 1] - pts[i - 1]).normalized()
        # Stable frame: the crozier lies in XZ, so the thin axis stays along Y.
        # Parallel transport flipped the tube where the spiral tightened.
        bitan = tang.cross(up)
        if bitan.length < 1e-6:
            bitan = tang.cross(Vector((1.0, 0.0, 0.0)))
        bitan.normalize()
        normal = bitan.cross(tang).normalized()
        frames.append((tang, normal, bitan))
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    rings = []
    for i, p in enumerate(pts):
        t = i / (len(pts) - 1)
        _, nrm, bit = frames[i]
        radius = r_base + (r_tip - r_base) * (t ** 0.85)
        # Flatten the crozier: wider across the spiral, thinner through it.
        wide = radius * 1.55
        thin = radius * 0.62
        rings.append([
            bm.verts.new(p + (bit * math.cos(2 * math.pi * k / ring) + nrm * math.sin(2 * math.pi * k / ring) * (thin / wide)) * wide)
            for k in range(ring)
        ])
    for i in range(len(rings) - 1):
        for k in range(ring):
            f = bm.faces.new((
                rings[i][k], rings[i][(k + 1) % ring],
                rings[i + 1][(k + 1) % ring], rings[i + 1][k],
            ))
            u0, u1 = k / ring, (k + 1) / ring
            v0, v1 = i / len(rings), (i + 1) / len(rings)
            for loop, uvw in zip(f.loops, ((u0, v0), (u1, v0), (u1, v1), (u0, v1))):
                loop[uv].uv = uvw
    bm.faces.new(rings[0])
    bm.faces.new(list(reversed(rings[-1])))
    bm.normal_update()
    mid = len(pts) // 2
    outward = (rings[mid][0].co - pts[mid])
    if outward.length > 1e-8 and rings[mid][0].normal.dot(outward.normalized()) < 0:
        bmesh.ops.reverse_faces(bm, faces=bm.faces)
        print("[hero] tube flipped", name)
    return finish(bm, name)


def coil_diameter(uncoil):
    pts = fiddle_points(uncoil)
    coil = pts[42:]
    xs = [p.x for p in coil]
    zs = [p.z for p in coil]
    return max(xs) - min(xs), max(zs) - min(zs)


def add_leaf(bm, uv, origin, yaw, length, width, pitch):
    cols, rows = 4, 7
    grid = []
    for j in range(rows + 1):
        t = j / rows
        env = math.sin(math.pi * t) ** 0.72
        if t < 0.1:
            env *= t / 0.1
        half = width * env
        row = []
        for i in range(cols + 1):
            u = i / cols - 0.5
            x = u * 2.0 * half
            cup = (abs(u) * 2.0) ** 2 * width * 0.55
            y = -cup + (0.0003 if abs(u) < 0.2 else 0.0)
            z = length * t
            p = Vector((x, y, z))
            p.rotate(Euler((pitch, 0.0, yaw), "XYZ"))
            row.append(bm.verts.new(origin + p))
        grid.append(row)
    for j in range(rows):
        for i in range(cols):
            f = bm.faces.new((grid[j][i], grid[j + 1][i], grid[j + 1][i + 1], grid[j][i + 1]))
            u0, u1 = i / cols, (i + 1) / cols
            v0, v1 = j / rows, (j + 1) / rows
            for loop, uvw in zip(f.loops, ((u0, v0), (u0, v1), (u1, v1), (u1, v0))):
                loop[uv].uv = uvw


def build_seedling():
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    add_leaf(bm, uv, Vector((0.0, 0.0, 0.010)), 0.35, 0.014, 0.0055, -0.55)
    add_leaf(bm, uv, Vector((0.0, 0.0, 0.010)), 0.35 + math.pi, 0.013, 0.0050, -0.48)
    add_leaf(bm, uv, Vector((0.0, 0.0, 0.006)), 1.3, 0.009, 0.0036, -0.85)
    leaves = finish(bm, "SeedlingLeaves")
    stem_pts = [Vector((0.0, 0.0, 0.0)), Vector((0.0003, 0.0, 0.006)), Vector((0.0, 0.0, 0.012))]
    stem = tube("SeedlingStem", stem_pts, 0.00075, 0.00045, 6)
    activate(leaves)
    stem.select_set(True)
    bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.name = "Seedling"
    obj.data.name = "Seedling"
    return obj


def build_pebble(name, loc, scale, seed):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(
        bm, subdivisions=2, radius=1.0, matrix=Matrix.Identity(4), calc_uvs=False
    )
    for v in bm.verts:
        p = v.co.copy()
        n = noise.noise(p * 2.4 + Vector((seed, 0.2, 0.5)))
        p *= 0.82 + 0.28 * n
        p.z *= 0.58
        v.co = p
    # Sit the bottom on the given z.
    zs = [v.co.z for v in bm.verts]
    dz = min(zs)
    for v in bm.verts:
        v.co = (v.co - Vector((0.0, 0.0, dz))) * scale + Vector(loc)
    obj = finish(bm, name)
    consistent_normals(obj)
    return obj


def make_torus(name, major, minor, z, maj_n=64, min_n=8):
    bm = bmesh.new()
    rings = []
    for i in range(maj_n):
        a = 2 * math.pi * i / maj_n
        ring = []
        for j in range(min_n):
            b = 2 * math.pi * j / min_n
            r = major + minor * math.cos(b)
            ring.append(bm.verts.new((r * math.cos(a), r * math.sin(a), z + minor * math.sin(b))))
        rings.append(ring)
    for i in range(maj_n):
        for j in range(min_n):
            bm.faces.new((
                rings[i][j],
                rings[(i + 1) % maj_n][j],
                rings[(i + 1) % maj_n][(j + 1) % min_n],
                rings[i][(j + 1) % min_n],
            ))
    return finish(bm, name)


def make_plane(name, verts):
    bm = bmesh.new()
    vs = [bm.verts.new(Vector(p)) for p in verts]
    bm.faces.new(vs)
    return finish(bm, name)


# ---------------------------------------------------------------- materials
def sock(node, name, *fallbacks):
    for key in (name,) + fallbacks:
        if key in node.inputs:
            return node.inputs[key]
    print("[hero] sockets", node.name, [i.name for i in node.inputs])
    raise KeyError(name)


def new_mat(name):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.node_tree.nodes.clear()
    mat.use_backface_culling = False
    if hasattr(mat, "surface_render_method"):
        mat.surface_render_method = "BLENDED"
    if hasattr(mat, "blend_method"):
        mat.blend_method = "HASHED"
    return mat


def principled(mat):
    nt = mat.node_tree
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return nt, bsdf, out


def mat_glass(cond):
    mat = new_mat("Glass")
    nt = mat.node_tree
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    trans = nt.nodes.new("ShaderNodeBsdfTransparent")
    trans.inputs["Color"].default_value = hex_lin("D5F3EE")
    glossy = nt.nodes.new("ShaderNodeBsdfGlossy")
    glossy.inputs["Color"].default_value = hex_lin("E7F6F2")
    glossy.inputs["Roughness"].default_value = 0.045
    fres = nt.nodes.new("ShaderNodeFresnel")
    fres.inputs["IOR"].default_value = 1.46
    mix = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(fres.outputs["Fac"], mix.inputs["Fac"])
    nt.links.new(trans.outputs["BSDF"], mix.inputs[1])
    nt.links.new(glossy.outputs["BSDF"], mix.inputs[2])
    emit = nt.nodes.new("ShaderNodeEmission")
    emit.inputs["Color"].default_value = hex_lin("8FF0C8")
    emit.inputs["Strength"].default_value = 0.06
    add = nt.nodes.new("ShaderNodeAddShader")
    nt.links.new(mix.outputs["Shader"], add.inputs[0])
    nt.links.new(emit.outputs["Emission"], add.inputs[1])
    if cond is not None:
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = cond
        tex.projection = "BOX"
        bw = nt.nodes.new("ShaderNodeRGBToBW")
        bump = nt.nodes.new("ShaderNodeBump")
        sock(bump, "Strength").default_value = 0.55
        sock(bump, "Distance").default_value = 0.0012
        coord = nt.nodes.new("ShaderNodeTexCoord")
        mapping = nt.nodes.new("ShaderNodeMapping")
        sock(mapping, "Scale").default_value = (14.0, 14.0, 9.0)
        nt.links.new(coord.outputs["Object"], mapping.inputs["Vector"])
        nt.links.new(mapping.outputs["Vector"], tex.inputs["Vector"])
        nt.links.new(tex.outputs["Color"], bw.inputs["Color"])
        nt.links.new(bw.outputs["Val"], bump.inputs["Height"])
        nt.links.new(bump.outputs["Normal"], glossy.inputs["Normal"])
    nt.links.new(add.outputs["Shader"], out.inputs["Surface"])
    if hasattr(mat, "blend_method"):
        mat.blend_method = "BLEND"
    if hasattr(mat, "show_transparent_back"):
        mat.show_transparent_back = False
    return mat


def mat_cork(img):
    mat = new_mat("Cork")
    nt, bsdf, _out = principled(mat)
    if hasattr(mat, "blend_method"):
        mat.blend_method = "OPAQUE"
    if hasattr(mat, "surface_render_method"):
        mat.surface_render_method = "DITHERED"
    sock(bsdf, "Roughness").default_value = 0.82
    sock(bsdf, "Specular IOR Level", "Specular").default_value = 0.25
    if img is not None:
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = img
        coord = nt.nodes.new("ShaderNodeTexCoord")
        mapping = nt.nodes.new("ShaderNodeMapping")
        sock(mapping, "Scale").default_value = (9.0, 9.0, 6.0)
        nt.links.new(coord.outputs["Object"], mapping.inputs["Vector"])
        nt.links.new(mapping.outputs["Vector"], tex.inputs["Vector"])
        nt.links.new(tex.outputs["Color"], sock(bsdf, "Base Color"))
        bw = nt.nodes.new("ShaderNodeRGBToBW")
        bump = nt.nodes.new("ShaderNodeBump")
        sock(bump, "Strength").default_value = 0.35
        sock(bump, "Distance").default_value = 0.0008
        nt.links.new(tex.outputs["Color"], bw.inputs["Color"])
        nt.links.new(bw.outputs["Val"], bump.inputs["Height"])
        nt.links.new(bump.outputs["Normal"], sock(bsdf, "Normal"))
    else:
        sock(bsdf, "Base Color").default_value = hex_lin("8A5A3B")
    return mat


def mat_soil(img):
    mat = new_mat("Soil")
    nt, bsdf, _out = principled(mat)
    if hasattr(mat, "surface_render_method"):
        mat.surface_render_method = "DITHERED"
    sock(bsdf, "Roughness").default_value = 0.92
    if img is not None:
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = img
        coord = nt.nodes.new("ShaderNodeTexCoord")
        mapping = nt.nodes.new("ShaderNodeMapping")
        sock(mapping, "Scale").default_value = (11.0, 11.0, 8.0)
        nt.links.new(coord.outputs["Object"], mapping.inputs["Vector"])
        nt.links.new(mapping.outputs["Vector"], tex.inputs["Vector"])
        nt.links.new(tex.outputs["Color"], sock(bsdf, "Base Color"))
    else:
        sock(bsdf, "Base Color").default_value = hex_lin("0D231D")
    return mat


def mat_moss(img):
    mat = new_mat("Moss")
    nt, bsdf, _out = principled(mat)
    if hasattr(mat, "surface_render_method"):
        mat.surface_render_method = "DITHERED"
    sock(bsdf, "Roughness").default_value = 0.78
    coord = nt.nodes.new("ShaderNodeTexCoord")
    mapping = nt.nodes.new("ShaderNodeMapping")
    sock(mapping, "Scale").default_value = (16.0, 16.0, 16.0)
    nt.links.new(coord.outputs["Object"], mapping.inputs["Vector"])
    if img is not None:
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = img
        tex.projection = "BOX"
        tex.projection_blend = 0.25
        nt.links.new(mapping.outputs["Vector"], tex.inputs["Vector"])
        nt.links.new(tex.outputs["Color"], sock(bsdf, "Base Color"))
    else:
        sock(bsdf, "Base Color").default_value = hex_lin("2C5B45")
    geom = nt.nodes.new("ShaderNodeNewGeometry")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(geom.outputs["Normal"], sep.inputs["Vector"])
    up = nt.nodes.new("ShaderNodeMath")
    up.operation = "MAXIMUM"
    up.inputs[1].default_value = 0.0
    nt.links.new(sep.outputs["Z"], up.inputs[0])
    noise_tex = nt.nodes.new("ShaderNodeTexNoise")
    noise_tex.inputs["Scale"].default_value = 18.0
    noise_tex.inputs["Detail"].default_value = 6.0
    nt.links.new(mapping.outputs["Vector"], noise_tex.inputs["Vector"])
    mult = nt.nodes.new("ShaderNodeMath")
    mult.operation = "MULTIPLY"
    nt.links.new(up.outputs["Value"], mult.inputs[0])
    nt.links.new(noise_tex.outputs["Fac"], mult.inputs[1])
    strength = nt.nodes.new("ShaderNodeMath")
    strength.operation = "MULTIPLY"
    strength.inputs[1].default_value = 0.85
    nt.links.new(mult.outputs["Value"], strength.inputs[0])
    sock(bsdf, "Emission Color", "Emission").default_value = hex_lin("8FF0C8")
    nt.links.new(strength.outputs["Value"], sock(bsdf, "Emission Strength"))
    return mat


def mat_tuft(img):
    mat = new_mat("MossTuft")
    nt = mat.node_tree
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    trans = nt.nodes.new("ShaderNodeBsdfTransparent")
    mix = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(bsdf.outputs["BSDF"], mix.inputs[2])
    nt.links.new(trans.outputs["BSDF"], mix.inputs[1])
    nt.links.new(mix.outputs["Shader"], out.inputs["Surface"])
    sock(bsdf, "Roughness").default_value = 0.7
    sock(bsdf, "Emission Color", "Emission").default_value = hex_lin("8FF0C8")
    sock(bsdf, "Emission Strength").default_value = 0.0
    if img is not None:
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = img
        nt.links.new(tex.outputs["Color"], sock(bsdf, "Base Color"))
        clip = nt.nodes.new("ShaderNodeMath")
        clip.operation = "GREATER_THAN"
        clip.inputs[1].default_value = 0.55
        nt.links.new(tex.outputs["Alpha"], clip.inputs[0])
        nt.links.new(clip.outputs["Value"], mix.inputs["Fac"])
    else:
        sock(bsdf, "Base Color").default_value = hex_lin("3E8F55")
        mix.inputs["Fac"].default_value = 1.0
    if hasattr(mat, "surface_render_method"):
        mat.surface_render_method = "DITHERED"
    if hasattr(mat, "blend_method"):
        mat.blend_method = "CLIP"
        if hasattr(mat, "alpha_threshold"):
            mat.alpha_threshold = 0.55
    return mat


def mat_frond(albedo, emission):
    mat = new_mat("Frond")
    nt = mat.node_tree
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    trans = nt.nodes.new("ShaderNodeBsdfTransparent")
    mix = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(trans.outputs["BSDF"], mix.inputs[1])
    nt.links.new(bsdf.outputs["BSDF"], mix.inputs[2])
    nt.links.new(mix.outputs["Shader"], out.inputs["Surface"])
    sock(bsdf, "Roughness").default_value = 0.45
    sock(bsdf, "Emission Color", "Emission").default_value = hex_lin("8FF0C8")
    if albedo is not None:
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = albedo
        nt.links.new(tex.outputs["Color"], sock(bsdf, "Base Color"))
        clip = nt.nodes.new("ShaderNodeMath")
        clip.operation = "GREATER_THAN"
        clip.inputs[1].default_value = 0.40
        nt.links.new(tex.outputs["Alpha"], clip.inputs[0])
        nt.links.new(clip.outputs["Value"], mix.inputs["Fac"])
    else:
        sock(bsdf, "Base Color").default_value = hex_lin("8FBF6A")
        mix.inputs["Fac"].default_value = 1.0
    if emission is not None:
        etex = nt.nodes.new("ShaderNodeTexImage")
        etex.image = emission
        bw = nt.nodes.new("ShaderNodeRGBToBW")
        mul = nt.nodes.new("ShaderNodeMath")
        mul.operation = "MULTIPLY"
        mul.inputs[1].default_value = 1.15
        nt.links.new(etex.outputs["Color"], bw.inputs["Color"])
        nt.links.new(bw.outputs["Val"], mul.inputs[0])
        nt.links.new(mul.outputs["Value"], sock(bsdf, "Emission Strength"))
    else:
        sock(bsdf, "Emission Strength").default_value = 1.2
    if hasattr(mat, "blend_method"):
        mat.blend_method = "CLIP"
        if hasattr(mat, "alpha_threshold"):
            mat.alpha_threshold = 0.4
    return mat


def mat_fiddle():
    mat = new_mat("Fiddle")
    nt, bsdf, _out = principled(mat)
    if hasattr(mat, "surface_render_method"):
        mat.surface_render_method = "DITHERED"
    sock(bsdf, "Base Color").default_value = hex_lin("A9D46A")
    sock(bsdf, "Roughness").default_value = 0.42
    sock(bsdf, "Emission Color", "Emission").default_value = hex_lin("C6E86A")
    coord = nt.nodes.new("ShaderNodeTexCoord")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(coord.outputs["Object"], sep.inputs["Vector"])
    ramp = nt.nodes.new("ShaderNodeMapRange")
    sock(ramp, "From Min").default_value = 0.01
    sock(ramp, "From Max").default_value = 0.07
    sock(ramp, "To Min").default_value = 0.25
    sock(ramp, "To Max").default_value = 1.05
    nt.links.new(sep.outputs["Z"], sock(ramp, "Value"))
    nt.links.new(ramp.outputs["Result"], sock(bsdf, "Emission Strength"))
    return mat


def mat_seed():
    mat = new_mat("Seedling")
    _nt, bsdf, _out = principled(mat)
    if hasattr(mat, "surface_render_method"):
        mat.surface_render_method = "DITHERED"
    sock(bsdf, "Base Color").default_value = hex_lin("7EBF62")
    sock(bsdf, "Roughness").default_value = 0.48
    sock(bsdf, "Emission Color", "Emission").default_value = hex_lin("C8F0A4")
    sock(bsdf, "Emission Strength").default_value = 0.9
    return mat


def mat_pebble():
    mat = new_mat("Pebble")
    _nt, bsdf, _out = principled(mat)
    if hasattr(mat, "surface_render_method"):
        mat.surface_render_method = "DITHERED"
    sock(bsdf, "Base Color").default_value = hex_lin("1C2622")
    sock(bsdf, "Roughness").default_value = 0.38
    sock(bsdf, "Specular IOR Level", "Specular").default_value = 0.45
    return mat


def mat_desk():
    mat = new_mat("Desk")
    _nt, bsdf, _out = principled(mat)
    if hasattr(mat, "surface_render_method"):
        mat.surface_render_method = "DITHERED"
    sock(bsdf, "Base Color").default_value = hex_lin("111514")
    sock(bsdf, "Roughness").default_value = 0.58
    return mat


def mat_wall():
    mat = new_mat("Wall")
    _nt, bsdf, _out = principled(mat)
    if hasattr(mat, "surface_render_method"):
        mat.surface_render_method = "DITHERED"
    sock(bsdf, "Base Color").default_value = hex_lin("0E1A1C")
    sock(bsdf, "Roughness").default_value = 0.9
    return mat


def mat_emit(name, color, strength):
    mat = new_mat(name)
    nt = mat.node_tree
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    emit = nt.nodes.new("ShaderNodeEmission")
    emit.inputs["Color"].default_value = hex_lin(color)
    emit.inputs["Strength"].default_value = strength
    nt.links.new(emit.outputs["Emission"], out.inputs["Surface"])
    if hasattr(mat, "surface_render_method"):
        mat.surface_render_method = "DITHERED"
    return mat


def assign(obj, mat):
    obj.data.materials.clear()
    obj.data.materials.append(mat)


def point_light(name, color, energy, loc, shadow=True, radius=0.02):
    light = bpy.data.lights.new(name, "POINT")
    light.color = hex_lin(color)[:3]
    light.energy = energy
    light.shadow_soft_size = radius
    light.use_shadow = shadow
    obj = bpy.data.objects.new(name, light)
    obj.location = loc
    bpy.context.collection.objects.link(obj)
    return obj


# ---------------------------------------------------------------- camera
def project(scene, cam, co):
    from bpy_extras.object_utils import world_to_camera_view
    ndc = world_to_camera_view(scene, cam, Vector(co))
    x = ndc.x * scene.render.resolution_x
    y = (1.0 - ndc.y) * scene.render.resolution_y
    return x, y


def aim(cam, look, distance, elev_deg):
    e = math.radians(elev_deg)
    cam.location = Vector(look) + Vector((0.0, math.cos(e), math.sin(e))) * distance
    direction = Vector(look) - cam.location
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    bpy.context.view_layer.update()


def frame_camera(scene, cam):
    """28.2 deg vertical FOV, ~13 deg elevation, 4550 px/m, jar base near (915, 830)."""
    cam.data.sensor_fit = "VERTICAL"
    cam.data.angle = math.radians(28.2)
    cam.data.clip_start = 0.01
    cam.data.clip_end = 20.0
    distance = 0.45
    look_z = 0.07
    for _ in range(6):
        lo, hi = 0.02, 0.12
        for _ in range(18):
            mid = 0.5 * (lo + hi)
            aim(cam, (0.0, 0.0, mid), distance, 13.0)
            _x, y = project(scene, cam, (0.0, 0.0, 0.0))
            if y > 830:
                hi = mid
            else:
                lo = mid
        look_z = 0.5 * (lo + hi)
        aim(cam, (0.0, 0.0, look_z), distance, 13.0)
        x0, y0 = project(scene, cam, (0.0, 0.0, look_z))
        x1, y1 = project(scene, cam, (0.01, 0.0, look_z))
        ppm = math.hypot(x1 - x0, y1 - y0) / 0.01
        if ppm <= 1.0:
            break
        distance *= ppm / 4550.0
    bx, by = project(scene, cam, (0.0, 0.0, 0.0))
    tx, ty = project(scene, cam, (0.0, 0.0, JAR_HEIGHT))
    lx, ly = project(scene, cam, (-JAR_RADIUS, 0.0, 0.06))
    rx, ry = project(scene, cam, (JAR_RADIUS, 0.0, 0.06))
    print(f"[hero] camera loc {tuple(round(c, 4) for c in cam.location)} look_z {look_z:.4f} D {distance:.4f}")
    print(f"[hero] project base ({bx:.1f}, {by:.1f}) top ({tx:.1f}, {ty:.1f}) height_px {by - ty:.1f}")
    print(f"[hero] project diameter_px {rx - lx:.1f} ppm target 4550")
    return cam


def look_at(cam, target):
    direction = Vector(target) - cam.location
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    bpy.context.view_layer.update()


def hide(objs, hidden):
    for o in objs:
        o.hide_render = hidden


# ---------------------------------------------------------------- export / render
def export_fbx(objs, filename):
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.hide_render = False
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    path = os.path.join(MOD, filename)
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
        path_mode="AUTO",
        global_scale=1.0,
    )
    print("[hero] exported", filename, [o.name for o in objs])


def setup_engine(scene):
    engines = [e.identifier for e in scene.render.bl_rna.properties["engine"].enum_items]
    print("[hero] engines", engines)
    scene.render.engine = "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in engines else "BLENDER_EEVEE"
    ee = scene.eevee
    for attr, val in (("taa_render_samples", SAMPLES), ("taa_samples", 16)):
        if hasattr(ee, attr):
            setattr(ee, attr, val)
    scene.render.resolution_x = 1824
    scene.render.resolution_y = 1024
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = False
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGB"
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "None"
    scene.view_settings.exposure = 0.15
    world = bpy.data.worlds.new("Night")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = hex_lin("0C1A1F")
    bg.inputs["Strength"].default_value = 0.55


def render_to(scene, path):
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("[hero] rendered", path)


def verify_fbx(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    print("[hero] reimport", os.path.basename(path))
    for o in bpy.context.scene.objects:
        if o.type != "MESH":
            print("   ", o.name, o.type, "loc", tuple(round(c, 4) for c in o.location))
            continue
        zs, rs, ys = [], [], []
        mw = o.matrix_world
        for v in o.data.vertices:
            w = mw @ v.co
            zs.append(w.z)
            ys.append(w.y)
            rs.append(math.hypot(w.x, w.y))
        if not zs:
            continue
        print(
            f"    {o.name} z {min(zs):.4f}..{max(zs):.4f} y {min(ys):.4f}..{max(ys):.4f} "
            f"r {max(rs):.4f} tris {tri_count(o)}"
        )


def main():
    os.makedirs(MOD, exist_ok=True)
    os.makedirs(RUN, exist_ok=True)
    print("[hero] blender", bpy.app.version_string)
    scene = reset()

    jar = lathe("Jar", jar_profile(), 112)
    consistent_normals(jar)
    ensure_outward(jar)
    cork = lathe("Cork", cork_profile(), 128)
    consistent_normals(cork)
    ensure_outward(cork)
    pit_cork(cork)
    consistent_normals(cork)
    soil = lathe("Soil", soil_profile(), 80)
    roughen_soil(soil)
    consistent_normals(soil)
    ensure_outward(soil)
    moss = build_moss()
    card = moss_card_proto()
    consistent_normals(card)
    skirt = build_skirt(card)

    fern_a = load_image("fern_a_albedo.png")
    fern_b = load_image("fern_b_albedo.png")
    aspect_a = 0.42
    aspect_b = 0.48
    if fern_a is not None and fern_a.size[1]:
        aspect_a = fern_a.size[0] / fern_a.size[1]
    if fern_b is not None and fern_b.size[1]:
        aspect_b = fern_b.size[0] / fern_b.size[1]
    frond_a = grid_mesh("FrondA", 0.070 * aspect_a, 0.070, 8, 24, fold=0.0065, arch=0.012, twist=0.10)
    frond_b = grid_mesh("FrondB", 0.062 * aspect_b, 0.062, 8, 24, fold=0.0055, arch=0.009, twist=-0.16)
    consistent_normals(frond_a)
    consistent_normals(frond_b)

    states = (("Fiddle00", 0.00), ("Fiddle33", 0.33), ("Fiddle50", 0.50), ("Fiddle75", 0.75), ("Fiddle100", 1.00))
    fiddles = []
    for name, u in states:
        dx, dz = coil_diameter(u)
        print(f"[hero] {name} coil bbox x {dx * 100:.2f} cm z {dz * 100:.2f} cm")
        obj = tube(name, fiddle_points(u))
        consistent_normals(obj)
        fiddles.append(obj)

    seedling = build_seedling()
    consistent_normals(seedling)
    pebbles = [
        build_pebble("PebbleA", (0.022, 0.024, 0.0245), 0.0062, 1.0),
        build_pebble("PebbleB", (-0.012, 0.030, 0.0240), 0.0048, 2.0),
        build_pebble("PebbleC", (0.031, 0.006, 0.0248), 0.0054, 3.0),
        build_pebble("PebbleD", (-0.028, 0.014, 0.0242), 0.0044, 4.0),
    ]

    heroes = [jar, cork, soil, moss, skirt, card, frond_a, frond_b, seedling] + fiddles + pebbles
    print("[hero] tris (exported meshes, one each)")
    total = 0
    for o in heroes:
        n = tri_count(o)
        total += n
        z0, z1, r = bounds(o)
        print(f"[hero] tris {o.name} {n}  z {z0:.4f}..{z1:.4f} r {r:.4f}")
    print(f"[hero] tris ALL_FILES {total}")
    # G1 draws one fiddle state, two fronds, two seedlings. The other fiddle states are alternates.
    g1_names = {
        "Jar", "Cork", "Soil", "Moss", "MossSkirt", "FrondA", "FrondB", "Fiddle33", "Seedling",
        "PebbleA", "PebbleB", "PebbleC", "PebbleD",
    }
    g1 = sum(tri_count(o) for o in heroes if o.name in g1_names) + tri_count(seedling)
    print(f"[hero] tris G1_SCENE {g1} (two seedlings, one fiddle state, skirt included)")
    z0, z1, r = bounds(jar)
    cz0, cz1, _cr = bounds(cork)
    print(f"[hero] jar height {z1 - z0:.4f} m diameter {2 * r:.4f} m")
    print(f"[hero] assembly height {cz1:.4f} m cork visible {cz1 - z1:.4f} m")

    exports = [
        ([jar], "Jar.fbx"),
        ([cork], "Cork.fbx"),
        ([soil], "Soil.fbx"),
        ([moss], "Moss.fbx"),
        ([skirt], "MossSkirt.fbx"),
        ([card], "MossCard.fbx"),
        ([frond_a], "FrondA.fbx"),
        ([frond_b], "FrondB.fbx"),
        ([seedling], "Seedling.fbx"),
        (pebbles, "Pebbles.fbx"),
    ]
    for obj, _u in zip(fiddles, [s[1] for s in states]):
        exports.append(([obj], f"{obj.name}.fbx"))
    for objs, filename in exports:
        export_fbx(objs, filename)

    if SKIP_RENDER:
        print("[hero] skip render")
        verify_fbx(os.path.join(MOD, "Jar.fbx"))
        return

    # Preview duplicates. Originals of placeable plants stay at the origin for export.
    left = duplicate(frond_a)
    left.name = "PrevFrondA"
    left.hide_render = False
    left.location = (-0.011, 0.006, 0.034)
    left.rotation_euler = Euler((0.22, -0.42, 0.30), "XYZ")
    right = duplicate(frond_b)
    right.name = "PrevFrondB"
    right.hide_render = False
    right.location = (0.013, 0.004, 0.033)
    right.rotation_euler = Euler((0.18, 0.50, -0.28), "XYZ")
    fiddle = duplicate(fiddles[1])
    fiddle.name = "PrevFiddle"
    fiddle.hide_render = False
    fiddle.location = (0.001, -0.001, 0.036)
    fiddle.rotation_euler = Euler((0.05, 0.0, 0.15), "XYZ")
    sprout_a = duplicate(seedling)
    sprout_a.name = "PrevSeedA"
    sprout_a.hide_render = False
    sprout_a.location = (0.004, 0.016, 0.031)
    sprout_a.rotation_euler = Euler((0.35, 0.0, 0.4), "XYZ")
    sprout_b = duplicate(seedling)
    sprout_b.name = "PrevSeedB"
    sprout_b.hide_render = False
    sprout_b.location = (-0.016, 0.010, 0.030)
    sprout_b.rotation_euler = Euler((0.2, 0.1, -0.6), "XYZ")
    sprout_b.scale = (0.75, 0.75, 0.75)
    # Copies inherit hide_render. Hide the origin masters after the copies exist.
    frond_a.hide_render = True
    frond_b.hide_render = True
    for f in fiddles:
        f.hide_render = True
    seedling.hide_render = True
    card.hide_render = True

    import random
    rng = random.Random(5)
    spore_proto = None
    spores = []
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=1, radius=1.0, matrix=Matrix.Identity(4), calc_uvs=False)
    spore_proto = finish(bm, "SporeProto")
    spore_proto.hide_render = True
    for i in range(16):
        ang = rng.random() * math.tau
        rad = rng.uniform(0.008, 0.055)
        z = rng.uniform(0.06, 0.15)
        s = rng.uniform(0.0011, 0.0024)
        sp = duplicate(spore_proto)
        sp.name = f"PrevSpore{i}"
        sp.location = (rad * math.cos(ang), rad * math.sin(ang), z)
        sp.scale = (s, s, s)
        spores.append(sp)

    ring = make_torus("PrevRing", 0.060, 0.00115, 0.0012)
    desk = make_plane("PrevDesk", ((-0.9, -0.7, -0.0004), (0.9, -0.7, -0.0004), (0.9, 0.9, -0.0004), (-0.9, 0.9, -0.0004)))
    wall = make_plane("PrevWall", ((-1.1, -0.55, 0.0), (1.1, -0.55, 0.0), (1.1, -0.55, 1.15), (-1.1, -0.55, 1.15)))
    lamp_mesh = None
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=2, radius=0.035, matrix=Matrix.Identity(4), calc_uvs=False)
    lamp_mesh = finish(bm, "PrevLamp")
    lamp_mesh.location = (-0.22, -0.20, 0.42)

    moss_img = load_image("moss_macro.png")
    tuft_img = load_image("moss_tuft.png")
    cork_img = load_image("cork.png")
    soil_img = load_image("soil.png")
    cond_img = load_image("condensation.png")
    emit_a = load_image("fern_a_emission.png")
    emit_b = load_image("fern_b_emission.png")

    assign(jar, mat_glass(cond_img))
    assign(cork, mat_cork(cork_img))
    assign(soil, mat_soil(soil_img))
    assign(moss, mat_moss(moss_img))
    assign(skirt, mat_tuft(tuft_img))
    assign(card, mat_tuft(tuft_img))
    assign(frond_a, mat_frond(fern_a, emit_a))
    assign(frond_b, mat_frond(fern_b, emit_b))
    for f in fiddles:
        assign(f, mat_fiddle())
    assign(seedling, mat_seed())
    pebble_mat = mat_pebble()
    for p in pebbles:
        assign(p, pebble_mat)
    assign(desk, mat_desk())
    assign(wall, mat_wall())
    assign(ring, mat_emit("Ring", "8FF0C8", 4.0))
    assign(spore_proto, mat_emit("Spore", "F2D27A", 18.0))
    assign(lamp_mesh, mat_emit("Lamp", "D99F67", 2.2))

    jar.visible_shadow = False
    skirt.visible_shadow = False
    frond_a.visible_shadow = False
    frond_b.visible_shadow = False

    point_light("JarGlow", "8FF0C8", 1.6, (0.0, 0.0, 0.07), shadow=False, radius=0.03)
    point_light("LampLight", "D99F67", 3.0, (-0.28, -0.15, 0.48), shadow=True, radius=0.08)
    point_light("Rim", "6FB7C9", 0.6, (0.05, 0.20, 0.16), shadow=False, radius=0.05)

    setup_engine(scene)
    cam_data = bpy.data.cameras.new("JarCam")
    cam = bpy.data.objects.new("JarCam", cam_data)
    bpy.context.collection.objects.link(cam)
    scene.camera = cam
    frame_camera(scene, cam)
    cam.data.dof.use_dof = True
    cam.data.dof.focus_distance = (Vector((0.0, 0.0, 0.07)) - cam.location).length
    cam.data.dof.aperture_fstop = 5.6

    render_to(scene, os.path.join(RUN, "preview.png"))

    # Close-ups of the authored surfaces, glass hidden so the lace and clumps read.
    plants = [left, right, fiddle, sprout_a, sprout_b, moss, skirt, soil] + pebbles
    preview_only = [ring, lamp_mesh, desk, wall] + spores
    cam.data.dof.use_dof = False

    scene.render.resolution_x = 1200
    scene.render.resolution_y = 900
    hide([jar, cork, fiddle, left, right, sprout_a, sprout_b] + preview_only, True)
    hide([moss, skirt, soil] + pebbles, False)
    cam.data.angle = math.radians(32.0)
    cam.location = (0.028, 0.105, 0.062)
    look_at(cam, (0.0, 0.0, 0.036))
    render_to(scene, os.path.join(RUN, "closeup-moss.png"))

    hide(plants + [jar, cork] + preview_only, True)
    hide([left], False)
    bpy.context.view_layer.update()
    center = sum((left.matrix_world @ Vector(c) for c in left.bound_box), Vector()) / 8.0
    cam.data.angle = math.radians(22.0)
    cam.location = center + Vector((0.01, 0.055, 0.012))
    look_at(cam, center)
    render_to(scene, os.path.join(RUN, "closeup-frond.png"))

    hide([left], True)
    hide([fiddle, moss], False)
    bpy.context.view_layer.update()
    center = sum((fiddle.matrix_world @ Vector(c) for c in fiddle.bound_box), Vector()) / 8.0
    # Bias the target toward the top of the bounds, where the coil is.
    top = max((fiddle.matrix_world @ Vector(c)).z for c in fiddle.bound_box)
    target = Vector((center.x, center.y, (center.z + top) * 0.5))
    cam.data.angle = math.radians(24.0)
    cam.location = target + Vector((0.028, 0.072, 0.018))
    look_at(cam, target)
    render_to(scene, os.path.join(RUN, "closeup-fiddlehead.png"))

    verify_fbx(os.path.join(MOD, "Jar.fbx"))
    verify_fbx(os.path.join(MOD, "Fiddle33.fbx"))


if __name__ == "__main__":
    try:
        main()
    except Exception:
        traceback.print_exc()
        sys.exit(1)

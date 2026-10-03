"""Blender 4.2, headless: blender -b -P tools/blender/terrarium_jar.py -- --out <dir>

The night-jar half of blender_heroes.py (Jar, Cork, Soil, Moss, Fiddle0..100, Frond, Seedling, SeedStem).
Units are metres. Blender Z is up; the FBX exporter writes Unity Y-up. Default --out is
apps/terrarium/Assets/Art/Models, resolved from the current working directory.
"""
import bpy, bmesh, math, os, sys
from mathutils import Euler, Vector, noise

_HERE = os.path.dirname(os.path.abspath(__file__))
if _HERE not in sys.path:
    sys.path.insert(0, _HERE)
import terrarium_moss

random_seed = 4
import random
random.seed(random_seed)


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
print("[hero] out", OUT)


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def obj_from_bm(bm, name):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(ob)
    for p in me.polygons: p.use_smooth = True
    return ob


def lathe(name, profile, segs=96, uv_v=None):
    """Revolve an (r, y) profile around Y. UV: u = angle, v = profile parameter (or uv_v per point)."""
    bm = bmesh.new(); uv = bm.loops.layers.uv.new()
    rows = []
    for i, (r, y) in enumerate(profile):
        row = []
        for s in range(segs + 1):
            a = 2 * math.pi * s / segs
            row.append(bm.verts.new((r * math.cos(a), -r * math.sin(a), y)))  # Blender Z-up; exported as Y-up
        rows.append(row)
    n = len(profile)
    vv = uv_v or [i / (n - 1) for i in range(n)]
    for i in range(n - 1):
        for s in range(segs):
            f = bm.faces.new((rows[i][s], rows[i][s + 1], rows[i + 1][s + 1], rows[i + 1][s]))
            for l, (uu, v) in zip(f.loops, ((s / segs, vv[i]), ((s + 1) / segs, vv[i]), ((s + 1) / segs, vv[i + 1]), (s / segs, vv[i + 1]))):
                l[uv].uv = (uu, v)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-7)
    return obj_from_bm(bm, name)


def export(names, path):
    bpy.ops.object.select_all(action='DESELECT')
    for n in names: bpy.data.objects[n].select_set(True)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                             object_types={'MESH'}, use_mesh_modifiers=True, mesh_smooth_type='FACE', bake_anim=False,
                             add_leaf_bones=False)
    print('[hero] exported', path, names)


# =========================================================================== JAR
reset()
# Style bible (docs/art/terrarium-style.md): 14.0 cm with the cork, 8.7 cm across, cork 1.5 cm.
# Body radius 4.50 cm is 9.0 cm across, inside the +5% band (max 9.14 cm), so the ferns
# still clear the wall. The profile is a lathe: straight wall, a quarter-ellipse shoulder
# (radius only decreases), a straight neck, then a 2 mm lip. No S-curve, so the neck
# cannot pouch out on the way into the mouth. One outer shell plus a short inner lip.
# An inner cylinder in front of the moss was dropped in T-TER-018.
BODY_R = 0.0450
NECK_R = 0.0340
LIP_TOP = 0.1250
CORK_TOP = 0.1400
WALL_TOP = 0.0900
SHOULDER_TOP = 0.1080
NECK_TOP = 0.1180


def mason_outer():
    """(radius, height) up the outside, then a short lip folded into the mouth.

    The shoulder is a quarter ellipse, so the tangent is vertical at the wall and
    again at the neck, and the radius falls the whole way. The neck that follows
    is a constant radius. The lip bead is 2.2 mm and returns to the neck.
    """
    pts = [(0.0, 0.0)]
    corner = 0.0150
    flat = BODY_R - corner
    pts.append((flat * 0.55, 0.00025))
    pts.append((flat, 0.0005))
    for i in range(1, 7):
        ang = (i / 6.0) * math.pi * 0.5
        r = flat + corner * math.sin(ang)
        y = 0.0005 + corner * (1.0 - math.cos(ang))
        pts.append((r, y))
    pts.append((BODY_R, WALL_TOP))
    rx = BODY_R - NECK_R
    ry = SHOULDER_TOP - WALL_TOP
    for i in range(1, 9):
        ang = (i / 8.0) * math.pi * 0.5
        r = NECK_R + rx * math.cos(ang)
        y = WALL_TOP + ry * math.sin(ang)
        pts.append((r, y))
    # A sample on the neck so the straight run is a real row, not only its endpoints.
    pts.append((NECK_R, (SHOULDER_TOP + NECK_TOP) * 0.5))
    pts.append((NECK_R, NECK_TOP))
    lip = NECK_R + 0.0022
    pts.extend([
        (NECK_R + 0.0008, 0.1194),
        (lip, 0.1212),
        (lip + 0.0003, 0.1228),
        (lip - 0.0004, 0.1242),
        (NECK_R + 0.0006, LIP_TOP),
    ])
    # Short inner face so the lip has a thickness. It stops at the top of the neck.
    pts.extend([
        (NECK_R - 0.0018, 0.1238),
        (NECK_R - 0.0024, 0.1216),
        (NECK_R - 0.0016, 0.1196),
    ])
    return pts


def assert_profile(profile):
    """The outer profile, before the lip folds inward, is a mason jar."""
    fold = len(profile)
    for i in range(1, len(profile)):
        if profile[i][1] < profile[i - 1][1] - 1e-6:
            fold = i
            break
    outer = profile[:fold]
    # The straight run is one edge at BODY_R, from the heel up to the shoulder.
    wall = [p for p in outer if abs(p[0] - BODY_R) <= 0.0002 and p[1] <= WALL_TOP + 1e-6]
    if len(wall) < 2 or max(p[1] for p in wall) - min(p[1] for p in wall) < 0.05:
        raise SystemExit("wall is not a straight run of at least 5 cm")
    shoulder = [p for p in outer if WALL_TOP - 1e-6 <= p[1] <= SHOULDER_TOP + 1e-6]
    if len(shoulder) < 4:
        raise SystemExit("shoulder has too few points")
    for i in range(1, len(shoulder)):
        if shoulder[i][0] > shoulder[i - 1][0] + 1e-6:
            raise SystemExit("shoulder radius increases (pouch) at y %.4f" % shoulder[i][1])
    if shoulder[0][0] - shoulder[-1][0] < 0.008:
        raise SystemExit("shoulder does not narrow into the neck")
    neck = [p for p in outer if SHOULDER_TOP - 1e-6 <= p[1] <= NECK_TOP + 1e-6]
    if len(neck) < 2 or any(abs(r - NECK_R) > 0.0004 for r, y in neck):
        raise SystemExit("neck is not a short straight cylinder")
    lip_pts = [p for p in outer if p[1] > NECK_TOP + 1e-6]
    if not lip_pts or max(r for r, y in lip_pts) > NECK_R + 0.0035:
        raise SystemExit("lip bead is wider than 3.5 mm")
    print("[hero] profile wall %.4f from %.4f to %.4f shoulder %.4f..%.4f neck %.4f lip %.4f" % (
        wall[0][0], min(p[1] for p in wall), max(p[1] for p in wall),
        shoulder[0][1], shoulder[-1][1], neck[-1][0], max(r for r, y in lip_pts)))


outer = mason_outer()
assert_profile(outer)
lathe("Jar", outer, 128)
print("[hero] jar profile")
for r, y in outer:
    print("[hero]   r {r:.4f} y {y:.4f}".format(r=r, y=y))


def drop_back_glass():
    """JarG1 never shades faces that point away from the eye. The glass back pass is off.

    Overdraw swaps in Fidelity/Overdraw, which is Cull Off, so those faces still add a
    layer. Drop them. Faces that face the eye, including the near lip, stay, so the
    rim ellipse holds from the hero camera.

    Export maps Blender (x, y, z) to Unity (-x, z, y). The JarG1 eye
    (0, 0.175, -0.44) is Blender (0, -0.44, 0.175).
    """
    obj = bpy.data.objects["Jar"]
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.normal_update()
    cam = Vector((0.0, -0.44, 0.175))
    doomed = []
    for face in bm.faces:
        view = cam - face.calc_center_median()
        if view.dot(face.normal) <= 0.05:
            doomed.append(face)
    if len(doomed) < 8 or len(doomed) > len(bm.faces) - 8:
        raise SystemExit("glass cull would delete %d of %d faces" % (len(doomed), len(bm.faces)))
    bmesh.ops.delete(bm, geom=doomed, context="FACES")
    bm.to_mesh(obj.data)
    obj.data.update()
    bm.free()
    print("[hero] glass back faces dropped", len(doomed))


drop_back_glass()
# Cork cap is 1.5 cm above the lip. The plug continues down inside the neck.
# Cap sits on the lip. The plug is inside the straight neck. Same UV scheme as before.
cork = [
    (0.0, CORK_TOP), (0.0240, CORK_TOP), (0.0316, 0.1393), (0.0346, 0.1380),
    (0.0355, 0.1362), (0.0348, 0.1334), (0.0334, 0.1298), (0.0322, 0.1264),
    (0.0308, 0.1246), (0.0308, 0.1168), (0.0316, 0.1148), (0.0316, 0.1100), (0.0, 0.1100),
]
lathe("Cork", cork, 96, uv_v=[1.0, 1.0, 0.96, 0.90, 0.78, 0.62, 0.42, 0.22, 0.10, 0.06, 0.02, 0.0, 0.0])


def roughen_cork_lip(amplitude=0.0009):
    """Break the lathe circle on the top lip so the cork reads as a cut, porous edge."""
    mesh = bpy.data.objects["Cork"].data
    for vert in mesh.vertices:
        radial = math.hypot(vert.co.x, vert.co.y)
        if vert.co.z < 0.128 or radial < 0.020:
            continue
        sample = noise.noise(Vector((vert.co.x, vert.co.y, vert.co.z)) * 90.0)
        vert.co.x += (vert.co.x / radial) * sample * amplitude
        vert.co.y += (vert.co.y / radial) * sample * amplitude
        if vert.co.z > 0.136:
            vert.co.z += sample * 0.0007
    mesh.update()
    print("[hero] cork lip roughened", "{:.4f}".format(amplitude))


roughen_cork_lip()
# Soil is a thin bed, about 17% of the 0.140 m jar, inside the rounded heel.
# A tall taper read as a lava-rock mound. The top is nearly flat; the moss carpet covers it.
lathe("Soil", [
    (0.0, 0.004), (0.026, 0.004), (0.034, 0.008), (0.0388, 0.014),
    (0.0396, 0.020), (0.030, 0.023), (0.0, 0.024),
], 96, uv_v=[0, 0, 0.15, 0.45, 0.75, 0.92, 1.0])


def roughen_soil(amplitude=0.0004):
    """A few tenths of a millimetre on the bed, so the lip is not a lathe circle."""
    mesh = bpy.data.objects["Soil"].data
    for vert in mesh.vertices:
        if vert.co.z < 0.016:
            continue
        sample = noise.noise(Vector((vert.co.x, vert.co.y, 1.7)) * 160.0)
        vert.co.z += sample * amplitude
    mesh.update()
    print("[hero] soil roughened", "{:.4f}".format(amplitude))


roughen_soil()


def mesh_bounds(name):
    zs, rs = [], []
    for vert in bpy.data.objects[name].data.vertices:
        zs.append(vert.co.z)
        rs.append(math.hypot(vert.co.x, vert.co.y))
    return min(zs), max(zs), max(rs)


def assert_bible():
    """Height, diameter and cork stay inside docs/art/terrarium-style.md section 6."""
    j0, j1, jr = mesh_bounds("Jar")
    c0, c1, cr = mesh_bounds("Cork")
    height = c1 - min(j0, 0.0)
    diameter = 2.0 * jr
    cork_h = c1 - LIP_TOP
    neck_rs = [math.hypot(v.co.x, v.co.y) for v in bpy.data.objects["Jar"].data.vertices if 0.109 <= v.co.z <= 0.117]
    if not neck_rs:
        raise SystemExit("no neck vertices")
    neck = min(neck_rs)
    print("[hero] bible height {h:.4f} diameter {d:.4f} cork {c:.4f} neck {n:.4f} body {b:.4f}".format(
        h=height, d=diameter, c=cork_h, n=neck, b=jr))
    if not (0.133 <= height <= 0.147):
        raise SystemExit("jar height {h:.4f} outside 0.133-0.147".format(h=height))
    if not (0.0826 <= diameter <= 0.0914):
        raise SystemExit("jar diameter {d:.4f} outside 0.0826-0.0914".format(d=diameter))
    if not (0.0135 <= cork_h <= 0.0165):
        raise SystemExit("cork height {c:.4f} outside 0.0135-0.0165".format(c=cork_h))
    if neck > jr * 0.82:
        raise SystemExit("neck {n:.4f} is not narrower than the body {b:.4f}".format(n=neck, b=jr))
    if cr > jr * 0.86:
        raise SystemExit("cork cap {c:.4f} is as wide as the body".format(c=cr))
    assert_mirror("Jar")


def assert_mirror(name, tol=2e-5):
    """JarG1 looks along Blender -Y, so left-right is a mirror in X."""
    buckets = {}
    for vert in bpy.data.objects[name].data.vertices:
        key = (round(vert.co.y, 5), round(vert.co.z, 5))
        buckets.setdefault(key, []).append(vert.co.x)
    missing = 0
    worst = 0.0
    for xs in buckets.values():
        for x in xs:
            best = min(abs(-x - other) for other in xs)
            if best > worst:
                worst = best
            if best > tol:
                missing += 1
    print("[hero] mirror {n} worst {w:.6f} miss {m}".format(n=name, w=worst, m=missing))
    if missing:
        raise SystemExit("mesh {n} is not left-right symmetric ({m} verts, worst {w:.6f})".format(
            n=name, m=missing, w=worst))


assert_bible()
# Moss is clump cushions plus the tuft skirt (terrarium_moss.py), not a lathe dome.
moss_names = terrarium_moss.build()


# fiddlehead: a swept tube along stem + logarithmic coil. Five uncoil states share topology.
# Unity lerps their vertices. FBX shape keys lost their scale in round 3, so these stay separate meshes.
def fiddle_points(uncoil, n=90):
    pts = []
    stem_n = 40
    for i in range(stem_n):
        t = i / (stem_n - 1)
        x = 0.004 + 0.006 * math.sin(t * 1.4) - 0.004 * t * t
        z = 0.030 + 0.040 * t + 0.016 * uncoil * t
        pts.append(Vector((x, 0.006 - 0.003 * t, z)))
    top = pts[-1]
    # the coil: starts heading up and curls back over toward -x, winding into a shrinking spiral; uncoil opens it
    turns = 2.6 * (1 - uncoil) + 0.12
    r0 = 0.0135 * (1 - 0.30 * uncoil)
    coil_n = n - stem_n
    for i in range(1, coil_n + 1):
        t = i / coil_n
        th = t * turns * 2 * math.pi
        r = r0 * math.exp(-0.42 * th / (2 * math.pi)) if uncoil < 0.999 else r0
        # spiral in the x-z plane, centred to the -x side of the stem top
        cxz = Vector((top.x - r0, top.y, top.z))
        ang = th
        p = Vector((cxz.x + r * math.cos(ang), top.y - 0.0015 * t, cxz.z + r * math.sin(ang)))
        # unfurled: the coil straightens into a continuation of the stem that leans and droops slightly
        straight = Vector((top.x - 0.040 * t, top.y, top.z + 0.020 * t - 0.012 * t * t))
        pts.append(p.lerp(straight, uncoil ** 1.5))
    return pts


def tube(name, pts, r_base=0.0018, r_tip=0.0009, ring=12):
    bm = bmesh.new(); uv = bm.loops.layers.uv.new(); rings = []
    for i, p in enumerate(pts):
        t = i / (len(pts) - 1)
        tan = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        up = Vector((0, 1, 0)) if abs(tan.y) < 0.9 else Vector((1, 0, 0))
        nx = tan.cross(up).normalized(); ny = tan.cross(nx).normalized()
        r = r_base + (r_tip - r_base) * t ** 0.8
        ring_vs = []
        for k in range(ring):
            # Same parametric spike on every uncoil state, so the vertex lerp keeps the hairs.
            wobble = noise.noise(Vector((i * 0.23, k * 0.47, 3.1)))
            spike = max(0.0, wobble) ** 2
            rr = r * (1.0 + 2.2 * spike)
            ang = 2 * math.pi * k / ring
            ring_vs.append(bm.verts.new(p + (nx * math.cos(ang) + ny * math.sin(ang)) * rr))
        rings.append(ring_vs)
    for i in range(len(rings) - 1):
        for k in range(ring):
            f = bm.faces.new((rings[i][k], rings[i][(k + 1) % ring], rings[i + 1][(k + 1) % ring], rings[i + 1][k]))
            for l, (uu, vv) in zip(f.loops, ((k / ring, i / len(rings)), ((k + 1) / ring, i / len(rings)), ((k + 1) / ring, (i + 1) / len(rings)), (k / ring, (i + 1) / len(rings)))):
                l[uv].uv = (uu, vv)
    bm.faces.new(rings[-1][::-1])
    return obj_from_bm(bm, name)


states = [0.0, 0.25, 0.5, 0.75, 1.0]
for s_ in states: tube(f"Fiddle{int(s_ * 100)}", fiddle_points(s_))


# frond card: a curled blade. The pinnae droop and the tip twists, so it is not a flat rectangle.
def frond(name, w=0.040, h=0.082, fold=0.38, arch=0.020, curl=0.014, twist=0.22, cols=8, rows=22, uv_col=0, uv_cols=2):
    """Curled card. UV.x selects one column of the 2-frond atlas."""
    bm = bmesh.new(); uv = bm.loops.layers.uv.new(); grid = []
    for j in range(rows + 1):
        v = j / rows; row = []
        for i in range(cols + 1):
            u = i / cols
            x = (u - 0.5) * w
            side = abs(u - 0.5) * 2.0
            # Fold on the rachis, arch back, and curl the pinnae down through the middle of the blade.
            y = -abs(x) * fold - arch * (v ** 1.35) - curl * math.sin(v * math.pi) * side
            z = v * h - 0.004 * side * side * math.sin(v * math.pi)
            p = Vector((x, y, z))
            p.rotate(Euler((twist * math.sin(v * math.pi) * (u - 0.5), 0.0, 0.0)))
            row.append(bm.verts.new(p))
        grid.append(row)
    for j in range(rows):
        for i in range(cols):
            f = bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]))
            for l, (uu, vv) in zip(f.loops, ((i / cols, j / rows), ((i + 1) / cols, j / rows), ((i + 1) / cols, (j + 1) / rows), (i / cols, (j + 1) / rows))):
                l[uv].uv = ((uv_col + uu) / uv_cols, vv)
    return obj_from_bm(bm, name)


frond("FrondV0", uv_col=0)
frond("FrondV1", w=0.034, h=0.076, fold=0.30, arch=0.016, curl=0.010, twist=0.16, uv_col=1)
frond("FrondV2", w=0.044, h=0.088, fold=0.46, arch=0.026, curl=0.018, twist=-0.20, uv_col=0)


# seedling: two cupped leaves on a short stem
def leaf(bm, uv, origin, ang, L=0.012, W=0.0055, tilt=0.6):
    vs = []
    for j in range(9):
        t = j / 8; wdt = W * math.sin(math.pi * t) ** 0.9
        row = []
        for s in (-1, -0.5, 0, 0.5, 1):
            x = s * wdt * (1 - 0.25 * abs(s) * (1 - t)); z = t * L
            p = Vector((x, -abs(x) * 0.5 + 0.0, z))
            p.rotate(__import__('mathutils').Euler((tilt * t, 0, ang)))
            row.append(bm.verts.new(origin + p))
        vs.append(row)
    for j in range(8):
        for k in range(4):
            f = bm.faces.new((vs[j][k], vs[j][k + 1], vs[j + 1][k + 1], vs[j + 1][k]))
            for l, (uu, vv) in zip(f.loops, ((k / 4, j / 8), ((k + 1) / 4, j / 8), ((k + 1) / 4, (j + 1) / 8), (k / 4, (j + 1) / 8))):
                l[uv].uv = (uu, vv)


bm = bmesh.new(); uvl = bm.loops.layers.uv.new()
leaf(bm, uvl, Vector((0, 0, 0.012)), 0.0); leaf(bm, uvl, Vector((0, 0, 0.012)), math.pi)
leaf(bm, uvl, Vector((0, 0, 0.006)), math.pi / 2, 0.009, 0.004, 0.9)
obj_from_bm(bm, "Seedling")
tube("SeedStem", [Vector((0, 0, 0)), Vector((0.0005, 0, 0.006)), Vector((0, 0, 0.012))], 0.0008, 0.0006, 6)

# convert to Y-up for Unity happens in the exporter (axis_up='Y'); Blender Z is up here
fbx_path = os.path.join(OUT, "night_jar.fbx")
names = ["Jar", "Cork", "Soil"] + moss_names + [
    "Fiddle0", "Fiddle25", "Fiddle50", "Fiddle75", "Fiddle100",
    "FrondV0", "FrondV1", "FrondV2", "Seedling", "SeedStem"]
export(names, fbx_path)
total = 0
for _name in names:
    me = bpy.data.objects[_name].data
    me.calc_loop_triangles()
    tris = len(me.loop_triangles)
    total += tris
    print("[hero] tris", _name, tris)
print("[hero] tris TOTAL", total)

# triangle census for the cost table
reset(); bpy.ops.import_scene.fbx(filepath=fbx_path)
tot = 0
for o in bpy.data.objects:
    if o.type == 'MESH':
        o.data.calc_loop_triangles(); t = len(o.data.loop_triangles); tot += t
        print(f'[hero] tris night_jar.fbx {o.name} {t}')
print(f'[hero] tris night_jar.fbx TOTAL {tot}')

"""Blender 4.2, headless:  blender -b -P blender_heroes.py
Models both hero objects and exports FBX into the Unity project (Assets/Fidelity/Models).
Units are metres, Y-up on export. Every mesh here is authored geometry (lathe profiles, swept curves with taper,
shape-keyed uncoil), not Unity primitives."""
import bpy, bmesh, math, random, os
from mathutils import Vector, noise

OUT = r"C:\hgspike\unity\Assets\Fidelity\Models"
os.makedirs(OUT, exist_ok=True)
random.seed(4)


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


def export(names, path, shape_keys=True):
    bpy.ops.object.select_all(action='DESELECT')
    for n in names: bpy.data.objects[n].select_set(True)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                             object_types={'MESH'}, use_mesh_modifiers=True, mesh_smooth_type='FACE', bake_anim=False,
                             add_leaf_bones=False)
    print('[hero] exported', path, names)


# =========================================================================== JAR
reset()
# glass: outer wall up the profile, over the lip, and back down the inside (one closed shell, 2 mm thick)
outer = [(0.0, 0.0), (0.038, 0.0), (0.0445, 0.003), (0.0462, 0.012), (0.0465, 0.060), (0.0462, 0.098), (0.0440, 0.110),
         (0.0400, 0.1165), (0.0378, 0.1195), (0.0376, 0.1215), (0.0388, 0.1235), (0.0390, 0.1270), (0.0378, 0.1290)]
inner = [(r - 0.0021, y) for (r, y) in outer[2:-2]][::-1]
prof = outer + [(0.0356, 0.1285), (0.0355, 0.1240)] + [(max(0.0, r), y) for (r, y) in inner if y > 0.004] + [(0.036, 0.004), (0.0, 0.004)]
lathe("Jar", prof, 128)
# cork: slightly tapered plug with a rounded top edge
cork = [(0.0, 0.1395), (0.0372, 0.1395), (0.0386, 0.1388), (0.0393, 0.1375), (0.0395, 0.1300), (0.0360, 0.1240), (0.0354, 0.1180), (0.0, 0.1180)]
lathe("Cork", cork, 96, uv_v=[1.0, 1.0, 0.95, 0.9, 0.35, 0.1, 0.0, 0.0])
# soil: the dark layer under the moss
lathe("Soil", [(0.0, 0.004), (0.0428, 0.004), (0.0436, 0.016), (0.0436, 0.030), (0.0, 0.031)], 96, uv_v=[0, 0, 0.45, 1.0, 1.0])
# moss mound: lathe dome, then a noise displacement so its silhouette is clumped, not a primitive
# moss mound: lathe dome wrapped in the moss band (cylindrical UV, v = height), noise-clumped silhouette.
# The cap gets a planar texture in the shader, so the lathe pole never shows.
moss_prof = [(0.0436, 0.027), (0.0436, 0.031), (0.0425, 0.036), (0.0395, 0.0405), (0.034, 0.0440), (0.025, 0.0468), (0.013, 0.0482), (0.0, 0.0486)]
m = lathe("Moss", moss_prof, 160, uv_v=[0.05, 0.2, 0.36, 0.5, 0.6, 0.66, 0.69, 0.70])
for v in m.data.vertices:
    p = v.co; h = (p.z - 0.031) / 0.018
    if h > 0.05:
        n = noise.noise(Vector((p.x * 170, p.y * 170, 0.4))) * 0.0020 + noise.noise(Vector((p.x * 560, p.y * 560, 1.3))) * 0.0007
        d = Vector((p.x, p.y, 0)).normalized() if Vector((p.x, p.y)).length > 1e-5 else Vector((0, 0, 0))
        v.co = p + d * n * 0.5 + Vector((0, 0, n * h))


# fiddlehead: a swept tube along stem + logarithmic coil; three uncoil states share topology -> shape keys
def fiddle_points(uncoil, n=90):
    pts = []
    stem_n = 40
    base = Vector((0.004, 0.006, 0.040))
    for i in range(stem_n):
        t = i / (stem_n - 1)
        x = 0.004 + 0.006 * math.sin(t * 1.4) - 0.004 * t * t
        z = 0.040 + 0.040 * t + 0.016 * uncoil * t
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
        rings.append([bm.verts.new(p + (nx * math.cos(2 * math.pi * k / ring) + ny * math.sin(2 * math.pi * k / ring)) * r) for k in range(ring)])
    for i in range(len(rings) - 1):
        for k in range(ring):
            f = bm.faces.new((rings[i][k], rings[i][(k + 1) % ring], rings[i + 1][(k + 1) % ring], rings[i + 1][k]))
            for l, (uu, vv) in zip(f.loops, ((k / ring, i / len(rings)), ((k + 1) / ring, i / len(rings)), ((k + 1) / ring, (i + 1) / len(rings)), (k / ring, (i + 1) / len(rings)))):
                l[uv].uv = (uu, vv)
    bm.faces.new(rings[-1][::-1])
    return obj_from_bm(bm, name)


states = [0.0, 0.25, 0.5, 0.75, 1.0]
# three uncoil states with identical topology; Unity lerps their vertices (shape keys lost their scale through FBX)
for s_ in states: tube(f"Fiddle{int(s_ * 100)}", fiddle_points(s_))


# frond card: a V-folded, arched strip that carries the painted pinnate texture (alpha-tested in Unity)
def frond(name, w=0.040, h=0.082, fold=0.30, arch=0.016, cols=6, rows=20):
    bm = bmesh.new(); uv = bm.loops.layers.uv.new(); grid = []
    for j in range(rows + 1):
        v = j / rows; row = []
        for i in range(cols + 1):
            u = i / cols; x = (u - 0.5) * w
            y = -abs(x) * fold - arch * v * v      # fold along the rachis; arch back toward the tip
            row.append(bm.verts.new((x, y, v * h)))
        grid.append(row)
    for j in range(rows):
        for i in range(cols):
            f = bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]))
            for l, (uu, vv) in zip(f.loops, ((i / cols, j / rows), ((i + 1) / cols, j / rows), ((i + 1) / cols, (j + 1) / rows), (i / cols, (j + 1) / rows))):
                l[uv].uv = (uu, vv)
    return obj_from_bm(bm, name)


frond("Frond")


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
sd = obj_from_bm(bm, "Seedling")
stem = tube("SeedStem", [Vector((0, 0, 0)), Vector((0.0005, 0, 0.006)), Vector((0, 0, 0.012))], 0.0008, 0.0006, 6)

# convert to Y-up for Unity happens in the exporter (axis_up='Y'); Blender Z is up here
export(["Jar", "Cork", "Soil", "Moss", "Fiddle0", "Fiddle25", "Fiddle50", "Fiddle75", "Fiddle100", "Frond", "Seedling", "SeedStem"], os.path.join(OUT, "night_jar.fbx"))

# =========================================================================== DIAL
reset()
R = 0.130; Hd = 0.009
# disc: top cap (planar UV 0..1) + bevelled rim (side UV around)
bm = bmesh.new(); uv = bm.loops.layers.uv.new(); segs = 192
centre = bm.verts.new((0, 0, Hd)); ring = []
for s in range(segs):
    a = 2 * math.pi * s / segs; ring.append(bm.verts.new((R * math.cos(a), R * math.sin(a), Hd)))
for s in range(segs):
    f = bm.faces.new((centre, ring[s], ring[(s + 1) % segs]))
    for l in f.loops:
        co = l.vert.co; l[uv].uv = (0.5 + co.x / (2 * R), 0.5 + co.y / (2 * R))
top = obj_from_bm(bm, "DialTop")
for p in top.data.polygons: p.use_smooth = False
side_prof = [(R, Hd), (R + 0.0012, Hd - 0.0008), (R + 0.0016, Hd - 0.003), (R + 0.0016, 0.002), (R + 0.0008, 0.0), (0.0, 0.0)]
lathe("DialSide", side_prof, segs, uv_v=[1.0, 0.92, 0.8, 0.12, 0.0, 0.0])
# soil bed: a low mound inside the 0.565 ring, noise-clodded, UV-mapped like the top so the painted bed lines up
bm = bmesh.new(); uv = bm.loops.layers.uv.new(); Rb = R * 0.575; rings_n = 24; sg = 128
rows = [[bm.verts.new((0, 0, Hd + 0.0075))]]
for j in range(1, rings_n + 1):
    rr = Rb * j / rings_n; row = []
    for s in range(sg):
        a = 2 * math.pi * s / sg; x, y = rr * math.cos(a), rr * math.sin(a)
        h = 0.0075 * (1 - (j / rings_n) ** 2.2) + (noise.noise(Vector((x * 140, y * 140, 0.5))) * 0.0012 if j < rings_n else 0)
        row.append(bm.verts.new((x, y, Hd + 0.0002 + h)))
    rows.append(row)
for s in range(sg):
    bm.faces.new((rows[0][0], rows[1][s], rows[1][(s + 1) % sg]))
for j in range(1, rings_n):
    for s in range(sg):
        bm.faces.new((rows[j][s], rows[j + 1][s], rows[j + 1][(s + 1) % sg], rows[j][(s + 1) % sg]))
for f in bm.faces:
    for l in f.loops:
        co = l.vert.co; l[uv].uv = (0.5 + co.x / (2 * R), 0.5 + co.y / (2 * R))
obj_from_bm(bm, "Bed")
# gnomon: a fountain-pen stroke made solid - a long tapering nib with a collar, leaning toward midday
pts = []
L = 0.115
prof = [(0.0, 0.0), (0.0048, 0.004), (0.0062, 0.012), (0.0068, 0.016), (0.0058, 0.020), (0.0052, 0.030), (0.0038, 0.060), (0.0020, 0.090), (0.0003, L)]
g = lathe("Gnomon", prof, 24)
g.rotation_euler = (math.radians(-28), math.radians(14), 0); g.location = (0, 0, Hd + 0.004)
bpy.context.view_layer.objects.active = g; g.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=False)
export(["DialTop", "DialSide", "Bed", "Gnomon"], os.path.join(OUT, "drawn_dial.fbx"))

# triangle census for the cost table
for f in ("night_jar.fbx", "drawn_dial.fbx"):
    reset(); bpy.ops.import_scene.fbx(filepath=os.path.join(OUT, f))
    tot = 0
    for o in bpy.data.objects:
        if o.type == 'MESH':
            o.data.calc_loop_triangles(); t = len(o.data.loop_triangles); tot += t
            print(f'[hero] tris {f} {o.name} {t}')
    print(f'[hero] tris {f} TOTAL {tot}')

"""Spike S4 (T-TER-043): the thick fiddlehead. Blender 4.2, headless:

    blender.exe -b -P tools/blender/terrarium_s4.py -- --out apps/terrarium/Assets/Art/Models

Writes s4_fiddle.fbx with five meshes, FiddleS4_0, _25, _50, _75, _100, in the night_jar frame (metres,
Blender Z up, the same frame as Fiddle0..100 in night_jar.fbx, so JarView keeps the A transform).

Against the A fiddle (terrarium_jar.py) this tube is:
  * thicker: 4.0 mm across at the root of the stem, 3.6 mm where the coil starts, 1.4 mm at the tip (A is 3.6 to 1.8
    with random spikes that stood in for hair; the hair is now shell fuzz in Fidelity/FiddleFuzz);
  * a tighter log spiral: 3.15 turns when fully curled (A 2.72), a turn-to-turn radius ratio of 0.63 (A 0.66),
    and a smaller outer radius so the coil stays near the 1.6 cm of the style bible;
  * carrying a core weight in vertex colour R: 0 on the stem, rising through the coil to 1 at the tip, which is the
    centre of the spiral. Fidelity/FiddleFuzz turns it into the self-lit core gradient.

The five states share topology, so JarView lerps their vertices like A. Shell fuzz is not in the mesh: JarView
copies each state N times at runtime (S4Fiddle.BuildStack), which is how the shell count becomes a knob.
"""
import math
import os
import sys

import bpy
import bmesh
from mathutils import Vector

STEM_N = 34
COIL_N = 86
RING = 14
STATES = (("FiddleS4_0", 0.0), ("FiddleS4_25", 0.25), ("FiddleS4_50", 0.5), ("FiddleS4_75", 0.75), ("FiddleS4_100", 1.0))
UV_TILE = 0.025          # metres of tube per V unit, and the circumference counts as one U unit


def smooth(x):
    x = max(0.0, min(1.0, x))
    return x * x * (3.0 - 2.0 * x)


def fiddle_points(u):
    """Stem, then the coil. Returns [(point, coil_fraction)]. coil_fraction is 0 on the stem."""
    pts = []
    for i in range(STEM_N):
        t = i / (STEM_N - 1)
        x = 0.004 + 0.006 * math.sin(t * 1.4) - 0.004 * t * t
        z = 0.030 + 0.040 * t + 0.016 * u * t
        pts.append((Vector((x, 0.006 - 0.003 * t, z)), 0.0))
    top = pts[-1][0]
    turns = 3.0 * (1.0 - u) + 0.15
    r0 = 0.0120 * (1.0 - 0.30 * u)
    decay = 0.46   # per turn: exp(-0.46) = 0.63
    for i in range(1, COIL_N + 1):
        t = i / COIL_N
        th = t * turns * 2.0 * math.pi
        r = r0 * math.exp(-decay * th / (2.0 * math.pi)) if u < 0.999 else r0
        centre = Vector((top.x - r0, top.y, top.z))
        p = Vector((centre.x + r * math.cos(th), top.y - 0.0020 * t, centre.z + r * math.sin(th)))
        straight = Vector((top.x - 0.040 * t, top.y, top.z + 0.020 * t - 0.012 * t * t))
        pts.append((p.lerp(straight, u ** 1.5), t))
    return pts


def radius_at(i, coil_t):
    if coil_t <= 0.0:
        return 0.0020 - 0.0002 * (i / (STEM_N - 1))
    return 0.0018 - 0.0011 * (coil_t ** 0.8)


def tube(name, u):
    pts = fiddle_points(u)
    pos = [p for p, _ in pts]
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    col = bm.loops.layers.color.new("Col")
    rings = []
    arc = [0.0]
    for i in range(1, len(pos)):
        arc.append(arc[-1] + (pos[i] - pos[i - 1]).length)
    for i, (p, coil_t) in enumerate(pts):
        tan = (pos[min(i + 1, len(pos) - 1)] - pos[max(i - 1, 0)]).normalized()
        up = Vector((0, 1, 0)) if abs(tan.y) < 0.9 else Vector((1, 0, 0))
        nx = tan.cross(up).normalized()
        ny = tan.cross(nx).normalized()
        r = radius_at(i, coil_t)
        rings.append([bm.verts.new(p + (nx * math.cos(2 * math.pi * k / RING) + ny * math.sin(2 * math.pi * k / RING)) * r)
                      for k in range(RING)])
    for i in range(len(rings) - 1):
        for k in range(RING):
            f = bm.faces.new((rings[i][k], rings[i][(k + 1) % RING], rings[i + 1][(k + 1) % RING], rings[i + 1][k]))
            corners = ((k / RING, arc[i]), ((k + 1) / RING, arc[i]), ((k + 1) / RING, arc[i + 1]), (k / RING, arc[i + 1]))
            idx = (i, i, i + 1, i + 1)
            for loop, (uu, ss), ri in zip(f.loops, corners, idx):
                loop[uv].uv = (uu, ss / UV_TILE)
                # Core weight: zero on the stem and the first third of the coil, one at the tip (the spiral centre).
                w = smooth((pts[ri][1] - 0.30) / 0.70)
                loop[col] = (w, w, w, 1.0)
    bm.faces.new(rings[-1][::-1])    # closed tip
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for poly in me.polygons:
        poly.use_smooth = True
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    return ob, pts


def resolve_out():
    out = os.path.join("apps", "terrarium", "Assets", "Art", "Models")
    if "--" in sys.argv:
        tail = sys.argv[sys.argv.index("--") + 1:]
        if "--out" in tail:
            i = tail.index("--out")
            if i + 1 >= len(tail):
                raise SystemExit("missing value for --out")
            out = tail[i + 1]
    out = os.path.abspath(out)
    os.makedirs(out, exist_ok=True)
    return out


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    names = []
    for name, u in STATES:
        ob, pts = tube(name, u)
        names.append(name)
        ob.data.calc_loop_triangles()
        coil = [p for p, t in pts if t > 0.0]
        xs = [p.x for p in coil]
        zs = [p.z for p in coil]
        length = sum((pts[i][0] - pts[i - 1][0]).length for i in range(1, len(pts)))
        print("[s4] %s verts %d tris %d coil bbox x %.2f cm z %.2f cm, centreline %.1f cm"
              % (name, len(ob.data.vertices), len(ob.data.loop_triangles), (max(xs) - min(xs)) * 100, (max(zs) - min(zs)) * 100, length * 100))
    verts = {len(bpy.data.objects[n].data.vertices) for n in names}
    if len(verts) != 1:
        raise SystemExit("fiddle states do not share topology: %s" % verts)
    path = os.path.join(resolve_out(), "s4_fiddle.fbx")
    bpy.ops.object.select_all(action="DESELECT")
    for n in names:
        bpy.data.objects[n].select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z", axis_up="Y", object_types={"MESH"}, use_mesh_modifiers=True,
        mesh_smooth_type="FACE", bake_anim=False, add_leaf_bones=False,
    )
    print("[s4] exported", path, names)


if __name__ == "__main__":
    main()

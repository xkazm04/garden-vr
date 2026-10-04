"""Spike S2 (T-TER-044): thick-glass jar shell. Blender 4.2, headless:

    blender.exe -b -P tools/blender/terrarium_s2.py -- --out apps/terrarium/Assets/Art/Models

Writes s2_jar.fbx with one mesh, JarS2, in the night_jar frame (metres, Blender Z up, exported Y up), so JarView
swaps it onto the Jar glass filter and keeps every transform. It is ONE closed glass solid revolved from a single
profile: up the outside, over a rolled lip, and down the inside to the raised floor. The inner wall is therefore a
real surface 2.5 mm inside the outer one with normals pointing into the cavity (flipped against the outer wall).

Against the A jar (terrarium_jar.py, 4.5 cm wall radius, 14 cm with cork):
  * inner shell: wall 2.5 mm thick; the cavity is 4.25 cm in radius, so soil (3.96 cm) and moss still fit;
  * thick foot: the outer heel is the same 1.5 cm rounded corner, the cavity floor sits 7.5 mm up with a 6 mm
    rounded inner corner, so the base is a glass block;
  * rolled lip: a 3.2 mm bead at the mouth (A is 2.2 mm), a round crown, and the inner face runs down the bore of
    the neck instead of stopping 1.5 mm below the cork;
  * round shoulder: the 90 degree quarter ellipse is replaced by a smootherstep blend with a vertical tangent at
    both ends and a longer run (wall to 9.0 cm unchanged, neck at 11.8 cm unchanged, so the silhouette extremes,
    jar height and the cork seat do not move).

Like the A jar, faces that point away from the G1 eye are dropped (the glass back pass is off and Fidelity/Overdraw
is Cull Off, so they would count as layers). Faces on the far inner wall face the eye (their normals point into the
cavity), so they stay: that is the second Fresnel edge of the thick wall.
"""
import math
import os
import sys

import bpy
import bmesh
from mathutils import Vector

BODY_R = 0.0450
NECK_R = 0.0340
WALL_T = 0.0025
WALL_TOP = 0.0900
SHOULDER_TOP = 0.1080
NECK_TOP = 0.1180
LIP_TOP = 0.1250
FLOOR_Y = 0.0075
SEGS = 128


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


def smoother(x):
    x = max(0.0, min(1.0, x))
    return x * x * x * (x * (x * 6.0 - 15.0) + 10.0)


def shoulder_pts(r_wall, r_neck, y0, y1, n):
    """Wall to neck. Radius falls monotonically with a vertical tangent at both ends."""
    pts = []
    for i in range(1, n + 1):
        t = i / n
        k = smoother(t)
        pts.append((r_wall + (r_neck - r_wall) * k, y0 + (y1 - y0) * t))
    return pts


def outer_profile():
    """Centre of the base, up the outside, over the rolled lip. Returns [(r, y)]."""
    pts = [(0.0, 0.0)]
    corner = 0.0150
    flat = BODY_R - corner
    pts.append((flat * 0.55, 0.00025))
    pts.append((flat, 0.0005))
    for i in range(1, 9):
        a = (i / 8.0) * math.pi * 0.5
        pts.append((flat + corner * math.sin(a), 0.0005 + corner * (1.0 - math.cos(a))))
    pts.append((BODY_R, WALL_TOP - 0.004))
    pts.extend(shoulder_pts(BODY_R, NECK_R, WALL_TOP - 0.004, SHOULDER_TOP + 0.004, 12))
    pts.append((NECK_R, (SHOULDER_TOP + 0.004 + NECK_TOP) * 0.5))
    pts.append((NECK_R, NECK_TOP))
    # Rolled bead: a half ellipse on the neck line, 3.2 mm out and 7 mm tall (A is a 2.2 mm bead), then the crown
    # rounds inward over the top of the glass toward the bore.
    cy, rbx, rby = 0.1215, 0.0032, 0.0035
    for i in range(0, 13):
        a = math.pi * (1.5 + i / 12.0)
        pts.append((NECK_R + rbx * math.cos(a), cy + rby * math.sin(a)))
    rn = NECK_R - WALL_T
    pts.append((NECK_R - 0.0012, 0.12490))
    pts.append((rn + 0.0008, 0.12430))
    pts.append((rn + 0.0002, 0.12320))
    return pts


def inner_profile():
    """From the lip crown down the bore to the raised floor centre. Returns [(r, y)] in travel order."""
    pts = []
    ri = BODY_R - WALL_T
    rn = NECK_R - WALL_T
    # Straight down the bore.
    pts.append((rn, 0.1220))
    pts.append((rn, NECK_TOP - 0.003))
    pts.append((rn, (SHOULDER_TOP + 0.004 + NECK_TOP) * 0.5))
    pts.append((rn, SHOULDER_TOP + 0.004))
    # The inner shoulder is the outer one pulled in by the wall thickness, so the glass stays 2.5 mm thick.
    for r, y in reversed(shoulder_pts(ri, rn, WALL_TOP - 0.004, SHOULDER_TOP + 0.004, 12)[:-1]):
        pts.append((r, y))
    pts.append((ri, WALL_TOP - 0.004))
    # Straight inner wall down to the floor corner, then the 6 mm rounded inner corner, then the raised floor.
    corner = 0.0060
    pts.append((ri, FLOOR_Y + corner))
    for i in range(1, 9):
        a = (i / 8.0) * math.pi * 0.5
        pts.append((ri - corner * (1.0 - math.cos(a)), FLOOR_Y + corner * (1.0 - math.sin(a))))
    pts.append(((ri - corner) * 0.55, FLOOR_Y))
    pts.append((0.0, FLOOR_Y))
    return pts


def lathe(name, profile, segs):
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new()
    rows = []
    for (r, y) in profile:
        row = []
        for s in range(segs + 1):
            a = 2.0 * math.pi * s / segs
            row.append(bm.verts.new((r * math.cos(a), -r * math.sin(a), y)))
        rows.append(row)
    n = len(profile)
    for i in range(n - 1):
        for s in range(segs):
            f = bm.faces.new((rows[i][s], rows[i][s + 1], rows[i + 1][s + 1], rows[i + 1][s]))
            for loop, (u, v) in zip(f.loops, ((s / segs, i / (n - 1)), ((s + 1) / segs, i / (n - 1)),
                                              ((s + 1) / segs, (i + 1) / (n - 1)), (s / segs, (i + 1) / (n - 1)))):
                loop[uv].uv = (u, v)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-7)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = True
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    return ob


def check_monotonic(profile, label):
    """The outside only narrows from the wall up (no pouch)."""
    for i in range(1, len(profile)):
        if profile[i][1] > WALL_TOP and profile[i][0] > profile[i - 1][0] + 1e-6 and profile[i][1] < NECK_TOP:
            raise SystemExit("%s: radius grows at y %.4f" % (label, profile[i][1]))


def drop_back_faces(obj):
    """Same rule as terrarium_jar.drop_back_glass: the JarG1 eye is Blender (0, -0.44, 0.175)."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.normal_update()
    cam = Vector((0.0, -0.44, 0.175))
    doomed = [f for f in bm.faces if (cam - f.calc_center_median()).dot(f.normal) <= 0.05]
    total = len(bm.faces)
    if len(doomed) < 8 or len(doomed) > total - 8:
        raise SystemExit("glass cull would delete %d of %d faces" % (len(doomed), total))
    bmesh.ops.delete(bm, geom=doomed, context="FACES")
    bm.to_mesh(obj.data)
    obj.data.update()
    bm.free()
    return total, len(doomed)


def main():
    out = resolve_out()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    outer = outer_profile()
    inner = inner_profile()
    check_monotonic(outer, "outer")
    profile = outer + inner
    ymax = max(y for _, y in profile)
    if abs(ymax - LIP_TOP) > 0.0006:
        raise SystemExit("unexpected top %.4f" % ymax)
    jar = lathe("JarS2", profile, SEGS)
    before_faces = len(jar.data.polygons)
    total, dropped = drop_back_faces(jar)
    jar.data.update()
    # Normals must point out of the glass solid: outer wall away from the axis, inner wall toward the axis.
    bm = bmesh.new()
    bm.from_mesh(jar.data)
    bm.normal_update()
    outward = inward = 0
    for f in bm.faces:
        c = f.calc_center_median()
        radial = Vector((c.x, c.y, 0.0))
        if radial.length < 1e-4:
            continue
        s = radial.normalized().dot(Vector((f.normal.x, f.normal.y, 0.0)))
        if c.y < 0.02 and False:
            continue
        if abs(s) < 0.2:
            continue
        if s > 0:
            outward += 1
        else:
            inward += 1
    bm.free()
    print("[s2] faces total %d dropped %d kept %d (outward %d inward %d)" % (total, dropped, total - dropped, outward, inward))
    tris = sum(len(p.vertices) - 2 for p in jar.data.polygons)
    print("[s2] tris %d verts %d" % (tris, len(jar.data.vertices)))
    bbox = [jar.matrix_world @ Vector(c) for c in jar.bound_box]
    print("[s2] bbox z %.4f..%.4f max r %.4f" % (min(v.z for v in bbox), max(v.z for v in bbox), max(abs(v.x) for v in bbox)))
    bpy.ops.object.select_all(action='DESELECT')
    jar.select_set(True)
    path = os.path.join(out, "s2_jar.fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z',
                             axis_up='Y', object_types={'MESH'}, use_mesh_modifiers=True, mesh_smooth_type='FACE',
                             bake_anim=False, add_leaf_bones=False)
    print("[s2] exported", path)
    print("[s2] profile (r, y):")
    for r, y in profile:
        print("[s2]   %.4f %.4f" % (r, y))


main()

"""T-TER-049 (S6): silhouette pass. Blender 4.2, headless:

    blender.exe -b -P tools/blender/terrarium_s6.py -- --out apps/terrarium/Assets/Art/Models

Writes s6_jar.fbx (night_jar frame, metres, Blender Z up, exported Y up) with seven meshes:

  JarS6, JarS6Lean  the S2 thick shell (one closed glass solid, inner wall 2.5 mm in, raised floor, rolled lip) with the
                    proportions of the reference jar at JarG1: a wider near-cylinder, a broad rounded shoulder, a short
                    neck and a wide mouth. The numbers come from apps/terrarium/Art/Scripts/s6_sil.py (hand-traced
                    reference outline, projected IoU fit with physical limits); see PROFILE below.
  CorkS6            a low flat plug: a 8 mm cap on the lip crown, a plug down the bore, a roughened rim.
  SoilS6, MossS6, MossShellS6, SprigsS6
                    the S3 moss-in-the-jar meshes (terrarium_s3.py) rebuilt for the wider cavity: soil wall to wall
                    from the raised floor, with its own 6 mm heel, a dense rounded mound whose rim rolls over the soil.

terrarium_s2.py and terrarium_s3.py are imported for their lathe, culling and mound builders; their module constants
are overridden here, so the A, S2 and S3 meshes are not touched.
"""
import math
import os
import sys

import bpy
import bmesh
from mathutils import Vector, noise

_HERE = os.path.dirname(os.path.abspath(__file__))
if _HERE not in sys.path:
    sys.path.insert(0, _HERE)

import terrarium_s2 as s2  # noqa: E402
import terrarium_s3 as s3  # noqa: E402

# Fit of the reference silhouette (s6_sil.py). Metres, jar frame.
PROFILE = dict(
    R=0.0470,        # body outer radius (A and S2: 0.0450)
    c=0.0250,        # heel, horizontal radius
    cv=0.0210,       # heel, vertical radius
    yb=0.0880,       # straight wall ends
    ys=0.1120,       # shoulder meets the neck
    pw=1.9,          # shoulder exponent: 2 is a quarter ellipse, more is squarer
    Rn=0.0345,       # neck outer radius
    yn=0.1195,       # neck top, where the lip bead starts
    rbx=0.0030,      # bead, radial
    rby=0.0024,      # bead, half height
    lipc=0.1265,     # bead centre height
    rc=0.0349,       # cork cap radius
    ct=0.1365,       # cork top
    ch=0.0080,       # cork cap height
)
WALL_T = 0.0025
FLOOR_Y = 0.0075
INNER_HEEL_H = 0.0210    # cavity corner, horizontal
INNER_HEEL_V = 0.0130    # cavity corner, vertical
SEGS = 128


def shoulder(R, Rn, y0, y1, pw, n=18):
    pts = []
    for i in range(1, n + 1):
        u = i / n
        t = 0.5 - 0.5 * math.cos(math.pi * u)      # denser at both ends, where the curvature is
        pts.append((Rn + (R - Rn) * (1.0 - t ** pw) ** (1.0 / pw), y0 + (y1 - y0) * t))
    return pts


def outer_profile(p):
    R, c, cv = p["R"], p["c"], p["cv"]
    pts = [(0.0, 0.0), ((R - c) * 0.55, 0.00025), (R - c, 0.0005)]
    for i in range(1, 11):
        a = (i / 10.0) * math.pi * 0.5
        pts.append((R - c + c * math.sin(a), 0.0005 + cv * (1.0 - math.cos(a))))
    pts.append((R, p["yb"]))
    pts.extend(shoulder(R, p["Rn"], p["yb"], p["ys"], p["pw"]))
    pts.append((p["Rn"], (p["ys"] + p["yn"]) * 0.5))
    pts.append((p["Rn"], p["yn"]))
    # Rolled bead on the neck line, then the crown rounds in over the glass toward the bore.
    for i in range(0, 13):
        a = math.pi * (1.5 + i / 12.0)
        pts.append((p["Rn"] + p["rbx"] * math.cos(a), p["lipc"] + p["rby"] * math.sin(a)))
    top = p["lipc"] + p["rby"]
    rn = p["Rn"] - WALL_T
    pts.append((p["Rn"] - 0.0012, top - 0.0001))
    pts.append((rn + 0.0008, top - 0.0007))
    pts.append((rn + 0.0002, top - 0.0017))
    return pts


def inner_profile(p):
    ri = p["R"] - WALL_T
    rn = p["Rn"] - WALL_T
    top = p["lipc"] + p["rby"]
    pts = [(rn, top - 0.0030), (rn, p["yn"] - 0.003), (rn, (p["ys"] + p["yn"]) * 0.5), (rn, p["ys"])]
    # Inner shoulder: the outer one pulled in by the wall thickness, from the neck down to the wall.
    for r, y in reversed(shoulder(ri, rn, p["yb"], p["ys"], p["pw"])[:-1]):
        pts.append((r, y))
    pts.append((ri, p["yb"]))
    # The cavity follows the outer heel: an ellipse 2.5 mm inside it horizontally, with the floor raised to FLOOR_Y, so the
    # foot is a glass lens and the cavity corner is as round as the reference interior.
    ci, cvi = INNER_HEEL_H, INNER_HEEL_V
    pts.append((ri, FLOOR_Y + cvi))
    for i in range(1, 11):
        a = (i / 10.0) * math.pi * 0.5
        pts.append((ri - ci * (1.0 - math.cos(a)), FLOOR_Y + cvi * (1.0 - math.sin(a))))
    pts.append(((ri - ci) * 0.55, FLOOR_Y))
    pts.append((0.0, FLOOR_Y))
    return pts


def min_wall(outer, inner, y_lo, y_hi):
    """Smallest horizontal gap between the outer and inner profile over a height band (mm check)."""
    def radius_at(profile, y, going_up):
        best = None
        for (r0, y0), (r1, y1) in zip(profile[:-1], profile[1:]):
            if min(y0, y1) - 1e-9 <= y <= max(y0, y1) + 1e-9 and abs(y1 - y0) > 1e-9:
                r = r0 + (r1 - r0) * (y - y0) / (y1 - y0)
                best = r if best is None else max(best, r)
        return best
    worst = 1.0
    y = y_lo
    while y <= y_hi:
        ro = radius_at(outer, y, True)
        ri = radius_at(inner, y, False)
        if ro is not None and ri is not None:
            worst = min(worst, ro - ri)
        y += 0.0005
    return worst


def cork_profile(p):
    rc, ct, ch = p["rc"], p["ct"], p["ch"]
    top = p["lipc"] + p["rby"]
    bore = p["Rn"] - WALL_T
    rp = bore - 0.0004                                  # the plug, a hair inside the bore
    plug_bottom = top - 0.0215
    prof = [
        (0.0, ct), (rc * 0.55, ct), (rc * 0.90, ct - 0.0001), (rc * 0.975, ct - 0.0005), (rc, ct - 0.0016),
        (rc, ct - ch + 0.0014), (rc * 0.985, ct - ch + 0.0003), (rc * 0.95, ct - ch - 0.0003),
        (rp + 0.0006, ct - ch - 0.0006), (rp, ct - ch - 0.0012), (rp, plug_bottom + 0.002),
        (rp * 0.95, plug_bottom), (0.0, plug_bottom),
    ]
    # v for the side texture: the cap band from the top down, then the plug.
    uv_v = [1.0, 1.0, 0.98, 0.94, 0.86, 0.60, 0.46, 0.40, 0.30, 0.22, 0.10, 0.02, 0.0]
    return prof, uv_v


def lathe_uv(name, profile, segs, uv_v):
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new()
    rows = []
    for r, y in profile:
        rows.append([bm.verts.new((r * math.cos(2 * math.pi * s / segs), -r * math.sin(2 * math.pi * s / segs), y))
                     for s in range(segs + 1)])
    n = len(profile)
    for i in range(n - 1):
        for s in range(segs):
            f = bm.faces.new((rows[i][s], rows[i][s + 1], rows[i + 1][s + 1], rows[i + 1][s]))
            for loop, (u, v) in zip(f.loops, ((s / segs, uv_v[i]), ((s + 1) / segs, uv_v[i]),
                                              ((s + 1) / segs, uv_v[i + 1]), (s / segs, uv_v[i + 1]))):
                loop[uv].uv = (u, v)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-7)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for poly in me.polygons:
        poly.use_smooth = True
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    return ob


def roughen_cork(ob, amplitude=0.0007):
    me = ob.data
    for v in me.vertices:
        radial = math.hypot(v.co.x, v.co.y)
        if radial < 0.016 or v.co.z < PROFILE["ct"] - PROFILE["ch"] - 0.001:
            continue
        s = noise.noise(Vector((v.co.x, v.co.y, v.co.z)) * 90.0)
        v.co.x += (v.co.x / radial) * s * amplitude
        v.co.y += (v.co.y / radial) * s * amplitude
        if v.co.z > PROFILE["ct"] - 0.0006:
            v.co.z += s * 0.0004
    me.update()


def soil_profile(ri, wall_r):
    """Soil wall to wall from the raised floor, with a 6 mm heel that follows the cavity corner."""
    z0 = FLOOR_Y + 0.0003
    ch_, cv_ = INNER_HEEL_H - 0.0003, INNER_HEEL_V - 0.0003
    flat = wall_r - ch_
    pts = [(0.0, z0), (flat * 0.55, z0), (flat, z0)]
    for i in range(1, 11):
        a = (i / 10.0) * math.pi * 0.5
        pts.append((flat + ch_ * math.sin(a), z0 + cv_ * (1.0 - math.cos(a))))
    top = s3.SOIL_WALL_TOP
    pts.extend([(wall_r, top), (wall_r - 0.0012, top + 0.0007), (wall_r - 0.0035, top + 0.0013), (0.0380, 0.0239),
                (0.0300, 0.0242), (0.0, s3.SOIL_TOP)])
    return [(r, z, min(1.0, z / s3.SOIL_TOP)) for r, z in pts]


DOME_RISE = 0.0108      # the mound crest stands this far above the rim (S3: 6.6 mm); the reference cushion is about 1 cm


def dome_z(x, y):
    """S3's mound with a taller, rounder crest, so the moss reads as a cushion and not a sheet."""
    r = math.hypot(x, y)
    t = min(r / s3.DOME_R, 1.0)
    base = 0.0262 + DOME_RISE * ((1.0 - t * t) ** 1.1)
    edge = 1.0 if t < 0.8 else max(0.0, (1.0 - t) / 0.2)
    return base + s3.lumps(x, y) * (0.3 + 0.7 * edge)


def rename(ob, name):
    ob.name = name
    ob.data.name = name
    return ob


def main():
    out = s2.resolve_out()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    p = PROFILE
    # terrarium_s2 helpers read these module constants.
    s2.BODY_R, s2.NECK_R, s2.WALL_T = p["R"], p["Rn"], WALL_T
    s2.WALL_TOP, s2.SHOULDER_TOP, s2.NECK_TOP, s2.LIP_TOP = p["yb"], p["ys"], p["yn"], p["lipc"] + p["rby"]
    s2.FLOOR_Y = FLOOR_Y
    outer = outer_profile(p)
    inner = inner_profile(p)
    gap = min_wall(outer, inner, 0.0075, p["ys"])
    print("[s6] thinnest wall %.2f mm between y 7.5 mm and the shoulder" % (gap * 1000.0))
    if gap < 0.0017:
        raise SystemExit("glass wall too thin: %.2f mm" % (gap * 1000.0))
    ymax = max(y for _, y in outer + inner)
    if abs(ymax - (p["lipc"] + p["rby"])) > 0.0004:
        raise SystemExit("unexpected glass top %.4f" % ymax)
    profile = outer + inner
    jar = s2.lathe("JarS6", profile, SEGS)
    total, dropped = s2.drop_back_faces(jar)
    jar.data.update()
    lean = s2.make_lean(jar)
    # make_lean names the copy JarS2Lean; its inner-wall cut band is 14 mm to 85.5 mm, inside the 88 mm wall.
    rename(jar, "JarS6")
    rename(lean, "JarS6Lean")
    print("[s6] glass faces %d dropped %d tris %d lean tris %d" % (
        total, dropped, sum(len(q.vertices) - 2 for q in jar.data.polygons), sum(len(q.vertices) - 2 for q in lean.data.polygons)))

    cprof, cuv = cork_profile(p)
    cork = lathe_uv("CorkS6", cprof, 96, cuv)
    roughen_cork(cork)
    cork.data.update()
    print("[s6] cork tris %d, cap %.1f mm tall, top %.4f" % (sum(len(q.vertices) - 2 for q in cork.data.polygons), p["ch"] * 1000, p["ct"]))

    # Moss: the S3 builders with the wider cavity.
    ri = p["R"] - WALL_T
    s3.BODY_R = ri
    s3.SOIL_WALL_TOP, s3.SOIL_TOP = 0.0225, 0.0245
    s3.dome_z = dome_z
    s3.WALL_R = ri - 0.0003
    soil = s3.lathe("SoilS3", soil_profile(ri, s3.WALL_R), 96)
    base = s3.sheet_mesh("MossS3", 22, 88, True)
    shell = s3.sheet_mesh("MossShellS3", 12, 48, True, roll_fracs=(0.55, 1.0), roll_inset=0.0004, fur_scale=0.10)
    s3.tip_shell_normals(shell)
    cards = s3.sprigs()
    meshes = []
    for ob, name in ((soil, "SoilS6"), (base, "MossS6"), (shell, "MossShellS6"), (cards, "SprigsS6")):
        z0, z1, rad = s3.bounds(ob)
        print("[s6] tris", name, s3.tris(ob), "z %.4f %.4f r %.4f" % (z0, z1, rad))
        if rad > ri - 0.0002:
            raise SystemExit("%s touches the glass: r %.5f" % (name, rad))
        meshes.append(rename(ob, name))

    bpy.ops.object.select_all(action="DESELECT")
    for ob in [jar, lean, cork] + meshes:
        ob.select_set(True)
    path = os.path.join(out, "s6_jar.fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z",
                             axis_up="Y", object_types={"MESH"}, use_mesh_modifiers=True, mesh_smooth_type="FACE",
                             bake_anim=False, add_leaf_bones=False)
    print("[s6] exported", path)
    print("[s6] outer profile (r, y):")
    for r, y in outer:
        print("[s6]   %.4f %.4f" % (r, y))


if __name__ == "__main__":
    main()

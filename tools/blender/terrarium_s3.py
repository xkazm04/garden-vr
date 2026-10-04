"""Spike S3 (T-TER-042): moss in the jar. Blender 4.2, headless:

    blender.exe -b -P tools/blender/terrarium_s3.py -- --out apps/terrarium/Assets/Art/Models

Writes s3_moss.fbx with four meshes, all in the night_jar frame (metres, Blender Z up):

  SoilS3       soil wall to wall. It follows the glass heel 0.3 mm inside and stands to 1.75 cm on the
               wall, then slopes up and inward under the moss. UV v is height, so strata run level.
  MossS3       the render mound, a polar sheet. The rim is welded at the wall and rolls over the soil in
               an irregular overhang that touches the glass.
  MossShellS3  a decimated copy of the sheet (about 1k triangles) with its rim normals tipped upward, so
               a shell offset climbs the wall instead of going through the glass. JarView stacks it N times.
  SprigsS3     150 sprig cards: wall-pressed tufts, a lip of rim sprigs, a few crest sprigs.

A variant of terrarium_moss.py. The locked A meshes (Moss, MossSkirt, Soil in night_jar.fbx) are not touched.
"""
import math
import os
import random
import sys

import bpy
import bmesh
from mathutils import Matrix, Vector, noise

_HERE = os.path.dirname(os.path.abspath(__file__))
if _HERE not in sys.path:
    sys.path.insert(0, _HERE)

# Glass: outer radius 0.0450, straight to 9 cm. The shell is one surface, so the inner wall is 0.0450.
BODY_R = 0.0450
HEEL = 0.0150
WALL_R = BODY_R - 0.0003          # soil and moss stop 0.3 mm short of the glass
SOIL_WALL_TOP = 0.0175            # the strata band on the glass
SOIL_TOP = 0.0240                 # soil_top(0) in terrarium_moss.py
DOME_R = 0.0405                   # the dome ends here, the rim starts
RIM_Z = 0.0262                    # sheet height at the wall before the roll
ATLAS = 8
SPRIGS = 150


def smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3.0 - 2.0 * t)


def soil_profile():
    """(r, z, v) up the soil. v is z over the bed height so the strata texture stands level."""
    flat = BODY_R - HEEL
    cs = HEEL - 0.0003
    pts = [(0.0, 0.0010), (flat * 0.55, 0.0010), (flat, 0.0010)]
    for i in range(1, 9):
        ang = (i / 8.0) * math.pi * 0.5
        pts.append((flat + cs * math.sin(ang), 0.0010 + (HEEL - 0.0010) * (1.0 - math.cos(ang))))
    # The heel arc ends at the wall: r = WALL_R, z = HEEL.
    pts.append((WALL_R, SOIL_WALL_TOP))
    # Then up and in, so the moss roll hangs over a visible slope and not over a wall of soil.
    pts.extend([(0.0436, 0.0198), (0.0415, 0.0222), (0.0380, 0.0233), (0.0300, 0.0237), (0.0, SOIL_TOP)])
    return [(r, z, min(1.0, z / SOIL_TOP)) for r, z in pts]


def lathe(name, profile, segs=128):
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    rows = []
    for r, z, _v in profile:
        row = []
        for s in range(segs + 1):
            a = 2.0 * math.pi * s / segs
            row.append(bm.verts.new((r * math.cos(a), -r * math.sin(a), z)))
        rows.append(row)
    for i in range(len(profile) - 1):
        for s in range(segs):
            face = bm.faces.new((rows[i][s], rows[i][s + 1], rows[i + 1][s + 1], rows[i + 1][s]))
            for loop, (u, v) in zip(face.loops, ((s / segs, profile[i][2]), ((s + 1) / segs, profile[i][2]),
                                                 ((s + 1) / segs, profile[i + 1][2]), (s / segs, profile[i + 1][2]))):
                # Two turns of the strata around the jar, so the band is not stretched.
                loop[uv].uv = (u * 2.0, v)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-7)
    return finish(bm, name)


def finish(bm, name):
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


def consistent_up(obj):
    """Recalculate outward and make sure the top faces the sky."""
    activate(obj)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    total = sum(p.normal.z * p.area for p in obj.data.polygons)
    if total < 0.0:
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.mesh.flip_normals()
        bpy.ops.object.mode_set(mode="OBJECT")
    for poly in obj.data.polygons:
        poly.use_smooth = True


def rim_z(theta):
    """Where the sheet meets the wall. A little lumpy, so the edge is not a ring."""
    return RIM_Z + 0.0014 * noise.noise(Vector((math.cos(theta) * 2.6, math.sin(theta) * 2.6, 1.3)))


def rim_hang(theta):
    """How far the roll hangs down the glass. 2.5 to 7 mm, irregular."""
    n = 0.5 + 0.5 * noise.noise(Vector((math.cos(theta) * 3.4, math.sin(theta) * 3.4, 7.1)))
    n2 = 0.5 + 0.5 * noise.noise(Vector((math.cos(theta) * 9.0, math.sin(theta) * 9.0, 2.9)))
    return 0.0016 + 0.0026 * n + 0.0010 * n2


def lumps(x, y):
    """Mound lumps in metres: broad hills (4 cm), clumps (1.2 cm) and tufts (6 mm). Also drives the cavity colour."""
    p = Vector((x, y, 0.2))
    broad = 0.0026 * noise.noise(p * 22.0) + 0.0010 * noise.noise(p * 41.0 + Vector((9.0, 4.0, 0.0)))
    clump = 0.0016 * noise.noise(p * 86.0 + Vector((5.0, 2.0, 1.0)))
    tuft = 0.0008 * noise.noise(p * 165.0 + Vector((3.0, 1.0, 0.0)))
    return max(broad, -0.0008) + clump + tuft


def cavity(x, y):
    """0 in a hollow between clumps, 1 on a crest. Written to vertex colour for the shader."""
    return max(0.0, min(1.0, 0.5 + lumps(x, y) / 0.0050))


def dome_z(x, y):
    r = math.hypot(x, y)
    t = min(r / DOME_R, 1.0)
    base = 0.0262 + 0.0066 * ((1.0 - t * t) ** 1.25)
    edge = 1.0 if t < 0.8 else max(0.0, (1.0 - t) / 0.2)
    return base + lumps(x, y) * (0.3 + 0.7 * edge)


def surface_z(x, y):
    """Height of the sheet. Past the dome the sheet settles to the wall height."""
    r = math.hypot(x, y)
    if r <= DOME_R:
        return dome_z(x, y)
    theta = math.atan2(y, x)
    t = smooth((r - DOME_R) / (WALL_R - DOME_R))
    return dome_z(DOME_R * x / r, DOME_R * y / r) * (1.0 - t) + rim_z(theta) * t


def surface_normal(x, y):
    e = 0.0004
    dzdx = (surface_z(x + e, y) - surface_z(x - e, y)) / (2 * e)
    dzdy = (surface_z(x, y + e) - surface_z(x, y - e)) / (2 * e)
    return Vector((-dzdx, -dzdy, 1.0)).normalized()


def sheet_mesh(name, rings, segs, roll, roll_fracs=(0.40, 0.75, 1.0), roll_inset=0.0, fur_scale=None):
    """Polar sheet from the centre to the wall. With roll, more rings hang down the glass.

    With fur_scale set, a second UV layer, FurScale, carries the shell offset scale per vertex: 1 on the sheet and
    fur_scale on the roll, where the glass is a fraction of a millimetre away."""
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    uv2 = bm.loops.layers.uv.new("FurScale") if fur_scale is not None else None
    roll_verts = set()
    centre = bm.verts.new((0.0, 0.0, surface_z(0.0, 0.0)))
    ring_verts = []
    radii = [WALL_R * ((k + 1) / rings) ** 0.92 for k in range(rings)]
    for r in radii:
        row = []
        for s in range(segs):
            th = 2.0 * math.pi * s / segs
            x, y = r * math.cos(th), r * math.sin(th)
            row.append(bm.verts.new((x, y, surface_z(x, y))))
        ring_verts.append(row)
    if roll:
        for frac in roll_fracs:
            row = []
            for s in range(segs):
                th = 2.0 * math.pi * s / segs
                x, y = WALL_R * math.cos(th), WALL_R * math.sin(th)
                # The roll bulges out to the glass, then tucks back as it hangs.
                tuck = 0.0006 * frac * frac
                rr = WALL_R - tuck - roll_inset
                vert = bm.verts.new((rr * math.cos(th), rr * math.sin(th), rim_z(th) - rim_hang(th) * frac))
                roll_verts.add(vert)
                row.append(vert)
            ring_verts.append(row)
    for s in range(segs):
        bm.faces.new((centre, ring_verts[0][s], ring_verts[0][(s + 1) % segs]))
    for k in range(len(ring_verts) - 1):
        for s in range(segs):
            t = (s + 1) % segs
            bm.faces.new((ring_verts[k][s], ring_verts[k + 1][s], ring_verts[k + 1][t], ring_verts[k][t]))
    for face in bm.faces:
        for loop in face.loops:
            p = loop.vert.co
            # One carpet photo across the sheet, like the A mound. The roll continues the same projection.
            loop[uv].uv = (p.x / 0.092 + 0.5, p.y / 0.092 + 0.5)
            if uv2 is not None:
                loop[uv2].uv = (fur_scale if loop.vert in roll_verts else 1.0, 0.0)
    ob = finish(bm, name)
    consistent_up(ob)
    colours = ob.data.color_attributes.new("Col", "BYTE_COLOR", "CORNER")
    for loop in ob.data.loops:
        v = ob.data.vertices[loop.vertex_index].co
        c = cavity(v.x, v.y)
        # Past the dome the cavity fades toward the wall, where the moss is in shadow under the glass rim.
        c *= 1.0 - 0.35 * smooth((math.hypot(v.x, v.y) - 0.036) / 0.0087)
        colours.data[loop.index].color = (c, c, c, 1.0)
    return ob


def tip_shell_normals(obj):
    """Shell normals: the surface normal, tipped toward up near the wall. A shell offset is
    thickness * normal, so near the rim this lifts it instead of pushing it through the glass."""
    me = obj.data
    normals = []
    for loop in me.loops:
        v = me.vertices[loop.vertex_index].co
        r = math.hypot(v.x, v.y)
        n = surface_normal(v.x, v.y)
        w = 0.92 * smooth((r - 0.030) / (WALL_R - 0.030))
        n = (n * (1.0 - w) + Vector((0.0, 0.0, 1.0)) * w).normalized()
        if r > WALL_R - 0.0012 and v.z < rim_z(math.atan2(v.y, v.x)) - 0.0004:
            # Roll vertices: the fur grows outward toward the glass, a fraction of a millimetre.
            n = Vector((v.x / r, v.y / r, -0.25)).normalized()
        normals.append(n)
    me.normals_split_custom_set(normals)


def card_matrix(loc, yaw, tilt, scale):
    z_axis = Vector((math.sin(tilt) * math.cos(yaw), math.sin(tilt) * math.sin(yaw), math.cos(tilt))).normalized()
    radial = Vector((math.cos(yaw), math.sin(yaw), 0.0))
    x_axis = radial.cross(Vector((0.0, 0.0, 1.0)))
    if x_axis.length < 1e-6:
        x_axis = Vector((1.0, 0.0, 0.0))
    x_axis.normalize()
    y_axis = z_axis.cross(x_axis).normalized()
    x_axis = y_axis.cross(z_axis).normalized()
    rot = Matrix((x_axis * scale[0], y_axis * scale[1], z_axis * scale[2])).transposed().to_4x4()
    return Matrix.Translation(Vector(loc)) @ rot


def sprigs():
    """About 150 cards, one mesh. A: wall tufts. B: a lip of rim sprigs. C: crest sprigs."""
    rng = random.Random(42)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    cols = rows = 2
    kinds = ["wall"] * 24 + ["rim"] * 76 + ["crest"] * (SPRIGS - 100)
    for i, kind in enumerate(kinds):
        yaw = rng.random() * math.tau
        if kind == "wall":
            rad = rng.uniform(0.0437, 0.0443)
            tilt = rng.uniform(0.02, 0.16)
            width = rng.uniform(0.0042, 0.0070)
            height = rng.uniform(0.0042, 0.0070)
            drop = rng.uniform(0.0010, 0.0025)       # the card starts under the roll
        elif kind == "rim":
            tilt = rng.uniform(0.95, 1.35)
            width = rng.uniform(0.0040, 0.0066)
            height = rng.uniform(0.0045, 0.0075)
            # The tip leans outward by height * sin(tilt). Keep it behind the glass instead of letting
            # the clamp below collapse the card into a vertical stick.
            reach = height * 1.15 * math.sin(tilt)
            rad = min(rng.uniform(0.034, 0.0415), WALL_R - 0.0008 - reach)
            drop = 0.0003
        else:
            rad = math.sqrt(rng.random()) * 0.021
            tilt = rng.uniform(0.15, 0.75)
            width = rng.uniform(0.0040, 0.0064)
            height = rng.uniform(0.0045, 0.0072)
            drop = 0.0003
        x, y = rad * math.cos(yaw), rad * math.sin(yaw)
        z = surface_z(x, y) - drop
        cell_x = (i * 3 + 1) % ATLAS
        cell_y = (i * 5 + 2) % ATLAS
        pad = 0.02 / ATLAS
        u0, v0 = cell_x / ATLAS + pad, cell_y / ATLAS + pad
        u1, v1 = (cell_x + 1) / ATLAS - pad, (cell_y + 1) / ATLAS - pad
        scale = (rng.uniform(0.85, 1.15), rng.uniform(0.85, 1.15), rng.uniform(0.85, 1.15))
        face_yaw = yaw + (rng.uniform(-0.35, 0.35) if kind != "wall" else rng.uniform(-0.25, 0.25))
        matrix = card_matrix((x, y, z), face_yaw, tilt, scale)
        grid = []
        for j in range(rows + 1):
            v = j / rows
            row = []
            for k in range(cols + 1):
                u = k / cols
                local = Vector(((u - 0.5) * width, -abs(u - 0.5) * 2.0 * 0.00025 - 0.00012 * (v ** 1.3), v * height))
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
    ob = finish(bm, "SprigsS3")
    # Keep every card behind the glass.
    moved = 0
    for vert in ob.data.vertices:
        radius = math.hypot(vert.co.x, vert.co.y)
        if radius > WALL_R:
            vert.co.x *= WALL_R / radius
            vert.co.y *= WALL_R / radius
            moved += 1
    ob.data.update()
    print("[s3] sprig vertices pulled inside the glass", moved)
    return ob


def tris(obj):
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def bounds(obj):
    zs, rs = [], []
    for v in obj.data.vertices:
        zs.append(v.co.z)
        rs.append(math.hypot(v.co.x, v.co.y))
    return min(zs), max(zs), max(rs)


def build():
    soil = lathe("SoilS3", soil_profile(), 96)
    base = sheet_mesh("MossS3", 22, 88, True)
    shell = sheet_mesh("MossShellS3", 12, 48, True, roll_fracs=(0.55, 1.0), roll_inset=0.0004, fur_scale=0.10)
    tip_shell_normals(shell)
    cards = sprigs()
    names = ["SoilS3", "MossS3", "MossShellS3", "SprigsS3"]
    for ob in (soil, base, shell, cards):
        z0, z1, rad = bounds(ob)
        print("[s3] tris", ob.name, tris(ob), "z %.4f %.4f r %.4f" % (z0, z1, rad))
        if rad > BODY_R - 0.0002:
            raise SystemExit("%s touches or crosses the glass: r %.5f" % (ob.name, rad))
    if tris(shell) > 1500:
        raise SystemExit("shell mesh over 1500 triangles: %d" % tris(shell))
    return names


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


if __name__ == "__main__":
    bpy.ops.wm.read_factory_settings(use_empty=True)
    names = build()
    path = os.path.join(resolve_out(), "s3_moss.fbx")
    bpy.ops.object.select_all(action="DESELECT")
    for name in names:
        bpy.data.objects[name].select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z", axis_up="Y", object_types={"MESH"}, use_mesh_modifiers=True,
        mesh_smooth_type="FACE", bake_anim=False, add_leaf_bones=False,
    )
    print("[s3] exported", path, names)

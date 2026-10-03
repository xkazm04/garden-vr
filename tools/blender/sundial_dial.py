"""Drawn dial for the Sundial app. Blender 4.2, headless.

    blender.exe -b -P tools/blender/sundial_dial.py -- --out apps/sundial/Assets/Art/Models

The body is the round-3 dial from blender_heroes.py (disc, bevelled rim, paper-stack
edge, lathe gnomon) at a 30 cm outer diameter, plus a low soil mound and one
bevelled tile (12 x 9 x 2 mm). Smoothed normals are stored for the ink hull:
XY and Z in UV layers Nxy and Nz (linear, so Unity's colour space cannot bend
them) and the same vector in a corner colour. Hard shading normals stay on the
mesh. Units are metres. Export is Y-up for Unity (axis_forward=-Z, axis_up=Y).
"""
import math
import os
import shutil
import sys
import traceback

import bpy
import bmesh
from bpy_extras.io_utils import axis_conversion
from mathutils import Vector

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DEFAULT_OUT = os.path.join(ROOT, "apps", "sundial", "Assets", "Art", "Models")

# Outer lip is the 30 cm diameter. The painted face sits just inside it.
OUTER = 0.150
FACE_R = 0.1472
FACE_Y = 0.012
SOIL_R = 0.068
GNOMON_L = 0.110
TILE_L = 0.012
TILE_W = 0.009
TILE_H = 0.002

# Blender Z-up, -Y forward -> Unity Y-up, -Z forward. The FBX exporter applies this
# to positions and normals. UV layers and vertex colours do not get it, so the
# outline normal is stored already multiplied by this matrix.
AXIS = axis_conversion(from_forward="-Y", from_up="Z", to_forward="-Z", to_up="Y").to_3x3()


def out_dir():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if "--out" in args:
        path = args[args.index("--out") + 1]
        if not os.path.isabs(path):
            path = os.path.join(ROOT, path)
        return path
    return DEFAULT_OUT


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def link(me, name):
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    return ob


def finish_bm(bm, name, smooth):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for poly in me.polygons:
        poly.use_smooth = smooth
    return link(me, name)


def lathe(name, profile, segs, uv_v, smooth):
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    rows = []
    for r, y in profile:
        row = []
        for s in range(segs + 1):
            a = 2 * math.pi * s / segs
            # Matches blender_heroes.lathe: Blender Z is up, the ring closes in XY.
            row.append(bm.verts.new((r * math.cos(a), -r * math.sin(a), y)))
        rows.append(row)
    n = len(profile)
    vv = uv_v or [i / (n - 1) for i in range(n)]
    for i in range(n - 1):
        for s in range(segs):
            face = bm.faces.new((rows[i][s], rows[i][s + 1], rows[i + 1][s + 1], rows[i + 1][s]))
            coords = (
                (s / segs, vv[i]),
                ((s + 1) / segs, vv[i]),
                ((s + 1) / segs, vv[i + 1]),
                (s / segs, vv[i + 1]),
            )
            for loop, uv_co in zip(face.loops, coords):
                loop[uv].uv = uv_co
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-7)
    return finish_bm(bm, name, smooth)


def apply_transform(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


def disc():
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    segs = 96
    centre = bm.verts.new((0.0, 0.0, FACE_Y))
    ring = []
    for s in range(segs):
        a = 2 * math.pi * s / segs
        ring.append(bm.verts.new((FACE_R * math.cos(a), FACE_R * math.sin(a), FACE_Y)))
    for s in range(segs):
        face = bm.faces.new((centre, ring[s], ring[(s + 1) % segs]))
        for loop in face.loops:
            co = loop.vert.co
            loop[uv].uv = (0.5 + co.x / (2 * FACE_R), 0.5 + co.y / (2 * FACE_R))
    # Flat cap: the painted face carries the ink ring. Hard normals stay +Z.
    return finish_bm(bm, "DialTop", False)


def rim():
    # Bevel, then three paper steps, then the underside. Outer radius is 15 cm.
    profile = [
        (FACE_R, FACE_Y),
        (FACE_R + 0.0011, FACE_Y - 0.0007),
        (OUTER, FACE_Y - 0.0018),
        (OUTER - 0.0011, FACE_Y - 0.0036),
        (OUTER, FACE_Y - 0.0052),
        (OUTER - 0.0011, FACE_Y - 0.0068),
        (OUTER, FACE_Y - 0.0084),
        (OUTER - 0.0006, 0.0014),
        (FACE_R * 0.92, 0.0),
        (0.0, 0.0),
    ]
    vv = [i / (len(profile) - 1) for i in range(len(profile))]
    return lathe("DialSide", profile, 96, vv, False)


def soil_height(x, y):
    r = min(1.0, math.hypot(x, y) / SOIL_R)
    return FACE_Y + 0.0003 + 0.0072 * (1.0 - r ** 2.15)


def soil():
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    rings_n = 16
    sg = 64
    rows = [[bm.verts.new((0.0, 0.0, soil_height(0.0, 0.0)))]]
    for j in range(1, rings_n + 1):
        rr = SOIL_R * j / rings_n
        row = []
        for s in range(sg):
            a = 2 * math.pi * s / sg
            x, y = rr * math.cos(a), rr * math.sin(a)
            wobble = 0.0
            if j < rings_n:
                wobble = math.sin(x * 140.0) * math.cos(y * 120.0) * 0.00045
            row.append(bm.verts.new((x, y, soil_height(x, y) + wobble)))
        rows.append(row)
    for s in range(sg):
        bm.faces.new((rows[0][0], rows[1][s], rows[1][(s + 1) % sg]))
    for j in range(1, rings_n):
        for s in range(sg):
            bm.faces.new((rows[j][s], rows[j + 1][s], rows[j + 1][(s + 1) % sg], rows[j][(s + 1) % sg]))
    # Planar UV in the face's frame so the painted soil on dial_face.png stays put.
    for face in bm.faces:
        for loop in face.loops:
            co = loop.vert.co
            loop[uv].uv = (0.5 + co.x / (2 * FACE_R), 0.5 + co.y / (2 * FACE_R))
    return finish_bm(bm, "Soil", True)


def gnomon():
    # A dip-pen nib. The profile radius is the half-width, about 2.4 mm.
    profile = [
        (0.0, 0.0),
        (0.0011, 0.004),
        (0.0019, 0.016),
        (0.0024, 0.034),
        (0.0017, 0.058),
        (0.0009, 0.082),
        (0.00028, 0.100),
        (0.00006, GNOMON_L),
    ]
    # v 0 is the butt in the soil, v 1 is the tip. The slit in gnomon.png sits on the upper half.
    uv_v = [0.0, 0.06, 0.16, 0.28, 0.42, 0.60, 0.80, 1.0]
    obj = lathe("Gnomon", profile, 20, uv_v, True)
    # The ring is in XY before the lean. Flatten Y so the blade reads as a nib.
    for vert in obj.data.vertices:
        vert.co.y *= 0.55
    peak = soil_height(0.0, 0.0)
    # Unity's 90 degree X import maps Blender (x, y, z) to (x, -z, y), so +Y is the far side
    # (image up) and +X is image right. A small +Z is the lift off the soil.
    # Aimed image-left, along the painted shadow. A nib aimed at the midday flower
    # disappears into that card and only the dark butt shows.
    tip = Vector((-0.78, 0.55, 0.10)).normalized()
    obj.rotation_euler = tip.to_track_quat("Z", "Y").to_euler()
    obj.location = (0.0, 0.0, peak + 0.0015)
    apply_transform(obj)
    return obj


def tile():
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for vert in bm.verts:
        vert.co.x *= TILE_L
        vert.co.y *= TILE_W
        vert.co.z *= TILE_H
        vert.co.z += TILE_H * 0.5
    uv = bm.loops.layers.uv.new("UVMap")
    bmesh.ops.bevel(bm, geom=list(bm.edges), offset=0.00035, segments=1, affect="EDGES")
    for vert in bm.verts:
        if vert.co.z < 0.0:
            vert.co.z = 0.0
    for face in bm.faces:
        for loop in face.loops:
            loop[uv].uv = (loop.vert.co.x / TILE_L + 0.5, loop.vert.co.y / TILE_W + 0.5)
    return finish_bm(bm, "Tile", False)


def smooth_normals(me):
    accum = [Vector((0.0, 0.0, 0.0)) for _ in me.vertices]
    for poly in me.polygons:
        for vi in poly.vertices:
            accum[vi] += poly.normal
    out = []
    for i, n in enumerate(accum):
        if n.length > 1e-8:
            n.normalize()
        else:
            n = me.vertices[i].normal.copy()
            if n.length > 1e-8:
                n.normalize()
        out.append((AXIS @ n).normalized())
    return out


def paint_face_uv(obj):
    """Planar UV in Unity XZ, so dial_face.png matches the mesh after the axis conversion.

    The exporter maps Blender +X to Unity -X and Blender +Y to Unity +Z. UV is not
    converted, so it is written from the converted coordinates.
    """
    me = obj.data
    layer = me.uv_layers["UVMap"]
    for poly in me.polygons:
        for li in poly.loop_indices:
            unity = AXIS @ me.vertices[me.loops[li].vertex_index].co
            layer.data[li].uv = (0.5 + unity.x / (2.0 * FACE_R), 0.5 + unity.z / (2.0 * FACE_R))


def store_outline_normals(obj):
    """Smoothed, axis-converted normals in UV layers and a corner colour."""
    me = obj.data
    normals = smooth_normals(me)
    bm = bmesh.new()
    bm.from_mesh(me)
    uv_nxy = bm.loops.layers.uv.get("Nxy") or bm.loops.layers.uv.new("Nxy")
    uv_nz = bm.loops.layers.uv.get("Nz") or bm.loops.layers.uv.new("Nz")
    color = bm.loops.layers.color.get("SmoothN") or bm.loops.layers.color.new("SmoothN")
    for face in bm.faces:
        for loop in face.loops:
            n = normals[loop.vert.index]
            enc = (n.x * 0.5 + 0.5, n.y * 0.5 + 0.5, n.z * 0.5 + 0.5)
            loop[uv_nxy].uv = (enc[0], enc[1])
            loop[uv_nz].uv = (enc[2], 0.0)
            loop[color] = (enc[0], enc[1], enc[2], 1.0)
    bm.to_mesh(me)
    bm.free()
    # Corner colours survive the FBX as the active colour attribute.
    if "SmoothN" in me.color_attributes:
        me.color_attributes.active_color = me.color_attributes["SmoothN"]
    sample = normals[0]
    print("[probe] outline normal %s %s -> (%.3f, %.3f, %.3f)" % (obj.name, "stored", sample.x, sample.y, sample.z))


def probe_bake(obj):
    """Ask Blender to bake normals in -b, and record whatever it answers."""
    scene = bpy.context.scene
    try:
        enum = bpy.ops.object.bake.get_rna_type().properties["type"].enum_items
        names = [item.identifier for item in enum]
        print("[probe] bake types %s" % ",".join(names))
    except Exception as exc:
        print("[probe] bake types FAIL %s %s" % (type(exc).__name__, exc))
    try:
        scene.render.engine = "CYCLES"
        scene.cycles.device = "CPU"
        scene.cycles.samples = 1
        image = bpy.data.images.new("probe_normal", 8, 8, alpha=False)
        mat = bpy.data.materials.new("ProbeBake")
        mat.use_nodes = True
        node = mat.node_tree.nodes.new("ShaderNodeTexImage")
        node.image = image
        mat.node_tree.nodes.active = node
        if obj.data.materials:
            obj.data.materials[0] = mat
        else:
            obj.data.materials.append(mat)
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.bake(type="NORMAL", use_clear=True, margin=0)
        print("[probe] bake type=NORMAL result=ok size=%d" % len(image.pixels))
    except Exception as exc:
        print("[probe] bake type=NORMAL result=FAIL %s: %s" % (type(exc).__name__, exc))


def export(names, path):
    bpy.ops.object.select_all(action="DESELECT")
    for name in names:
        bpy.data.objects[name].select_set(True)
    kwargs = dict(
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
        colors_type="LINEAR",
    )
    # Older exporters reject colors_type. Drop it rather than lose the mesh.
    try:
        bpy.ops.export_scene.fbx(**kwargs)
    except TypeError as exc:
        print("[probe] fbx colors_type rejected %s" % exc)
        kwargs.pop("colors_type", None)
        bpy.ops.export_scene.fbx(**kwargs)
    print("[dial] exported", path, names)


def tri_count(obj):
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def bounds_line(obj):
    xs, ys, zs = [], [], []
    for v in obj.data.vertices:
        xs.append(v.co.x)
        ys.append(v.co.y)
        zs.append(v.co.z)
    return "x %.4f..%.4f y %.4f..%.4f z %.4f..%.4f" % (min(xs), max(xs), min(ys), max(ys), min(zs), max(zs))


def census(path):
    reset()
    bpy.ops.import_scene.fbx(filepath=path)
    total = 0
    for obj in bpy.data.objects:
        if obj.type != "MESH":
            continue
        count = tri_count(obj)
        total += count
        print("[dial] tris %s %s %d %s" % (os.path.basename(path), obj.name, count, bounds_line(obj)))
        me = obj.data
        layers = [layer.name for layer in me.uv_layers]
        colours = [attr.name for attr in me.color_attributes] if hasattr(me, "color_attributes") else []
        print("[probe] reimport %s uv=%s colors=%s" % (obj.name, ",".join(layers), ",".join(colours)))
    print("[dial] tris %s TOTAL %d" % (os.path.basename(path), total))
    return total


def save_png(name, width, height, rgba, path):
    image = bpy.data.images.new(name, width, height, alpha=True, float_buffer=True)
    image.pixels.foreach_set(rgba)
    image.filepath_raw = path
    image.file_format = "PNG"
    image.save()
    bpy.data.images.remove(image)


def noise(x, y):
    n = math.sin(x * 12.9898 + y * 78.233) * 43758.5453
    return n - math.floor(n)


def write_procedural(tex_dir):
    # Paper grain for the rim. Object-space grain in the shader sits on top of this.
    w = h = 128
    paper = [0.0] * (w * h * 4)
    for y in range(h):
        for x in range(w):
            n = noise(x * 0.37, y * 0.41) * 0.55 + noise(x * 0.11, y * 0.13) * 0.45
            tone = 0.90 + (n - 0.5) * 0.08
            i = (y * w + x) * 4
            paper[i] = tone
            paper[i + 1] = tone * 0.96
            paper[i + 2] = tone * 0.90
            paper[i + 3] = 1.0
    save_png("dial_paper", w, h, paper, os.path.join(tex_dir, "dial_paper.png"))

    # Silver nib. v 0 is the butt, v 1 is the tip. A dark slit runs up the blade.
    gw, gh = 256, 1024
    gnomon = [0.0] * (gw * gh * 4)
    for y in range(gh):
        v = y / (gh - 1)
        for x in range(gw):
            u = x / (gw - 1)
            lite = 0.86 + (noise(x * 0.37, y * 0.11) - 0.5) * 0.03
            lite += max(0.0, math.cos((u - 0.22) * math.pi * 2.0)) * 0.08
            lite -= max(0.0, math.cos((u - 0.78) * math.pi * 2.0)) * 0.05
            lite += max(0.0, v - 0.86) * 0.04
            slit = abs(u - 0.5) < (0.010 + 0.010 * (1.0 - v)) and v > 0.45
            collar = 0.06 < v < 0.13
            i = (y * gw + x) * 4
            if slit:
                gnomon[i] = 0.16
                gnomon[i + 1] = 0.15
                gnomon[i + 2] = 0.14
            elif collar:
                gnomon[i] = lite * 0.62
                gnomon[i + 1] = lite * 0.64
                gnomon[i + 2] = lite * 0.68
            else:
                gnomon[i] = lite * 0.90
                gnomon[i + 1] = lite * 0.93
                gnomon[i + 2] = lite * 0.98
            gnomon[i + 3] = 1.0
    save_png("gnomon", gw, gh, gnomon, os.path.join(tex_dir, "gnomon.png"))

    # Soft wash, bright at the butt (v 0) and gone by the tip. Alpha carries the shape.
    sw, sh = 64, 128
    shadow = [0.0] * (sw * sh * 4)
    for y in range(sh):
        v = y / (sh - 1)
        along = max(0.0, 1.0 - v) ** 1.35
        for x in range(sw):
            u = abs(x / (sw - 1) * 2.0 - 1.0)
            side = max(0.0, 1.0 - u * u)
            a = along * (side ** 1.6)
            i = (y * sw + x) * 4
            shadow[i] = 1.0
            shadow[i + 1] = 1.0
            shadow[i + 2] = 1.0
            shadow[i + 3] = a
    save_png("gnomon_shadow", sw, sh, shadow, os.path.join(tex_dir, "gnomon_shadow.png"))


def copy_interim(tex_dir):
    src = os.path.join(ROOT, "shared", "assets", "seed-textures", "sundial-drawn")
    names = [
        "dial_face.png", "soil.png",
        "plant_sunrise_0.png", "plant_sunrise_1.png", "plant_sunrise_2.png",
        "plant_midday_0.png", "plant_midday_1.png", "plant_midday_2.png",
        "plant_dusk_0.png", "plant_dusk_1.png", "plant_dusk_2.png",
        "halo_sunrise.png", "halo_midday.png", "halo_dusk.png",
    ]
    for name in names:
        shutil.copyfile(os.path.join(src, name), os.path.join(tex_dir, name))
        # Replace any older sidecar that described a different generator.
        sidecar = os.path.join(tex_dir, name + ".provenance.txt")
        with open(sidecar, "w", encoding="utf-8", newline="\n") as handle:
            handle.write(
                "asset: %s\n"
                "method: copied by tools/blender/sundial_dial.py from shared/assets/seed-textures/sundial-drawn\n"
                "note: painted by code (IWSDK seat SVG), interim\n"
                "derived_from_art_reference: no\n" % name
            )
    lines = [
        "# Dial textures",
        "",
        "Interim art for the drawn dial. Nothing in this folder that the dial uses is taken from the owner's reference frames.",
        "",
        "| File | Note |",
        "|---|---|",
        "| dial_face.png | painted by code (IWSDK seat SVG), interim |",
        "| soil.png | painted by code (IWSDK seat SVG), interim |",
        "| plant_sunrise_0.png | painted by code (IWSDK seat SVG), interim |",
        "| plant_sunrise_1.png | painted by code (IWSDK seat SVG), interim |",
        "| plant_sunrise_2.png | painted by code (IWSDK seat SVG), interim |",
        "| plant_midday_0.png | painted by code (IWSDK seat SVG), interim |",
        "| plant_midday_1.png | painted by code (IWSDK seat SVG), interim |",
        "| plant_midday_2.png | painted by code (IWSDK seat SVG), interim |",
        "| plant_dusk_0.png | painted by code (IWSDK seat SVG), interim |",
        "| plant_dusk_1.png | painted by code (IWSDK seat SVG), interim |",
        "| plant_dusk_2.png | painted by code (IWSDK seat SVG), interim |",
        "| halo_sunrise.png | painted by code (IWSDK seat SVG), interim |",
        "| halo_midday.png | painted by code (IWSDK seat SVG), interim |",
        "| halo_dusk.png | painted by code (IWSDK seat SVG), interim |",
        "| dial_paper.png | painted by code, paper grain for the rim, interim |",
        "| gnomon.png | painted by code, silver nib with an ink slit, interim |",
        "| gnomon_shadow.png | painted by code, soft wash, interim |",
        "",
    ]
    path = os.path.join(tex_dir, "PROVENANCE.md")
    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write("\n".join(lines))
    print("[dial] textures", tex_dir)


def build():
    print("[probe] axis blender +X -> %s" % (AXIS @ Vector((1, 0, 0))))
    print("[probe] axis blender +Y -> %s" % (AXIS @ Vector((0, 1, 0))))
    print("[probe] axis blender +Z -> %s" % (AXIS @ Vector((0, 0, 1))))
    reset()
    top = disc()
    side = rim()
    mound = soil()
    pen = gnomon()
    one = tile()
    probe_bake(one)
    paint_face_uv(top)
    paint_face_uv(mound)
    for obj in (top, side, mound, pen, one):
        store_outline_normals(obj)
    destination = out_dir()
    os.makedirs(destination, exist_ok=True)
    dial_path = os.path.join(destination, "drawn_dial.fbx")
    tile_path = os.path.join(destination, "Tile.fbx")
    export(["DialTop", "DialSide", "Soil", "Gnomon"], dial_path)
    # The tile shares the file with the dial parts only as its own mesh.
    # Re-export it alone so Unity can instance one raised tile.
    export(["Tile"], tile_path)
    dial_total = census(dial_path)
    tile_total = census(tile_path)
    print("[probe] vertex-colour smooth normals written")
    print("[dial] tris TOTAL %d (dial %d + tile %d)" % (dial_total + tile_total, dial_total, tile_total))
    print("[dial] outer diameter %.3f m gnomon %.3f m tile %.0fx%.0fx%.0f mm" % (
        OUTER * 2.0, GNOMON_L, TILE_L * 1000, TILE_W * 1000, TILE_H * 1000))
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if "--meshes-only" in args:
        print("[dial] meshes only; left textures in place")
        return
    tex_dir = os.path.join(ROOT, "apps", "sundial", "Assets", "Art", "Textures")
    os.makedirs(tex_dir, exist_ok=True)
    copy_interim(tex_dir)
    write_procedural(tex_dir)


if __name__ == "__main__":
    try:
        build()
    except Exception:
        traceback.print_exc()
        sys.exit(1)

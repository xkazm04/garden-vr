"""Field Notebook dial: board, soil, pen gnomon, seven tiles per arc.

Blender 4.2, headless, from the repo root:

    blender.exe -b -P tools/blender/sundial_hero.py

Metres, Z-up in Blender. FBX export is Y-up for Unity
(axis_forward=-Z, axis_up=Y). Outer diameter is 30 cm.
The gnomon is 11 cm. Each tile is 12 x 9 x 2 mm.

Set HERO_SKIP_RENDER=1 to export only. HERO_WIDTH_PX (default 1040) is the
on-screen diameter of the 30 cm face at the A2-05-1 framing.
"""
import math
import os
import sys
import traceback

import bpy
import bmesh
from mathutils import Vector, noise

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
TEX = os.path.join(ROOT, "apps", "sundial", "Assets", "Art", "Textures")
MOD = os.path.join(ROOT, "apps", "sundial", "Assets", "Art", "Models")
RUN = os.path.join(ROOT, "orchestration", "runs", "sundial", "T-SUN-013")
SKIP_RENDER = os.environ.get("HERO_SKIP_RENDER", "") == "1"
SAMPLES = int(os.environ.get("HERO_SAMPLES", "16"))
WIDTH_PX = float(os.environ.get("HERO_WIDTH_PX", "1040"))
AIM = (float(os.environ.get("HERO_AIM_X", "785")), float(os.environ.get("HERO_AIM_Y", "615")))

# Outer rim of the top face. The lip sits a millimetre outside this.
DIAL_R = 0.150
BOARD_Z = 0.014
SOIL_R = 0.086
GNOMON_L = 0.110
TILE_L = 0.012
TILE_W = 0.009
TILE_H = 0.002

# Same angles as the face texture. 0 is +X, 90 is the far side of the dial.
ARCS = [
    ("sunrise", 242, 112, (0.90, 0.72, 0.40, 1)),
    ("midday", 106, 22, (0.86, 0.48, 0.42, 1)),
    ("dusk", 16, -100, (0.58, 0.48, 0.70, 1)),
]
PALE = (0.94, 0.91, 0.85, 1)
LATE = (0.78, 0.74, 0.68, 1)


def lin(r, g, b, a=1.0):
    def c(u):
        return u / 12.92 if u <= 0.04045 else ((u + 0.055) / 1.055) ** 2.4
    return (c(r), c(g), c(b), a)


def srgb(rgba):
    return lin(rgba[0], rgba[1], rgba[2], rgba[3] if len(rgba) > 3 else 1.0)


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
    return obj


def activate(obj):
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


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


def dial_xy(radius, deg):
    """Texture angle to Blender XY. +90 deg is the far side (image up)."""
    a = math.radians(deg)
    # Camera on +Y: image-right is world -X. Negate X so texture-right is image-right.
    return -radius * math.cos(a), -radius * math.sin(a)


def load_image(filename):
    path = os.path.join(TEX, filename)
    if not os.path.exists(path):
        raise SystemExit("missing texture %s" % path)
    img = bpy.data.images.load(path)
    try:
        img.colorspace_settings.name = "sRGB"
    except (TypeError, ValueError):
        pass
    return img


def new_mat(name):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.node_tree.nodes.clear()
    return mat


def sock(node, *names):
    for name in names:
        if name in node.inputs:
            return node.inputs[name]
    raise KeyError(names)


def toon_nodes(mat, color_socket_out, alpha_socket=None):
    """2-step toon into emission. Flat tops stay on the lit step."""
    nt = mat.node_tree
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    emit = nt.nodes.new("ShaderNodeEmission")
    geom = nt.nodes.new("ShaderNodeNewGeometry")
    light = nt.nodes.new("ShaderNodeCombineXYZ")
    # Image-left is world +X when the camera sits on +Y.
    light.inputs[0].default_value = 0.55
    light.inputs[1].default_value = -0.15
    light.inputs[2].default_value = 0.82
    dot = nt.nodes.new("ShaderNodeVectorMath")
    dot.operation = "DOT_PRODUCT"
    nt.links.new(geom.outputs["Normal"], dot.inputs[0])
    nt.links.new(light.outputs["Vector"], dot.inputs[1])
    comp = nt.nodes.new("ShaderNodeMath")
    comp.operation = "GREATER_THAN"
    comp.inputs[1].default_value = 0.32
    nt.links.new(dot.outputs["Value"], comp.inputs[0])
    tint = nt.nodes.new("ShaderNodeRGB")
    tint.outputs[0].default_value = (0.84, 0.76, 0.68, 1.0)  # 0.78 with a warm shift
    shade = nt.nodes.new("ShaderNodeMixRGB")
    shade.blend_type = "MULTIPLY"
    shade.inputs["Fac"].default_value = 1.0
    nt.links.new(color_socket_out, shade.inputs["Color1"])
    nt.links.new(tint.outputs["Color"], shade.inputs["Color2"])
    mix = nt.nodes.new("ShaderNodeMixRGB")
    nt.links.new(comp.outputs["Value"], mix.inputs["Fac"])
    nt.links.new(shade.outputs["Color"], mix.inputs["Color1"])
    nt.links.new(color_socket_out, mix.inputs["Color2"])
    nt.links.new(mix.outputs["Color"], emit.inputs["Color"])
    if alpha_socket is None:
        nt.links.new(emit.outputs["Emission"], out.inputs["Surface"])
        return
    trans = nt.nodes.new("ShaderNodeBsdfTransparent")
    mix_s = nt.nodes.new("ShaderNodeMixShader")
    clip = nt.nodes.new("ShaderNodeMath")
    clip.operation = "GREATER_THAN"
    clip.inputs[1].default_value = 0.35
    nt.links.new(alpha_socket, clip.inputs[0])
    nt.links.new(clip.outputs["Value"], mix_s.inputs["Fac"])
    nt.links.new(trans.outputs["BSDF"], mix_s.inputs[1])
    nt.links.new(emit.outputs["Emission"], mix_s.inputs[2])
    nt.links.new(mix_s.outputs["Shader"], out.inputs["Surface"])
    mat.blend_method = "CLIP"
    if hasattr(mat, "alpha_threshold"):
        mat.alpha_threshold = 0.35
    mat.use_backface_culling = False
    if hasattr(mat, "surface_render_method"):
        mat.surface_render_method = "DITHERED"


def mat_color(name, rgba):
    mat = new_mat(name)
    rgb = mat.node_tree.nodes.new("ShaderNodeRGB")
    rgb.outputs[0].default_value = srgb(rgba)
    toon_nodes(mat, rgb.outputs["Color"])
    return mat


def mat_image(name, img, unlit=False):
    mat = new_mat(name)
    nt = mat.node_tree
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = img
    if unlit:
        out = nt.nodes.new("ShaderNodeOutputMaterial")
        emit = nt.nodes.new("ShaderNodeEmission")
        trans = nt.nodes.new("ShaderNodeBsdfTransparent")
        mix = nt.nodes.new("ShaderNodeMixShader")
        clip = nt.nodes.new("ShaderNodeMath")
        clip.operation = "GREATER_THAN"
        clip.inputs[1].default_value = 0.35
        nt.links.new(tex.outputs["Color"], emit.inputs["Color"])
        nt.links.new(tex.outputs["Alpha"], clip.inputs[0])
        nt.links.new(clip.outputs["Value"], mix.inputs["Fac"])
        nt.links.new(trans.outputs["BSDF"], mix.inputs[1])
        nt.links.new(emit.outputs["Emission"], mix.inputs[2])
        nt.links.new(mix.outputs["Shader"], out.inputs["Surface"])
        mat.blend_method = "CLIP"
        if hasattr(mat, "alpha_threshold"):
            mat.alpha_threshold = 0.35
        mat.use_backface_culling = False
        if hasattr(mat, "surface_render_method"):
            mat.surface_render_method = "DITHERED"
    else:
        toon_nodes(mat, tex.outputs["Color"])
    return mat


def mat_halo(img):
    mat = new_mat("Halo")
    nt = mat.node_tree
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    emit = nt.nodes.new("ShaderNodeEmission")
    trans = nt.nodes.new("ShaderNodeBsdfTransparent")
    mix = nt.nodes.new("ShaderNodeMixShader")
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = img
    nt.links.new(tex.outputs["Color"], emit.inputs["Color"])
    emit.inputs["Strength"].default_value = 1.4
    nt.links.new(tex.outputs["Alpha"], mix.inputs["Fac"])
    nt.links.new(trans.outputs["BSDF"], mix.inputs[1])
    nt.links.new(emit.outputs["Emission"], mix.inputs[2])
    nt.links.new(mix.outputs["Shader"], out.inputs["Surface"])
    mat.blend_method = "BLEND"
    mat.use_backface_culling = False
    if hasattr(mat, "surface_render_method"):
        mat.surface_render_method = "BLENDED"
    return mat


def assign(obj, mat):
    obj.data.materials.clear()
    obj.data.materials.append(mat)


# ---------------------------------------------------------------- geometry
def lathe(name, profile, segs=128):
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
            for loop, uu, vv in zip(
                f.loops,
                (s / segs, (s + 1) / segs, (s + 1) / segs, s / segs),
                (v0, v0, v1, v1),
            ):
                loop[uv].uv = (uu, vv)
    poles = [v for v in bm.verts if math.hypot(v.co.x, v.co.y) < 1e-7]
    if poles:
        bmesh.ops.remove_doubles(bm, verts=poles, dist=1e-6)
    obj = finish(bm, name)
    for p in obj.data.polygons:
        p.use_smooth = True
    return obj


def dial_cap():
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    segs = 160
    centre = bm.verts.new((0, 0, BOARD_Z))
    ring = []
    for s in range(segs):
        a = 2 * math.pi * s / segs
        ring.append(bm.verts.new((DIAL_R * math.cos(a), DIAL_R * math.sin(a), BOARD_Z)))
    for s in range(segs):
        f = bm.faces.new((centre, ring[s], ring[(s + 1) % segs]))
        for loop in f.loops:
            co = loop.vert.co
            loop[uv].uv = (0.5 - co.x / (2 * DIAL_R), 0.5 - co.y / (2 * DIAL_R))
    obj = finish(bm, "DialTop")
    for p in obj.data.polygons:
        p.use_smooth = False
    return obj


def dial_rim():
    # Stepped radius so the edge reads as stacked paper over a wood core.
    profile = [
        (0.1500, 0.0140),
        (0.1512, 0.0126),
        (0.1504, 0.0116),
        (0.1490, 0.0108),
        (0.1503, 0.0096),
        (0.1488, 0.0086),
        (0.1498, 0.0070),
        (0.1506, 0.0048),
        (0.1494, 0.0024),
        (0.1476, 0.0006),
        (0.0000, 0.0000),
    ]
    return lathe("DialRim", profile, 128)


def soil_mound():
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    rings_n = 28
    sg = 96
    rows = [[bm.verts.new((0.0, 0.0, soil_height(0, 0)))]]
    for j in range(1, rings_n + 1):
        rr = SOIL_R * j / rings_n
        row = []
        for s in range(sg):
            a = 2 * math.pi * s / sg
            x, y = rr * math.cos(a), rr * math.sin(a)
            edge = j == rings_n
            wob = 0.0 if edge else noise.noise(Vector((x * 80, y * 80, 1.5))) * 0.0016
            row.append(bm.verts.new((x, y, soil_height(x, y) + wob)))
        rows.append(row)
    for s in range(sg):
        bm.faces.new((rows[0][0], rows[1][s], rows[1][(s + 1) % sg]))
    for j in range(1, rings_n):
        for s in range(sg):
            bm.faces.new((rows[j][s], rows[j + 1][s], rows[j + 1][(s + 1) % sg], rows[j][(s + 1) % sg]))
    for f in bm.faces:
        for loop in f.loops:
            co = loop.vert.co
            loop[uv].uv = (0.5 - co.x / (2 * SOIL_R), 0.5 - co.y / (2 * SOIL_R))
    obj = finish(bm, "SoilBed")
    for p in obj.data.polygons:
        p.use_smooth = True
    return obj


def soil_height(x, y):
    r = math.hypot(x, y) / SOIL_R
    r = min(1.0, r)
    return BOARD_Z + 0.011 * (1.0 - r ** 2.15)


def gnomon():
    # Butt at z=0, sharp nib at z=11 cm. Collar is the gold band.
    profile = [
        (0.0054, 0.000),
        (0.0058, 0.014),
        (0.0052, 0.026),
        (0.0066, 0.032),
        (0.0070, 0.042),
        (0.0050, 0.048),
        (0.0038, 0.064),
        (0.0024, 0.086),
        (0.0010, 0.102),
        (0.0002, GNOMON_L),
    ]
    obj = lathe("Gnomon", profile, 28)
    body = mat_color("GnomonInk", (0.16, 0.15, 0.13, 1))
    gold = mat_color("GnomonGold", (0.78, 0.63, 0.35, 1))
    obj.data.materials.append(body)
    obj.data.materials.append(gold)
    for p in obj.data.polygons:
        z = sum(obj.data.vertices[i].co.z for i in p.vertices) / len(p.vertices)
        p.material_index = 1 if 0.030 < z < 0.046 else 0
    # Lean the nib toward the viewer (+Y) and a little to image-right (world -X).
    obj.rotation_euler = (math.radians(-14), 0.0, 0.0)
    obj.location = (0.0, 0.0, soil_height(0, 0) - 0.004)
    activate(obj)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return obj


def rounded_tile(name):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co.x *= TILE_L
        v.co.y *= TILE_W
        v.co.z *= TILE_H
        v.co.z += TILE_H / 2.0
    uv = bm.loops.layers.uv.new("UVMap")
    bmesh.ops.bevel(bm, geom=list(bm.edges), offset=0.0007, segments=2, affect="EDGES")
    for f in bm.faces:
        for loop in f.loops:
            loop[uv].uv = (loop.vert.co.x / TILE_L + 0.5, loop.vert.co.y / TILE_W + 0.5)
    obj = finish(bm, name)
    for p in obj.data.polygons:
        p.use_smooth = False
    return obj


def place_tiles(proto):
    """Seven slots along each arc, inset so they stay inside the wash."""
    placed = []
    states = ("kept", "kept", "pale", "kept", "late", "kept", "pale")
    mats = {}
    for arc_name, a0, a1, kept in ARCS:
        span = (a0 - a1) % 360
        for i, state in enumerate(states):
            t = 0.12 + 0.76 * (i / 6.0)
            deg = a0 - span * t
            ang = math.radians(deg)
            x, y = dial_xy(0.128, deg)
            dup = proto.copy()
            dup.data = proto.data.copy()
            bpy.context.collection.objects.link(dup)
            dup.location = (x, y, BOARD_Z)
            # derivative of dial_xy (-cos, -sin)
            tangent = (math.sin(ang), -math.cos(ang))
            dup.rotation_euler = (0.0, 0.0, math.atan2(tangent[1], tangent[0]))
            key = "%s-%s" % (arc_name, state)
            if key not in mats:
                colour = kept if state == "kept" else PALE if state == "pale" else LATE
                mats[key] = mat_color("Tile_" + key, colour)
            assign(dup, mats[key])
            activate(dup)
            bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
            placed.append(dup)
    activate(placed[0])
    for o in placed:
        o.select_set(True)
    bpy.ops.object.join()
    placed[0].name = "Tiles"
    return placed[0]


def plant_card(name, width, height):
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    segs = 6
    rows = []
    for j in range(segs + 1):
        v = j / segs
        row = []
        for i in range(2):
            u = i
            x = (u - 0.5) * width
            bend = -0.003 * ((u - 0.5) * 2) ** 2
            row.append(bm.verts.new((x, bend, v * height)))
        rows.append(row)
    for j in range(segs):
        f = bm.faces.new((rows[j][0], rows[j][1], rows[j + 1][1], rows[j + 1][0]))
        for loop, uu, vv in zip(f.loops, (0, 1, 1, 0), (j / segs, j / segs, (j + 1) / segs, (j + 1) / segs)):
            loop[uv].uv = (uu, vv)
    obj = finish(bm, name)
    for p in obj.data.polygons:
        p.use_smooth = True
    return obj


def place_plants():
    specs = [
        ("sunrise", 162, 0.048, 0.070, -8),
        ("midday", 70, 0.052, 0.076, 6),
        ("dusk", -38, 0.046, 0.062, 14),
    ]
    cards = []
    for kind, deg, radius, height, yaw in specs:
        img = load_image("plant_%s_0.png" % kind)
        aspect = img.size[0] / img.size[1]
        card = plant_card("Plant_" + kind, height * aspect, height)
        assign(card, mat_image("PlantMat_" + kind, img, unlit=True))
        x, y = dial_xy(radius, deg)
        card.location = (x, y, soil_height(x, y) - 0.006)
        card.rotation_euler = (math.radians(6), 0.0, math.radians(yaw))
        cards.append(card)
        if kind == "midday":
            halo_img = load_image("halo_midday.png")
            scale = halo_img.size[1] / img.size[1]
            halo_h = height * scale
            halo_w = height * aspect * (halo_img.size[0] / img.size[0])
            halo = plant_card("Halo_midday", halo_w, halo_h)
            assign(halo, mat_image("HaloMat", halo_img, unlit=True))
            extra = (halo_h - height) / 2.0
            halo.location = (x, y - 0.006, card.location.z - extra)
            halo.rotation_euler = card.rotation_euler.copy()
            cards.append(halo)
    return cards


def gnomon_shadow():
    """Soft painted wedge. Light is from the upper left, so the wash falls down-right."""
    direction = Vector((-0.55, 0.20, 0.0)).normalized()
    side = Vector((-direction.y, direction.x, 0.0))
    origin = Vector((0.0, 0.004, 0.0))
    length = 0.092
    bm = bmesh.new()
    col = bm.loops.layers.color.new("Col")
    steps = 8
    rows = []
    for i in range(steps + 1):
        t = i / steps
        centre = origin + direction * (length * t)
        half = 0.004 + 0.018 * t
        row = []
        for s in (-1, 1):
            p = centre + side * (half * s)
            p.z = soil_height(p.x, p.y) + 0.0012
            row.append(bm.verts.new(p))
        rows.append(row)
    for i in range(steps):
        f = bm.faces.new((rows[i][0], rows[i][1], rows[i + 1][1], rows[i + 1][0]))
        for loop, vert_i in zip(f.loops, (rows[i][0], rows[i][1], rows[i + 1][1], rows[i + 1][0])):
            t = 0 if vert_i in rows[i] else 1
            # fade along the length; the index is recovered from z-order of rows
        for loop in f.loops:
            # t from how far the vert is along the wedge
            along = (Vector((loop.vert.co.x, loop.vert.co.y, 0)) - origin).dot(direction) / length
            fade = max(0.0, 1.0 - along) ** 1.3
            loop[col] = (0.35, 0.30, 0.26, 0.38 * fade)
    obj = finish(bm, "GnomonShadow")
    mat = new_mat("ShadowWash")
    nt = mat.node_tree
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    emit = nt.nodes.new("ShaderNodeEmission")
    trans = nt.nodes.new("ShaderNodeBsdfTransparent")
    mix = nt.nodes.new("ShaderNodeMixShader")
    vc = nt.nodes.new("ShaderNodeVertexColor")
    vc.layer_name = "Col"
    emit.inputs["Color"].default_value = (0.25, 0.20, 0.16, 1)
    nt.links.new(vc.outputs["Alpha"], mix.inputs["Fac"])
    nt.links.new(trans.outputs["BSDF"], mix.inputs[1])
    nt.links.new(emit.outputs["Emission"], mix.inputs[2])
    nt.links.new(mix.outputs["Shader"], out.inputs["Surface"])
    mat.blend_method = "BLEND"
    if hasattr(mat, "surface_render_method"):
        mat.surface_render_method = "BLENDED"
    assign(obj, mat)
    return obj


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
        path_mode="STRIP",
        embed_textures=False,
        global_scale=1.0,
    )
    print("[hero] exported", filename, [o.name for o in objs])


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


def solve_shift(scene, cam, look):
    """Put the dial centre on the reference pixel, whatever the shift sign is."""
    cam.data.shift_x = 0.0
    cam.data.shift_y = 0.0
    bpy.context.view_layer.update()
    x0, y0 = project(scene, cam, look)

    def sample(sx, sy):
        cam.data.shift_x = sx
        cam.data.shift_y = sy
        bpy.context.view_layer.update()
        return project(scene, cam, look)

    x1, y1 = sample(0.05, 0.0)
    x2, y2 = sample(0.0, 0.05)
    j00, j01 = (x1 - x0) / 0.05, (x2 - x0) / 0.05
    j10, j11 = (y1 - y0) / 0.05, (y2 - y0) / 0.05
    det = j00 * j11 - j01 * j10
    if abs(det) < 1e-6:
        raise SystemExit("camera shift jacobian is singular")
    ex, ey = AIM[0] - x0, AIM[1] - y0
    sx = (ex * j11 - j01 * ey) / det
    sy = (j00 * ey - ex * j10) / det
    cam.data.shift_x = sx
    cam.data.shift_y = sy
    bpy.context.view_layer.update()
    # one more linear step from the new point
    x3, y3 = project(scene, cam, look)
    ex, ey = AIM[0] - x3, AIM[1] - y3
    cam.data.shift_x = sx + (ex * j11 - j01 * ey) / det
    cam.data.shift_y = sy + (j00 * ey - ex * j10) / det
    bpy.context.view_layer.update()


def frame_camera(scene, cam, look):
    cam.data.sensor_fit = "VERTICAL"
    cam.data.angle = math.radians(30.0)
    cam.data.clip_start = 0.01
    cam.data.clip_end = 10.0
    f = (scene.render.resolution_y / 2.0) / math.tan(cam.data.angle / 2.0)
    distance = f * (DIAL_R * 2.0) / WIDTH_PX
    aim(cam, look, distance, 37.0)
    # width is not exactly f*size/d once the disc is tilted; nudge distance
    for _ in range(6):
        left = project(scene, cam, (look[0] - DIAL_R, look[1], look[2]))
        right = project(scene, cam, (look[0] + DIAL_R, look[1], look[2]))
        width = abs(right[0] - left[0])
        if width < 1:
            break
        distance *= width / WIDTH_PX
        aim(cam, look, distance, 37.0)
    solve_shift(scene, cam, look)
    left = project(scene, cam, (look[0] - DIAL_R, look[1], look[2]))
    right = project(scene, cam, (look[0] + DIAL_R, look[1], look[2]))
    centre = project(scene, cam, look)
    print("[hero] camera loc %s distance %.4f" % (tuple(round(c, 4) for c in cam.location), distance))
    print("[hero] project centre (%.1f, %.1f) width %.1f target %s x %.1f" % (
        centre[0], centre[1], abs(right[0] - left[0]), AIM, WIDTH_PX))
    return centre, abs(right[0] - left[0])


def setup_engine(scene):
    engines = [e.identifier for e in scene.render.bl_rna.properties["engine"].enum_items]
    print("[hero] engines", engines)
    scene.render.engine = "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in engines else "BLENDER_EEVEE"
    ee = scene.eevee
    for attr, val in (("taa_render_samples", SAMPLES), ("taa_samples", 8)):
        if hasattr(ee, attr):
            setattr(ee, attr, val)
    if hasattr(ee, "use_raytracing"):
        ee.use_raytracing = False
    scene.render.resolution_x = 1824
    scene.render.resolution_y = 1024
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    transforms = [e.identifier for e in scene.view_settings.bl_rna.properties["view_transform"].enum_items]
    print("[hero] view transforms", transforms)
    for name in ("Standard", "sRGB", "Raw"):
        if name in transforms:
            scene.view_settings.view_transform = name
            break
    scene.view_settings.look = "None"
    scene.view_settings.exposure = 0.0
    world = bpy.data.worlds.new("Empty")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (0, 0, 0, 0)
    bg.inputs["Strength"].default_value = 0.0


def verify_fbx(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    print("[hero] reimport", os.path.basename(path))
    for o in bpy.context.scene.objects:
        if o.type != "MESH":
            continue
        z0, z1, r = bounds(o)
        print("   %s z %.4f..%.4f r %.4f tris %d" % (o.name, z0, z1, r, tri_count(o)))


def build():
    scene = reset()
    os.makedirs(MOD, exist_ok=True)
    os.makedirs(RUN, exist_ok=True)
    print("[hero] blender", bpy.app.version_string)

    cap = dial_cap()
    rim = dial_rim()
    assign(cap, mat_image("DialFace", load_image("dial_face.png")))
    assign(rim, mat_color("DialRim", (0.91, 0.86, 0.76, 1)))
    soil = soil_mound()
    assign(soil, mat_image("Soil", load_image("soil.png")))
    pen = gnomon()
    shadow = gnomon_shadow()
    proto = rounded_tile("Tile")
    assign(proto, mat_color("TileProto", PALE))
    tiles = place_tiles(proto)
    cards = place_plants()

    # A card kept at the origin, for later scene placement.
    origin_card = plant_card("PlantCard", 0.050, 0.075)
    assign(origin_card, mat_color("PlantCardFlat", (0.7, 0.75, 0.6, 1)))

    print("[hero] tris DialTop %d" % tri_count(cap))
    print("[hero] tris DialRim %d" % tri_count(rim))
    print("[hero] tris SoilBed %d" % tri_count(soil))
    print("[hero] tris Gnomon %d" % tri_count(pen))
    print("[hero] tris Tile %d" % tri_count(proto))
    print("[hero] tris Tiles %d" % tri_count(tiles))
    print("[hero] tris PlantCard %d" % tri_count(origin_card))
    for o in (cap, rim, soil, pen, proto, tiles, origin_card):
        z0, z1, r = bounds(o)
        print("[hero] bounds %s z %.4f..%.4f r %.4f" % (o.name, z0, z1, r))

    z0, z1, r = bounds(rim)
    diameter = r * 2
    if not (0.30 * 0.95 <= diameter <= 0.30 * 1.05):
        raise SystemExit("dial diameter %.4f m is outside 30 cm +/- 5%%" % diameter)
    # nib is the vertex farthest from the soil centre
    far = max(pen.data.vertices, key=lambda v: (pen.matrix_world @ v.co).length)
    tip = pen.matrix_world @ far.co
    base = min(pen.data.vertices, key=lambda v: (pen.matrix_world @ v.co).z)
    butt = pen.matrix_world @ base.co
    length = (tip - butt).length
    print("[hero] gnomon tip %s butt %s length %.4f" % (
        tuple(round(c, 4) for c in tip), tuple(round(c, 4) for c in butt), length))
    if not (GNOMON_L * 0.90 <= length <= GNOMON_L * 1.10):
        raise SystemExit("gnomon length %.4f m is outside 11 cm +/- 10%%" % length)
    if tiles.data and tri_count(tiles) < 21:
        raise SystemExit("tiles mesh is empty")

    export_fbx([cap, rim], "DialBoard.fbx")
    export_fbx([soil], "SoilBed.fbx")
    export_fbx([pen], "Gnomon.fbx")
    export_fbx([proto], "Tile.fbx")
    export_fbx([tiles], "Tiles.fbx")
    export_fbx([origin_card], "PlantCard.fbx")

    total = sum(tri_count(o) for o in (cap, rim, soil, pen, proto, tiles, origin_card))
    scene_tris = total - tri_count(proto) - tri_count(origin_card)
    for extra in cards + [shadow]:
        scene_tris += tri_count(extra)
    print("[hero] tris EXPORTED %d" % total)
    print("[hero] tris PREVIEW %d (placed cards, halo, shadow; proto tile and proto card not double counted)" % scene_tris)

    if not SKIP_RENDER:
        setup_engine(scene)
        cam_data = bpy.data.cameras.new("DialCam")
        cam = bpy.data.objects.new("DialCam", cam_data)
        bpy.context.collection.objects.link(cam)
        scene.camera = cam
        look = (0.0, 0.0, BOARD_Z)
        centre, width = frame_camera(scene, cam, look)
        if abs(centre[0] - AIM[0]) > 8 or abs(centre[1] - AIM[1]) > 8:
            raise SystemExit("framing missed the aim pixel: %s" % (centre,))
        # hide the origin proto so it does not sit in the middle of the render
        proto.hide_render = True
        origin_card.hide_render = True
        path = os.path.join(RUN, "dial-render.png")
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        print("[hero] rendered", path)

    for filename in ("DialBoard.fbx", "Gnomon.fbx", "Tiles.fbx", "SoilBed.fbx"):
        verify_fbx(os.path.join(MOD, filename))
    print("[hero] done")


def main():
    try:
        build()
    except SystemExit:
        raise
    except Exception:
        traceback.print_exc()
        sys.exit(1)


if __name__ == "__main__":
    main()

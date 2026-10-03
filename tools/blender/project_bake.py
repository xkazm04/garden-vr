"""Project a camera image onto the moss mound and bake it to the mound's own UV.

    blender.exe -b -P tools/blender/project_bake.py -- --op checker --out <dir>
    blender.exe -b -P tools/blender/project_bake.py -- --op passes --out <dir>
    blender.exe -b -P tools/blender/project_bake.py -- --op bake --views views.json --previous macro.png --out <dir>

`--op checker` projects a known checker from the JarG1 camera and re-renders it.
The mean absolute difference inside the mound mask must be under 0.05.
The colour is not multiplied by the facing weight. That weight is only for
blending several repaints. A facing multiply would make the re-render darker
than the checker and the gate would fail for a reason that is not alignment.

UV_PROJECT was probed: after apply, a vertex UV matches world_to_camera_view
within 1e-7. This script writes the same UVs with world_to_camera_view so a
vertex the depth test rejects can be moved off the image. project_from_view
is not used. It needs a viewport.

The mound comes from terrarium_moss.build_mound, the same sheet the jar uses.
Its planar UV stays the bake target. Jar_Moss is not edited. The moss look is locked.
"""
from __future__ import annotations

import array
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
from mathutils import Vector

import f4_lib
import terrarium_moss

VIEWS = (
    {"id": "gate", "yaw": 0.0, "pitch": 0.0},
    {"id": "yaw-pos", "yaw": 18.0, "pitch": 0.0},
    {"id": "yaw-neg", "yaw": -18.0, "pitch": 0.0},
    {"id": "pitch-up", "yaw": 0.0, "pitch": 12.0},
)
MACRO = os.path.join("apps", "terrarium", "Assets", "Art", "Textures", "moss_macro.png")


def argv_after():
    if "--" not in sys.argv:
        return []
    return sys.argv[sys.argv.index("--") + 1:]


def flag(args, name, default=None):
    if name not in args:
        return default
    index = args.index(name)
    if index + 1 >= len(args):
        raise SystemExit("missing value for %s" % name)
    return args[index + 1]


def say(line):
    print(line, flush=True)


def srgb_to_linear(c):
    if c <= 0.04045:
        return c / 12.92
    return ((c + 0.055) / 1.055) ** 2.4


def linear_buffer_from_png(path):
    """Loaded PNG pixels come back as the sRGB encoding. Return scene-linear RGB."""
    image = f4_lib.load_image(path, "Non-Color")
    width, height, buf = f4_lib.image_buffer(image)
    out = array.array("f", [0.0]) * (width * height * 3)
    for i in range(width * height):
        for c in range(3):
            out[i * 3 + c] = srgb_to_linear(buf[i * 4 + c])
    bpy.data.images.remove(image)
    return width, height, out


def encoded_buffer(path):
    image = f4_lib.load_image(path, "Non-Color")
    width, height, buf = f4_lib.image_buffer(image)
    bpy.data.images.remove(image)
    return width, height, buf


def prepare_scene():
    f4_lib.reset_empty()
    scene = bpy.context.scene
    f4_lib.configure_color(scene)
    f4_lib.configure_cycles(scene, 1, f4_lib.W, f4_lib.H)
    device = f4_lib.set_cycles_device("OPTIX")
    say("[project] device %s" % device.get("type"))
    return scene, device


def add_mound():
    moss = terrarium_moss.build_mound()
    say("[project] mound tris %d" % f4_lib.tri_count(moss))
    return moss


def place_view(scene, view):
    eye, look = f4_lib.view_directions(view["yaw"], view["pitch"])
    angle = f4_lib.view_angle_deg(eye, look, f4_lib.EYE, f4_lib.LOOK)
    if angle > 25.0 + 1e-3:
        raise RuntimeError("view %s is %.2f deg off JarG1, limit 25" % (view["id"], angle))
    cam = f4_lib.add_camera_unity(scene, eye, look, "Cam_" + view["id"])
    return cam, eye, look, angle


def reject_occluded(obj, scene, cam, uv_name):
    """Move loops the camera cannot see off the projection image.

    A hit more than 3 mm in front of the vertex is another surface. The mound
    is a single sheet, so this is mostly the depth test the dossier asks for.
    """
    depsgraph = bpy.context.evaluated_depsgraph_get()
    origin = cam.matrix_world.translation.copy()
    layer = obj.data.uv_layers[uv_name]
    hidden = 0
    for poly in obj.data.polygons:
        for loop_index in poly.loop_indices:
            vert = obj.data.vertices[obj.data.loops[loop_index].vertex_index]
            world = obj.matrix_world @ vert.co
            direction = world - origin
            dist = direction.length
            if dist < 1e-8:
                continue
            direction = direction / dist
            hit, loc, _normal, _index, hit_obj, _matrix = scene.ray_cast(
                depsgraph, origin + direction * 0.001, direction, distance=dist
            )
            if hit and (loc - origin).length < dist - 0.003:
                layer.data[loop_index].uv = (-1.0, -1.0)
                hidden += 1
                _ = hit_obj
    return hidden


def projection_material(image, uv_name, weight_only):
    mat = bpy.data.materials.new("Project")
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    emit = nt.nodes.new("ShaderNodeEmission")
    emit.inputs["Strength"].default_value = 1.0
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = image
    tex.interpolation = "Closest"
    tex.extension = "CLIP"
    uv = nt.nodes.new("ShaderNodeUVMap")
    uv.uv_map = uv_name
    nt.links.new(uv.outputs["UV"], tex.inputs["Vector"])
    if weight_only:
        geom = nt.nodes.new("ShaderNodeNewGeometry")
        dot = nt.nodes.new("ShaderNodeVectorMath")
        dot.operation = "DOT_PRODUCT"
        nt.links.new(geom.outputs["Normal"], dot.inputs[0])
        nt.links.new(geom.outputs["Incoming"], dot.inputs[1])
        clamp = nt.nodes.new("ShaderNodeMath")
        clamp.operation = "MAXIMUM"
        clamp.inputs[1].default_value = 0.0
        nt.links.new(dot.outputs["Value"], clamp.inputs[0])
        power = nt.nodes.new("ShaderNodeMath")
        power.operation = "POWER"
        power.inputs[1].default_value = 2.0
        nt.links.new(clamp.outputs["Value"], power.inputs[0])
        nt.links.new(power.outputs["Value"], emit.inputs["Strength"])
        emit.inputs["Color"].default_value = (1.0, 1.0, 1.0, 1.0)
    else:
        nt.links.new(tex.outputs["Color"], emit.inputs["Color"])
    nt.links.new(emit.outputs["Emission"], out.inputs["Surface"])
    return mat


def bake_current(obj, image, samples=1):
    # Destination is the mound's own UV. The shader node names the projection UV.
    layer = obj.data.uv_layers.get("UVMap")
    if layer is not None:
        obj.data.uv_layers.active = layer
        layer.active_render = True
    return f4_lib.bake(obj, image, "EMIT", samples=samples)


def render_passes(scene, cam, obj, out, view_id, albedo_image):
    os.makedirs(out, exist_ok=True)
    f4_lib.configure_cycles(scene, 1, f4_lib.W, f4_lib.H)
    flat_mat = f4_lib.make_emission_image_material("Flat", albedo_image, "UVMap")
    for node in flat_mat.node_tree.nodes:
        if node.type == "TEX_IMAGE":
            node.interpolation = "Linear"
            node.extension = "EXTEND"
    f4_lib.assign_material(obj, flat_mat)
    flat_path = os.path.join(out, "flat-%s.png" % view_id)
    f4_lib.render_still(scene, cam, flat_path, samples=1)
    mask_mat = f4_lib.make_solid_emission("MaskWhite", (1.0, 1.0, 1.0))
    f4_lib.assign_material(obj, mask_mat)
    mask_path = os.path.join(out, "mask-%s.png" % view_id)
    f4_lib.render_still(scene, cam, mask_path, samples=1)
    depth_mat = depth_material()
    f4_lib.assign_material(obj, depth_mat)
    depth_path = os.path.join(out, "depth-%s.png" % view_id)
    f4_lib.render_still(scene, cam, depth_path, samples=1)
    normal_mat = camera_normal_material()
    f4_lib.assign_material(obj, normal_mat)
    normal_path = os.path.join(out, "normal-%s.png" % view_id)
    f4_lib.render_still(scene, cam, normal_path, samples=1)
    return {"flat": flat_path, "mask": mask_path, "depth": depth_path, "normal": normal_path}


def depth_material():
    mat = bpy.data.materials.new("Depth")
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    emit = nt.nodes.new("ShaderNodeEmission")
    cam = nt.nodes.new("ShaderNodeCameraData")
    span = nt.nodes.new("ShaderNodeMapRange")
    # View Z is negative in front of the camera. The jar sits about 0.45 m away.
    span.inputs["From Min"].default_value = -0.55
    span.inputs["From Max"].default_value = -0.35
    span.inputs["To Min"].default_value = 0.0
    span.inputs["To Max"].default_value = 1.0
    span.clamp = True
    nt.links.new(cam.outputs["View Z"], span.inputs["Value"])
    nt.links.new(span.outputs["Result"], emit.inputs["Color"])
    nt.links.new(emit.outputs["Emission"], out.inputs["Surface"])
    return mat


def camera_normal_material():
    mat = bpy.data.materials.new("CamNormal")
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    emit = nt.nodes.new("ShaderNodeEmission")
    geom = nt.nodes.new("ShaderNodeNewGeometry")
    transform = nt.nodes.new("ShaderNodeVectorTransform")
    transform.convert_from = "WORLD"
    transform.convert_to = "CAMERA"
    transform.vector_type = "NORMAL"
    nt.links.new(geom.outputs["Normal"], transform.inputs["Vector"])
    # Camera-space normal -1..1 to 0..1.
    scale = nt.nodes.new("ShaderNodeVectorMath")
    scale.operation = "SCALE"
    scale.inputs["Scale"].default_value = 0.5
    nt.links.new(transform.outputs["Vector"], scale.inputs[0])
    add = nt.nodes.new("ShaderNodeVectorMath")
    add.operation = "ADD"
    add.inputs[1].default_value = (0.5, 0.5, 0.5)
    nt.links.new(scale.outputs["Vector"], add.inputs[0])
    nt.links.new(add.outputs["Vector"], emit.inputs["Color"])
    nt.links.new(emit.outputs["Emission"], out.inputs["Surface"])
    return mat


def mean_abs(render_path, checker_path, mask_path):
    rw, rh, rbuf = encoded_buffer(render_path)
    cw, ch, cbuf = encoded_buffer(checker_path)
    mw, mh, mbuf = encoded_buffer(mask_path)
    if (rw, rh) != (cw, ch) or (rw, rh) != (mw, mh):
        raise RuntimeError("image sizes differ %s %s %s" % ((rw, rh), (cw, ch), (mw, mh)))
    total = 0.0
    count = 0
    n = rw * rh
    for i in range(n):
        if mbuf[i * 4] < 0.5:
            continue
        for channel in range(3):
            total += abs(rbuf[i * 4 + channel] - cbuf[i * 4 + channel])
            count += 1
    if count == 0:
        return None, 0
    return total / count, count // 3


def op_checker(out):
    scene, device = prepare_scene()
    cam, _eye, _look, angle = place_view(scene, VIEWS[0])
    base = f4_lib.project_pixel(scene, cam, Vector((0.0, 0.0, 0.0)))
    say("[project] base pixel %.2f %.2f angle %.3f" % (base[0], base[1], angle))
    mound = add_mound()
    checker = f4_lib.checker_image("checker", f4_lib.W, f4_lib.H, 12, 8)
    checker_path = f4_lib.save_image(checker, os.path.join(out, "checker.png"))
    f4_lib.assign_camera_uvs(mound, scene, cam, "proj")
    hidden = reject_occluded(mound, scene, cam, "proj")
    say("[project] occluded loops %d" % hidden)
    f4_lib.assign_material(mound, projection_material(checker, "proj", weight_only=False))
    baked = f4_lib.new_image("checker_bake", 1024, 1024, "sRGB")
    report = bake_current(mound, baked, samples=1)
    say("[project] checker bake %s" % json.dumps(report))
    bake_path = f4_lib.save_image(baked, os.path.join(out, "bake.png"))
    baked_file = f4_lib.load_image(bake_path, "sRGB")
    show = f4_lib.make_emission_image_material("BakedChecker", baked_file, "UVMap")
    for node in show.node_tree.nodes:
        if node.type == "TEX_IMAGE":
            node.interpolation = "Closest"
    f4_lib.assign_material(mound, show)
    render_path = os.path.join(out, "render.png")
    f4_lib.render_still(scene, cam, render_path, samples=1)
    f4_lib.assign_material(mound, f4_lib.make_solid_emission("White", (1.0, 1.0, 1.0)))
    mask_path = os.path.join(out, "mask.png")
    f4_lib.render_still(scene, cam, mask_path, samples=1)
    diff, pixels = mean_abs(render_path, checker_path, mask_path)
    result = {
        "mean_abs": diff,
        "mask_pixels": pixels,
        "gate": 0.05,
        "pass": diff is not None and diff < 0.05,
        "base_px": [base[0], base[1]],
        "occluded_loops": hidden,
        "device": device.get("type"),
        "bake": report,
        "note": "Single JarG1 view, no facing multiply. Closest sampling. Standard view transform.",
    }
    with open(os.path.join(out, "checker.json"), "w", encoding="utf-8") as handle:
        json.dump(result, handle, indent=2)
    say("[project] mean_abs %s pixels %s" % (diff, pixels))
    if not result["pass"]:
        say("[project] CHECKER_FAIL")
        sys.exit(1)
    say("[project] CHECKER_OK")


def op_passes(out, macro_path):
    scene, _device = prepare_scene()
    mound = add_mound()
    if not os.path.isabs(macro_path):
        macro_path = os.path.abspath(macro_path)
    albedo = f4_lib.load_image(macro_path, "sRGB")
    written = []
    for view in VIEWS:
        cam, eye, look, angle = place_view(scene, view)
        say("[project] pass %s yaw %s pitch %s angle %.3f eye %s" % (
            view["id"], view["yaw"], view["pitch"], angle, [round(v, 4) for v in eye]))
        paths = render_passes(scene, cam, mound, out, view["id"], albedo)
        paths["angle_deg"] = angle
        paths["eye_unity"] = list(eye)
        paths["look_unity"] = list(look)
        written.append({"view": view, "paths": paths})
        # Drop the camera so the next one is the only projector.
        bpy.data.objects.remove(cam, do_unlink=True)
    with open(os.path.join(out, "passes.json"), "w", encoding="utf-8") as handle:
        json.dump(written, handle, indent=2)
    say("[project] PASSES_OK %d" % len(written))


def load_views(path):
    with open(path, "r", encoding="utf-8") as handle:
        payload = json.load(handle)
    return payload["views"]


def blend_and_dilate(color_images, weight_images, previous_path, out_path, dilate_px=8):
    """color_images and weight_images are bake datablocks in scene-linear space."""
    width, height = color_images[0].size
    acc = array.array("f", [0.0]) * (width * height * 3)
    wsum = array.array("f", [0.0]) * (width * height)
    for color, weight in zip(color_images, weight_images):
        _cw, _ch, cbuf = f4_lib.image_buffer(color)
        _ww, _wh, wbuf = f4_lib.image_buffer(weight)
        for i in range(width * height):
            w = wbuf[i * 4]
            if w <= 1e-4:
                continue
            wsum[i] += w
            acc[i * 3] += cbuf[i * 4] * w
            acc[i * 3 + 1] += cbuf[i * 4 + 1] * w
            acc[i * 3 + 2] += cbuf[i * 4 + 2] * w
    pw, ph, previous = linear_buffer_from_png(previous_path)
    if (pw, ph) != (width, height):
        # Resample the previous texture through a temporary image.
        # Load as sRGB so pixels are scene-linear, then scale in that space.
        # Tagging the image Non-Color afterwards would not convert the buffer,
        # and a second sRGB decode would darken the previous moss.
        src = f4_lib.load_image(previous_path, "sRGB")
        src.scale(width, height)
        _w, _h, raw = f4_lib.image_buffer(src)
        previous = array.array("f", [0.0]) * (width * height * 3)
        for i in range(width * height):
            for c in range(3):
                previous[i * 3 + c] = raw[i * 4 + c]
        bpy.data.images.remove(src)
    painted = bytearray(width * height)
    result = array.array("f", [0.0]) * (width * height * 3)
    for i in range(width * height):
        if wsum[i] > 0.02:
            painted[i] = 1
            inv = 1.0 / wsum[i]
            result[i * 3] = acc[i * 3] * inv
            result[i * 3 + 1] = acc[i * 3 + 1] * inv
            result[i * 3 + 2] = acc[i * 3 + 2] * inv
        else:
            result[i * 3] = previous[i * 3]
            result[i * 3 + 1] = previous[i * 3 + 1]
            result[i * 3 + 2] = previous[i * 3 + 2]
    # 8 px seam dilation. Each step copies a painted neighbour into an unpainted texel.
    for _step in range(dilate_px):
        nxt = bytearray(painted)
        nxt_color = array.array("f", result)
        for y in range(height):
            for x in range(width):
                i = y * width + x
                if painted[i]:
                    continue
                for ox, oy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    xx = x + ox
                    yy = y + oy
                    if xx < 0 or yy < 0 or xx >= width or yy >= height:
                        continue
                    j = yy * width + xx
                    if painted[j]:
                        nxt[i] = 1
                        nxt_color[i * 3] = result[j * 3]
                        nxt_color[i * 3 + 1] = result[j * 3 + 1]
                        nxt_color[i * 3 + 2] = result[j * 3 + 2]
                        break
        painted = nxt
        result = nxt_color
    image = f4_lib.new_image("moss_repaint", width, height, "sRGB")
    packed = array.array("f", [0.0]) * (width * height * 4)
    for i in range(width * height):
        packed[i * 4] = result[i * 3]
        packed[i * 4 + 1] = result[i * 3 + 1]
        packed[i * 4 + 2] = result[i * 3 + 2]
        packed[i * 4 + 3] = 1.0
    image.pixels.foreach_set(packed)
    image.update()
    f4_lib.save_image(image, out_path)
    covered = sum(painted)
    return {"width": width, "height": height, "painted_after_dilate": covered, "path": out_path}


def op_bake(out, views_path, previous, resolution):
    scene, device = prepare_scene()
    mound = add_mound()
    views = load_views(views_path)
    colors = []
    weights = []
    notes = []
    for view in views:
        image_path = view["image"]
        if not os.path.isabs(image_path):
            image_path = os.path.abspath(image_path)
        cam, eye, _look, angle = place_view(scene, view)
        f4_lib.assign_camera_uvs(mound, scene, cam, "proj")
        hidden = reject_occluded(mound, scene, cam, "proj")
        repaint = f4_lib.load_image(image_path, "sRGB")
        if tuple(repaint.size) != (f4_lib.W, f4_lib.H):
            say("[project] resize %s %s -> %dx%d" % (view["id"], list(repaint.size), f4_lib.W, f4_lib.H))
            repaint.scale(f4_lib.W, f4_lib.H)
        color_img = f4_lib.new_image("color_" + view["id"], resolution, resolution, "sRGB")
        weight_img = f4_lib.new_image("weight_" + view["id"], resolution, resolution, "Non-Color")
        # Unweighted colour. The weight image is blended in afterwards.
        # Multiplying here and again in the blend would darken the gate view.
        f4_lib.assign_material(mound, projection_material(repaint, "proj", weight_only=False))
        color_bake = bake_current(mound, color_img, samples=1)
        f4_lib.assign_material(mound, projection_material(repaint, "proj", weight_only=True))
        weight_bake = bake_current(mound, weight_img, samples=1)
        colors.append(color_img)
        weights.append(weight_img)
        notes.append({
            "id": view["id"],
            "angle_deg": angle,
            "eye_unity": list(eye),
            "occluded_loops": hidden,
            "color": color_bake,
            "weight": weight_bake,
            "image": image_path,
        })
        say("[project] baked view %s angle %.2f weight_mean %.4f" % (
            view["id"], angle, weight_bake["mean"][0]))
        bpy.data.objects.remove(cam, do_unlink=True)
    if not os.path.isabs(previous):
        previous = os.path.abspath(previous)
    out_png = os.path.join(out, "moss-repaint.png")
    # The python blend is a few seconds at 1024. It is the seam dilation too.
    summary = blend_and_dilate(colors, weights, previous, out_png, dilate_px=8)
    summary["views"] = notes
    summary["device"] = device.get("type")
    summary["previous"] = previous
    summary["validity"] = "Colours are the gate-camera repaint where that view faces the surface, blended with the other views by max(0, N·V)^2. Plausible within the probed angles, all under 25 deg. The back of the mound keeps the previous macro."
    with open(os.path.join(out, "bake.json"), "w", encoding="utf-8") as handle:
        json.dump(summary, handle, indent=2)
    # Re-render the baked texture from JarG1 so the report has an image.
    cam, _e, _l, _a = place_view(scene, VIEWS[0])
    baked = f4_lib.load_image(out_png, "sRGB")
    show = f4_lib.make_emission_image_material("RepaintShow", baked, "UVMap")
    for node in show.node_tree.nodes:
        if node.type == "TEX_IMAGE":
            node.interpolation = "Linear"
            node.extension = "EXTEND"
    f4_lib.assign_material(mound, show)
    f4_lib.render_still(scene, cam, os.path.join(out, "rerender-gate.png"), samples=1)
    say("[project] BAKE_OK %s" % out_png)


def main():
    args = argv_after()
    op = flag(args, "--op", "checker")
    out = flag(args, "--out", os.path.join("orchestration", "runs", "terrarium", "T-TER-041", op))
    if not os.path.isabs(out):
        out = os.path.abspath(out)
    os.makedirs(out, exist_ok=True)
    if op == "checker":
        op_checker(out)
    elif op == "passes":
        op_passes(out, flag(args, "--macro", MACRO))
    elif op == "bake":
        op_bake(
            out,
            flag(args, "--views"),
            flag(args, "--previous", MACRO),
            int(flag(args, "--res", "1024")),
        )
    else:
        raise SystemExit("unknown --op %s" % op)


if __name__ == "__main__":
    main()

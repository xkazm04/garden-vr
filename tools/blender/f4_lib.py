"""Shared helpers for the F4 Blender tools.

Blender 4.2, background mode (`blender -b -P`). The JarG1 camera matches
`apps/terrarium/Art/Scripts/spec_anchor.py`: Unity eye (0, 0.175, -0.44),
look-at (0, 0.072, 0), 28.2 deg vertical FOV, then the same post-LookAt
pitch. CaptureCli applies Euler(-lensShift.y, lensShift.x, 0) with
lensShift (0, -0.4). spec_anchor folds that into a -0.4 deg camera-X
rotation so the jar base lands on pixel (912, 832), inside +/- 3 of the
bible point (915, 830).

Export of the jar meshes maps Blender (x, y, z) to Unity (-x, z, y).
The inverse, Unity (x, y, z) to Blender (-x, z, y), is what places this
camera on the mound built by terrarium_moss.py.

`bpy.ops.uv.project_from_view` needs a viewport, so projection UVs are
written with `world_to_camera_view` (or the UV_PROJECT modifier when the
probe shows it matches). Do not call project_from_view.
"""
from __future__ import annotations

import array
import math
import os

import bpy
import bmesh
from mathutils import Matrix, Vector

W = 1824
H = 1024
EYE = (0.0, 0.175, -0.44)
LOOK = (0.0, 0.072, 0.0)
VFOV = 28.2
PITCH_DEG = 0.4
BASE_TARGET = (915.0, 830.0)


def cross(a, b):
    return (
        a[1] * b[2] - a[2] * b[1],
        a[2] * b[0] - a[0] * b[2],
        a[0] * b[1] - a[1] * b[0],
    )


def vnorm(a):
    length = math.sqrt(a[0] * a[0] + a[1] * a[1] + a[2] * a[2])
    if length < 1e-12:
        return (0.0, 0.0, 0.0)
    return (a[0] / length, a[1] / length, a[2] / length)


def vsub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def vadd(a, b):
    return (a[0] + b[0], a[1] + b[1], a[2] + b[2])


def vscale(a, s):
    return (a[0] * s, a[1] * s, a[2] * s)


def vdot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def camera_axes_unity(eye=EYE, look=LOOK, pitch_deg=PITCH_DEG):
    """Right, up, forward in Unity. Columns of spec_anchor.camera_axes."""
    forward = vnorm(vsub(look, eye))
    up = (0.0, 1.0, 0.0)
    right = vnorm(cross(up, forward))
    cup = cross(forward, right)
    t = math.radians(-pitch_deg)
    c, s = math.cos(t), math.sin(t)
    # look @ Rx(-pitch). New forward = -s * up + c * forward.
    new_up = vadd(vscale(cup, c), vscale(forward, s))
    new_forward = vadd(vscale(cup, -s), vscale(forward, c))
    return right, vnorm(new_up), vnorm(new_forward)


def spec_project_unity(point, eye=EYE, look=LOOK, pitch_deg=PITCH_DEG, width=W, height=H, vfov=VFOV):
    """Pixel (x, y) with y down. Matches spec_anchor.project."""
    right, up, forward = camera_axes_unity(eye, look, pitch_deg)
    delta = vsub(point, eye)
    cam_x = vdot(delta, right)
    cam_y = vdot(delta, up)
    cam_z = vdot(delta, forward)
    if cam_z <= 1e-8:
        return None
    tan_y = math.tan(math.radians(vfov) / 2.0)
    tan_x = tan_y * (width / float(height))
    x_ndc = cam_x / (cam_z * tan_x)
    y_ndc = cam_y / (cam_z * tan_y)
    return (x_ndc + 1.0) * 0.5 * width, (1.0 - y_ndc) * 0.5 * height


def unity_to_blender_point(p):
    return Vector((-p[0], p[2], p[1]))


def unity_to_blender_dir(d):
    return Vector((-d[0], d[2], d[1]))


def view_angle_deg(eye_a, look_a, eye_b, look_b):
    fa = vnorm(vsub(look_a, eye_a))
    fb = vnorm(vsub(look_b, eye_b))
    c = max(-1.0, min(1.0, vdot(fa, fb)))
    return math.degrees(math.acos(c))


def reset_empty():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def configure_color(scene):
    """Standard view keeps an sRGB PNG of an emission texture equal to the source bytes.

    AgX, the 4.2 default, would bend the checker and the mean-abs gate would fail
    even when the UVs are right.
    """
    scene.display_settings.display_device = "sRGB"
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    scene.view_settings.exposure = 0.0
    scene.view_settings.gamma = 1.0
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGB"
    scene.render.image_settings.color_depth = "8"
    scene.render.image_settings.compression = 15
    scene.render.film_transparent = False
    scene.render.use_compositing = False
    scene.render.use_sequencer = False
    scene.render.use_motion_blur = False
    if scene.world is None:
        scene.world = bpy.data.worlds.new("World")
    scene.world.use_nodes = True
    bg = scene.world.node_tree.nodes.get("Background")
    if bg is not None:
        bg.inputs["Color"].default_value = (0.0, 0.0, 0.0, 1.0)
        bg.inputs["Strength"].default_value = 0.0


def configure_cycles(scene, samples, width, height):
    scene.render.engine = "CYCLES"
    scene.render.resolution_x = int(width)
    scene.render.resolution_y = int(height)
    scene.render.resolution_percentage = 100
    scene.cycles.samples = int(samples)
    scene.cycles.use_denoising = False
    scene.cycles.use_adaptive_sampling = False
    scene.cycles.pixel_filter_type = "BOX"
    scene.cycles.filter_width = 0.01
    scene.cycles.seed = 0
    scene.cycles.film_exposure = 1.0
    if hasattr(scene.cycles, "use_fast_gi"):
        scene.cycles.use_fast_gi = False


def set_cycles_device(prefer="OPTIX"):
    """Select a Cycles device. Returns the state query the probe records.

    OPTIX is the dossier device. CUDA, then CPU, are the recorded fallbacks
    when the preference does not stick. A silent no-op here is a failed probe.
    """
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    prefs = bpy.context.preferences.addons["cycles"].preferences
    tried = []
    chosen = None
    for device_type in (prefer, "CUDA", "CPU"):
        if device_type in tried:
            continue
        tried.append(device_type)
        try:
            prefs.compute_device_type = device_type
        except TypeError as exc:
            tried.append("%s:%s" % (device_type, exc))
            continue
        try:
            prefs.get_devices()
        except Exception as exc:  # noqa: BLE001 - probe must record the failure
            tried.append("get_devices:%s" % exc)
        devices = []
        for device in prefs.devices:
            if device.type == device_type or (device_type == "CPU" and device.type == "CPU"):
                device.use = True
            elif device_type != "CPU" and device.type == "CPU":
                device.use = False
            else:
                device.use = device.type == device_type
            devices.append({"name": device.name, "type": device.type, "use": bool(device.use)})
        enabled = [d for d in devices if d["use"] and d["type"] == device_type]
        if device_type == "CPU":
            scene.cycles.device = "CPU"
            chosen = {"type": "CPU", "devices": devices, "tried": tried}
            break
        if enabled:
            scene.cycles.device = "GPU"
            # Re-read after the scene assignment. That is the follow-up query.
            chosen = {
                "type": device_type,
                "scene_device": scene.cycles.device,
                "preference": prefs.compute_device_type,
                "devices": devices,
                "tried": tried,
            }
            break
    if chosen is None:
        scene.cycles.device = "CPU"
        chosen = {"type": "CPU", "scene_device": "CPU", "tried": tried, "devices": []}
    chosen["scene_device"] = scene.cycles.device
    chosen["preference"] = prefs.compute_device_type
    return chosen


def add_camera_unity(scene, eye, look, name, pitch_deg=PITCH_DEG, vfov=VFOV):
    right, up, forward = camera_axes_unity(eye, look, pitch_deg)
    loc = unity_to_blender_point(eye)
    bx = unity_to_blender_dir(right)
    by = unity_to_blender_dir(up)
    bz = unity_to_blender_dir(forward)
    # Blender cameras look down local -Z, so the matrix +Z axis points backward.
    back = -bz
    cam_data = bpy.data.cameras.new(name)
    cam_data.sensor_fit = "VERTICAL"
    cam_data.angle = math.radians(vfov)
    cam_data.clip_start = 0.01
    cam_data.clip_end = 20.0
    cam_data.shift_x = 0.0
    cam_data.shift_y = 0.0
    obj = bpy.data.objects.new(name, cam_data)
    scene.collection.objects.link(obj)
    obj.matrix_world = Matrix((
        (bx.x, by.x, back.x, loc.x),
        (bx.y, by.y, back.y, loc.y),
        (bx.z, by.z, back.z, loc.z),
        (0.0, 0.0, 0.0, 1.0),
    ))
    scene.camera = obj
    bpy.context.view_layer.update()
    return obj


def project_pixel(scene, cam, co_world):
    from bpy_extras.object_utils import world_to_camera_view
    ndc = world_to_camera_view(scene, cam, Vector(co_world))
    x = ndc.x * scene.render.resolution_x
    y = (1.0 - ndc.y) * scene.render.resolution_y
    return x, y, ndc.x, ndc.y, ndc.z


def activate(obj):
    if bpy.context.object and getattr(bpy.context.object, "mode", "OBJECT") != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def tri_count(obj):
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def apply_transforms(obj):
    activate(obj)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


def ensure_uv(obj, name):
    layers = obj.data.uv_layers
    existing = layers.get(name)
    if existing is not None:
        layers.active = existing
        return existing
    layer = layers.new(name=name)
    layers.active = layer
    return layer


def mesh_islands(bm):
    seen = set()
    islands = []
    for face in bm.faces:
        if face.index in seen:
            continue
        stack = [face]
        seen.add(face.index)
        island = []
        while stack:
            current = stack.pop()
            island.append(current)
            for edge in current.edges:
                for linked in edge.link_faces:
                    if linked.index not in seen:
                        seen.add(linked.index)
                        stack.append(linked)
        islands.append(island)
    return islands


def delete_floaters(obj, min_fraction=0.01):
    """Drop loose face islands under min_fraction of the face count."""
    mesh = obj.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.faces.ensure_lookup_table()
    total = len(bm.faces)
    if total == 0:
        bm.free()
        return {"faces_before": 0, "removed_islands": 0, "faces_after": 0}
    islands = mesh_islands(bm)
    drop = []
    removed = 0
    for island in islands:
        if len(island) / float(total) < min_fraction:
            drop.extend(island)
            removed += 1
    if drop:
        bmesh.ops.delete(bm, geom=drop, context="FACES")
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()
    return {
        "faces_before": total,
        "islands_before": len(islands),
        "removed_islands": removed,
        "faces_after": len(mesh.polygons),
    }


def world_bounds(obj):
    xs, ys, zs = [], [], []
    for vert in obj.data.vertices:
        world = obj.matrix_world @ vert.co
        xs.append(world.x)
        ys.append(world.y)
        zs.append(world.z)
    if not xs:
        zero = [0.0, 0.0, 0.0]
        return {"min": zero, "max": zero, "size": zero}
    lo = [min(xs), min(ys), min(zs)]
    hi = [max(xs), max(ys), max(zs)]
    size = [hi[0] - lo[0], hi[1] - lo[1], hi[2] - lo[2]]
    return {"min": lo, "max": hi, "size": size}


def normalize_size(obj, size_m):
    """Uniform scale so the longest world axis equals size_m. Bottom sits on Z=0, centred in X and Y."""
    apply_transforms(obj)
    bounds = world_bounds(obj)
    longest = max(bounds["size"])
    if longest < 1e-8:
        raise RuntimeError("mesh has no extent")
    factor = size_m / longest
    obj.scale = (factor, factor, factor)
    apply_transforms(obj)
    bounds = world_bounds(obj)
    cx = 0.5 * (bounds["min"][0] + bounds["max"][0])
    cy = 0.5 * (bounds["min"][1] + bounds["max"][1])
    z0 = bounds["min"][2]
    for vert in obj.data.vertices:
        vert.co.x -= cx
        vert.co.y -= cy
        vert.co.z -= z0
    obj.data.update()
    return {"scale": factor, "bounds_m": world_bounds(obj), "size_m": size_m}


def loop_count_nonmanifold(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.edges.ensure_lookup_table()
    count = sum(1 for edge in bm.edges if not edge.is_manifold)
    components = len(mesh_islands(bm)) if bm.faces else 0
    bm.free()
    return count, components


def uv_overlap_fraction(obj, uv_name, grid=128):
    """Share of occupied UV cells that more than one triangle touches."""
    layer = obj.data.uv_layers.get(uv_name)
    if layer is None:
        return None
    obj.data.calc_loop_triangles()
    hits = {}
    for tri in obj.data.loop_triangles:
        uvs = []
        for loop_index in tri.loops:
            uv = layer.data[loop_index].uv
            uvs.append((uv.x, uv.y))
        min_u = min(p[0] for p in uvs)
        max_u = max(p[0] for p in uvs)
        min_v = min(p[1] for p in uvs)
        max_v = max(p[1] for p in uvs)
        if max_u - min_u < 1e-8 and max_v - min_v < 1e-8:
            continue
        x0 = max(0, int(min_u * grid))
        x1 = min(grid - 1, int(max_u * grid))
        y0 = max(0, int(min_v * grid))
        y1 = min(grid - 1, int(max_v * grid))
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                key = y * grid + x
                hits[key] = hits.get(key, 0) + 1
    if not hits:
        return 0.0
    overlap = sum(1 for value in hits.values() if value > 1)
    return overlap / float(len(hits))


def texel_density(obj, uv_name, resolution):
    """Mean pixels per metre. UV area times resolution squared, over world area."""
    layer = obj.data.uv_layers.get(uv_name)
    if layer is None or resolution <= 0:
        return None
    obj.data.calc_loop_triangles()
    world_area = 0.0
    uv_area = 0.0
    for tri in obj.data.loop_triangles:
        verts = [obj.matrix_world @ obj.data.vertices[index].co for index in tri.vertices]
        world_area += ((verts[1] - verts[0]).cross(verts[2] - verts[0])).length * 0.5
        uvs = [layer.data[loop_index].uv for loop_index in tri.loops]
        du1 = Vector((uvs[1].x - uvs[0].x, uvs[1].y - uvs[0].y, 0.0))
        du2 = Vector((uvs[2].x - uvs[0].x, uvs[2].y - uvs[0].y, 0.0))
        uv_area += du1.cross(du2).length * 0.5
    if world_area < 1e-12:
        return None
    return math.sqrt(uv_area * resolution * resolution / world_area)


def assign_camera_uvs(obj, scene, cam, uv_name):
    """Per-loop UV from world_to_camera_view. y=0 is the bottom of the image, matching Blender."""
    from bpy_extras.object_utils import world_to_camera_view
    ensure_uv(obj, uv_name)
    layer = obj.data.uv_layers[uv_name]
    bpy.context.view_layer.update()
    written = 0
    behind = 0
    for poly in obj.data.polygons:
        for loop_index in poly.loop_indices:
            vert = obj.data.vertices[obj.data.loops[loop_index].vertex_index]
            world = obj.matrix_world @ vert.co
            ndc = world_to_camera_view(scene, cam, world)
            if ndc.z <= 0.0:
                behind += 1
                layer.data[loop_index].uv = (0.0, 0.0)
            else:
                layer.data[loop_index].uv = (ndc.x, ndc.y)
                written += 1
    obj.data.uv_layers.active = layer
    return {"uv": uv_name, "loops": written, "behind": behind}


def uv_project_modifier(obj, cam, uv_name, aspect_x, aspect_y):
    """Add UV_PROJECT, apply it, and compare a vertex to world_to_camera_view.

    The operator can return FINISHED and still leave the UVs untouched. The
    returned delta is that follow-up.
    """
    ensure_uv(obj, uv_name)
    # A known sentinel so a no-op apply is visible.
    layer = obj.data.uv_layers[uv_name]
    for datum in layer.data:
        datum.uv = (0.25, 0.25)
    mod = obj.modifiers.new("F4Project", "UV_PROJECT")
    props = set(mod.bl_rna.properties.keys())
    if "uv_layer" in props:
        mod.uv_layer = uv_name
    elif "uv_map" in props:
        mod.uv_map = uv_name
    if "projector_count" in props:
        mod.projector_count = 1
    if "aspect_x" in props:
        mod.aspect_x = float(aspect_x)
    if "aspect_y" in props:
        mod.aspect_y = float(aspect_y)
    if "scale_x" in props:
        mod.scale_x = 1.0
    if "scale_y" in props:
        mod.scale_y = 1.0
    projectors = getattr(mod, "projectors", None)
    if projectors is not None and len(projectors) > 0:
        projectors[0].object = cam
    activate(obj)
    result = {"properties": sorted(props), "apply": None, "error": None}
    try:
        applied = bpy.ops.object.modifier_apply(modifier=mod.name)
        result["apply"] = list(applied)
    except Exception as exc:  # noqa: BLE001
        result["error"] = "%s: %s" % (type(exc).__name__, exc)
        if obj.modifiers.get(mod.name) is not None:
            obj.modifiers.remove(mod)
    still = obj.modifiers.get("F4Project")
    result["modifier_remaining"] = still is not None
    # Sample the first loop and the camera projection of its vertex.
    from bpy_extras.object_utils import world_to_camera_view
    bpy.context.view_layer.update()
    loop = obj.data.loops[0]
    vert = obj.data.vertices[loop.vertex_index]
    got = obj.data.uv_layers[uv_name].data[0].uv
    ndc = world_to_camera_view(bpy.context.scene, cam, obj.matrix_world @ vert.co)
    result["uv"] = [float(got.x), float(got.y)]
    result["camera_uv"] = [float(ndc.x), float(ndc.y)]
    result["delta"] = math.hypot(got.x - ndc.x, got.y - ndc.y)
    result["changed_from_sentinel"] = abs(got.x - 0.25) > 1e-4 or abs(got.y - 0.25) > 1e-4
    return result


def new_image(name, width, height, colorspace, color=None):
    image = bpy.data.images.new(name, width=int(width), height=int(height), alpha=False, float_buffer=True)
    image.colorspace_settings.name = colorspace
    if color is not None:
        n = width * height
        buf = array.array("f", color) * n
        image.pixels.foreach_set(buf)
        image.update()
    return image


def save_image(image, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    image.filepath_raw = path
    image.file_format = "PNG"
    image.save()
    return path


def image_buffer(image):
    width, height = image.size
    buf = array.array("f", [0.0]) * (width * height * 4)
    image.pixels.foreach_get(buf)
    return width, height, buf


def fill_image(image, rgba):
    width, height = image.size
    buf = array.array("f", rgba) * (width * height)
    image.pixels.foreach_set(buf)
    image.update()


def sample_mean(image):
    _w, _h, buf = image_buffer(image)
    n = len(buf) // 4
    if n == 0:
        return [0.0, 0.0, 0.0]
    acc = [0.0, 0.0, 0.0]
    for i in range(n):
        acc[0] += buf[i * 4]
        acc[1] += buf[i * 4 + 1]
        acc[2] += buf[i * 4 + 2]
    return [acc[0] / n, acc[1] / n, acc[2] / n]


def sample_std(image):
    mean = sample_mean(image)
    _w, _h, buf = image_buffer(image)
    n = len(buf) // 4
    acc = [0.0, 0.0, 0.0]
    for i in range(n):
        for c in range(3):
            d = buf[i * 4 + c] - mean[c]
            acc[c] += d * d
    return [math.sqrt(acc[c] / n) for c in range(3)], mean


def clear_materials(obj):
    obj.data.materials.clear()


def make_emission_image_material(name, image, uv_name):
    mat = bpy.data.materials.new(name)
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
    nt.links.new(tex.outputs["Color"], emit.inputs["Color"])
    nt.links.new(emit.outputs["Emission"], out.inputs["Surface"])
    out.location = (400, 0)
    emit.location = (160, 0)
    tex.location = (-80, 0)
    return mat


def make_solid_emission(name, color):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    emit = nt.nodes.new("ShaderNodeEmission")
    emit.inputs["Color"].default_value = (color[0], color[1], color[2], 1.0)
    emit.inputs["Strength"].default_value = 1.0
    nt.links.new(emit.outputs["Emission"], out.inputs["Surface"])
    return mat


def add_bake_target_node(obj, image):
    """Active Image Texture node. Unconnected, so it does not change the shader being baked."""
    if not obj.data.materials:
        mat = make_solid_emission(obj.name + "_bake", (0.0, 0.0, 0.0))
        obj.data.materials.append(mat)
    mat = obj.data.materials[0]
    mat.use_nodes = True
    node = mat.node_tree.nodes.new("ShaderNodeTexImage")
    node.image = image
    node.location = (160, -240)
    for other in mat.node_tree.nodes:
        other.select = False
    node.select = True
    mat.node_tree.nodes.active = node
    return node


def bake(obj, image, bake_type, selected_to_active=False, high=None, cage_extrusion=0.0, max_ray=0.0, samples=1):
    """Run one Cycles bake and return a follow-up sample of the image.

    The image is cleared to black first. A bake that no-ops stays black.
    """
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = int(samples)
    scene.cycles.use_denoising = False
    fill_image(image, (0.0, 0.0, 0.0, 1.0))
    add_bake_target_node(obj, image)
    bake_settings = scene.render.bake
    bake_settings.use_clear = True
    bake_settings.use_selected_to_active = bool(selected_to_active)
    bake_settings.cage_extrusion = float(cage_extrusion)
    bake_settings.max_ray_distance = float(max_ray)
    bake_settings.margin = 8
    if hasattr(bake_settings, "margin_type"):
        bake_settings.margin_type = "EXTEND"
    bake_settings.normal_space = "TANGENT"
    if bake_type == "DIFFUSE":
        bake_settings.use_pass_direct = False
        bake_settings.use_pass_indirect = False
        bake_settings.use_pass_color = True
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="DESELECT")
    if selected_to_active:
        if high is None:
            raise RuntimeError("selected-to-active bake needs a high object")
        high.select_set(True)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    report = {"type": bake_type, "selected_to_active": bool(selected_to_active)}
    try:
        result = bpy.ops.object.bake(type=bake_type, use_clear=True, margin=8, target="IMAGE_TEXTURES")
        report["operator"] = list(result)
    except Exception as exc:  # noqa: BLE001
        report["error"] = "%s: %s" % (type(exc).__name__, exc)
        report["operator"] = ["EXCEPTION"]
    std, mean = sample_std(image)
    report["mean"] = mean
    report["std"] = std
    report["wrote"] = max(std) > 1e-4 or max(mean) > 1e-3
    return report


def render_still(scene, cam, path, samples=1):
    scene.camera = cam
    scene.cycles.samples = int(samples)
    scene.render.filepath = path
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.render.render(write_still=True)
    return path


def load_image(path, colorspace):
    image = bpy.data.images.load(path, check_existing=False)
    image.colorspace_settings.name = colorspace
    return image


def resize_image(image, width, height):
    image.scale(int(width), int(height))
    return image


def copy_image(src, name):
    dup = src.copy()
    dup.name = name
    return dup


def checker_image(name, width, height, cells_x, cells_y):
    """Black and white checker in sRGB image space. 0 and 1 survive a Standard round trip."""
    image = new_image(name, width, height, "sRGB")
    buf = array.array("f", [0.0]) * (width * height * 4)
    # Pixel y=0 is the bottom row. The saved PNG flips it, and so does the re-render.
    for y in range(height):
        cy = int(y * cells_y / height)
        for x in range(width):
            cx = int(x * cells_x / width)
            on = (cx + cy) % 2 == 0
            value = 1.0 if on else 0.0
            i = (y * width + x) * 4
            buf[i] = value
            buf[i + 1] = value
            buf[i + 2] = value
            buf[i + 3] = 1.0
    image.pixels.foreach_set(buf)
    image.update()
    return image


def assign_material(obj, mat):
    obj.data.materials.clear()
    obj.data.materials.append(mat)


def triangulate(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()


def weld_cracks(obj, dist):
    """Merge vertices closer than dist. Does not dissolve faces."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    before_verts = len(bm.verts)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=dist)
    loose = [vert for vert in bm.verts if not vert.link_faces]
    if loose:
        bmesh.ops.delete(bm, geom=loose, context="VERTS")
    after_verts = len(bm.verts)
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    nonmanifold, components = loop_count_nonmanifold(obj)
    return {
        "dist": dist,
        "verts_before": before_verts,
        "verts_after": after_verts,
        "tris": tri_count(obj),
        "components": components,
        "non_manifold_edges": nonmanifold,
    }


def _in_band(tris, target, tol):
    if target <= 0:
        return True
    return abs(tris - target) / float(target) <= tol


def _swap_mesh(obj, mesh):
    """Point obj at mesh and drop the previous datablock if nothing else uses it."""
    old = obj.data
    if old == mesh:
        return
    obj.data = mesh
    if old.users == 0:
        bpy.data.meshes.remove(old)


def collapse_clean(obj, target, tol, size_m):
    """Collapse to the triangle budget, then drop dust and weld decimate cracks.

    A ratio near 0.003 on a 400k mesh jumps over the budget, and a quarter-face
    step tears the surface into coincident sheets. Half-face steps stay on the
    count. The sheets sit a few millionths of the object apart, so a weld at
    that scale joins them. A wider weld deletes real faces, and dissolving
    "degenerate" faces at this size deletes the mesh, so neither is used.
    A weld that leaves the +/- tol band is discarded.
    """
    provisional = target
    if target > 0:
        provisional = max(target, int(round(target * 1.12)))
    first = decimate_to_tris(obj, provisional, tol)
    dust = delete_floaters(obj, 0.01)
    base = obj.data.copy()
    trials = []
    kept = None
    for frac in (8e-6, 2e-5, 4e-5):
        _swap_mesh(obj, base.copy())
        trial = weld_cracks(obj, max(size_m, 1e-6) * frac)
        trial["frac"] = frac
        trials.append(trial)
        in_band = trial["components"] == 1 and _in_band(trial["tris"], target, tol)
        if in_band:
            if kept is not None and kept.users == 0:
                bpy.data.meshes.remove(kept)
            kept = obj.data.copy()
            if trial["tris"] <= target:
                break
        elif trial["components"] == 1 and target > 0 and trial["tris"] < target * (1.0 - tol):
            break
    second = None
    if kept is not None:
        _swap_mesh(obj, kept)
    else:
        _swap_mesh(obj, base.copy())
        if target > 0 and tri_count(obj) > target * (1.0 + tol):
            second = decimate_to_tris(obj, target, tol)
            dust["second"] = delete_floaters(obj, 0.01)
    if base.users == 0:
        bpy.data.meshes.remove(base)
    tris = tri_count(obj)
    nonmanifold, components = loop_count_nonmanifold(obj)
    return {
        "first": first,
        "dust": dust,
        "welds": trials,
        "second": second,
        "tris": tris,
        "components": components,
        "non_manifold_edges": nonmanifold,
        "method": "collapse",
        "target": target,
        "provisional": provisional,
        "within_tol": _in_band(tris, target, tol) and components == 1,
    }


def snap_to_surface(low, high, distance):
    """Pull low vertices onto the high surface so a decimate spike cannot change the world size."""
    moved = 0
    missed = 0
    for vert in low.data.vertices:
        hit, loc, _normal, _index = high.closest_point_on_mesh(vert.co, distance=distance)
        if hit:
            vert.co = loc
            moved += 1
        else:
            missed += 1
    low.data.update()
    return {
        "moved": moved,
        "missed": missed,
        "longest_m": max(world_bounds(low)["size"]),
    }


def island_sizes(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    sizes = sorted((len(island) for island in mesh_islands(bm)), reverse=True)
    bm.free()
    return sizes


def decimate_to_tris(obj, target, tol=0.05):
    """Collapse-decimate to target triangles, +/- tol.

    A single tiny ratio (about 0.003 on a 400k mesh) jumps over the budget.
    Each step removes at most half of the faces. Quarter-face steps tear the
    surface into coincident sheets. The last step uses the exact remaining ratio.
    """
    triangulate(obj)
    if tri_count(obj) == 0:
        raise RuntimeError("nothing to decimate")
    ratios = []
    counts = []
    for _ in range(12):
        got = tri_count(obj)
        counts.append(got)
        if target <= 0 or abs(got - target) / float(target) <= tol:
            break
        if got < target * (1.0 - tol):
            break
        ratio = target / float(got)
        # Stay above the budget so the next step can land on it.
        if got * 0.5 > target * (1.0 + tol):
            ratio = 0.5
        ratio = min(max(ratio, 1e-6), 1.0)
        before = got
        mod = obj.modifiers.new("F4Decimate", "DECIMATE")
        mod.decimate_type = "COLLAPSE"
        mod.ratio = ratio
        if hasattr(mod, "use_collapse_triangulate"):
            mod.use_collapse_triangulate = True
        activate(obj)
        bpy.ops.object.modifier_apply(modifier=mod.name)
        ratios.append(ratio)
        if tri_count(obj) >= before:
            break
    got = tri_count(obj)
    return {
        "tris": got,
        "ratios": ratios,
        "counts": counts + [got],
        "iterations": len(ratios),
        "method": "collapse",
        "target": target,
        "within_tol": target > 0 and abs(got - target) / float(target) <= tol,
    }


def smart_uv(obj, uv_name="UVMap"):
    """Smart UV Project on the active mesh. Falls back to angle unwrap, then a per-face grid.

    Headless mode has no Image Editor. The operator is attempted anyway, then the
    follow-up checks that a loop UV moved off the sentinel.
    """
    ensure_uv(obj, uv_name)
    layer = obj.data.uv_layers[uv_name]
    for datum in layer.data:
        datum.uv = (0.0, 0.0)
    activate(obj)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    report = {"method": None, "error": None}
    try:
        result = bpy.ops.uv.smart_project(
            angle_limit=math.radians(66.0),
            island_margin=0.02,
            area_weight=0.0,
            correct_aspect=True,
            scale_to_bounds=True,
        )
        report["operator"] = list(result)
        report["method"] = "smart_project"
    except Exception as exc:  # noqa: BLE001
        report["error"] = "%s: %s" % (type(exc).__name__, exc)
    bpy.ops.object.mode_set(mode="OBJECT")
    moved = any(abs(d.uv.x) > 1e-6 or abs(d.uv.y) > 1e-6 for d in obj.data.uv_layers[uv_name].data)
    report["moved"] = moved
    if moved:
        return report
    activate(obj)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    try:
        result = bpy.ops.uv.unwrap(method="ANGLE_BASED", margin=0.02)
        report["unwrap"] = list(result)
        report["method"] = "unwrap"
    except Exception as exc:  # noqa: BLE001
        report["unwrap_error"] = "%s: %s" % (type(exc).__name__, exc)
    bpy.ops.object.mode_set(mode="OBJECT")
    moved = any(abs(d.uv.x) > 1e-6 or abs(d.uv.y) > 1e-6 for d in obj.data.uv_layers[uv_name].data)
    report["moved"] = moved
    if moved:
        return report
    grid_project(obj, uv_name)
    report["method"] = "grid_project"
    report["moved"] = True
    return report


def grid_project(obj, uv_name):
    """One cell per triangle, packed in a grid. Used only when the UV operators no-op."""
    obj.data.calc_loop_triangles()
    tris = list(obj.data.loop_triangles)
    n = max(len(tris), 1)
    side = math.ceil(math.sqrt(n))
    cell = 1.0 / side
    pad = cell * 0.08
    layer = obj.data.uv_layers[uv_name]
    for index, tri in enumerate(tris):
        col = index % side
        row = index // side
        origin_u = col * cell + pad
        origin_v = row * cell + pad
        span = cell - 2.0 * pad
        corners = ((0.0, 0.0), (span, 0.0), (0.0, span))
        for corner, loop_index in zip(corners, tri.loops):
            layer.data[loop_index].uv = (origin_u + corner[0], origin_v + corner[1])


def export_fbx(obj, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    activate(obj)
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
        path_mode="COPY",
        embed_textures=False,
        global_scale=1.0,
    )
    return path


def import_gltf(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    new_objs = [obj for obj in bpy.data.objects if obj not in before]
    meshes = [obj for obj in new_objs if obj.type == "MESH"]
    if not meshes:
        meshes = [obj for obj in bpy.data.objects if obj.type == "MESH"]
    if not meshes:
        raise RuntimeError("gltf import produced no mesh: %s" % path)
    meshes.sort(key=lambda obj: len(obj.data.polygons), reverse=True)
    return meshes[0]


def export_glb(obj, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    activate(obj)
    bpy.ops.export_scene.gltf(
        filepath=path,
        export_format="GLB",
        use_selection=True,
        export_apply=True,
        export_texcoords=True,
        export_materials="EXPORT",
    )
    return path


def scorecard(obj, uv_name, resolution, extra):
    nonmanifold, components = loop_count_nonmanifold(obj)
    card = {
        "verts": len(obj.data.vertices),
        "tris": tri_count(obj),
        "faces": len(obj.data.polygons),
        "components": components,
        "non_manifold_edges": nonmanifold,
        "uv_overlap_fraction": uv_overlap_fraction(obj, uv_name),
        "bounds_m": world_bounds(obj),
        "texel_density_px_per_m": texel_density(obj, uv_name, resolution),
        "uv": uv_name,
        "texture_px": resolution,
        "blender": bpy.app.version_string,
    }
    card.update(extra)
    return card


def duplicate_object(obj, name):
    dup = obj.copy()
    dup.data = obj.data.copy()
    dup.name = name
    dup.data.name = name
    bpy.context.scene.collection.objects.link(dup)
    return dup


def flip_normals(obj):
    activate(obj)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.flip_normals()
    bpy.ops.object.mode_set(mode="OBJECT")


def largest_mesh():
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if not meshes:
        raise RuntimeError("no mesh in the scene")
    meshes.sort(key=lambda obj: len(obj.data.polygons), reverse=True)
    return meshes[0]


def view_directions(yaw_deg, pitch_deg, eye=EYE, look=LOOK):
    """Orbit the eye around the look point. Yaw is around Unity up. Pitch raises the eye."""
    offset = vsub(eye, look)
    yaw = math.radians(yaw_deg)
    c, s = math.cos(yaw), math.sin(yaw)
    x, y, z = offset
    x2 = c * x + s * z
    z2 = -s * x + c * z
    y2 = y
    # Pitch toward world up, keeping the distance.
    dist = math.sqrt(x2 * x2 + y2 * y2 + z2 * z2)
    horizontal = math.sqrt(x2 * x2 + z2 * z2)
    elev = math.atan2(y2, horizontal) + math.radians(pitch_deg)
    hyp = math.cos(elev) * dist
    y3 = math.sin(elev) * dist
    if horizontal < 1e-8:
        x3, z3 = x2, z2
    else:
        x3 = x2 / horizontal * hyp
        z3 = z2 / horizontal * hyp
    new_eye = vadd(look, (x3, y3, z3))
    return new_eye, look

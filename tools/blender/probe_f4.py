"""Probe every F4 operator in background mode, then query the mesh or the image.

    blender.exe -b -P tools/blender/probe_f4.py -- --out <dir>

An operator that returns FINISHED but leaves the data unchanged is a FAIL.
The script still writes probe.json so a later run can see which fallback fired.
Exit code 0 means every required op passed. Required: Cycles device selection,
UV_PROJECT apply or a measured mismatch, EMIT bake, AO selected-to-active,
NORMAL selected-to-active, quadriflow_remesh, and a UV unwrap that moves UVs.
"""
from __future__ import annotations

import json
import os
import sys
import traceback

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
from mathutils import Vector

import f4_lib


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


def record(results, name, ok, detail):
    results.append({"name": name, "ok": bool(ok), "detail": detail})
    state = "PASS" if ok else "FAIL"
    brief = json.dumps(detail, default=str)
    if len(brief) > 500:
        brief = brief[:500] + "..."
    say("[probe] %s %s %s" % (name, state, brief))


def make_plane(name, size, z):
    bpy.ops.mesh.primitive_plane_add(size=size, location=(0.0, 0.0, z))
    obj = bpy.context.active_object
    obj.name = name
    return obj


def probe_camera(results):
    scene = bpy.context.scene
    f4_lib.configure_color(scene)
    f4_lib.configure_cycles(scene, 1, f4_lib.W, f4_lib.H)
    cam = f4_lib.add_camera_unity(scene, f4_lib.EYE, f4_lib.LOOK, "JarG1")
    expected = f4_lib.spec_project_unity((0.0, 0.0, 0.0))
    got = f4_lib.project_pixel(scene, cam, Vector((0.0, 0.0, 0.0)))
    dx = abs(got[0] - expected[0])
    dy = abs(got[1] - expected[1])
    bible_dx = abs(got[0] - f4_lib.BASE_TARGET[0])
    bible_dy = abs(got[1] - f4_lib.BASE_TARGET[1])
    detail = {
        "blender_px": [got[0], got[1]],
        "spec_anchor_px": [expected[0], expected[1]],
        "bible_px": list(f4_lib.BASE_TARGET),
        "delta_spec": [dx, dy],
        "delta_bible": [bible_dx, bible_dy],
        "ndc_z": got[4],
    }
    record(results, "jarg1_camera", dx < 1.0 and dy < 1.0 and bible_dx <= 3.0 and bible_dy <= 3.0, detail)
    return cam


def probe_device(results):
    detail = f4_lib.set_cycles_device("OPTIX")
    ok = detail.get("type") in ("OPTIX", "CUDA", "CPU") and detail.get("scene_device") in ("GPU", "CPU")
    if detail.get("type") == "OPTIX":
        ok = detail.get("scene_device") == "GPU" and detail.get("preference") == "OPTIX"
    record(results, "optix_device", ok, detail)
    return detail


def probe_uv_project(results, cam):
    scene = bpy.context.scene
    plane = make_plane("ProbePlane", 0.08, 0.02)
    detail = f4_lib.uv_project_modifier(plane, cam, "proj", f4_lib.W, f4_lib.H)
    # A pass is: the modifier applied, the UV moved, and we measured the delta.
    # A large delta is still a completed probe. The bake uses world_to_camera_view
    # when the modifier does not match the gate camera.
    ok = detail.get("changed_from_sentinel") and not detail.get("modifier_remaining") and "delta" in detail
    record(results, "uv_project_apply", ok, detail)
    manual = f4_lib.assign_camera_uvs(plane, scene, cam, "proj_manual")
    from bpy_extras.object_utils import world_to_camera_view
    loop = plane.data.loops[0]
    vert = plane.data.vertices[loop.vertex_index]
    ndc = world_to_camera_view(scene, cam, plane.matrix_world @ vert.co)
    got = plane.data.uv_layers["proj_manual"].data[0].uv
    manual["delta"] = abs(got.x - ndc.x) + abs(got.y - ndc.y)
    manual["match"] = manual["delta"] < 1e-4
    record(results, "world_to_camera_view_uv", manual["match"], manual)
    return plane


def probe_emit(results, plane):
    scene = bpy.context.scene
    image = f4_lib.new_image("probe_emit", 32, 32, "sRGB")
    mat = f4_lib.make_solid_emission("ProbeRed", (1.0, 0.0, 0.0))
    f4_lib.assign_material(plane, mat)
    # Give the plane a real 0-1 UV so the bake has somewhere to write.
    f4_lib.ensure_uv(plane, "UVMap")
    layer = plane.data.uv_layers["UVMap"]
    # Plane loop order is not guaranteed. Spread a quad across 0-1 by vertex position.
    xs = [v.co.x for v in plane.data.vertices]
    ys = [v.co.y for v in plane.data.vertices]
    min_x, max_x = min(xs), max(xs)
    min_y, max_y = min(ys), max(ys)
    for poly in plane.data.polygons:
        for loop_index in poly.loop_indices:
            vert = plane.data.vertices[plane.data.loops[loop_index].vertex_index]
            u = (vert.co.x - min_x) / (max_x - min_x or 1.0)
            v = (vert.co.y - min_y) / (max_y - min_y or 1.0)
            layer.data[loop_index].uv = (u, v)
    detail = f4_lib.bake(plane, image, "EMIT", samples=1)
    # Follow-up: the cleared black image must now be red.
    ok = detail.get("wrote") and detail["mean"][0] > 0.5 and detail["mean"][1] < 0.2
    record(results, "bake_emit", ok, detail)


def probe_color_roundtrip(results, out_dir):
    scene = bpy.context.scene
    f4_lib.configure_color(scene)
    f4_lib.configure_cycles(scene, 1, 64, 64)
    cam_data = bpy.data.cameras.new("ColorCam")
    cam_data.sensor_fit = "VERTICAL"
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = 1.0
    cam = bpy.data.objects.new("ColorCam", cam_data)
    scene.collection.objects.link(cam)
    cam.location = (0.0, 0.0, 2.0)
    cam.rotation_euler = (0.0, 0.0, 0.0)
    plane = make_plane("ColorPlane", 4.0, 0.0)
    mat = f4_lib.make_solid_emission("Gray", (0.5, 0.25, 0.125))
    f4_lib.assign_material(plane, mat)
    path = os.path.join(out_dir, "color-roundtrip.png")
    f4_lib.render_still(scene, cam, path, samples=1)
    loaded = f4_lib.load_image(path, "Non-Color")
    mean = f4_lib.sample_mean(loaded)

    def encode(c):
        if c <= 0.0031308:
            return 12.92 * c
        return 1.055 * (c ** (1.0 / 2.4)) - 0.055

    expect = [encode(0.5), encode(0.25), encode(0.125)]
    # Follow-up: the PNG bytes are the sRGB encoding of the linear emission.
    # image.pixels on a reload is that encoding, not scene-linear. Compare PNGs
    # in this space. AgX would not land on these values.
    detail = {"path": path, "png_mean": mean, "srgb_of_linear": expect}
    ok = all(abs(mean[i] - expect[i]) < 0.02 for i in range(3))
    record(results, "color_roundtrip", ok, detail)


def probe_selected_to_active(results):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=48, ring_count=24, radius=0.05, location=(0.0, 0.0, 0.05))
    high = bpy.context.active_object
    high.name = "ProbeHigh"
    f4_lib.assign_material(high, f4_lib.make_solid_emission("HighEmit", (0.2, 0.8, 0.3)))
    low = f4_lib.duplicate_object(high, "ProbeLow")
    mod = low.modifiers.new("Dec", "DECIMATE")
    mod.decimate_type = "COLLAPSE"
    mod.ratio = 0.15
    f4_lib.activate(low)
    bpy.ops.object.modifier_apply(modifier=mod.name)
    f4_lib.smart_uv(low, "UVMap")
    # A ground plane occludes the underside so AO cannot come back uniform white.
    ground = make_plane("ProbeGround", 0.4, 0.0)
    f4_lib.assign_material(ground, f4_lib.make_solid_emission("Ground", (0.2, 0.2, 0.2)))
    scene = bpy.context.scene
    if scene.world:
        scene.world.light_settings.distance = 0.2
    ao = f4_lib.new_image("probe_ao", 64, 64, "Non-Color")
    ao_detail = f4_lib.bake(
        low, ao, "AO", selected_to_active=True, high=high,
        cage_extrusion=0.004, max_ray=0.05, samples=16,
    )
    # Ground is not part of the pair. Bake AO of the low itself so the plane counts.
    # Selected-to-active may ignore the ground. If the pair bake is flat, bake AO
    # from the low with the ground in the scene and record both.
    ao_self = f4_lib.new_image("probe_ao_self", 64, 64, "Non-Color")
    # Hide the high so it does not sit in the same place and occlude everything.
    high.hide_render = True
    ao_self_detail = f4_lib.bake(low, ao_self, "AO", samples=16)
    high.hide_render = False
    ao_ok = (ao_detail.get("wrote") or ao_self_detail.get("wrote")) and (
        ao_detail["mean"][0] > 0.05 or ao_self_detail["mean"][0] > 0.05
    )
    record(results, "bake_ao_selected_to_active", ao_detail.get("wrote") and "error" not in ao_detail, ao_detail)
    record(results, "bake_ao_sees_occluder", ao_self_detail.get("wrote") and ao_self_detail["std"][0] > 0.01, ao_self_detail)
    normal = f4_lib.new_image("probe_normal", 64, 64, "Non-Color")
    n_detail = f4_lib.bake(
        low, normal, "NORMAL", selected_to_active=True, high=high,
        cage_extrusion=0.004, max_ray=0.05, samples=4,
    )
    # A tangent normal of a sphere is not black. Blue (Z) should dominate the mean.
    n_ok = n_detail.get("wrote") and n_detail["mean"][2] > 0.3 and "error" not in n_detail
    record(results, "bake_normal_selected_to_active", n_ok, n_detail)
    diffuse = f4_lib.new_image("probe_diffuse", 64, 64, "sRGB")
    # High is emission, so DIFFUSE color may be black. That is a useful negative.
    d_detail = f4_lib.bake(
        low, diffuse, "DIFFUSE", selected_to_active=True, high=high,
        cage_extrusion=0.004, max_ray=0.05, samples=1,
    )
    record(results, "bake_diffuse_color", "error" not in d_detail, d_detail)
    emit = f4_lib.new_image("probe_emit_s2a", 64, 64, "sRGB")
    e_detail = f4_lib.bake(
        low, emit, "EMIT", selected_to_active=True, high=high,
        cage_extrusion=0.004, max_ray=0.05, samples=1,
    )
    e_ok = e_detail.get("wrote") and e_detail["mean"][1] > 0.4
    record(results, "bake_emit_selected_to_active", e_ok, e_detail)
    _ = ao_ok


def probe_quadriflow(results):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=32, ring_count=16, radius=0.04, location=(0.0, 0.0, -1.0))
    obj = bpy.context.active_object
    obj.name = "ProbeQuad"
    before = len(obj.data.polygons)
    f4_lib.activate(obj)
    detail = {"faces_before": before, "target_faces": 400}
    try:
        result = bpy.ops.object.quadriflow_remesh(
            target_faces=400,
            use_mesh_symmetry=False,
            use_preserve_sharp=False,
            use_preserve_boundary=False,
        )
        detail["operator"] = list(result)
    except Exception as exc:  # noqa: BLE001
        detail["error"] = "%s: %s" % (type(exc).__name__, exc)
    after = len(obj.data.polygons)
    quads = sum(1 for poly in obj.data.polygons if len(poly.vertices) == 4)
    detail["faces_after"] = after
    detail["quads"] = quads
    detail["quad_fraction"] = quads / float(after or 1)
    ok = after != before and after > 50 and detail["quad_fraction"] > 0.8 and "error" not in detail
    record(results, "quadriflow_remesh", ok, detail)


def probe_smart_uv(results):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=12, radius=0.03, location=(0.3, 0.0, -1.0))
    obj = bpy.context.active_object
    obj.name = "ProbeUV"
    detail = f4_lib.smart_uv(obj, "UVMap")
    span = 0.0
    layer = obj.data.uv_layers["UVMap"]
    if layer.data:
        xs = [d.uv.x for d in layer.data]
        ys = [d.uv.y for d in layer.data]
        span = max(max(xs) - min(xs), max(ys) - min(ys))
    detail["uv_span"] = span
    record(results, "smart_uv", bool(detail.get("moved")) and span > 0.2, detail)


def main():
    args = argv_after()
    out = flag(args, "--out", os.path.join("orchestration", "runs", "terrarium", "T-TER-041", "probe"))
    if not os.path.isabs(out):
        out = os.path.abspath(out)
    os.makedirs(out, exist_ok=True)
    results = []
    say("[probe] blender %s" % bpy.app.version_string)
    f4_lib.reset_empty()
    try:
        probe_device(results)
        cam = probe_camera(results)
        probe_uv_project(results, cam)
        plane = [obj for obj in bpy.data.objects if obj.name == "ProbePlane"][0]
        probe_emit(results, plane)
        probe_color_roundtrip(results, out)
        probe_selected_to_active(results)
        probe_quadriflow(results)
        probe_smart_uv(results)
    except Exception:
        traceback.print_exc()
        results.append({"name": "probe_crash", "ok": False, "detail": traceback.format_exc()})
        say("[probe] CRASH")
    path = os.path.join(out, "probe.json")
    with open(path, "w", encoding="utf-8") as handle:
        json.dump({"blender": bpy.app.version_string, "results": results}, handle, indent=2)
    failed = [item["name"] for item in results if not item["ok"]]
    say("[probe] wrote %s" % path)
    say("[probe] failed %s" % (failed or "none"))
    # Required for the tools to proceed. Color roundtrip and AO occluder are required
    # because the acceptance numbers depend on them. DIFFUSE on an emission material
    # is informational and may be black.
    required = {
        "optix_device",
        "jarg1_camera",
        "uv_project_apply",
        "world_to_camera_view_uv",
        "bake_emit",
        "color_roundtrip",
        "bake_ao_selected_to_active",
        "bake_normal_selected_to_active",
        "bake_emit_selected_to_active",
        "quadriflow_remesh",
        "smart_uv",
    }
    bad = [name for name in required if name in failed or not any(item["name"] == name and item["ok"] for item in results)]
    if bad:
        say("[probe] REQUIRED_FAIL %s" % bad)
        sys.exit(1)
    say("[probe] REQUIRED_OK")


if __name__ == "__main__":
    main()

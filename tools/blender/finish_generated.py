"""Finish a generated mesh for the engine.

    blender.exe -b -P tools/blender/finish_generated.py -- --in x.glb --class mushroom --tris 1200 --size-cm 2.5 --out <dir>

The self-test builds the dossier fixture (a Blender UV sphere at about 200k faces,
checker albedo, plus a speck under 1% of the faces) and runs this same path:

    blender.exe -b -P tools/blender/finish_generated.py -- --self-test --out <dir>

Floaters are removed before the world-size step. A speck metres away would
otherwise become the longest axis and shrink the hero. The dossier lists
normalise first; the scorecard records this order and why.

Base colour is an EMIT bake of the albedo, selected to active. The probe showed
DIFFUSE color on an emission material stays black, so that pass is not the transfer.
Normal and AO are selected-to-active on Cycles OptiX. Thickness is an inward AO:
the low mesh normals are flipped for the bake and restored after it.
"""
from __future__ import annotations

import json
import os
import sys
import traceback

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bmesh
import bpy

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


def has(args, name):
    return name in args


def say(line):
    print(line, flush=True)


def connect_albedo_to_emission(obj):
    """Make the surface emit the mesh's albedo so the EMIT bake is a colour transfer."""
    if not obj.data.materials:
        mat = f4_lib.make_solid_emission(obj.name + "_albedo", (0.6, 0.6, 0.6))
        obj.data.materials.append(mat)
        return {"source": "default-gray"}
    mat = obj.data.materials[0]
    if mat is None:
        mat = f4_lib.make_solid_emission(obj.name + "_albedo", (0.6, 0.6, 0.6))
        obj.data.materials[0] = mat
        return {"source": "default-gray"}
    mat.use_nodes = True
    nt = mat.node_tree
    images = [node for node in nt.nodes if node.type == "TEX_IMAGE" and node.image]
    out = next((node for node in nt.nodes if node.type == "OUTPUT_MATERIAL"), None)
    if out is None:
        out = nt.nodes.new("ShaderNodeOutputMaterial")
    emit = nt.nodes.new("ShaderNodeEmission")
    emit.inputs["Strength"].default_value = 1.0
    source = "none"
    if images:
        nt.links.new(images[0].outputs["Color"], emit.inputs["Color"])
        source = images[0].image.name
    else:
        principled = next((node for node in nt.nodes if node.type == "BSDF_PRINCIPLED"), None)
        if principled is not None:
            emit.inputs["Color"].default_value = principled.inputs["Base Color"].default_value
            source = "principled-base-color"
        else:
            emit.inputs["Color"].default_value = (0.6, 0.6, 0.6, 1.0)
            source = "default-gray"
    nt.links.new(emit.outputs["Emission"], out.inputs["Surface"])
    return {"source": source}


def build_fixture(path):
    """UV sphere near 200k faces, checker emission, and a speck the floater pass must drop."""
    bpy.ops.mesh.primitive_uv_sphere_add(segments=500, ring_count=400, radius=1.0, location=(0.0, 0.0, 1.0))
    sphere = bpy.context.active_object
    sphere.name = "FixtureSphere"
    faces = len(sphere.data.polygons)
    say("[finish] fixture sphere faces %d" % faces)
    checker = f4_lib.checker_image("fixture_checker", 64, 64, 8, 8)
    checker.pack()
    mat = f4_lib.make_emission_image_material("FixtureChecker", checker, "UVMap")
    # The primitive has a UVMap already. Point the material at it.
    f4_lib.assign_material(sphere, mat)
    for poly in sphere.data.polygons:
        poly.use_smooth = True
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=0.01, location=(4.0, 0.0, 1.0))
    speck = bpy.context.active_object
    speck.name = "FixtureSpeck"
    f4_lib.assign_material(speck, mat)
    f4_lib.activate(sphere)
    speck.select_set(True)
    sphere.select_set(True)
    bpy.ops.object.join()
    joined = bpy.context.active_object
    joined.name = "Fixture"
    f4_lib.export_glb(joined, path)
    say("[finish] fixture glb %s faces %d" % (path, len(joined.data.polygons)))
    return faces


def quadriflow_then_fit(obj, target_tris, tol):
    target_faces = max(int(round(target_tris / 2.0)), 32)
    before = len(obj.data.polygons)
    f4_lib.activate(obj)
    result = bpy.ops.object.quadriflow_remesh(
        target_faces=target_faces,
        use_mesh_symmetry=False,
        use_preserve_sharp=False,
        use_preserve_boundary=False,
    )
    after = len(obj.data.polygons)
    say("[finish] quadriflow %s faces %d -> %d" % (list(result), before, after))
    tris = f4_lib.tri_count(obj)
    report = {"method": "quadriflow", "faces": after, "tris_after_quadriflow": tris, "operator": list(result)}
    if abs(tris - target_tris) / float(target_tris) > tol:
        fitted = f4_lib.decimate_to_tris(obj, target_tris, tol)
        report["collapse_after"] = fitted
        report["method"] = "quadriflow+collapse"
    else:
        report["tris"] = tris
        report["within_tol"] = True
    return report


def voxel_clean(obj, size_m, divisions=140):
    """Rebuild the low as one watertight skin with a Remesh modifier (voxel mode).

    Generator meshes carry non-manifold sheets (T-TER-046: 97 components, 3845 bad edges on a clean-looking
    mushroom). Edge collapse on that tears the surface into shards and stalls near 2k tris. A voxel skin is
    manifold, so the collapse afterwards can reach the budget. The high keeps the detail for the bake.
    """
    f4_lib.activate(obj)
    mod = obj.modifiers.new("VoxelClean", "REMESH")
    mod.mode = "VOXEL"
    mod.voxel_size = size_m / divisions
    mod.use_smooth_shade = False
    before = len(obj.data.polygons)
    bpy.ops.object.modifier_apply(modifier=mod.name)
    # The skin keeps hundreds of speck shells from inner geometry. Drop them now, or the dust pass after the
    # collapse deletes most of the budget (T-TER-046: 381 islands, 1342 tris -> 568).
    dust = f4_lib.delete_floaters(obj, 0.005)
    # Voxelising an open generator shell can leave the skin inside out, and the selected-to-active bake then
    # misses the high (mushroom albedo came out as thin rings). Make the winding outward.
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    return {"method": "voxel", "voxel_size_m": size_m / divisions, "faces_before": before, "faces_after": len(obj.data.polygons), "dust": dust}


def scaled_collapse(obj, target, tol, size_m, factor=100.0):
    """collapse_clean at x100 scale, then back. Edge lengths of 1e-4 m make the quadric collapse tear shards."""
    for vert in obj.data.vertices:
        vert.co *= factor
    obj.data.update()
    result = f4_lib.collapse_clean(obj, target, tol, size_m * factor)
    for vert in obj.data.vertices:
        vert.co /= factor
    obj.data.update()
    result["scaled_by"] = factor
    return result


def finish(obj, args, out):
    asset_class = flag(args, "--class", "prop")
    target = int(flag(args, "--tris", "1200"))
    size_cm = float(flag(args, "--size-cm", "2.5"))
    resolution = int(flag(args, "--res", "1024"))
    remesh = flag(args, "--remesh", "collapse")
    tol = 0.05
    device = f4_lib.set_cycles_device("OPTIX")
    say("[finish] device %s" % json.dumps(device))
    f4_lib.apply_transforms(obj)
    # A glTF import splits every UV seam into coincident vertices, so one generator mesh is thousands of "islands" and
    # collapse cannot reduce it. Weld the splits first (T-TER-046: TRELLIS.2 mushroom, 10125 islands, tris stalled at 5411
    # and the dust pass then deleted every face). Welding keeps loop UVs, so the albedo bake still reads the texture.
    diag = max(f4_lib.world_bounds(obj)["size"])
    seam_weld = f4_lib.weld_cracks(obj, max(diag, 1e-6) * 1e-5)
    say("[finish] seam weld %s" % json.dumps(seam_weld))
    floater = f4_lib.delete_floaters(obj, 0.01)
    say("[finish] floaters %s" % json.dumps(floater))
    # Join nothing else: floaters were faces on this mesh.
    norm = f4_lib.normalize_size(obj, size_cm / 100.0)
    say("[finish] normalize %s" % json.dumps(norm))
    albedo = connect_albedo_to_emission(obj)
    say("[finish] albedo %s" % json.dumps(albedo))
    for poly in obj.data.polygons:
        poly.use_smooth = True
    high = obj
    high.name = "High"
    low = f4_lib.duplicate_object(high, "Low")
    if low.data.materials:
        low.data.materials[0] = high.data.materials[0].copy()
    size_m = size_cm / 100.0
    if remesh == "quadriflow":
        decimate = quadriflow_then_fit(low, target, tol)
    elif remesh == "voxel":
        voxel = voxel_clean(low, size_m)
        say("[finish] voxel %s" % json.dumps(voxel))
        decimate = scaled_collapse(low, target, tol, size_m)
        decimate["voxel"] = voxel
    elif remesh == "scaled":
        decimate = scaled_collapse(low, target, tol, size_m)
    else:
        decimate = f4_lib.collapse_clean(low, target, tol, size_m)
    # Decimate can push a vertex outside the normalised box. The low has to
    # stay on the high or the selected-to-active cage misses and the world
    # size drifts. Snap does not change the triangle count.
    snap = f4_lib.snap_to_surface(low, high, size_m)
    decimate["snap"] = snap
    say("[finish] decimate %s" % json.dumps(decimate))
    uv = f4_lib.smart_uv(low, "UVMap")
    say("[finish] uv %s" % json.dumps(uv))
    for poly in low.data.polygons:
        poly.use_smooth = True
    cage = size_m * 0.04
    ray = cage * 3.0
    scene = bpy.context.scene
    f4_lib.configure_color(scene)
    f4_lib.configure_cycles(scene, 1, 256, 256)
    if scene.world:
        scene.world.light_settings.distance = size_m * 2.0

    albedo_img = f4_lib.new_image("albedo", resolution, resolution, "sRGB")
    albedo_bake = f4_lib.bake(
        low, albedo_img, "EMIT", selected_to_active=True, high=high,
        cage_extrusion=cage, max_ray=ray, samples=1,
    )
    say("[finish] bake albedo %s" % json.dumps(albedo_bake))
    normal_img = f4_lib.new_image("normal", resolution, resolution, "Non-Color")
    normal_bake = f4_lib.bake(
        low, normal_img, "NORMAL", selected_to_active=True, high=high,
        cage_extrusion=cage, max_ray=ray, samples=8,
    )
    say("[finish] bake normal %s" % json.dumps(normal_bake))
    ao_img = f4_lib.new_image("ao", resolution, resolution, "Non-Color")
    ao_bake = f4_lib.bake(
        low, ao_img, "AO", selected_to_active=True, high=high,
        cage_extrusion=cage, max_ray=ray, samples=32,
    )
    say("[finish] bake ao %s" % json.dumps(ao_bake))
    # Inward AO. Rays leave the low surface along the flipped normal and stop
    # at max_ray, so a closed shape records how soon the opposite surface hits.
    f4_lib.flip_normals(low)
    thick_img = f4_lib.new_image("thickness", resolution, resolution, "Non-Color")
    scene.world.light_settings.distance = size_m
    thick_bake = f4_lib.bake(
        low, thick_img, "AO", selected_to_active=False,
        cage_extrusion=0.0, max_ray=size_m, samples=32,
    )
    f4_lib.flip_normals(low)
    say("[finish] bake thickness %s" % json.dumps(thick_bake))

    paths = {}
    for image, name in (
        (albedo_img, "albedo.png"),
        (normal_img, "normal.png"),
        (ao_img, "ao.png"),
        (thick_img, "thickness.png"),
    ):
        paths[name] = f4_lib.save_image(image, os.path.join(out, name))
    # The low material shows the baked albedo for the after render and the FBX.
    baked = f4_lib.load_image(paths["albedo.png"], "sRGB")
    show = f4_lib.make_emission_image_material("BakedAlbedo", baked, "UVMap")
    f4_lib.assign_material(low, show)
    fbx_path = f4_lib.export_fbx(low, os.path.join(out, "%s.fbx" % asset_class))
    card = f4_lib.scorecard(low, "UVMap", resolution, {
        "class": asset_class,
        "requested_tris": target,
        "tolerance": tol,
        "within_tolerance": abs(f4_lib.tri_count(low) - target) / float(target) <= tol,
        "size_cm": size_cm,
        "remesh": decimate,
        "uv_method": uv,
        "floaters": floater,
        "normalize": norm,
        "device": device,
        "bakes": {
            "albedo": albedo_bake,
            "normal": normal_bake,
            "ao": ao_bake,
            "thickness": thick_bake,
        },
        "textures": paths,
        "fbx": fbx_path,
        "albedo_transfer": "EMIT selected-to-active of the albedo. DIFFUSE color stayed black in the probe.",
        "thickness_meaning": "inward AO after flipping the low normals. Dark means a nearby opposite surface.",
        "order": "delete floaters, then normalize. A distant speck must not set the world size. The low is then collapse-decimated, dust islands dropped, crack verts welded, and snapped onto the high.",
    })
    card_path = os.path.join(out, "scorecard.json")
    with open(card_path, "w", encoding="utf-8") as handle:
        json.dump(card, handle, indent=2)
    say("[finish] scorecard %s tris %d within %s" % (card_path, card["tris"], card["within_tolerance"]))
    return {"low": low, "high": high, "card": card, "paths": paths}


def preview(high, low, out):
    scene = bpy.context.scene
    f4_lib.configure_color(scene)
    f4_lib.configure_cycles(scene, 16, 512, 512)
    cam_data = bpy.data.cameras.new("PreviewCam")
    cam_data.sensor_fit = "VERTICAL"
    cam_data.angle = 0.6
    cam_data.clip_start = 0.001
    cam_data.clip_end = 10.0
    cam = bpy.data.objects.new("PreviewCam", cam_data)
    scene.collection.objects.link(cam)
    # The finished mesh sits on Z=0 and is a few centimetres across.
    cam.location = (0.04, -0.05, 0.03)
    from mathutils import Vector
    aim = Vector((0.0, 0.0, 0.012))
    cam.rotation_euler = (aim - cam.location).to_track_quat("-Z", "Y").to_euler()
    high.hide_render = False
    low.hide_render = True
    f4_lib.render_still(scene, cam, os.path.join(out, "before.png"), samples=16)
    high.hide_render = True
    low.hide_render = False
    f4_lib.render_still(scene, cam, os.path.join(out, "after.png"), samples=16)
    say("[finish] previews written")


def run_self_test(out):
    fixture = os.path.join(out, "fixture.glb")
    faces = build_fixture(fixture)
    f4_lib.reset_empty()
    obj = f4_lib.import_gltf(fixture)
    # Import can leave an empty parent. Bake and measure the mesh itself.
    f4_lib.apply_transforms(obj)
    result = finish(obj, ["--class", "sphere", "--tris", "1200", "--size-cm", "2.5", "--res", "512", "--remesh", "collapse"], out)
    preview(result["high"], result["low"], out)
    card = result["card"]
    problems = []
    if not card["within_tolerance"]:
        problems.append("tris %d not within 5%% of 1200" % card["tris"])
    if card["components"] != 1:
        problems.append("components %d, floater still present" % card["components"])
    longest = max(card["bounds_m"]["size"])
    if abs(longest - 0.025) > 0.001:
        problems.append("longest axis %.5f m, want 0.025" % longest)
    if not card["bakes"]["normal"].get("wrote"):
        problems.append("normal bake did not write")
    if not card["bakes"]["ao"].get("wrote"):
        problems.append("ao bake did not write")
    if not card["bakes"]["albedo"].get("wrote"):
        problems.append("albedo bake did not write")
    # The checker must survive: albedo std well above a flat colour.
    if max(card["bakes"]["albedo"]["std"]) < 0.05:
        problems.append("albedo std %s, checker did not transfer" % card["bakes"]["albedo"]["std"])
    if faces < 150000:
        problems.append("fixture faces %d, want about 200k" % faces)
    if problems:
        say("[finish] SELF_TEST_FAIL %s" % problems)
        sys.exit(1)
    say("[finish] SELF_TEST_OK tris %d longest_m %.5f faces_in %d" % (card["tris"], longest, faces))


def main():
    args = argv_after()
    out = flag(args, "--out", os.path.join("orchestration", "runs", "terrarium", "T-TER-041", "finish"))
    if not os.path.isabs(out):
        out = os.path.abspath(out)
    os.makedirs(out, exist_ok=True)
    f4_lib.reset_empty()
    scene = bpy.context.scene
    f4_lib.configure_color(scene)
    f4_lib.configure_cycles(scene, 1, 64, 64)
    if has(args, "--self-test"):
        run_self_test(out)
        return
    src = flag(args, "--in")
    if not src:
        raise SystemExit("need --in x.glb or --self-test")
    if not os.path.isabs(src):
        src = os.path.abspath(src)
    obj = f4_lib.import_gltf(src)
    f4_lib.apply_transforms(obj)
    result = finish(obj, args, out)
    preview(result["high"], result["low"], out)
    if not result["card"]["within_tolerance"]:
        say("[finish] TRI_BUDGET_MISS")
        sys.exit(1)
    say("[finish] OK")


if __name__ == "__main__":
    try:
        main()
    except SystemExit as exc:
        code = exc.code
        if code not in (0, None):
            sys.exit(code if isinstance(code, int) else 1)
    except Exception:
        traceback.print_exc()
        sys.exit(1)

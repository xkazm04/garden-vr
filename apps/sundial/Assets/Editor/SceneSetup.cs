#if UNITY_EDITOR
using System.IO;
using GardenVR.Input;
using GardenVR.Room;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GardenVR.Sundial.Editor
{
    /// <summary>
    /// Idempotent Main scene: seated rig, kitchen plate, desk 0.55 m ahead, empty dial anchor.
    /// Batch: -executeMethod GardenVR.Sundial.Editor.SceneSetup.Run
    /// </summary>
    public static class SceneSetup
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";
        public const string PlatePath = "Assets/Art/Plates/plate-dial.png";
        public const string PlateMaterialPath = "Assets/Art/Plates/PcRoomPlate.mat";
        public const string ProvenancePath = "Assets/Art/Plates/PROVENANCE.md";
        public const string DialId = "dial";

        /// <summary>Negative pitch raises the far rim so the face leans toward the user.</summary>
        public const float DialTiltDegrees = -12f;

        // FidelityTools.DialCamera. Relative to DialRoot. Root projects near pixel (785, 615).
        public static readonly Vector3 DialG1Eye = new Vector3(0f, 0.322f, -0.411f);
        public static readonly Vector3 DialG1Look = new Vector3(0f, 0.012f, 0f);
        public const float DialG1Fov = 30f;
        public static readonly Vector2 DialG1LensShift = new Vector2(3.6f, 0.5f);

        // Rig eye (0, 1.05, 0) written in DialRoot local space. The look is the dial face,
        // not the terrarium desk point 0.15 m in front of it (that gaze saw the disc edge-on).
        public static readonly Vector3 DialSeatedEye = new Vector3(0f, 0.407796f, -0.475608f);
        public static readonly Vector3 DialSeatedLook = new Vector3(0f, 0.012f, 0f);

        /// <summary>Kitchen photo vertical angle in the seated view. Wider than DialG1's 30 deg, narrower than the 130 deg sphere that cropped the inpaint.</summary>
        public const float SeatedPlateVerticalDegrees = 64f;
        public const float SeatedPlateDistance = 2.2f;
        public const string SeatedPlateMeshPath = "Assets/Art/Models/SeatedPlate.asset";

        // Round-3 sun: rays travel toward image-right and down, so the source is the upper-left window.
        static readonly Vector3 WindowLightDirection = new Vector3(0.45f, -0.62f, -0.64f);

        public static void Run()
        {
            CopyPlate();
            BuildScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[SceneSetup] done scene=" + ScenePath + " plate=" + PlatePath);
        }

        static void CopyPlate()
        {
            EnsureAssetFolder("Assets/Art");
            EnsureAssetFolder("Assets/Art/Plates");
            string source = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "..", "..", "shared", "assets", "room-plates", "plate-dial.png"));
            if (!File.Exists(source)) throw new System.InvalidOperationException("plate not found: " + source);
            string destination = Path.GetFullPath(Path.Combine(Application.dataPath, "Art", "Plates", "plate-dial.png"));
            File.Copy(source, destination, true);

            const string provenance =
                "# Plates\n\n" +
                "Development stand-ins for passthrough. Never shipped.\n\n" +
                "| File | Note |\n" +
                "|---|---|\n" +
                "| plate-dial.png | kitchen plate, inpainted from the owner's reference frame, development only, never shipped |\n";
            File.WriteAllText(Path.Combine(Application.dataPath, "Art", "Plates", "PROVENANCE.md"), provenance);

            AssetDatabase.ImportAsset(PlatePath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(PlatePath) as TextureImporter;
            if (importer != null)
            {
                importer.sRGBTexture = true;
                importer.mipmapEnabled = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
        }

        static void BuildScene()
        {
            EnsureAssetFolder("Assets/Scenes");
            var scene = File.Exists(ScenePath)
                ? UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single)
                : UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                    UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                    UnityEditor.SceneManagement.NewSceneMode.Single);

            DestroyNamed("SeatedRig");
            DestroyNamed("DeskAnchor");
            DestroyNamed("EyeCamera");
            DestroyNamed("KeyLight");
            DestroyNamed("Daylight");
            DestroyNamed("DialRoot");
            DestroyLights();

            const string prefabPath = "Packages/com.gardenvr.room/Runtime/Prefabs/SeatedRig.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) throw new System.InvalidOperationException("prefab missing: " + prefabPath);
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (rig == null) throw new System.InvalidOperationException("could not instance " + prefabPath);
            rig.name = "SeatedRig";
            rig.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            if (rig.GetComponent<KeyboardMouseIntentSource>() == null)
                throw new System.InvalidOperationException("KeyboardMouseIntentSource missing on the seated rig");

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(PlatePath);
            if (texture == null) throw new System.InvalidOperationException("plate texture missing: " + PlatePath);
            var plate = rig.GetComponentInChildren<PcRoomPlate>(true);
            if (plate == null) throw new System.InvalidOperationException("PcRoomPlate missing on the seated rig");
            var renderer = plate.GetComponent<Renderer>();
            renderer.sharedMaterial = SaveScenePlate(texture);
            var plateData = new SerializedObject(plate);
            plateData.FindProperty("_plate").objectReferenceValue = texture;
            plateData.FindProperty("_exposure").floatValue = PcRoomPlate.DefaultExposure;
            plateData.FindProperty("_fadeSeconds").floatValue = PcRoomPlate.DefaultFadeSeconds;
            plateData.FindProperty("_pcOnly").boolValue = true;
            plateData.FindProperty("_renderer").objectReferenceValue = renderer;
            plateData.ApplyModifiedPropertiesWithoutUndo();

            var desk = rig.GetComponentInChildren<PcDeskAnchor>(true);
            if (desk == null) throw new System.InvalidOperationException("PcDeskAnchor missing on the seated rig");
            var deskData = new SerializedObject(desk);
            deskData.FindProperty("_height").floatValue = PcDeskAnchor.DefaultHeight;
            deskData.FindProperty("_distance").floatValue = PcDeskAnchor.SundialDistance;
            deskData.FindProperty("_lateral").floatValue = 0f;
            deskData.ApplyModifiedPropertiesWithoutUndo();
            desk.ApplyPose();

            var dial = new GameObject("DialRoot");
            dial.transform.SetParent(desk.transform, false);
            dial.transform.localPosition = Vector3.zero;
            dial.transform.localRotation = Quaternion.Euler(DialTiltDegrees, 0f, 0f);
            dial.transform.localScale = Vector3.one;
            var target = dial.AddComponent<IntentTarget>();
            var targetData = new SerializedObject(target);
            targetData.FindProperty("id").stringValue = DialId;
            targetData.ApplyModifiedPropertiesWithoutUndo();

            var light = CreateDaylight();
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.5f);
            RenderSettings.sun = light;

            AimSeatedAtDial(dial);
            FitSeatedPlate();
            ConfirmFraming(rig, dial);

            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static Light CreateDaylight()
        {
            var lightGo = new GameObject("Daylight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.95f, 0.85f, 1f);
            light.intensity = 1f;
            light.shadows = LightShadows.None;
            lightGo.transform.rotation = Quaternion.LookRotation(WindowLightDirection.normalized, Vector3.up);
            return light;
        }

        static void ConfirmFraming(GameObject rig, GameObject dial)
        {
            Camera eye = null;
            var cameras = rig.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i].gameObject.name == "EyeCamera") eye = cameras[i];
            }
            if (eye == null) throw new System.InvalidOperationException("EyeCamera missing on the seated rig");

            Vector3 seatedEye = dial.transform.TransformPoint(DialSeatedEye);
            float eyeError = Vector3.Distance(seatedEye, eye.transform.position);
            if (eyeError > 0.002f)
                throw new System.InvalidOperationException("DialSeated eye misses the rig eye by " + eyeError.ToString("0.0000") + " m");

            Vector3 seatedLook = dial.transform.TransformPoint(DialSeatedLook);
            Vector3 want = (seatedLook - eye.transform.position).normalized;
            float lookError = Vector3.Angle(want, eye.transform.forward);
            if (lookError > 0.25f)
                throw new System.InvalidOperationException("DialSeated look misses the rig view by " + lookError.ToString("0.00") + " deg");

            Vector3 g1Eye = dial.transform.TransformPoint(DialG1Eye);
            Vector3 g1Look = dial.transform.TransformPoint(DialG1Look);
            Quaternion g1Rotation = Quaternion.LookRotation(g1Look - g1Eye, Vector3.up) * Quaternion.Euler(-DialG1LensShift.y, DialG1LensShift.x, 0f);
            Vector2 rootPixel = ProjectTopLeft(g1Eye, g1Rotation, DialG1Fov, dial.transform.position);
            Vector2 facePixel = ProjectTopLeft(g1Eye, g1Rotation, DialG1Fov, g1Look);
            Debug.Log(
                "[SceneSetup] DialG1 root pixel (" + rootPixel.x.ToString("0.0") + ", " + rootPixel.y.ToString("0.0") +
                ") face pixel (" + facePixel.x.ToString("0.0") + ", " + facePixel.y.ToString("0.0") +
                ") target (785, 615) desk=" + PcDeskAnchor.SundialDistance.ToString("0.00") +
                " tilt=" + DialTiltDegrees.ToString("0") +
                " light=" + WindowLightDirection.normalized.ToString("0.00"));
        }

        /// <summary>Same pose as CaptureCli.CreateCamera. Y is top-left, matching the reference pixel.</summary>
        static Vector2 ProjectTopLeft(Vector3 eye, Quaternion rotation, float fov, Vector3 world)
        {
            Vector3 offset = world - eye;
            float z = Vector3.Dot(offset, rotation * Vector3.forward);
            float x = Vector3.Dot(offset, rotation * Vector3.right);
            float y = Vector3.Dot(offset, rotation * Vector3.up);
            if (z < 1e-5f) throw new System.InvalidOperationException("dial centre is behind the DialG1 camera");
            float tan = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            float aspect = 1824f / 1024f;
            float ndcX = (x / z) / (tan * aspect);
            float ndcY = (y / z) / tan;
            return new Vector2((ndcX * 0.5f + 0.5f) * 1824f, (1f - (ndcY * 0.5f + 0.5f)) * 1024f);
        }

        static Material SaveScenePlate(Texture2D texture)
        {
            Shader shader = Shader.Find("Fidelity/Plate");
            if (shader == null) throw new System.InvalidOperationException("shader not found: Fidelity/Plate");
            var material = AssetDatabase.LoadAssetAtPath<Material>(PlateMaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "PcRoomPlate" };
                AssetDatabase.CreateAsset(material, PlateMaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }
            material.SetTexture("_MainTex", texture);
            material.SetFloat("_Exposure", PcRoomPlate.DefaultExposure);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Point the seated eye at the dial face and size the kitchen plate so the photo is a room, not a magnified crop.
        /// Does not rebuild the dial. Batch: -executeMethod GardenVR.Sundial.Editor.SceneSetup.FixSeatedView
        /// </summary>
        public static void FixSeatedView()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                ScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
            GameObject dial = GameObject.Find("DialRoot");
            if (dial == null) throw new System.InvalidOperationException("DialRoot missing");
            AimSeatedAtDial(dial);
            FitSeatedPlate();
            GameObject trace = GameObject.Find("DeskTrace");
            if (trace != null)
                trace.transform.localScale = new Vector3(0.0001f, trace.transform.localScale.y, 1f);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log("[SceneSetup] seated view aimed at the dial");
        }

        /// <summary>Head pivot looks down at the dial face. Eye local pose stays at the pivot origin.</summary>
        public static void AimSeatedAtDial(GameObject dial)
        {
            GameObject pivot = GameObject.Find("HeadPivot");
            GameObject eye = GameObject.Find("EyeCamera");
            if (pivot == null || eye == null) throw new System.InvalidOperationException("seated eye missing");
            Vector3 target = dial.transform.TransformPoint(DialSeatedLook);
            Vector3 dir = target - eye.transform.position;
            if (dir.sqrMagnitude < 1e-8f) throw new System.InvalidOperationException("seated look is zero");
            Quaternion eyeLocal = eye.transform.localRotation;
            pivot.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up) * Quaternion.Inverse(eyeLocal);
            PrefabUtility.RecordPrefabInstancePropertyModifications(pivot.transform);
            float down = Mathf.Atan2(-dir.y, new Vector2(dir.x, dir.z).magnitude) * Mathf.Rad2Deg;
            Debug.Log("[SceneSetup] seated look-down " + down.ToString("0.00") + " deg distance " + dir.magnitude.ToString("0.000") + " m");
        }

        /// <summary>
        /// Replace the 130 deg spherical plate with a quad that subtends <see cref="SeatedPlateVerticalDegrees"/>.
        /// The shared curve stays on the prefab for the terrarium. This is the sundial scene instance only.
        /// </summary>
        public static void FitSeatedPlate()
        {
            GameObject plate = GameObject.Find("PcRoomPlate");
            if (plate == null) throw new System.InvalidOperationException("PcRoomPlate missing");
            Mesh quad = AssetDatabase.LoadAssetAtPath<Mesh>(SeatedPlateMeshPath);
            if (quad == null)
            {
                EnsureAssetFolder("Assets/Art");
                EnsureAssetFolder("Assets/Art/Models");
                quad = RoomPlateMesh.TraceQuad();
                quad.name = "SeatedPlate";
                AssetDatabase.CreateAsset(quad, SeatedPlateMeshPath);
            }
            MeshFilter filter = plate.GetComponent<MeshFilter>();
            if (filter == null) throw new System.InvalidOperationException("PcRoomPlate has no mesh");
            filter.sharedMesh = quad;
            float height = 2f * SeatedPlateDistance * Mathf.Tan(SeatedPlateVerticalDegrees * 0.5f * Mathf.Deg2Rad);
            float width = height * (1824f / 1024f);
            plate.transform.localPosition = new Vector3(0f, 0f, SeatedPlateDistance);
            plate.transform.localRotation = Quaternion.identity;
            plate.transform.localScale = new Vector3(width, height, 1f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
            PrefabUtility.RecordPrefabInstancePropertyModifications(plate.transform);
            Debug.Log("[SceneSetup] seated plate " + SeatedPlateVerticalDegrees.ToString("0") + " deg vertical, " + width.ToString("0.00") + " x " + height.ToString("0.00") + " m at " + SeatedPlateDistance.ToString("0.00") + " m");
        }

        static void DestroyLights()
        {
            var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null) Object.DestroyImmediate(lights[i].gameObject);
            }
        }

        static void DestroyNamed(string name)
        {
            var found = GameObject.Find(name);
            if (found != null) Object.DestroyImmediate(found);
        }

        static void EnsureAssetFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) return;
            string parent = Path.GetDirectoryName(assetFolder)?.Replace('\\', '/');
            string name = Path.GetFileName(assetFolder);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif

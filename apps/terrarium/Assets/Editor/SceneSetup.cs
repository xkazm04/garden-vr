#if UNITY_EDITOR
using System.IO;
using GardenVR.Room;
using GardenVR.Room.Editor;
using UnityEditor;
using UnityEngine;

namespace GardenVR.Terrarium.Editor
{
    /// <summary>
    /// Idempotent Main scene: seated rig prefab, jar room plate, desk 0.40 m ahead.
    /// Batch: -executeMethod GardenVR.Terrarium.Editor.SceneSetup.Run
    /// </summary>
    public static class SceneSetup
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";
        public const string PlatePath = "Assets/Art/Plates/plate-jar.png";
        public const string SeatedPlatePath = "Assets/Art/Plates/plate-jar-seated.png";
        public const string PlateMaterialPath = "Assets/Art/Plates/PcRoomPlate.mat";
        public const string ProvenancePath = "Assets/Art/Plates/PROVENANCE.md";

        /// <summary>Degrees the seated eye looks above the desk so the jar sits in the lower middle of the 60 deg lens.</summary>
        public const float SeatedLookAboveDesk = 16f;

        /// <summary>
        /// Scene seated pose. <see cref="PcDeskAnchor.TerrariumDistance"/> stays 0.40 m.
        /// A 14 cm jar at that reach is about a quarter of a 60 deg frame. 0.27 m ahead
        /// and 0.20 m below the eye (desk height 0.85 m) brings it to about a third.
        /// </summary>
        public const float SeatedDeskDistance = 0.27f;
        public const float SeatedDeskHeight = 0.85f;

        public static void Run()
        {
            CopyPlate();
            RoomSetup.BuildRigPrefab();
            BuildScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[SceneSetup] done scene=" + ScenePath + " prefab=" + RoomSetup.PrefabPath);
        }

        static void CopyPlate()
        {
            EnsureAssetFolder("Assets/Art");
            EnsureAssetFolder("Assets/Art/Plates");
            CopySharedPlate("plate-jar.png", PlatePath);
            CopySharedPlate("plate-jar-seated.png", SeatedPlatePath);

            const string provenance =
                "# Plates\n\n" +
                "Development stand-ins for passthrough. Never shipped.\n\n" +
                "| File | Note |\n" +
                "|---|---|\n" +
                "| plate-jar.png | G1 plate, inpainted from the owner's reference frame, development only, never shipped |\n" +
                "| plate-jar-seated.png | nearer seated view of the same night desk, empty room, development only, never shipped |\n";
            File.WriteAllText(Path.Combine(Application.dataPath, "Art", "Plates", "PROVENANCE.md"), provenance);

            ConfigurePlate(PlatePath);
            ConfigurePlate(SeatedPlatePath);
        }

        static void CopySharedPlate(string fileName, string assetPath)
        {
            string source = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "..", "..", "shared", "assets", "room-plates", fileName));
            if (!File.Exists(source)) throw new System.InvalidOperationException("plate not found: " + source);
            string destination = Path.GetFullPath(Path.Combine(Application.dataPath, "Art", "Plates", fileName));
            File.Copy(source, destination, true);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        static void ConfigurePlate(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        static void BuildScene()
        {
            EnsureAssetFolder("Assets/Scenes");
            // BuildRigPrefab plants a temporary object in whatever scene is open. Drop it before loading Main.
            var active = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (active.isDirty)
            {
                UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                    UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                    UnityEditor.SceneManagement.NewSceneMode.Single);
            }
            var scene = File.Exists(ScenePath)
                ? UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single)
                : UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                    UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                    UnityEditor.SceneManagement.NewSceneMode.Single);

            DestroyNamed("SeatedRig");
            DestroyNamed("DeskAnchor");
            DestroyNamed("EyeCamera");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RoomSetup.PrefabPath);
            if (prefab == null) throw new System.InvalidOperationException("prefab missing: " + RoomSetup.PrefabPath);
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (rig == null) throw new System.InvalidOperationException("could not instance " + RoomSetup.PrefabPath);
            rig.name = "SeatedRig";
            // Desk local then lands on the world origin, where the jar stays.
            rig.transform.SetPositionAndRotation(
                new Vector3(0f, -SeatedDeskHeight, -SeatedDeskDistance),
                Quaternion.identity);

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SeatedPlatePath);
            if (texture == null) throw new System.InvalidOperationException("plate texture missing: " + SeatedPlatePath);
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
            deskData.FindProperty("_height").floatValue = SeatedDeskHeight;
            deskData.FindProperty("_distance").floatValue = SeatedDeskDistance;
            deskData.FindProperty("_lateral").floatValue = 0f;
            deskData.ApplyModifiedPropertiesWithoutUndo();
            desk.ApplyPose();

            var seated = rig.GetComponent<SeatedRig>();
            if (seated == null) throw new System.InvalidOperationException("SeatedRig missing on the prefab");
            seated.LookAboveDesk = SeatedLookAboveDesk;
            SeatedRig.PlaceLensCard(plate.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(seated);
            PrefabUtility.RecordPrefabInstancePropertyModifications(plate.transform);
            Transform pivot = rig.transform.Find("HeadPivot");
            if (pivot != null) PrefabUtility.RecordPrefabInstancePropertyModifications(pivot);
            if (seated.PlateAnchor != null)
                PrefabUtility.RecordPrefabInstancePropertyModifications(seated.PlateAnchor);

            if (Object.FindAnyObjectByType<Light>() == null)
            {
                var lightGo = new GameObject("KeyLight");
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(0xD9 / 255f, 0x9F / 255f, 0x67 / 255f, 1f);
                light.intensity = 0.35f;
                lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
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

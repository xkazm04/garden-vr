#if UNITY_EDITOR
using System.IO;
using GardenVR.Input;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GardenVR.Room.Editor
{
    /// <summary>
    /// Builds Packages/com.gardenvr.room/Runtime/Prefabs/SeatedRig.prefab.
    /// Batch: -executeMethod GardenVR.Room.Editor.RoomSetup.BuildRigPrefab
    /// </summary>
    public static class RoomSetup
    {
        public const string PrefabPath = "Packages/com.gardenvr.room/Runtime/Prefabs/SeatedRig.prefab";
        public const string MeshPath = "Packages/com.gardenvr.room/Runtime/Meshes/RoomPlateCurve.asset";
        public const string TraceMeshPath = "Packages/com.gardenvr.room/Runtime/Meshes/DeskTraceQuad.asset";
        public const string PlateMaterialPath = "Packages/com.gardenvr.room/Runtime/Prefabs/RoomPlate.mat";
        public const string TraceMaterialPath = "Packages/com.gardenvr.room/Runtime/Prefabs/DeskTrace.mat";

        [MenuItem("Garden VR/Room/Build Seated Rig Prefab")]
        public static void BuildRigPrefab()
        {
            EnsureFolder("Packages/com.gardenvr.room/Runtime/Prefabs");
            EnsureFolder("Packages/com.gardenvr.room/Runtime/Meshes");

            SaveMesh(MeshPath, RoomPlateMesh.Curve(RoomPlateMesh.Radius, 80f, 65f, 64, 32));
            Mesh traceMesh = SaveMesh(TraceMeshPath, RoomPlateMesh.TraceQuad());
            Material plateMaterial = SavePlateMaterial();
            Material traceMaterial = SaveTraceMaterial();

            var root = new GameObject("SeatedRig");
            try
            {
                var rig = root.AddComponent<SeatedRig>();
                var intents = root.AddComponent<KeyboardMouseIntentSource>();

                var headPoseGo = new GameObject("HeadPose");
                headPoseGo.transform.SetParent(root.transform, false);
                var headPose = headPoseGo.AddComponent<KeyboardMouseHeadPose>();

                var pivot = new GameObject("HeadPivot");
                pivot.transform.SetParent(root.transform, false);

                var eyeGo = new GameObject("EyeCamera");
                eyeGo.transform.SetParent(pivot.transform, false);
                var camera = eyeGo.AddComponent<Camera>();
                eyeGo.AddComponent<AudioListener>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0x0E / 255f, 0x1A / 255f, 0x1C / 255f, 1f);
                camera.allowHDR = false;
                if (eyeGo.GetComponent<UniversalAdditionalCameraData>() == null)
                    eyeGo.AddComponent<UniversalAdditionalCameraData>();

                // World-anchored. The G1 photos are only true at their own camera, so the seated view
                // uses a card sized to the 90 deg capture and a wider plate with no hands.
                var anchorGo = new GameObject("RoomPlateAnchor");
                anchorGo.transform.SetParent(root.transform, false);

                var plateGo = new GameObject("PcRoomPlate");
                plateGo.transform.SetParent(anchorGo.transform, false);
                plateGo.AddComponent<MeshFilter>().sharedMesh = traceMesh;
                SeatedRig.PlaceSeatedCard(plateGo.transform);
                var plateRenderer = plateGo.AddComponent<MeshRenderer>();
                plateRenderer.sharedMaterial = plateMaterial;
                plateRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                plateRenderer.receiveShadows = false;
                var plate = plateGo.AddComponent<PcRoomPlate>();

                var deskGo = new GameObject("PcDeskAnchor");
                deskGo.transform.SetParent(root.transform, false);
                var desk = deskGo.AddComponent<PcDeskAnchor>();

                var traceGo = new GameObject("DeskTrace");
                traceGo.transform.SetParent(deskGo.transform, false);
                traceGo.AddComponent<MeshFilter>().sharedMesh = traceMesh;
                var traceRenderer = traceGo.AddComponent<MeshRenderer>();
                traceRenderer.sharedMaterial = traceMaterial;
                traceRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                traceRenderer.receiveShadows = false;

                rig.Bind(pivot.transform, camera, headPose);
                rig.SetPlateAnchor(anchorGo.transform);
                intents.View = camera;
                desk.Height = PcDeskAnchor.DefaultHeight;
                desk.Distance = PcDeskAnchor.TerrariumDistance;
                desk.Lateral = 0f;
                desk.BindTrace(traceGo.transform);
                // Edit-mode shots do not run Start, so leave the trace fully drawn on the near edge.
                traceGo.transform.localScale = new Vector3(0.48f, 0.006f, 1f);
                traceGo.transform.localPosition = new Vector3(0f, 0.012f, -0.16f);

                var plateObject = new SerializedObject(plate);
                plateObject.FindProperty("_renderer").objectReferenceValue = plateRenderer;
                plateObject.FindProperty("_exposure").floatValue = PcRoomPlate.DefaultExposure;
                plateObject.FindProperty("_fadeSeconds").floatValue = PcRoomPlate.DefaultFadeSeconds;
                plateObject.FindProperty("_pcOnly").boolValue = true;
                plateObject.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[RoomSetup] prefab " + PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static Material SavePlateMaterial()
        {
            Shader shader = Shader.Find("Fidelity/Plate");
            if (shader == null) throw new System.InvalidOperationException("shader not found: Fidelity/Plate");
            var material = AssetDatabase.LoadAssetAtPath<Material>(PlateMaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "RoomPlate" };
                AssetDatabase.CreateAsset(material, PlateMaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }
            material.SetFloat("_Exposure", PcRoomPlate.DefaultExposure);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material SaveTraceMaterial()
        {
            Shader shader = Shader.Find("Fidelity/Card");
            if (shader == null) throw new System.InvalidOperationException("shader not found: Fidelity/Card");
            var material = AssetDatabase.LoadAssetAtPath<Material>(TraceMaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "DeskTrace" };
                AssetDatabase.CreateAsset(material, TraceMaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }
            material.SetTexture("_MainTex", Texture2D.whiteTexture);
            material.SetColor("_Color", PcDeskAnchor.TraceMint);
            material.SetColor("_Color2", Color.black);
            material.SetFloat("_Src", 1f);
            material.SetFloat("_Dst", 1f);
            material.SetFloat("_Ring", 0f);
            material.SetFloat("_Boil", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Mesh SaveMesh(string path, Mesh built)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                built.name = Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(built, path);
                return built;
            }

            mesh.Clear();
            mesh.vertices = built.vertices;
            mesh.uv = built.uv;
            if (built.colors != null && built.colors.Length == built.vertices.Length)
                mesh.colors = built.colors;
            mesh.triangles = built.triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            Object.DestroyImmediate(built);
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) return;
            string parent = Path.GetDirectoryName(assetFolder)?.Replace('\\', '/');
            string name = Path.GetFileName(assetFolder);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
                throw new System.InvalidOperationException("cannot create folder " + assetFolder);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif

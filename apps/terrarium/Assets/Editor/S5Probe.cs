#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using GardenVR.Capture;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GardenVR.Terrarium.Editor
{
    /// <summary>
    /// Spike S5 (T-TER-045) probe. <c>Planes</c> answers "are the breath ring and the jar foot on one plane?" with numbers:
    /// the world heights of the ring card, the desk surface, the glass foot and the cork, and the screen positions of the
    /// ring circle and the foot circle in the JarG1 and seated cameras (an ellipse each: left, right, front, back, centre).
    /// Concentric circles on one plane project to concentric ellipses with the same axis ratio. Writes JSON to <c>-out</c>.
    /// </summary>
    public static class S5Probe
    {
        const string Scene = "Assets/Scenes/Main.unity";

        public static void Planes()
        {
            try
            {
                string output = DoPlanes();
                Debug.Log("[S5] Planes OK " + output);
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[S5] Planes FAIL " + e);
                EditorApplication.Exit(1);
            }
        }

        static string Arg(string name)
        {
            string[] argv = Environment.GetCommandLineArgs();
            for (int i = 0; i < argv.Length - 1; i++)
                if (string.Equals(argv[i], "-" + name, StringComparison.OrdinalIgnoreCase)) return argv[i + 1];
            throw new InvalidOperationException("missing -" + name);
        }

        static string F(float v) { return v.ToString("0.####", CultureInfo.InvariantCulture); }

        static string P(Camera cam, Vector3 world, int w, int h)
        {
            Vector3 s = cam.WorldToScreenPoint(world);
            return "[" + F(s.x / cam.pixelWidth * w) + "," + F((1f - s.y / cam.pixelHeight) * h) + "]";
        }

        static string DoPlanes()
        {
            EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            JarView jar = UnityEngine.Object.FindAnyObjectByType<JarView>();
            if (jar == null) throw new InvalidOperationException("no JarView");
            jar.ApplyCaptureState(CaptureState.Parse("breath=0.5,uncoil=0.3,fog=0.45,time=3"));
            Transform ring = null, glass = null, cork = null;
            foreach (Transform t in jar.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "BreathRing") ring = t;
                if (t.name == "Jar" && t.GetComponent<MeshFilter>() != null) glass = t;
                if (t.name == "Cork" && t.GetComponent<MeshFilter>() != null) cork = t;
            }
            if (ring == null || glass == null || cork == null) throw new InvalidOperationException("ring, glass or cork missing");
            var sb = new StringBuilder("{\n");
            Bounds gb = glass.GetComponent<Renderer>().bounds;
            Bounds cb = cork.GetComponent<Renderer>().bounds;
            sb.Append("  \"jarOriginY\": " + F(jar.transform.position.y) + ",\n");
            sb.Append("  \"ringCardWorldY\": " + F(ring.position.y) + ",\n");
            sb.Append("  \"ringCardHalfExtent\": " + F(ring.lossyScale.x * 0.5f) + ",\n");
            sb.Append("  \"glassMinY\": " + F(gb.min.y) + ", \"glassMaxY\": " + F(gb.max.y) + ", \"glassRadiusX\": " + F(gb.extents.x) + ",\n");
            sb.Append("  \"corkMinY\": " + F(cb.min.y) + ", \"corkMaxY\": " + F(cb.max.y) + ",\n");
            var deskTops = new List<string>();
            foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
            {
                if (r.transform.IsChildOf(jar.transform)) continue;
                string n = r.name.ToLowerInvariant();
                if (n.Contains("desk") || n.Contains("table"))
                    deskTops.Add("{\"name\":\"" + r.name + "\",\"minY\":" + F(r.bounds.min.y) + ",\"maxY\":" + F(r.bounds.max.y) + ",\"minZ\":" + F(r.bounds.min.z) + ",\"maxZ\":" + F(r.bounds.max.z) + "}");
            }
            sb.Append("  \"deskRenderers\": [" + string.Join(",", deskTops.ToArray()) + "],\n");
            float ringR = ring.lossyScale.x * 0.5f * 0.92f;  // ring.png peaks at r = 0.92 of the half extent
            const float footR = 0.045f;
            Vector3 o = jar.transform.position;
            Vector3[] dirs = { Vector3.left, Vector3.right, Vector3.back, Vector3.forward };
            string[] names = { "left", "right", "front", "back" };
            var cams = new List<string>();
            foreach (string framing in new[] { "JarG1", "SeatedPOV" })
            {
                Camera cam = MakeCamera(framing, jar.transform);
                int w = 1824, h = 1024;
                var cs = new StringBuilder("    \"" + framing + "\": {\n");
                cs.Append("      \"eye\": [" + F(cam.transform.position.x) + "," + F(cam.transform.position.y) + "," + F(cam.transform.position.z) + "], \"fov\": " + F(cam.fieldOfView) + ",\n");
                cs.Append("      \"ringCentre\": " + P(cam, new Vector3(o.x, ring.position.y, o.z), w, h) + ",\n");
                cs.Append("      \"footCentre\": " + P(cam, new Vector3(o.x, gb.min.y, o.z), w, h) + ",\n");
                for (int i = 0; i < dirs.Length; i++)
                {
                    cs.Append("      \"ring_" + names[i] + "\": " + P(cam, new Vector3(o.x, ring.position.y, o.z) + dirs[i] * ringR, w, h) + ",\n");
                    cs.Append("      \"foot_" + names[i] + "\": " + P(cam, new Vector3(o.x, gb.min.y, o.z) + dirs[i] * footR, w, h) + (i == dirs.Length - 1 ? "\n" : ",\n"));
                }
                cs.Append("    }");
                cams.Add(cs.ToString());
                UnityEngine.Object.DestroyImmediate(cam.gameObject);
            }
            sb.Append("  \"cameras\": {\n" + string.Join(",\n", cams.ToArray()) + "\n  }\n}\n");
            string outPath = Path.GetFullPath(Arg("out"));
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            File.WriteAllText(outPath, sb.ToString());
            return outPath;
        }

        static Camera MakeCamera(string framingName, Transform jar)
        {
            Framing framing = Framings.LoadApp().Get(framingName);
            var go = new GameObject("S5ProbeCamera");
            var cam = go.AddComponent<Camera>();
            cam.aspect = 1824f / 1024f;
            cam.fieldOfView = framing.Fov;
            GameObject eye = framingName == "SeatedPOV" ? GameObject.Find("EyeCamera") : null;
            if (eye != null)
            {
                cam.transform.SetPositionAndRotation(eye.transform.position, eye.transform.rotation);
                Camera eyeCam = eye.GetComponent<Camera>();
                if (eyeCam != null) cam.fieldOfView = eyeCam.fieldOfView;
            }
            else
            {
                Vector3 eyePos = jar.TransformPoint(framing.Eye);
                Vector3 look = jar.TransformPoint(framing.LookAt);
                cam.transform.rotation = Quaternion.LookRotation(look - eyePos, Vector3.up) * Quaternion.Euler(-framing.LensShift.y, framing.LensShift.x, 0f);
                cam.transform.position = eyePos;
            }
            return cam;
        }
    }
}
#endif

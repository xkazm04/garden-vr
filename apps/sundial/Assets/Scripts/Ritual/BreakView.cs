using System.Collections.Generic;
using GardenVR.Core;
using GardenVR.Input;
using UnityEngine;
using UnityEngine.Rendering;

namespace GardenVR.Sundial
{
    /// <summary>Paper slips and authored ink paths. The PC window anchor is on the room plate at 3 m.</summary>
    public sealed class BreakView : MonoBehaviour
    {
        TextMesh _note, _eyes, _action;
        GameObject _mote, _figure;
        readonly List<LineRenderer> _lines = new List<LineRenderer>();
        Material _ink;
        BreakPart _part;
        HandPose _pose;
        bool _active;
        public Transform EyeTarget => _mote == null ? null : _mote.transform;

        void Awake()
        {
            _ink = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            _ink.SetColor("_BaseColor", InkLetter.Ink);
            _note = Note("BreakNote", new Vector2(.12f, .055f));
            _eyes = Note("BreakEyeSlip", new Vector2(.43f, .12f));
            _action = Note("BreakAction", new Vector2(.105f, .032f), "break.start");
            _mote = new GameObject("break.eye"); _mote.transform.SetParent(transform, false);
            _mote.AddComponent<SphereCollider>().radius = .045f;
            _mote.AddComponent<IntentTarget>().Id = "break.eye";
            var ring = new Vector2[41];
            for (int i = 0; i < ring.Length; i++)
            {
                float a = i * Mathf.PI * 2 / (ring.Length - 1);
                ring[i] = new Vector2(Mathf.Cos(a) * .039f, Mathf.Sin(a) * .032f);
            }
            Stroke(_mote.transform, ring, .003f);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Stroke(_mote.transform, new[] { d * .052f, d * .064f }, .002f);
            }
            _figure = new GameObject("BreakPoseFigure"); _figure.transform.SetParent(transform, false);
            DrawPose(HandPose.PalmOpen);
        }

        void OnEnable() { RenderPipelineManager.beginCameraRendering += Place; }
        void OnDisable() { RenderPipelineManager.beginCameraRendering -= Place; }
        void OnDestroy() { if (_ink != null) Destroy(_ink); }

        TextMesh Note(string name, Vector2 size, string id = null)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false);
            var mesh = go.AddComponent<TextMesh>();
            InkLetter.Apply(mesh, "", 64, .002f);
            InkLetter.AttachNote(go.transform, size);
            if (id != null)
            {
                go.AddComponent<BoxCollider>().size = new Vector3(size.x * .8f, size.y * .8f, .005f);
                go.AddComponent<IntentTarget>().Id = id;
            }
            return mesh;
        }

        void Letter(TextMesh text, string value, float width, float height)
        {
            if (text.text == value) return;
            InkLetter.Apply(text, value, 64, .002f);
            Vector3 bounds = text.GetComponent<MeshRenderer>().localBounds.size;
            text.characterSize *= Mathf.Min(width / Mathf.Max(.001f, bounds.x), height / Mathf.Max(.001f, bounds.y));
        }

        public void Show(BreakSession session, bool paused, bool available, string message, IHandIntentSource source)
        {
            _active = session != null && !session.Complete;
            _part = session == null ? BreakPart.Done : session.Part;
            bool eyes = _active && _part == BreakPart.EyeRest;
            _mote.SetActive(eyes); _eyes.gameObject.SetActive(eyes);
            _note.gameObject.SetActive(_active || !string.IsNullOrEmpty(message));
            _action.gameObject.SetActive(available || _active);
            _action.GetComponent<IntentTarget>().Id = _active ? (paused ? "break.resume" : "break.pause") : "break.start";
            Letter(_action, _active ? (paused ? "Continue break" : "Pause break") : "Take a break", .08f, .014f);
            string line = message;
            if (_active)
            {
                if (paused) line = "Take your time\nPinch continue when ready";
                else if (_part == BreakPart.Stretch) line = "Reach to each mark\nstretch " + session.Stretch.Reaches + " of 3";
                else if (_part == BreakPart.Mobility)
                {
                    HandPose pose = session.Mobility.Current;
                    if (pose != _pose) DrawPose(pose);
                    var kind = pose == HandPose.PalmOpen ? HandIntentKind.PalmOpen : HandIntentKind.PinchHold;
                    line = (pose == HandPose.PalmOpen ? "Hold an open palm" : "Hold a pinch") + "\n" +
                        source?.BindingHint(kind) + "   " + session.Mobility.Done + " of 3";
                }
                else line = "A moment by the window\nLooking away holds your place";
            }
            Letter(_note, line ?? "", .097f, .033f);
            if (eyes) Letter(_eyes, "Rest your eyes here\n" + session.Eyes.SecondsLeft + " s", .34f, .074f);
            _figure.SetActive(_active && _part == BreakPart.Mobility && !paused);
        }

        void Place(ScriptableRenderContext context, Camera cam)
        {
            if (cam == null || cam.cameraType != CameraType.Game || _note == null) return;
            PlaceAt(cam, _note.transform, .09f, .30f, .45f, .15f, .12f);
            PlaceAt(cam, _action.transform, .09f, .19f, .45f, .12f, .105f);
            PlaceAt(cam, _figure.transform, .09f, .42f, .45f, .038f, .023f);
            PlaceAt(cam, _mote.transform, .257f, .932f, EyeRest.TargetDistance(null), .05f, .128f);
            PlaceAt(cam, _eyes.transform, .263f, .84f, EyeRest.TargetDistance(null), .17f, .43f);
        }

        static void PlaceAt(Camera cam, Transform item, float x, float y, float distance, float viewportWidth, float authoredWidth)
        {
            Ray ray = cam.ViewportPointToRay(new Vector3(x, y, 0));
            item.position = cam.transform.position + ray.direction * distance;
            item.rotation = cam.transform.rotation;
            float depth = Vector3.Dot(item.position - cam.transform.position, cam.transform.forward);
            float worldWidth = 2 * depth * Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad * .5f) * cam.aspect;
            item.localScale = Vector3.one * (worldWidth * viewportWidth / authoredWidth);
        }

        LineRenderer Stroke(Transform parent, Vector2[] points, float width)
        {
            var go = new GameObject("ink"); go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>(); line.useWorldSpace = false;
            line.sharedMaterial = _ink; line.widthMultiplier = width; line.numCapVertices = 3;
            line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
            line.positionCount = points.Length;
            for (int i = 0; i < points.Length; i++) line.SetPosition(i, new Vector3(points[i].x, points[i].y, 0));
            return line;
        }

        void DrawPose(HandPose pose)
        {
            _pose = pose;
            foreach (var line in _lines) if (line != null) { line.gameObject.SetActive(false); Destroy(line.gameObject); }
            _lines.Clear();
            // A compact continuous hand contour, with unequal fingers and a folded thumb for the pinch.
            Vector2[] points = pose == HandPose.PalmOpen ? new[] {
                new Vector2(-9,-16), new Vector2(-10,-7), new Vector2(-17,2), new Vector2(-17,6),
                new Vector2(-14,7), new Vector2(-8,1), new Vector2(-9,19), new Vector2(-7,22),
                new Vector2(-5,21), new Vector2(-3,7), new Vector2(-3,25), new Vector2(0,27),
                new Vector2(2,25), new Vector2(3,8), new Vector2(5,22), new Vector2(8,23),
                new Vector2(10,21), new Vector2(8,6), new Vector2(12,16), new Vector2(15,16),
                new Vector2(16,13), new Vector2(12,-6), new Vector2(8,-16)
            } : new[] {
                new Vector2(-9,-16), new Vector2(-10,-7), new Vector2(-15,0), new Vector2(-14,8),
                new Vector2(-7,17), new Vector2(1,20), new Vector2(8,18), new Vector2(10,14),
                new Vector2(7,11), new Vector2(1,13), new Vector2(-4,8), new Vector2(3,5),
                new Vector2(8,11), new Vector2(11,12), new Vector2(15,7), new Vector2(12,-6), new Vector2(8,-16)
            };
            for (int i = 0; i < points.Length; i++) points[i] *= .0007f;
            _lines.Add(Stroke(_figure.transform, points, .0007f));
        }
    }
}

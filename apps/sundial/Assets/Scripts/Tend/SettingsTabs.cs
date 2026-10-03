using System.Collections.Generic;
using GardenVR.Input;
using UnityEngine;
using UnityEngine.Rendering;

namespace GardenVR.Sundial
{
    /// <summary>
    /// A paper tab on the near rim. Poke opens it. Each row is a tab: mute, beds, voice, reduced motion.
    /// Poke flips that one setting and writes the save. The ink dot is on. The pencil ring is off.
    /// Nothing here reads a keyboard or a mouse.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(60)]
    public sealed class SettingsTabs : MonoBehaviour
    {
        public const string TabId = "tab.settings";
        public const string MuteId = "tab.settings.mute";
        public const string BedsId = "tab.settings.beds";
        public const string VoiceId = "tab.settings.voice";
        public const string MotionId = "tab.settings.motion";
        public const string CueTap = "tab.tap";

        public const string TabLabel = "Settings";
        public const string MuteLabel = "Mute";
        public const string BedsLabel = "Beds";
        public const string VoiceLabel = "Voice";
        public const string MotionLabel = "Reduced motion";

        static readonly Color Ink = new Color(0.165f, 0.149f, 0.133f, 1f);
        static readonly Color Pencil = new Color(0.541f, 0.506f, 0.471f, 1f);

        SundialController _controller;
        SundialService _service;
        DialView _view;
        bool _built;
        GameObject _root;
        GameObject _sheet;
        Material _inkMat;
        Texture2D _disc;
        Texture2D _ring;
        Mesh _quad;
        readonly List<string> _cues = new List<string>();
        readonly Row[] _rows = new Row[4];

        public bool Open { get; private set; }
        public IReadOnlyList<string> Cues { get { return _cues; } }

        public int CueCount(string id)
        {
            int count = 0;
            for (int i = 0; i < _cues.Count; i++)
            {
                if (_cues[i] == id) count++;
            }
            return count;
        }

        /// <summary>The words drawn on that tab. Empty when the sheet is shut or the id is unknown.</summary>
        public string Label(string id)
        {
            if (id == TabId) return Words(_root);
            Row row = FindRow(id);
            if (row == null || row.Words == null) return "";
            return row.Words.text;
        }

        public bool HandlePoke(string id)
        {
            if (_service == null) return false;
            if (!_built) Build();
            if (id == TabId)
            {
                Open = !Open;
                if (_sheet != null) _sheet.SetActive(Open);
                Tap();
                return true;
            }
            if (!Open) return false;
            Row row = FindRow(id);
            if (row == null) return false;
            row.Toggle(_service);
            RefreshMarks();
            Tap();
            return true;
        }

        void Awake()
        {
            _controller = GetComponent<SundialController>();
            _view = GetComponent<DialView>();
            if (_controller != null) _service = _controller.Service;
        }

        void Start()
        {
            if (!Application.isPlaying) return;
            if (_controller == null) _controller = GetComponent<SundialController>();
            if (_service == null && _controller != null) _service = _controller.Service;
            if (_view == null) _view = GetComponent<DialView>();
            Build();
        }

        void LateUpdate()
        {
            if (!Application.isPlaying || !_built || _root == null) return;
            Place();
            RefreshMarks();
        }

        void OnDestroy()
        {
            if (_inkMat != null) Destroy(_inkMat);
            if (_disc != null) Destroy(_disc);
            if (_ring != null) Destroy(_ring);
            if (_quad != null) Destroy(_quad);
        }

        void Build()
        {
            if (_built) return;
            _built = true;
            EnsureInk();
            _rows[0] = new Row(MuteId, MuteLabel, s => s.Mute, (s, on) => s.SetMute(on));
            _rows[1] = new Row(BedsId, BedsLabel, s => s.Beds, (s, on) => s.SetBeds(on));
            _rows[2] = new Row(VoiceId, VoiceLabel, s => s.Voice, (s, on) => s.SetVoice(on));
            _rows[3] = new Row(MotionId, MotionLabel, s => s.ReducedMotion, (s, on) => s.SetReducedMotion(on));

            _root = new GameObject(TabId);
            _root.transform.SetParent(transform, false);
            var ear = MakeTarget(_root.transform, "ear", TabLabel, TabId, 0.092f, 0.032f);
            ear.transform.localPosition = Vector3.zero;
            InkLetter.AttachNote(ear.transform, new Vector2(0.092f, 0.032f));

            _sheet = new GameObject("sheet");
            _sheet.transform.SetParent(_root.transform, false);
            _sheet.transform.localPosition = new Vector3(0f, 0.112f, 0f);
            var paper = new GameObject("paper");
            paper.transform.SetParent(_sheet.transform, false);
            paper.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            paper.transform.localPosition = new Vector3(0f, 0.016f, 0.002f);
            paper.transform.localScale = new Vector3(0.220f, 0.188f, 1f);
            paper.AddComponent<MeshFilter>().sharedMesh = _quad;
            var paperRenderer = paper.AddComponent<MeshRenderer>();
            paperRenderer.sharedMaterial = _inkMat;
            paperRenderer.shadowCastingMode = ShadowCastingMode.Off;
            paperRenderer.receiveShadows = false;
            InkLetter.Tint(paperRenderer, InkLetter.Note(0.220f / 0.188f), Color.white);

            float[] ys = { 0.056f, 0.030f, 0.004f, -0.022f };
            for (int i = 0; i < _rows.Length; i++)
            {
                Row row = _rows[i];
                GameObject line = MakeTarget(_sheet.transform, row.Id, row.Label, row.Id, 0.118f, 0.020f);
                line.transform.localPosition = new Vector3(0.008f, ys[i], -0.012f);
                row.Words = line.GetComponent<TextMesh>();
                row.Mark = MakeMark(line.transform);
            }
            _sheet.SetActive(false);
            Open = false;
        }

        GameObject MakeTarget(Transform parent, string name, string text, string id, float width, float height)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var mesh = go.AddComponent<TextMesh>();
            InkLetter.Apply(mesh, text, 48, 0.0022f);
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(width, height, 0.01f);
            var target = go.AddComponent<IntentTarget>();
            target.Id = id;
            return go;
        }

        Renderer MakeMark(Transform line)
        {
            var go = new GameObject("mark");
            go.transform.SetParent(line, false);
            go.transform.localPosition = new Vector3(-0.052f, 0f, -0.001f);
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            go.transform.localScale = new Vector3(0.010f, 0.010f, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = _quad;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _inkMat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.material.renderQueue = 3100;
            Tint(renderer, _ring, Color.white);
            return renderer;
        }

        void RefreshMarks()
        {
            if (_service == null) return;
            for (int i = 0; i < _rows.Length; i++)
            {
                Row row = _rows[i];
                if (row == null || row.Mark == null) continue;
                bool on = row.Read(_service);
                Tint(row.Mark, on ? _disc : _ring, Color.white);
            }
        }

        void Place()
        {
            float y = _view != null && _view.faceY > 0.001f ? _view.faceY + 0.004f : 0.016f;
            _root.transform.localPosition = new Vector3(0f, y, -0.158f);
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 away = _root.transform.position - cam.transform.position;
            if (away.sqrMagnitude < 1e-8f) return;
            _root.transform.rotation = Quaternion.LookRotation(away, Vector3.up);
        }

        void Tap()
        {
            _cues.Add(CueTap);
            Transform at = _root != null ? _root.transform : transform;
            if (_controller != null) _controller.Play(CueTap, at);
            else Debug.Log("[Sundial] cue " + CueTap);
        }

        Row FindRow(string id)
        {
            for (int i = 0; i < _rows.Length; i++)
            {
                if (_rows[i] != null && _rows[i].Id == id) return _rows[i];
            }
            return null;
        }

        static string Words(GameObject root)
        {
            if (root == null) return "";
            Transform ear = root.transform.Find("ear");
            if (ear == null) return "";
            TextMesh mesh = ear.GetComponent<TextMesh>();
            return mesh == null ? "" : mesh.text;
        }

        void EnsureInk()
        {
            Shader shader = Shader.Find("Fidelity/Card");
            if (shader == null) return;
            _inkMat = new Material(shader) { name = "SettingsInk" };
            _inkMat.SetFloat("_Src", 5f);
            _inkMat.SetFloat("_Dst", 10f);
            _inkMat.SetFloat("_Ring", 0f);
            _inkMat.SetFloat("_Coverage", 0f);
            _inkMat.SetFloat("_Mask", 0f);
            _inkMat.SetFloat("_ZWrite", 0f);
            _disc = Disc(Ink, true);
            _ring = Disc(Pencil, false);
            _quad = Quad();
        }

        static void Tint(Renderer renderer, Texture2D texture, Color color)
        {
            if (renderer == null) return;
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            if (texture != null) block.SetTexture("_MainTex", texture);
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
        }

        static Texture2D Disc(Color ink, bool filled)
        {
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var pixels = new Color[n * n];
            float mid = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x - mid) * (x - mid) + (y - mid) * (y - mid));
                    bool on = filled ? d < 11f : d > 8f && d < 13f;
                    pixels[y * n + x] = on ? ink : new Color(0f, 0f, 0f, 0f);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false, false);
            return tex;
        }

        static Mesh Quad()
        {
            var mesh = new Mesh { name = "SettingsQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f)
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        sealed class Row
        {
            public readonly string Id;
            public readonly string Label;
            public TextMesh Words;
            public Renderer Mark;
            readonly System.Func<SundialService, bool> _read;
            readonly System.Action<SundialService, bool> _write;

            public Row(string id, string label, System.Func<SundialService, bool> read, System.Action<SundialService, bool> write)
            {
                Id = id;
                Label = label;
                _read = read;
                _write = write;
            }

            public bool Read(SundialService service)
            {
                return service != null && _read(service);
            }

            public void Toggle(SundialService service)
            {
                if (service == null) return;
                _write(service, !Read(service));
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GardenVR.Core;
using GardenVR.Input;
using UnityEngine;

namespace GardenVR.Sundial
{
    /// <summary>Transient speech and presentation only. Core guards the line and owns the journal.</summary>
    [DefaultExecutionOrder(110)]
    public sealed class ReflectionController : MonoBehaviour
    {
        public const string SpeakId = "reflection.speak";
        public const string OfferLine = "Hold to tell the garden about today";
        SundialController _controller;
        SpeechController _speech;
        IHandIntentSource _source;
        Task<ReflectionResult> _call;
        CancellationTokenSource _cancel;
        SundialSave _requestSave;
        int _day;
        string _key;
        readonly List<GameObject> _chrome = new List<GameObject>();
        public SundialCoach CoachOverride { get; set; }
        public bool Open { get; private set; }
        public bool Waiting => _call != null;
        public string Line { get; private set; }
        public bool FromModel { get; private set; }
        SundialService Service => _controller.Service;

        void Awake() { _controller = GetComponent<SundialController>(); _speech = GetComponent<SpeechController>(); }
        void Start() { if (_source == null) SetSource(_controller.Source); }
        public void SetSource(IHandIntentSource source)
        {
            Unsubscribe(); CancelPending(); _source = source;
            if (isActiveAndEnabled) Subscribe();
        }
        void Subscribe() { if (_source != null) { _source.Intent += OnIntent; _source.SystemPause += Pause; } }
        void Unsubscribe() { if (_source != null) { _source.Intent -= OnIntent; _source.SystemPause -= Pause; } }
        void OnEnable() { Subscribe(); }
        void OnDisable() { Unsubscribe(); Close(); }
        void OnApplicationFocus(bool focused) { if (!focused) Pause(); }
        void OnApplicationPause(bool paused) { if (paused) Pause(); }

        public void Offer()
        {
            CancelPending(); Open = true; Line = null; FromModel = false; _key = null;
        }
        public void Close() { CancelPending(); Open = false; Line = null; ClearChrome(); }
        void Pause() { CancelPending(); }
        public void ForgetJournal() { Close(); }

        void CancelPending()
        {
            if (Open) _speech?.CompleteCoachCall();
            _cancel?.Cancel(); _cancel?.Dispose(); _cancel = null;
            if (_call != null) _call.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            _call = null; _requestSave = null; _key = null;
        }
        bool Hidden => _controller.Dismissed || Service.Scrubbing ||
            (_controller.Wizard != null && _controller.Wizard.Running) ||
            (GetComponent<WeekDialController>()?.Turn > .02f) == true ||
            (_controller.Settings != null && _controller.Settings.Open);

        void OnIntent(HandIntent intent)
        {
            if (intent.Kind == HandIntentKind.PalmOpen) { Pause(); return; }
            if (Hidden || intent.Kind != HandIntentKind.Pinch) return;
            if (intent.TargetId == "reflection.open" && CanReopen()) { Offer(); return; }
            if (!Open) return;
            if (intent.TargetId == "reflection.done") { Close(); return; }
            if (intent.TargetId == "reflection.again") { Offer(); return; }
            if (Waiting || Line != null) return;
            for (int i = 0; i < OneWord.Count; i++)
                if (intent.TargetId == "reflection.stone." + i) { Reflect(null, OneWord.Stones[i]); return; }
        }
        bool CanReopen()
        {
            if (Service.Save.Journal.On(Service.Today().Index) != null) return true;
            var habit = Service.HabitForArc("winddown");
            return SundialRules.IsBreathHabit(habit) && Service.Ledger.IsKept(habit.Id, Service.Today().Index);
        }
        void Reflect(string transcript, string stone)
        {
            _day = Service.Today().Index;
            _requestSave = Service.Save;
            _cancel = new CancellationTokenSource();
            _call = (CoachOverride ?? _controller.Coach).ReflectAsync(transcript, Service.ReflectionToday(), stone, _cancel.Token);
            _speech.CompleteCoachCall();
        }
        void Update()
        {
            if (Service == null) return;
            if (_controller.Source != _source) SetSource(_controller.Source);
            if (Hidden || (_source != null && !_source.IsTracked))
            {
                CancelPending(); ClearChrome(); return;
            }
            if (_call != null && _call.IsCompleted)
            {
                var call = _call; _call = null;
                _cancel.Dispose(); _cancel = null;
                _speech.CompleteCoachCall();
                if (call.Status == TaskStatus.RanToCompletion && ReferenceEquals(_requestSave, Service.Save) && Open)
                {
                    var result = call.Result;
                    if (Service.KeepReflection(_day, result.Line)) { Line = result.Line; FromModel = result.FromModel; }
                }
                else if (call.IsFaulted) { var ignored = call.Exception; }
                _requestSave = null;
            }
            if (Open && !Waiting && Line == null)
            {
                if (!string.IsNullOrWhiteSpace(_speech.Transcript) && (_source == null || !_source.IsPinching))
                    Reflect(_speech.Transcript, null);
                else if (!_speech.Armed && (_source == null || !_source.IsPinching)) _speech.Arm(SpeakId);
            }
            string key = Open + ":" + Waiting + ":" + Line + ":" + _speech.Available + ":" + CanReopen();
            if (_key != key) { ClearChrome(); Draw(); _key = key; }
        }
        void LateUpdate()
        {
            foreach (var go in _chrome) if (go != null) ReflectionPaper.Face(go.transform);
        }
        void ClearChrome()
        {
            foreach (var go in _chrome) if (go != null) { go.SetActive(false); Destroy(go); }
            _chrome.Clear(); _key = null;
        }
        void Note(string id, string text, float x, float y, float z, float w, float h, bool target = true)
        {
            _chrome.Add(ReflectionPaper.Note(transform, id, text,
                new Vector3(x * .67f, _controller.View.faceY + y * .67f, z * .67f), new Vector2(w, h) * .70f, target));
        }
        void Draw()
        {
            if (!Open)
            {
                if (CanReopen()) Note("reflection.open", "Reflect", -.11f, .015f, -.10f, .055f, .026f);
                return;
            }
            if (Line != null)
            {
                string date = new GardenDay(_day).CivilDate.ToString("ddd d MMM", System.Globalization.CultureInfo.InvariantCulture);
                Note("ReflectionLine", date + "\n\n" + ReflectionPaper.Wrap(Line, 24) + "\n\nkept in the notebook",
                    -.145f, .085f, -.025f, .125f, .115f, false);
                Note("reflection.again", "Reflect again", -.15f, .020f, -.035f, .070f, .027f);
            }
            else
            {
                Note(SpeakId, Waiting ? "A moment for today" : _speech.Available ? ReflectionPaper.Wrap(OfferLine, 24) : "Choose a word for today",
                    -.145f, .085f, -.025f, .13f, .067f, !Waiting && _speech.Available);
                if (!Waiting)
                    for (int i = 0; i < OneWord.Count; i++)
                        Note("reflection.stone." + i, OneWord.Stones[i], -.18f + (i % 3) * .039f,
                            .041f - (i / 3) * .023f, -.027f, .040f, .026f);
            }
            Note("reflection.done", "Done", -.10f, .020f, -.072f, .046f, .027f);
        }
    }

    static class ReflectionPaper
    {
        public static GameObject Note(Transform parent, string id, string text, Vector3 position, Vector2 size, bool target)
        {
            var go = new GameObject(id); go.transform.SetParent(parent, false); go.transform.localPosition = position;
            var words = go.AddComponent<TextMesh>(); InkLetter.Apply(words, text, 48, .0025f);
            var bounds = go.GetComponent<MeshRenderer>().localBounds.size;
            words.characterSize *= Mathf.Min(1, size.x * .58f / Mathf.Max(.001f, bounds.x), size.y * .62f / Mathf.Max(.001f, bounds.y));
            var paper = InkLetter.AttachNote(go.transform, size);
            var owned = go.AddComponent<ReflectionPaperMaterials>();
            owned.Lift(paper.GetComponent<Renderer>(), go.GetComponent<Renderer>());
            if (id.StartsWith("reflection.stone.", StringComparison.Ordinal)) owned.ShapeStone(paper);
            if (target)
            {
                go.AddComponent<BoxCollider>().size = new Vector3(size.x * .80f, size.y * .80f, .004f);
                go.AddComponent<IntentTarget>().Id = id;
            }
            Face(go.transform); return go;
        }
        public static void Face(Transform t)
        {
            var cam = Camera.main;
            if (cam == null) cam = GameObject.Find("EyeCamera")?.GetComponent<Camera>();
            if (cam != null) t.rotation = Quaternion.LookRotation(t.position - cam.transform.position, Vector3.up);
        }
        public static string Wrap(string text, int width)
        {
            string result = ""; int n = 0;
            foreach (string word in text.Split(' '))
            {
                if (n > 0 && n + word.Length + 1 > width) { result += "\n"; n = 0; }
                if (n > 0) { result += " "; n++; }
                result += word; n += word.Length;
            }
            return result;
        }
    }

    sealed class ReflectionPaperMaterials : MonoBehaviour
    {
        Material _paper, _words;
        Mesh _stone;
        public void Lift(Renderer paper, Renderer words)
        {
            _paper = new Material(paper.sharedMaterial) { renderQueue = 3390 };
            paper.sharedMaterial = _paper;
            _words = new Material(words.sharedMaterial) { renderQueue = 3400 };
            words.sharedMaterial = _words;
        }
        public void ShapeStone(GameObject paper)
        {
            // An authored asymmetric pebble contour, shared by the six ink word stones.
            var rim = new[] { new Vector3(-.45f,-.06f), new Vector3(-.40f,-.24f), new Vector3(-.25f,-.36f),
                new Vector3(-.03f,-.39f), new Vector3(.23f,-.33f), new Vector3(.40f,-.20f),
                new Vector3(.46f,.02f), new Vector3(.38f,.24f), new Vector3(.19f,.36f),
                new Vector3(-.07f,.39f), new Vector3(-.29f,.29f), new Vector3(-.43f,.14f) };
            var vertices = new Vector3[rim.Length * 2 + 1];
            var colors = new Color[vertices.Length];
            var triangles = new List<int>();
            colors[0] = new Color(.89f, .87f, .81f);
            for (int i = 0; i < rim.Length; i++)
            {
                int next = (i + 1) % rim.Length;
                vertices[1 + i] = rim[i] * .95f; colors[1 + i] = new Color(.81f, .80f, .74f);
                vertices[1 + rim.Length + i] = rim[i]; colors[1 + rim.Length + i] = InkLetter.Ink;
                triangles.AddRange(new[] { 0, 1 + next, 1 + i, 1 + i, 1 + next, 1 + rim.Length + next,
                    1 + i, 1 + rim.Length + next, 1 + rim.Length + i });
            }
            _stone = new Mesh { name = "Ink word pebble", vertices = vertices, colors = colors };
            _stone.SetTriangles(triangles, 0); _stone.RecalculateNormals(); _stone.RecalculateBounds();
            paper.GetComponent<MeshFilter>().sharedMesh = _stone;
            InkLetter.Tint(paper.GetComponent<Renderer>(), Texture2D.whiteTexture, new Color(.86f, .84f, .78f));
        }
        void OnDestroy()
        {
            if (_paper != null) Destroy(_paper); if (_words != null) Destroy(_words); if (_stone != null) Destroy(_stone);
        }
    }
}

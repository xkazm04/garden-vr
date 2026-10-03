using System.Globalization;
using GardenVR.Core;
using GardenVR.Input;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace GardenVR.Terrarium
{
    /// <summary>
    /// One pebble beside the jar opens the rest. Each stone carries one etched setting.
    /// Mute, voice, night bed, and motion sit on the near arc. Pace, breaths, and hold sit behind them.
    /// Poke cycles the value. The intents come from the hand provider.
    /// </summary>
    public sealed class SettingsPebbles : MonoBehaviour
    {
        public const string LeadId = "pebble.settings";
        public const string MuteId = "pebble.set.mute";
        public const string VoiceId = "pebble.set.voice";
        public const string BedId = "pebble.set.bed";
        public const string MotionId = "pebble.set.motion";
        public const string PaceId = "pebble.set.pace";
        public const string BreathsId = "pebble.set.breaths";
        public const string HoldId = "pebble.set.hold";

        static readonly int[] Inhale = { 3, 4, 5 };
        static readonly int[] Exhale = { 5, 6, 7 };
        static readonly int[] BreathChoices = { 3, 4, 6, 8 };

        static readonly Color Stone = new Color(0.22f, 0.20f, 0.17f, 1f);
        static readonly Color StoneLit = new Color(0.16f, 0.34f, 0.26f, 1f);

        JarRitualController _controller;
        GameObject _fan;
        bool _open;

        struct Row
        {
            public string Id;
            public string Kind;
            public TextMeshPro Words;
        }

        Row[] _rows;

        public bool Open { get { return _open; } }

        public void Bind(JarRitualController controller)
        {
            _controller = controller;
        }

        public void Build()
        {
            ClearChildren();
            RitualSettings settings = ReadSettings();
            BuildLead();
            _fan = new GameObject("Fan");
            _fan.transform.SetParent(transform, false);
            const float Near = -0.052f;
            const float Far = -0.102f;
            _rows = new Row[7];
            _rows[0] = BuildRow(_fan.transform, MuteId, "mute", MuteText(settings), -0.117f, Near);
            _rows[1] = BuildRow(_fan.transform, VoiceId, "voice", settings.VoiceGuide ? "Voice on" : "Voice off", -0.039f, Near);
            _rows[2] = BuildRow(_fan.transform, BedId, "bed", settings.NightBed ? "Night bed on" : "Night bed off", 0.039f, Near);
            _rows[3] = BuildRow(_fan.transform, MotionId, "motion", settings.ReducedMotion ? "Quieter motion" : "Full motion", 0.117f, Near);
            _rows[4] = BuildRow(_fan.transform, PaceId, "pace", PaceText(settings), -0.100f, Far);
            _rows[5] = BuildRow(_fan.transform, BreathsId, "breaths", BreathText(settings), 0f, Far);
            _rows[6] = BuildRow(_fan.transform, HoldId, "hold", settings.HoldMode == "Toggle" ? "Toggle" : "Hold", 0.100f, Far);
            _fan.SetActive(false);
            _open = false;
        }

        public void OnIntent(HandIntent intent)
        {
            if (intent.Kind != HandIntentKind.Poke && intent.Kind != HandIntentKind.Pinch) return;
            if (intent.TargetId == LeadId)
            {
                SetOpen(!_open);
                Tap();
                return;
            }
            if (!_open || _rows == null) return;
            for (int i = 0; i < _rows.Length; i++)
            {
                if (_rows[i].Id != intent.TargetId) continue;
                Cycle(_rows[i].Kind);
                Tap();
                return;
            }
        }

        public string Label(string id)
        {
            if (_rows == null || id == null) return null;
            for (int i = 0; i < _rows.Length; i++)
            {
                if (_rows[i].Id != id || _rows[i].Words == null) continue;
                return _rows[i].Words.text;
            }
            return null;
        }

        public void SetOpen(bool open)
        {
            _open = open;
            if (_fan != null && _fan.activeSelf != open) _fan.SetActive(open);
        }

        /// <summary>Seated capture of the open pebbles. Defaults, no save file.</summary>
        public static void PresentOpen(Transform jar)
        {
            if (jar == null) return;
            var go = new GameObject("SettingsPebbles");
            go.transform.SetParent(jar, false);
            var pebbles = go.AddComponent<SettingsPebbles>();
            pebbles.Build();
            pebbles.SetOpen(true);
        }

        void Cycle(string kind)
        {
            RitualSettings settings = ReadSettings();
            if (kind == "pace")
            {
                int index = 0;
                for (int i = 0; i < Inhale.Length; i++)
                {
                    if ((int)settings.InhaleSec == Inhale[i]) index = i;
                }
                index = (index + 1) % Inhale.Length;
                settings.InhaleSec = Inhale[index];
                settings.ExhaleSec = Exhale[index];
            }
            else if (kind == "breaths")
            {
                int index = 0;
                for (int i = 0; i < BreathChoices.Length; i++)
                {
                    if (settings.Breaths == BreathChoices[i]) index = i;
                }
                settings.Breaths = BreathChoices[(index + 1) % BreathChoices.Length];
            }
            else if (kind == "mute") settings.Mute = !settings.Mute;
            else if (kind == "voice") settings.VoiceGuide = !settings.VoiceGuide;
            else if (kind == "bed") settings.NightBed = !settings.NightBed;
            else if (kind == "motion") settings.ReducedMotion = !settings.ReducedMotion;
            else if (kind == "hold") settings.HoldMode = settings.HoldMode == "Toggle" ? "Hold" : "Toggle";
            Write(settings);
            Refresh(settings);
        }

        void Refresh(RitualSettings settings)
        {
            if (_rows == null) return;
            for (int i = 0; i < _rows.Length; i++)
            {
                if (_rows[i].Words == null) continue;
                string text = _rows[i].Words.text;
                if (_rows[i].Kind == "pace") text = PaceText(settings);
                else if (_rows[i].Kind == "breaths") text = BreathText(settings);
                else if (_rows[i].Kind == "mute") text = MuteText(settings);
                else if (_rows[i].Kind == "voice") text = settings.VoiceGuide ? "Voice on" : "Voice off";
                else if (_rows[i].Kind == "bed") text = settings.NightBed ? "Night bed on" : "Night bed off";
                else if (_rows[i].Kind == "motion") text = settings.ReducedMotion ? "Quieter motion" : "Full motion";
                else if (_rows[i].Kind == "hold") text = settings.HoldMode == "Toggle" ? "Toggle" : "Hold";
                EtchedLettering.Fit(_rows[i].Words, text, 0.062f, 0.007f);
            }
        }

        void Write(RitualSettings settings)
        {
            if (_controller == null || _controller.Service == null) return;
            _controller.Service.ApplySettings(settings);
            _controller.NoteSettingsChanged();
        }

        RitualSettings ReadSettings()
        {
            if (_controller != null && _controller.Service != null && _controller.Service.Settings != null)
                return _controller.Service.Settings;
            return new RitualSettings();
        }

        void Tap()
        {
            if (_controller != null) _controller.PlayCue("pebble.tap", transform);
        }

        void BuildLead()
        {
            var pivot = new GameObject("SettingsPebble");
            pivot.transform.SetParent(transform, false);
            pivot.transform.localPosition = new Vector3(0.108f, 0.008f, 0.012f);
            StoneMesh(pivot.transform, 0.020f);
            EtchedLettering.Place(pivot.transform, "Words", "Settings", new Vector3(0f, 0.020f, 0f), Quaternion.identity, 0.046f, 0.008f);
            var box = pivot.AddComponent<BoxCollider>();
            box.size = new Vector3(0.040f, 0.036f, 0.028f);
            box.center = new Vector3(0f, 0.008f, 0f);
            var target = pivot.AddComponent<IntentTarget>();
            target.Id = LeadId;
            target.PokeOnly = true;
        }

        Row BuildRow(Transform parent, string id, string kind, string text, float x, float z)
        {
            var pivot = new GameObject("Pebble-" + kind);
            pivot.transform.SetParent(parent, false);
            pivot.transform.localPosition = new Vector3(x, 0.008f, z);
            StoneMesh(pivot.transform, 0.016f);
            TextMeshPro words = EtchedLettering.Place(pivot.transform, "Words", text, new Vector3(0f, 0.016f, 0f), Quaternion.identity, 0.060f, 0.0065f);
            var box = pivot.AddComponent<BoxCollider>();
            box.size = new Vector3(0.042f, 0.032f, 0.024f);
            box.center = new Vector3(0f, 0.006f, 0f);
            var target = pivot.AddComponent<IntentTarget>();
            target.Id = id;
            target.PokeOnly = true;
            return new Row { Id = id, Kind = kind, Words = words };
        }

        static void StoneMesh(Transform parent, float width)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Stone";
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = new Vector3(width, width * 0.62f, width * 0.78f);
            Shader shader = Shader.Find("Fidelity/Glow");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var mat = new Material(shader) { name = "PebbleStone" };
            if (mat.HasProperty("_Tint")) mat.SetColor("_Tint", Stone);
            if (mat.HasProperty("_Emission")) mat.SetColor("_Emission", StoneLit);
            if (mat.HasProperty("_Rim")) mat.SetColor("_Rim", Color.black);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Stone);
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        void ClearChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(transform.GetChild(i).gameObject);
        }

        static string MuteText(RitualSettings settings)
        {
            return settings != null && settings.Mute ? "Muted" : "Sound on";
        }

        static string PaceText(RitualSettings settings)
        {
            int inhale = settings != null ? (int)settings.InhaleSec : 4;
            int exhale = settings != null ? (int)settings.ExhaleSec : 6;
            return inhale.ToString(CultureInfo.InvariantCulture) + " s in, " + exhale.ToString(CultureInfo.InvariantCulture) + " s out";
        }

        static string BreathText(RitualSettings settings)
        {
            int breaths = settings != null ? settings.Breaths : 6;
            return breaths.ToString(CultureInfo.InvariantCulture) + " breaths";
        }
    }
}

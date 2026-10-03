using System.Collections.Generic;
using System.Text;
using GardenVR.Core;
using GardenVR.Input;
using GardenVR.Room;
using UnityEngine;
using UnityEngine.Rendering;

namespace GardenVR.Terrarium
{
    /// <summary>
    /// Six seed packets along the desk's near edge, then an etched label for each planted habit.
    /// Today and Yesterday are the check-in targets. The undo mark sits on the label for the six-second window.
    /// </summary>
    public sealed class HabitDesk : MonoBehaviour
    {
        public struct HabitView
        {
            public string Preset;
            public bool Yesterday;
            public bool Undo;
        }

        static readonly Color Mint = new Color(0x8F / 255f, 0xF0 / 255f, 0xC8 / 255f, 1f);
        static readonly Color Moon = new Color(0xDD / 255f, 0xF3 / 255f, 0xFF / 255f, 1f);
        static readonly Color Gold = new Color(0xF2 / 255f, 0xD2 / 255f, 0x7A / 255f, 1f);
        const float PacketWidth = 0.040f;
        const float PacketHeight = 0.034f;
        const float Span = 0.40f;

        PcDeskAnchor _anchor;
        bool _reduced;
        string _signature = "";
        Material[] _packetMats;
        Color[] _packetTints;
        Mesh _quad;

        public void Bind(PcDeskAnchor anchor)
        {
            _anchor = anchor;
        }

        public void Apply(bool offered, bool reduced, IReadOnlyList<HabitView> planted)
        {
            _reduced = reduced;
            string next = Signature(offered, planted);
            if (next == _signature) return;
            _signature = next;
            Rebuild(offered, planted);
        }

        public bool YesterdayVisible(string preset)
        {
            Transform child = FindNamed("Yesterday-" + preset);
            return child != null && child.gameObject.activeInHierarchy;
        }

        public bool UndoVisible(string preset)
        {
            Transform child = FindNamed("Undo-" + preset);
            return child != null && child.gameObject.activeInHierarchy;
        }

        void Update()
        {
            if (!Application.isPlaying || _packetMats == null) return;
            float glow = 1f;
            if (!_reduced)
                glow = 0.72f + 0.28f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.1f));
            for (int i = 0; i < _packetMats.Length; i++)
            {
                if (_packetMats[i] != null)
                    _packetMats[i].SetColor("_Emission", _packetTints[i] * glow);
            }
        }

        void OnDestroy()
        {
            KillPackets();
            if (_quad != null) Object.DestroyImmediate(_quad);
        }

        void Rebuild(bool offered, IReadOnlyList<HabitView> planted)
        {
            KillPackets();
            for (int i = transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(transform.GetChild(i).gameObject);

            var plantedIds = new HashSet<string>();
            if (planted != null)
            {
                for (int i = 0; i < planted.Count; i++)
                {
                    if (!string.IsNullOrEmpty(planted[i].Preset))
                        plantedIds.Add(planted[i].Preset);
                }
            }

            if (offered)
            {
                var mats = new List<Material>();
                var tints = new List<Color>();
                for (int i = 0; i < Companions.PresetKeys.Length; i++)
                {
                    string preset = Companions.PresetKeys[i];
                    if (plantedIds.Contains(preset)) continue;
                    Color tint = Tint(Companions.SpeciesFor(preset));
                    mats.Add(BuildPacket(preset, i, tint));
                    tints.Add(tint);
                }
                _packetMats = mats.ToArray();
                _packetTints = tints.ToArray();
            }

            if (planted == null) return;
            for (int i = 0; i < planted.Count; i++)
            {
                HabitView view = planted[i];
                if (string.IsNullOrEmpty(view.Preset)) continue;
                int index = IndexOf(view.Preset);
                BuildLabel(view, index < 0 ? i : index);
            }
        }

        Transform FindNamed(string name)
        {
            Transform[] all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == name) return all[i];
            }
            return null;
        }

        Material BuildPacket(string preset, int index, Color tint)
        {
            var go = new GameObject("Packet-" + preset);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(XOf(index), 0.029f, -Near + 0.04f);
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var card = new GameObject("Card");
            card.transform.SetParent(go.transform, false);
            card.transform.localScale = new Vector3(PacketWidth, PacketHeight, 1f);
            var filter = card.AddComponent<MeshFilter>();
            filter.sharedMesh = Quad();
            var renderer = card.AddComponent<MeshRenderer>();
            Material mat = Glow(tint);
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            // The packet root is yawed 180 so the card faces the user. The etched line takes the counter-yaw.
            AddText(go.transform, Companions.Label(preset), 0.0009f, Vector3.zero, true);
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(PacketWidth, PacketHeight, 0.012f);
            var target = go.AddComponent<IntentTarget>();
            target.Id = "seed." + preset;
            return mat;
        }

        void BuildLabel(HabitView view, int index)
        {
            var root = new GameObject("Label-" + view.Preset);
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(XOf(index), 0.02f, -Near + 0.015f);
            root.transform.localRotation = Quaternion.identity;
            AddText(root.transform, Companions.Label(view.Preset), 0.00105f, new Vector3(0f, 0.016f, 0f));
            BuildTarget(root.transform, "Today-" + view.Preset, "Today", "label." + view.Preset + ".today",
                new Vector3(-0.022f, 0f, 0f), new Vector3(0.040f, 0.032f, 0.012f), 0.00135f, true);
            BuildTarget(root.transform, "Yesterday-" + view.Preset, "Yesterday", "label." + view.Preset + ".yesterday",
                new Vector3(0.022f, 0f, 0f), new Vector3(0.040f, 0.032f, 0.012f), 0.00105f, view.Yesterday);
            BuildTarget(root.transform, "Undo-" + view.Preset, "undo", "undo." + view.Preset,
                new Vector3(0f, -0.016f, 0f), new Vector3(0.032f, 0.032f, 0.012f), 0.0014f, view.Undo);
        }

        static void BuildTarget(Transform parent, string name, string word, string id, Vector3 localPos, Vector3 size, float character, bool active)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            AddText(go.transform, word, character, Vector3.zero);
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            go.SetActive(true);
            var target = go.AddComponent<IntentTarget>();
            target.Id = id;
            target.PokeOnly = false;
            go.SetActive(active);
        }

        static void AddText(Transform parent, string word, float character, Vector3 localPos, bool counterTurn = false)
        {
            Quaternion turn = counterTurn ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
            float cap = Mathf.Clamp(character * 9f, 0.0065f, 0.012f);
            EtchedLettering.Place(parent, "Words", word, localPos, turn, 0.038f, cap);
        }

        Material Glow(Color tint)
        {
            Shader shader = Shader.Find("Fidelity/Glow");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            mat.name = "PacketMat";
            mat.SetTexture("_MainTex", Texture2D.whiteTexture);
            mat.SetTexture("_EmissionTex", Texture2D.whiteTexture);
            mat.SetColor("_Tint", tint);
            mat.SetColor("_Emission", tint);
            mat.SetColor("_Rim", Color.black);
            mat.SetFloat("_Cutoff", 0.05f);
            return mat;
        }

        Mesh Quad()
        {
            if (_quad != null) return _quad;
            _quad = new Mesh();
            _quad.name = "PacketQuad";
            _quad.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f)
            };
            _quad.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            };
            _quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            _quad.RecalculateNormals();
            _quad.RecalculateBounds();
            return _quad;
        }

        void KillPackets()
        {
            if (_packetMats == null) return;
            for (int i = 0; i < _packetMats.Length; i++)
            {
                if (_packetMats[i] != null) Object.DestroyImmediate(_packetMats[i]);
            }
            _packetMats = null;
            _packetTints = null;
        }

        float Near
        {
            get { return _anchor != null ? _anchor.NearEdge : 0.16f; }
        }

        static float XOf(int index)
        {
            int n = Companions.PresetKeys.Length;
            return -Span * 0.5f + Span * (index + 0.5f) / n;
        }

        static int IndexOf(string preset)
        {
            for (int i = 0; i < Companions.PresetKeys.Length; i++)
            {
                if (Companions.PresetKeys[i] == preset) return i;
            }
            return -1;
        }

        static Color Tint(CompanionSpecies species)
        {
            if (species == CompanionSpecies.MoonMoss) return Moon;
            if (species == CompanionSpecies.StarFern) return Gold;
            return Mint;
        }

        static string Signature(bool offered, IReadOnlyList<HabitView> planted)
        {
            var sb = new StringBuilder();
            sb.Append(offered ? '1' : '0');
            if (planted == null) return sb.ToString();
            for (int i = 0; i < planted.Count; i++)
            {
                HabitView view = planted[i];
                sb.Append('|').Append(view.Preset);
                sb.Append(view.Yesterday ? 'Y' : 'n');
                sb.Append(view.Undo ? 'U' : 'n');
            }
            return sb.ToString();
        }
    }
}

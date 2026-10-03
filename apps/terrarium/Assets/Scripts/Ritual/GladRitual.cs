using System.Globalization;
using System.Text;
using GardenVR.Core;
using GardenVR.Input;
using UnityEngine;
using UnityEngine.Rendering;

namespace GardenVR.Terrarium
{
    /// <summary>
    /// Three fern leaves in the moss, each a pinch target. A drop uses the existing dew material
    /// on its own instance so the locked asset stays put. After the third pinch the drops ease
    /// down into the moss. Nothing about the three things is kept here.
    /// </summary>
    public sealed class GladRitual : MonoBehaviour
    {
        public const string LeafPrefix = "glad.leaf.";
        public const float SettleSeconds = 1.6f;
        public const string LineIdle = "Pinch a leaf for one good thing.";
        public const string LineNext = "And another.";
        public const string LineLast = "One more.";
        public const string LineDone = "Done.";

        static readonly Color DewRest = new Color(0.75f, 1f, 0.88f);
        static readonly Vector3[] Spots =
        {
            new Vector3(-0.020f, JarView.MossBedY + 0.012f, -0.014f),
            new Vector3(0.001f, JarView.MossBedY + 0.016f, -0.020f),
            new Vector3(0.020f, JarView.MossBedY + 0.012f, -0.014f)
        };

        readonly ThreeGoodThings _session = new ThreeGoodThings();
        readonly Transform[] _drops = new Transform[ThreeGoodThings.LeafCount];
        readonly Material[] _glows = new Material[ThreeGoodThings.LeafCount];
        readonly Vector3[] _rest = new Vector3[ThreeGoodThings.LeafCount];
        readonly Vector3[] _moss = new Vector3[ThreeGoodThings.LeafCount];

        JarView _view;
        GameObject _words;
        string _shown;
        float _settle = -1f;
        bool _settled;
        bool _counted;
        int _countedDay = int.MinValue;

        public ThreeGoodThings Session { get { return _session; } }
        public bool Built { get; private set; }
        public int Placed { get { return _session.Placed; } }
        public bool Settled { get { return _settled; } }
        public bool Counted { get { return _counted; } }
        public string Line { get { return _shown; } }

        public static string LeafId(int index)
        {
            return LeafPrefix + index.ToString(CultureInfo.InvariantCulture);
        }

        public static bool TryLeafIndex(string targetId, out int leaf)
        {
            leaf = -1;
            if (string.IsNullOrEmpty(targetId) || !targetId.StartsWith(LeafPrefix, System.StringComparison.Ordinal))
                return false;
            string tail = targetId.Substring(LeafPrefix.Length);
            if (tail.Length != 1 || tail[0] < '0' || tail[0] > '2') return false;
            leaf = tail[0] - '0';
            return true;
        }

        public bool Owns(string targetId)
        {
            int leaf;
            return TryLeafIndex(targetId, out leaf);
        }

        public void Bind(JarView view)
        {
            _view = view;
            if (Built || view == null)
            {
                if (view == null) Debug.Log("[Glad] view missing");
                return;
            }
            Transform frond = FindNamed(view.transform, "Frond");
            if (frond == null) frond = view.newFrond != null ? view.newFrond.transform : null;
            if (frond == null)
            {
                Debug.Log("[Glad] no frond under " + view.name + " kids=" + ChildNames(view.transform));
                return;
            }
            MeshFilter filter = frond.GetComponent<MeshFilter>();
            Renderer fernRenderer = frond.GetComponent<Renderer>();
            Material fern = fernRenderer != null ? fernRenderer.sharedMaterial : null;
            if (fern == null) fern = view.fernMat;
            if (filter == null || filter.sharedMesh == null || fern == null)
            {
                Debug.Log("[Glad] frond " + frond.name + " mesh=" + (filter != null && filter.sharedMesh != null) + " mat=" + (fern != null));
                return;
            }
            Material dewShared = view.dew != null ? view.dew.sharedMaterial : null;

            var root = new GameObject("GladLeaves");
            root.transform.SetParent(view.transform, false);
            float[] yaw = { 24f, -4f, -28f };
            for (int i = 0; i < ThreeGoodThings.LeafCount; i++)
            {
                var leaf = new GameObject("GladLeaf" + i.ToString(CultureInfo.InvariantCulture));
                leaf.transform.SetParent(root.transform, false);
                leaf.transform.localPosition = Spots[i];
                leaf.transform.localRotation = Quaternion.Euler(0f, yaw[i], 0f) * frond.localRotation;
                leaf.transform.localScale = frond.localScale * 0.46f;
                var meshFilter = leaf.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = filter.sharedMesh;
                var meshRenderer = leaf.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = fern;
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
                var box = leaf.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0.05f, 0f);
                box.size = new Vector3(0.14f, 0.18f, 0.04f);
                var target = leaf.AddComponent<IntentTarget>();
                target.Id = LeafId(i);

                var drop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Collider dropCollider = drop.GetComponent<Collider>();
                if (dropCollider != null)
                {
                    if (Application.isPlaying) Destroy(dropCollider);
                    else DestroyImmediate(dropCollider);
                }
                drop.name = "GladDrop" + i.ToString(CultureInfo.InvariantCulture);
                drop.transform.SetParent(view.transform, false);
                drop.transform.localScale = Vector3.one * 0.0044f;
                Renderer dropRenderer = drop.GetComponent<Renderer>();
                if (dewShared != null && dropRenderer != null)
                {
                    var glow = new Material(dewShared) { name = "GladDew" + i.ToString(CultureInfo.InvariantCulture) };
                    glow.SetColor("_Emission", DewRest);
                    glow.SetColor("_Tint", new Color(0.78f, 1f, 0.92f));
                    dropRenderer.sharedMaterial = glow;
                    _glows[i] = glow;
                }
                drop.SetActive(false);
                _drops[i] = drop.transform;
                _rest[i] = Spots[i] + new Vector3(0f, 0.020f, -0.002f);
                _moss[i] = new Vector3(Spots[i].x * 0.4f, JarView.MossBedY + 0.004f, Spots[i].z * 0.45f);
            }
            Built = true;
        }

        public Transform DropTransform(int leaf)
        {
            if (leaf < 0 || leaf >= _drops.Length) return null;
            return _drops[leaf];
        }

        public float DropLocalY(int leaf)
        {
            Transform drop = DropTransform(leaf);
            return drop != null ? drop.localPosition.y : 0f;
        }

        /// <summary>True when this pinch placed a new drop. <paramref name="leaf"/> is that leaf.</summary>
        public bool TryPinch(string targetId, out int leaf)
        {
            leaf = -1;
            if (!Built || _counted) return false;
            if (!TryLeafIndex(targetId, out leaf)) return false;
            if (!_session.TryPlace(leaf)) return false;
            ShowDrop(leaf, _rest[leaf], 1.65f);
            if (_session.Complete)
            {
                _settle = 0f;
                _settled = false;
            }
            return true;
        }

        public void NoteCounted(int today)
        {
            _counted = true;
            _countedDay = today;
        }

        /// <summary>A new garden day clears a finished ritual. A day already stored shows the drops at rest.</summary>
        public void Sync(int? doneDay, int today)
        {
            if (!Built) return;
            if (doneDay == today)
            {
                if (!_session.Complete) ShowRestored(today);
                return;
            }
            if (_counted && _countedDay != today)
                Clear();
        }

        public void Tick(float dt, bool showWords, bool reducedMotion)
        {
            if (dt < 0f) dt = 0f;
            if (_settle >= 0f && !_settled)
            {
                if (reducedMotion) _settle = SettleSeconds;
                else _settle += dt;
                float u = Mathf.Clamp01(_settle / SettleSeconds);
                float eased = u * u * (3f - 2f * u);
                for (int i = 0; i < ThreeGoodThings.LeafCount; i++)
                {
                    if (_drops[i] == null) continue;
                    _drops[i].localPosition = Vector3.Lerp(_rest[i], _moss[i], eased);
                    float glow = Mathf.Lerp(1.7f, 1f, eased);
                    if (_glows[i] != null) _glows[i].SetColor("_Emission", DewRest * glow);
                }
                if (u >= 1f) _settled = true;
            }
            PresentLine(ChooseLine(showWords));
        }

        string ChooseLine(bool showWords)
        {
            if (!showWords || !Built) return null;
            if (_settle >= 0f && !_settled) return LineDone;
            if (_counted || _settled) return null;
            if (_session.Placed <= 0) return LineIdle;
            if (_session.Placed == 1) return LineNext;
            if (_session.Placed == 2) return LineLast;
            return null;
        }

        void ShowRestored(int today)
        {
            _session.RestoreDone();
            _counted = true;
            _countedDay = today;
            _settled = true;
            _settle = SettleSeconds;
            for (int i = 0; i < ThreeGoodThings.LeafCount; i++)
                ShowDrop(i, _moss[i], 1f);
        }

        void Clear()
        {
            _session.Reset();
            _counted = false;
            _countedDay = int.MinValue;
            _settled = false;
            _settle = -1f;
            for (int i = 0; i < ThreeGoodThings.LeafCount; i++)
            {
                if (_drops[i] != null) _drops[i].gameObject.SetActive(false);
                if (_glows[i] != null) _glows[i].SetColor("_Emission", DewRest);
            }
        }

        static string ChildNames(Transform root)
        {
            var sb = new StringBuilder();
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            int limit = all.Length < 24 ? all.Length : 24;
            for (int i = 0; i < limit; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(all[i].name);
            }
            return sb.ToString();
        }

        static Transform FindNamed(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name)) return null;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == name) return all[i];
            }
            return null;
        }

        void ShowDrop(int leaf, Vector3 local, float glow)
        {
            if (_drops[leaf] == null) return;
            _drops[leaf].gameObject.SetActive(true);
            _drops[leaf].localPosition = local;
            _drops[leaf].localScale = Vector3.one * 0.0044f;
            if (_glows[leaf] != null) _glows[leaf].SetColor("_Emission", DewRest * glow);
        }

        void PresentLine(string line)
        {
            if (line == _shown) return;
            _shown = line;
            if (_view == null) return;
            if (string.IsNullOrEmpty(line))
            {
                if (_words != null) _words.SetActive(false);
                return;
            }
            if (_words == null)
            {
                var tmp = EtchedLettering.Place(
                    _view.transform, "GladWords", line,
                    new Vector3(0f, 0.004f, -0.070f), Quaternion.identity, 0.14f, 0.0075f);
                _words = tmp != null ? tmp.gameObject : null;
                return;
            }
            _words.SetActive(true);
            EtchedLettering.SetText(_words, line, 0.14f, 0.0075f);
        }

        void OnDestroy()
        {
            for (int i = 0; i < _glows.Length; i++)
            {
                if (_glows[i] == null) continue;
                if (Application.isPlaying) Destroy(_glows[i]);
                else DestroyImmediate(_glows[i]);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using GardenVR.Core;
using GardenVR.Input;
using UnityEngine;
using UnityEngine.Rendering;

namespace GardenVR.Terrarium
{
    /// <summary>
    /// Six word stones on the moss lip, by the glass. After today's ritual a pinch sinks one
    /// into the moss and that word is kept. The others ease away. Nothing here is a score.
    /// </summary>
    public sealed class OneWordRitual : MonoBehaviour
    {
        public const string StonePrefix = "word.stone.";
        public const string LineOffer = "One word for today.";
        public const float SettleSeconds = 1.6f;
        public const int KeptPose = 4;
        // Above the voice caption (y 0.055), so "quiet" does not sit on "Breathe in."
        public static readonly Vector3 LookPos = new Vector3(0f, 0.078f, -0.090f);

        const string RootName = "WordStones";
        const string PebblePath = "Assets/Art/Models/Pebbles.fbx";
        const float StoneMetres = 0.015f;

        static readonly List<Material> CaptureMats = new List<Material>();
        static Mesh[] _meshes;
        static bool _loggedMesh;

        readonly OneWord _session = new OneWord();
        readonly Transform[] _stones = new Transform[OneWord.Count];
        readonly Transform[] _labels = new Transform[OneWord.Count];
        readonly Material[] _mats = new Material[OneWord.Count];
        readonly Vector3[] _rest = new Vector3[OneWord.Count];
        readonly Vector3[] _moss = new Vector3[OneWord.Count];
        readonly float[] _fit = new float[OneWord.Count];
        readonly Vector3[] _centers = new Vector3[OneWord.Count];

        JarView _view;
        GameObject _prompt;
        GameObject _look;
        string _shown;
        string _lookShown;
        float _settle = -1f;
        bool _settled;
        bool _offering;
        bool _hidden = true;
        int _keptDay = int.MinValue;

        public OneWord Session { get { return _session; } }
        public bool Built { get; private set; }
        public bool Offered { get { return _offering && !_session.Kept; } }
        public bool Settled { get { return _settled; } }
        public int Index { get { return _session.Picked.HasValue ? _session.Picked.Value : -1; } }
        public string Line { get { return _shown; } }
        public string LookLine { get { return _lookShown; } }

        public static string StoneId(int index)
        {
            return StonePrefix + index.ToString(CultureInfo.InvariantCulture);
        }

        public static bool TryStoneIndex(string targetId, out int stone)
        {
            stone = -1;
            if (string.IsNullOrEmpty(targetId) || !targetId.StartsWith(StonePrefix, StringComparison.Ordinal))
                return false;
            string tail = targetId.Substring(StonePrefix.Length);
            if (tail.Length != 1 || tail[0] < '0' || tail[0] > '5') return false;
            stone = tail[0] - '0';
            return true;
        }

        public static Vector3 RestLocal(int index)
        {
            float t = OneWord.Count <= 1 ? 0.5f : index / (float)(OneWord.Count - 1);
            float deg = Mathf.Lerp(-78f, 78f, t);
            float rad = deg * Mathf.Deg2Rad;
            const float radius = 0.046f;
            return new Vector3(Mathf.Sin(rad) * radius, JarView.MossBedY + 0.007f, -Mathf.Cos(rad) * radius);
        }

        public static Vector3 MossLocal(int index)
        {
            Vector3 rest = RestLocal(index);
            return new Vector3(rest.x * 0.62f, JarView.MossBedY - 0.005f, rest.z * 0.62f);
        }

        public static Vector3 LabelLocal(Vector3 stone)
        {
            return stone + new Vector3(0f, 0.012f, -0.012f);
        }

        public bool Owns(string targetId)
        {
            int stone;
            return TryStoneIndex(targetId, out stone);
        }

        public void Bind(JarView view)
        {
            _view = view;
            if (Built || view == null) return;
            Transform root = NewRoot(view.transform);
            Material source = SourceMaterial(view);
            Mesh[] meshes = PebbleMeshes();
            for (int i = 0; i < OneWord.Count; i++)
            {
                _rest[i] = RestLocal(i);
                _moss[i] = MossLocal(i);
                Mesh mesh = meshes[i % meshes.Length];
                _fit[i] = FitScale(mesh);
                _centers[i] = mesh != null ? mesh.bounds.center : Vector3.zero;
                _stones[i] = MakeStone(root, i, mesh, source, _mats);
                _labels[i] = MakeLabel(root, i, OneWord.Stones[i], _rest[i]);
                Place(i, _rest[i], _fit[i]);
                SetVisible(i, false);
            }
            Built = true;
        }

        public Transform StoneTransform(int stone)
        {
            if (stone < 0 || stone >= _stones.Length) return null;
            return _stones[stone];
        }

        public float StoneLocalY(int stone)
        {
            if (stone < 0 || stone >= _stones.Length || _stones[stone] == null) return 0f;
            Transform stoneTransform = _stones[stone];
            Vector3 center = stoneTransform.localRotation * Vector3.Scale(_centers[stone], stoneTransform.localScale);
            return (stoneTransform.localPosition + center).y;
        }

        public string Label(int stone)
        {
            if (stone < 0 || stone >= _labels.Length || _labels[stone] == null) return null;
            return EtchedLettering.Read(_labels[stone].gameObject);
        }

        /// <summary>True when this pinch chose a new stone. The caller keeps it, then <see cref="BeginSink"/>.</summary>
        public bool TryPinch(string targetId, out int stone)
        {
            stone = -1;
            if (!Built || !_offering || _session.Kept) return false;
            if (!TryStoneIndex(targetId, out stone)) return false;
            return _session.TryPick(stone);
        }

        public void CancelPick()
        {
            _session.ClearPick();
        }

        public void BeginSink(int today)
        {
            if (!Built || !_session.Kept) return;
            _keptDay = today;
            _offering = false;
            _hidden = false;
            _settled = false;
            _settle = 0f;
            int chosen = _session.Picked.Value;
            for (int i = 0; i < OneWord.Count; i++)
                SetVisible(i, true);
            Place(chosen, _rest[chosen], _fit[chosen]);
        }

        public void ShowLook(string word)
        {
            // The voice line sits on the moss. Hide the small stone word until the replay ends.
            for (int i = 0; i < OneWord.Count; i++)
            {
                if (_labels[i] != null && _labels[i].gameObject.activeSelf)
                    _labels[i].gameObject.SetActive(false);
            }
            if (word == _lookShown && (_look == null || _look.activeSelf == !string.IsNullOrEmpty(word))) return;
            _lookShown = word;
            if (_view == null) return;
            if (string.IsNullOrEmpty(word))
            {
                if (_look != null) _look.SetActive(false);
                return;
            }
            if (_look == null)
            {
                var tmp = EtchedLettering.Place(_view.transform, "LookWord", word, LookPos, Quaternion.identity, 0.09f, 0.012f);
                _look = tmp != null ? tmp.gameObject : null;
                return;
            }
            _look.SetActive(true);
            EtchedLettering.SetText(_look, word, 0.09f, 0.012f);
        }

        public void ClearLook()
        {
            _lookShown = null;
            if (_look != null) _look.SetActive(false);
            if (!_session.Kept || !Built) return;
            int chosen = _session.Picked.Value;
            if (_labels[chosen] != null) _labels[chosen].gameObject.SetActive(true);
        }

        public void Sync(IList<KeptWord> words, Garden garden, int today, bool open)
        {
            if (!Built) return;
            string word = OneWord.On(words, today);
            bool ritualToday = garden != null && garden.LastRitualDay == today;
            if (word != null)
            {
                if (!_session.Kept) ShowRestored(word, today);
                return;
            }
            if (_keptDay != int.MinValue && _keptDay != today)
                Clear();
            if (open && ritualToday) ShowOffer();
            else if (!_session.Kept) HideOffer();
        }

        public void Tick(float dt, bool open, bool reducedMotion)
        {
            if (dt < 0f) dt = 0f;
            if (_settle >= 0f && !_settled)
            {
                if (reducedMotion) _settle = SettleSeconds;
                else _settle += dt;
                float u = Mathf.Clamp01(_settle / SettleSeconds);
                float eased = u * u * (3f - 2f * u);
                int chosen = _session.Picked.HasValue ? _session.Picked.Value : -1;
                for (int i = 0; i < OneWord.Count; i++)
                {
                    if (_stones[i] == null) continue;
                    if (i == chosen)
                    {
                        Vector3 at = Vector3.Lerp(_rest[i], _moss[i], eased);
                        Place(i, at, _fit[i]);
                        continue;
                    }
                    float scale = _fit[i] * (1f - eased);
                    Place(i, _rest[i], scale);
                    if (u >= 1f) SetVisible(i, false);
                }
                if (u >= 1f) _settled = true;
            }
            PresentLine(ChooseLine(open));
        }

        /// <summary>
        /// A still for the locked plate. 1 is the six stones, 2 is "quiet" already in the moss,
        /// 3 is that sunk stone plus the look-back word.
        /// </summary>
        public static void PresentCapture(Transform jar, JarView view, int pose)
        {
            ClearCapture(jar);
            if (jar == null || view == null || pose <= 0) return;
            Transform root = NewRoot(jar);
            Material source = SourceMaterial(view);
            Mesh[] meshes = PebbleMeshes();
            int chosen = pose >= 2 ? KeptPose : -1;
            for (int i = 0; i < OneWord.Count; i++)
            {
                if (chosen >= 0 && i != chosen) continue;
                Mesh mesh = meshes[i % meshes.Length];
                float fit = FitScale(mesh);
                Vector3 at = chosen >= 0 ? MossLocal(i) : RestLocal(i);
                Transform stone = MakeStone(root, i, mesh, source, null);
                Vector3 center = mesh != null ? mesh.bounds.center : Vector3.zero;
                stone.localScale = Vector3.one * fit;
                stone.localPosition = at - stone.localRotation * (center * fit);
                MakeLabel(root, i, OneWord.Stones[i], at);
            }
            if (pose >= 3)
            {
                EtchedLettering.Place(jar, "LookWord", OneWord.Stones[KeptPose], LookPos, Quaternion.identity, 0.09f, 0.012f);
            }
        }

        public static void ClearCapture(Transform jar)
        {
            if (jar == null) return;
            Transform root = jar.Find(RootName);
            if (root != null) DestroyObject(root.gameObject);
            Transform look = jar.Find("LookWord");
            if (look != null) DestroyObject(look.gameObject);
            for (int i = 0; i < CaptureMats.Count; i++) DestroyObject(CaptureMats[i]);
            CaptureMats.Clear();
        }

        string ChooseLine(bool open)
        {
            if (!Built) return null;
            if (_settle >= 0f && !_settled) return _session.Word;
            if (!open || _session.Kept || _settled) return null;
            return LineOffer;
        }

        void ShowOffer()
        {
            if (_offering) return;
            _offering = true;
            _hidden = false;
            _settled = false;
            _settle = -1f;
            for (int i = 0; i < OneWord.Count; i++)
            {
                SetVisible(i, true);
                Place(i, _rest[i], _fit[i]);
            }
        }

        void HideOffer()
        {
            if (_hidden || _session.Kept) return;
            _offering = false;
            _hidden = true;
            for (int i = 0; i < OneWord.Count; i++) SetVisible(i, false);
        }

        void ShowRestored(string word, int today)
        {
            _session.Restore(word);
            if (!_session.Kept) return;
            _keptDay = today;
            _offering = false;
            _hidden = false;
            _settled = true;
            _settle = SettleSeconds;
            int chosen = _session.Picked.Value;
            for (int i = 0; i < OneWord.Count; i++)
            {
                bool show = i == chosen;
                SetVisible(i, show);
                if (show) Place(i, _moss[i], _fit[i]);
            }
        }

        void Clear()
        {
            _session.Reset();
            _keptDay = int.MinValue;
            _settled = false;
            _settle = -1f;
            _offering = false;
            _hidden = true;
            for (int i = 0; i < OneWord.Count; i++) SetVisible(i, false);
        }

        void Place(int index, Vector3 local, float scale)
        {
            if (_stones[index] != null)
            {
                float used = Mathf.Max(0f, scale);
                _stones[index].localScale = Vector3.one * used;
                Vector3 offset = _stones[index].localRotation * (_centers[index] * used);
                _stones[index].localPosition = local - offset;
            }
            if (_labels[index] != null)
                _labels[index].localPosition = LabelLocal(local);
        }

        void SetVisible(int index, bool on)
        {
            if (_stones[index] != null && _stones[index].gameObject.activeSelf != on)
                _stones[index].gameObject.SetActive(on);
            if (_labels[index] != null && _labels[index].gameObject.activeSelf != on)
                _labels[index].gameObject.SetActive(on);
        }

        void PresentLine(string line)
        {
            if (line == _shown) return;
            _shown = line;
            if (_view == null) return;
            if (string.IsNullOrEmpty(line))
            {
                if (_prompt != null) _prompt.SetActive(false);
                return;
            }
            if (_prompt == null)
            {
                var tmp = EtchedLettering.Place(
                    _view.transform, "WordPrompt", line,
                    new Vector3(0f, 0.000f, -0.082f), Quaternion.identity, 0.12f, 0.0065f);
                _prompt = tmp != null ? tmp.gameObject : null;
                return;
            }
            _prompt.SetActive(true);
            EtchedLettering.SetText(_prompt, line, 0.12f, 0.0065f);
        }

        static Transform NewRoot(Transform parent)
        {
            var root = new GameObject(RootName);
            root.transform.SetParent(parent, false);
            return root.transform;
        }

        static Transform MakeStone(Transform root, int index, Mesh mesh, Material source, Material[] keep)
        {
            var stone = new GameObject("WordStone" + index.ToString(CultureInfo.InvariantCulture));
            stone.transform.SetParent(root, false);
            stone.transform.localRotation = StoneRotation(mesh, index);
            var filter = stone.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = stone.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            Material mat = source != null ? new Material(source) { name = "WordStone" + index.ToString(CultureInfo.InvariantCulture) } : null;
            if (mat != null)
            {
                TintStone(mat, index);
                renderer.sharedMaterial = mat;
                if (keep != null) keep[index] = mat;
                else CaptureMats.Add(mat);
            }
            var box = stone.AddComponent<BoxCollider>();
            if (mesh != null)
            {
                box.center = mesh.bounds.center;
                box.size = mesh.bounds.size;
            }
            var target = stone.AddComponent<IntentTarget>();
            target.Id = StoneId(index);
            return stone.transform;
        }

        static Transform MakeLabel(Transform root, int index, string word, Vector3 stoneLocal)
        {
            var tmp = EtchedLettering.Place(root, "WordLabel" + index.ToString(CultureInfo.InvariantCulture), word, LabelLocal(stoneLocal), Quaternion.identity, 0.036f, 0.006f);
            return tmp != null ? tmp.transform : null;
        }

        static void TintStone(Material mat, int index)
        {
            // The soil photo is a dark carpet. A stone wants a warm face, not that carpet.
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", Texture2D.whiteTexture);
            if (mat.HasProperty("_TopTex")) mat.SetTexture("_TopTex", Texture2D.whiteTexture);
            if (mat.HasProperty("_EmissionTex")) mat.SetTexture("_EmissionTex", Texture2D.whiteTexture);
            if (mat.HasProperty("_TopAmount")) mat.SetFloat("_TopAmount", 0f);
            float t = OneWord.Count <= 1 ? 0f : index / (float)(OneWord.Count - 1);
            if (mat.HasProperty("_Tint"))
            {
                Color tint = Color.Lerp(new Color(0.62f, 0.50f, 0.40f, 1f), new Color(0.46f, 0.50f, 0.46f, 1f), t);
                mat.SetColor("_Tint", tint);
            }
            if (mat.HasProperty("_Emission"))
                mat.SetColor("_Emission", new Color(0.11f, 0.09f, 0.06f, 1f));
            if (mat.HasProperty("_Rim"))
                mat.SetColor("_Rim", new Color(0.34f, 0.26f, 0.16f, 1f));
            if (mat.HasProperty("_RimPower")) mat.SetFloat("_RimPower", 1.5f);
            if (mat.HasProperty("_Tri")) mat.SetFloat("_Tri", 0f);
            // The soil gradient is a few centimetres tall. A stone needs its own short window, or the face stays flat.
            if (mat.HasProperty("_GradBottom")) mat.SetFloat("_GradBottom", 0.40f);
            if (mat.HasProperty("_GradTop")) mat.SetFloat("_GradTop", 1.12f);
            if (mat.HasProperty("_GradY"))
                mat.SetVector("_GradY", new Vector4(JarView.MossBedY - 0.006f, JarView.MossBedY + 0.018f, 0f, 0f));
            if (mat.HasProperty("_LightPos"))
                mat.SetVector("_LightPos", new Vector4(0f, 0.16f, -0.10f, 0.35f));
            if (mat.HasProperty("_LightColor"))
                mat.SetColor("_LightColor", new Color(0.90f, 0.68f, 0.40f, 1f));
        }

        /// <summary>Lay the pebble's thin axis on the moss, then yaw it so the six are not copies.</summary>
        static Quaternion StoneRotation(Mesh mesh, int index)
        {
            Vector3 size = mesh != null ? mesh.bounds.size : Vector3.one;
            int thin = 0;
            if (size.y < size[thin]) thin = 1;
            if (size.z < size[thin]) thin = 2;
            Vector3 axis = thin == 0 ? Vector3.right : thin == 1 ? Vector3.up : Vector3.forward;
            return Quaternion.Euler(0f, index * 47f, 0f) * Quaternion.FromToRotation(axis, Vector3.up);
        }

        static Material SourceMaterial(JarView view)
        {
            if (view == null) return null;
            if (view.soilMat != null) return view.soilMat;
            return SharedOn(view.transform, "Soil") ?? SharedOn(view.transform, "Cork");
        }

        static Material SharedOn(Transform root, string name)
        {
            Transform found = FindNamed(root, name);
            Renderer renderer = found != null ? found.GetComponent<Renderer>() : null;
            return renderer != null ? renderer.sharedMaterial : null;
        }

        static Transform FindNamed(Transform root, string name)
        {
            if (root == null) return null;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == name) return all[i];
            }
            return null;
        }

        static float FitScale(Mesh mesh)
        {
            if (mesh == null) return 1f;
            Vector3 size = mesh.bounds.size;
            float longest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            if (longest < 0.00001f) return 1f;
            return StoneMetres / longest;
        }

        static Mesh[] PebbleMeshes()
        {
            if (_meshes != null && _meshes.Length > 0) return _meshes;
            Mesh[] loaded = LoadFbxMeshes();
            if (loaded != null && loaded.Length > 0)
            {
                _meshes = loaded;
                if (!_loggedMesh)
                {
                    _loggedMesh = true;
                    Debug.Log("[OneWord] pebble meshes " + loaded.Length.ToString(CultureInfo.InvariantCulture) + " from " + PebblePath);
                }
                return _meshes;
            }
            _meshes = new[] { RiverStone() };
            if (!_loggedMesh)
            {
                _loggedMesh = true;
                Debug.Log("[OneWord] pebble mesh fallback river stone");
            }
            return _meshes;
        }

        static Mesh[] LoadFbxMeshes()
        {
            Type db = Type.GetType("UnityEditor.AssetDatabase, UnityEditor");
            if (db == null) return null;
            MethodInfo[] methods = db.GetMethods(BindingFlags.Public | BindingFlags.Static);
            GameObject model = null;
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method.Name != "LoadAssetAtPath" || !method.IsGenericMethodDefinition) continue;
                MethodInfo closed = method.MakeGenericMethod(typeof(GameObject));
                model = closed.Invoke(null, new object[] { PebblePath }) as GameObject;
                break;
            }
            if (model == null) return null;
            MeshFilter[] filters = model.GetComponentsInChildren<MeshFilter>(true);
            var found = new List<Mesh>();
            for (int i = 0; i < filters.Length; i++)
            {
                Mesh mesh = filters[i] != null ? filters[i].sharedMesh : null;
                if (mesh == null || mesh.vertexCount < 4) continue;
                if (found.Contains(mesh)) continue;
                found.Add(mesh);
                Debug.Log("[OneWord] mesh " + mesh.name
                    + " verts " + mesh.vertexCount.ToString(CultureInfo.InvariantCulture)
                    + " size " + mesh.bounds.size.ToString("F3")
                    + " center " + mesh.bounds.center.ToString("F3"));
            }
            return found.Count > 0 ? found.ToArray() : null;
        }

        static Mesh RiverStone()
        {
            const int slices = 10;
            const int stacks = 6;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int stack = 0; stack <= stacks; stack++)
            {
                float v = stack / (float)stacks;
                float phi = v * Mathf.PI;
                float y = Mathf.Cos(phi) * 0.42f;
                float ring = Mathf.Sin(phi);
                for (int slice = 0; slice < slices; slice++)
                {
                    float u = slice / (float)slices;
                    float theta = u * Mathf.PI * 2f;
                    float wobble = 1f + 0.08f * Mathf.Sin(theta * 2f + phi);
                    vertices.Add(new Vector3(Mathf.Cos(theta) * ring * 0.62f * wobble, y, Mathf.Sin(theta) * ring * 0.48f * wobble));
                }
            }
            for (int stack = 0; stack < stacks; stack++)
            {
                for (int slice = 0; slice < slices; slice++)
                {
                    int a = stack * slices + slice;
                    int b = stack * slices + (slice + 1) % slices;
                    int c = a + slices;
                    int d = b + slices;
                    triangles.Add(a);
                    triangles.Add(c);
                    triangles.Add(b);
                    triangles.Add(b);
                    triangles.Add(c);
                    triangles.Add(d);
                }
            }
            var mesh = new Mesh { name = "WordRiverStone" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static void DestroyObject(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(obj);
            else UnityEngine.Object.DestroyImmediate(obj);
        }

        void OnDestroy()
        {
            for (int i = 0; i < _mats.Length; i++) DestroyObject(_mats[i]);
        }
    }
}

using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GardenVR.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace GardenVR.Terrarium
{
    /// <summary>One sprig in the moss. Tests find it by the object name Companion-&lt;preset&gt;.</summary>
    public sealed class CompanionSprig : MonoBehaviour
    {
        public string Preset;
        public CompanionSpecies Species;
        public int Leaves;
    }

    /// <summary>
    /// Sprigs for the live garden and for batch capture. One combined mesh per sprig,
    /// so three companions add three draws. Capture adds one etched name each.
    /// </summary>
    public sealed class CompanionGarden
    {
        public sealed class Shot
        {
            public string Preset;
            public CompanionSpecies Species;
            public int Leaves;
            public float Vitality;
            public bool Etch;
        }

        const float SprigHeight = 0.046f;
        const float LeafW = 0.012f;
        const float LeafH = 0.008f;

        static readonly Color Mint = new Color(0x8F / 255f, 0xF0 / 255f, 0xC8 / 255f, 1f);
        static readonly Color Moon = new Color(0xDD / 255f, 0xF3 / 255f, 0xFF / 255f, 1f);
        static readonly Color Gold = new Color(0xF2 / 255f, 0xD2 / 255f, 0x7A / 255f, 1f);

        static Mesh _leaf;
        static Mesh _card;
        string _signature = "";

        public static List<Shot> CaptureShots(int companions, int leaves)
        {
            var list = new List<Shot>();
            int count = Mathf.Clamp(companions, 0, Companions.MaxHabits);
            int leafCount = Mathf.Max(0, leaves);
            for (int i = 0; i < count; i++)
            {
                string preset = Companions.PresetKeys[i];
                list.Add(new Shot
                {
                    Preset = preset,
                    Species = Companions.SpeciesFor(preset),
                    Leaves = leafCount,
                    Vitality = 1f,
                    Etch = true
                });
            }
            return list;
        }

        public void Show(Transform parent, IReadOnlyList<Shot> shots)
        {
            if (parent == null) return;
            string next = Signature(shots);
            if (next == _signature && parent.Find("Companions") != null) return;
            _signature = next;
            Clear(parent);
            if (shots == null || shots.Count == 0) return;

            var root = new GameObject("Companions");
            root.transform.SetParent(parent, false);
            Mesh body = LoadBody();
            Shader shader = Shader.Find("Fidelity/Glow");
            if (shader == null) shader = Shader.Find("Unlit/Transparent Cutout");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            for (int i = 0; i < shots.Count; i++)
            {
                Shot shot = shots[i];
                if (shot == null || string.IsNullOrEmpty(shot.Preset)) continue;
                BuildSprig(root.transform, body, shader, shot, i, shots.Count);
                if (shot.Etch) BuildEtch(root.transform, shot, i, shots.Count);
            }
        }

        static void BuildSprig(Transform root, Mesh body, Shader shader, Shot shot, int slot, int count)
        {
            var go = new GameObject("Companion-" + shot.Preset);
            go.transform.SetParent(root, false);
            var sprig = go.AddComponent<CompanionSprig>();
            sprig.Preset = shot.Preset;
            sprig.Species = shot.Species;
            sprig.Leaves = shot.Leaves;

            Vector3 right, up, normal;
            Quaternion facing = Facing(body, out right, out up, out normal);
            float minY, maxY;
            Extents(body, facing, out minY, out maxY);
            float height = Mathf.Max(0.001f, maxY - minY);
            float scale = SprigHeight / height;
            float x = count <= 1 ? 0f : (slot - (count - 1) * 0.5f) * 0.020f;
            // In front of the day-7 frond fan (front root is z=-0.014) and inside the glass.
            float z = -0.020f + (slot - (count - 1) * 0.5f) * 0.002f;
            go.transform.localRotation = facing;
            go.transform.localScale = new Vector3(scale, scale, scale);
            go.transform.localPosition = new Vector3(x, JarView.MossBedY - minY * scale, z);

            var filter = go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            filter.sharedMesh = shot.Leaves > 0 ? Combine(body, right, up, normal, shot.Leaves) : body;
            renderer.sharedMaterial = MaterialFor(shader, shot);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static void BuildEtch(Transform root, Shot shot, int slot, int count)
        {
            float x = count <= 1 ? 0f : (slot - (count - 1) * 0.5f) * 0.09f;
            // The etched face reads from the -Z camera with no yaw.
            EtchedLettering.Place(root, "Etch-" + shot.Preset, Companions.Label(shot.Preset),
                new Vector3(x, 0.032f, -0.118f), Quaternion.identity, 0.05f, 0.008f);
        }

        static Mesh Combine(Mesh body, Vector3 right, Vector3 up, Vector3 normal, int leaves)
        {
            var parts = new CombineInstance[leaves + 1];
            parts[0].mesh = body;
            parts[0].transform = Matrix4x4.identity;
            Mesh leaf = Leaf();
            Vector3 size = body.bounds.size;
            float across = Mathf.Abs(Vector3.Dot(new Vector3(size.x, size.y, size.z), new Vector3(Mathf.Abs(right.x), Mathf.Abs(right.y), Mathf.Abs(right.z))));
            float along = Mathf.Abs(Vector3.Dot(size, new Vector3(Mathf.Abs(up.x), Mathf.Abs(up.y), Mathf.Abs(up.z))));
            if (across < 0.001f) across = 0.02f;
            if (along < 0.001f) along = 0.03f;
            for (int i = 0; i < leaves; i++)
            {
                float u = leaves == 1 ? 0.5f : (i + 0.5f) / leaves;
                float v = 0.32f + 0.56f * Mathf.Repeat(i * 0.618034f, 1f);
                Vector3 pos = body.bounds.center
                    + right * ((u - 0.5f) * across * 0.72f)
                    + up * ((v - 0.5f) * along * 0.62f)
                    + normal * (0.0016f * (i % 2 == 0 ? 1f : -1f));
                float spin = (i - (leaves - 1) * 0.5f) * 16f;
                Quaternion rot = Quaternion.LookRotation(normal, up) * Quaternion.Euler(0f, 0f, spin);
                parts[i + 1].mesh = leaf;
                parts[i + 1].transform = Matrix4x4.TRS(pos, rot, Vector3.one);
            }
            var combined = new Mesh();
            combined.name = "SprigLive";
            combined.CombineMeshes(parts, true, true);
            return combined;
        }

        static Material MaterialFor(Shader shader, Shot shot)
        {
            Color tint = Tint(shot.Species);
            Texture2D tex = Resources.Load<Texture2D>(TexturePath(shot.Species));
            var mat = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
            mat.name = "SprigMat";
            if (tex != null)
            {
                mat.SetTexture("_MainTex", tex);
                mat.SetTexture("_EmissionTex", tex);
            }
            else
            {
                mat.SetTexture("_MainTex", Texture2D.whiteTexture);
                mat.SetTexture("_EmissionTex", Texture2D.whiteTexture);
            }
            float life = Mathf.Clamp(shot.Vitality, Garden.VitalityFloor, 1f);
            // Runtime cards are graded so the dark paper reads inside the glass.
            // A hard cutoff keeps the glow halo from dithering into specks.
            mat.SetColor("_Tint", tex != null ? new Color(1.22f, 1.22f, 1.22f, 1f) : tint);
            mat.SetFloat("_GradBottom", 1.12f);
            mat.SetFloat("_GradTop", 1.32f);
            mat.SetVector("_GradY", new Vector4(0.028f, 0.082f, 0f, 0f));
            mat.SetColor("_Emission", tint * (0.7f * life));
            mat.SetColor("_Rim", tint * 0.28f);
            mat.SetFloat("_RimPower", 2.2f);
            mat.SetFloat("_Cutoff", 0.1f);
            mat.SetFloat("_Soft", 0f);
            return mat;
        }

        static Mesh LoadBody()
        {
            Mesh[] meshes = Resources.LoadAll<Mesh>("Companions/sprig");
            Mesh best = null;
            if (meshes != null)
            {
                for (int i = 0; i < meshes.Length; i++)
                {
                    Mesh mesh = meshes[i];
                    if (mesh == null || !mesh.isReadable) continue;
                    if (mesh.name == "Sprig") return mesh;
                    if (best == null) best = mesh;
                }
            }
            return best != null ? best : Card();
        }

        static Mesh Card()
        {
            if (_card != null) return _card;
            _card = new Mesh();
            _card.name = "SprigCard";
            _card.vertices = new[]
            {
                new Vector3(-0.013f, 0f, 0f),
                new Vector3(0.013f, 0f, 0f),
                new Vector3(0.013f, 0.046f, 0f),
                new Vector3(-0.013f, 0.046f, 0f)
            };
            _card.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            };
            _card.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            _card.RecalculateNormals();
            _card.RecalculateBounds();
            return _card;
        }

        static Mesh Leaf()
        {
            if (_leaf != null) return _leaf;
            _leaf = new Mesh();
            _leaf.name = "SprigLeaf";
            _leaf.vertices = new[]
            {
                new Vector3(-LeafW * 0.5f, 0f, 0f),
                new Vector3(LeafW * 0.5f, 0f, 0f),
                new Vector3(LeafW * 0.5f, LeafH, 0f),
                new Vector3(-LeafW * 0.5f, LeafH, 0f)
            };
            _leaf.uv = new[]
            {
                new Vector2(0.30f, 0.55f),
                new Vector2(0.70f, 0.55f),
                new Vector2(0.70f, 0.95f),
                new Vector2(0.30f, 0.95f)
            };
            _leaf.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            _leaf.RecalculateNormals();
            _leaf.RecalculateBounds();
            return _leaf;
        }

        static void Clear(Transform parent)
        {
            Transform existing = parent.Find("Companions");
            if (existing == null) return;
            MeshFilter[] filters = existing.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                Mesh mesh = filters[i].sharedMesh;
                if (mesh != null && mesh.name == "SprigLive") Object.DestroyImmediate(mesh);
            }
            Renderer[] renderers = existing.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Material mat = renderers[i].sharedMaterial;
                if (mat != null && mat.name == "SprigMat") Object.DestroyImmediate(mat);
            }
            Object.DestroyImmediate(existing.gameObject);
        }

        static string Signature(IReadOnlyList<Shot> shots)
        {
            if (shots == null || shots.Count == 0) return "";
            var sb = new StringBuilder();
            for (int i = 0; i < shots.Count; i++)
            {
                Shot shot = shots[i];
                if (shot == null) continue;
                sb.Append(shot.Preset).Append(':');
                sb.Append(shot.Leaves).Append(':');
                sb.Append(shot.Vitality.ToString("0.00", CultureInfo.InvariantCulture)).Append(':');
                sb.Append(shot.Etch ? 'e' : 'p').Append(';');
            }
            return sb.ToString();
        }

        static Quaternion Facing(Mesh mesh, out Vector3 right, out Vector3 up, out Vector3 normal)
        {
            Vector3 size = mesh.bounds.size;
            int thin = IndexOfMin(size);
            int tall = IndexOfMax(size);
            if (thin == tall)
            {
                right = Vector3.right;
                up = Vector3.up;
                normal = Vector3.forward;
                return Quaternion.Euler(0f, 180f, 0f);
            }
            int wide = 3 - thin - tall;
            Vector3[] axis = { Vector3.right, Vector3.up, Vector3.forward };
            normal = axis[thin];
            up = axis[tall];
            right = Vector3.Cross(up, normal);
            if (Vector3.Dot(right, axis[wide]) < 0f) right = -right;
            Quaternion toUp = Quaternion.FromToRotation(up, Vector3.up);
            Vector3 turned = toUp * normal;
            if (turned.sqrMagnitude < 1e-8f) return Quaternion.Euler(0f, 180f, 0f);
            return Quaternion.FromToRotation(turned, Vector3.back) * toUp;
        }

        static void Extents(Mesh mesh, Quaternion facing, out float minY, out float maxY)
        {
            Vector3 bmin = mesh.bounds.min;
            Vector3 bmax = mesh.bounds.max;
            minY = float.PositiveInfinity;
            maxY = float.NegativeInfinity;
            for (int xi = 0; xi < 2; xi++)
            {
                for (int yi = 0; yi < 2; yi++)
                {
                    for (int zi = 0; zi < 2; zi++)
                    {
                        Vector3 corner = new Vector3(
                            xi == 0 ? bmin.x : bmax.x,
                            yi == 0 ? bmin.y : bmax.y,
                            zi == 0 ? bmin.z : bmax.z);
                        float y = (facing * corner).y;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }
        }

        static int IndexOfMin(Vector3 size)
        {
            int index = 0;
            if (size.y < size.x) index = 1;
            if (size.z < Axis(size, index)) index = 2;
            return index;
        }

        static int IndexOfMax(Vector3 size)
        {
            int index = 0;
            if (size.y > size.x) index = 1;
            if (size.z > Axis(size, index)) index = 2;
            return index;
        }

        static float Axis(Vector3 size, int index)
        {
            if (index == 0) return size.x;
            if (index == 1) return size.y;
            return size.z;
        }

        static Color Tint(CompanionSpecies species)
        {
            if (species == CompanionSpecies.MoonMoss) return Moon;
            if (species == CompanionSpecies.StarFern) return Gold;
            return Mint;
        }

        static string TexturePath(CompanionSpecies species)
        {
            if (species == CompanionSpecies.MoonMoss) return "Companions/sprig_moon";
            if (species == CompanionSpecies.StarFern) return "Companions/sprig_gold";
            return "Companions/sprig_glow";
        }
    }
}

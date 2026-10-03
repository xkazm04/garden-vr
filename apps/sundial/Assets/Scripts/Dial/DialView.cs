using System;
using System.Collections.Generic;
using System.Globalization;
using GardenVR.Capture;
using GardenVR.Input;
using UnityEngine;
using UnityEngine.Rendering;

namespace GardenVR.Sundial
{
    /// <summary>
    /// Materials and the card mesh <see cref="DialView.Build"/> hangs on the drawn-dial FBX.
    /// DialSetup saves these as assets so the prefab keeps them.
    /// </summary>
    public sealed class DialLibrary
    {
        public Material Face, Rim, Soil, Gnomon, Tiles, Catcher, Shadow, Halo, Pool, Contact;
        public Material Morning, Midday, WindDown, Bloom;
        public Texture2D[] MorningCards, MiddayCards, WindDownCards;
        public Texture2D[] BloomCards;
        public Texture2D[] Halos;
        public Mesh Card, Cross, ShadowMesh, CatcherQuad, PoolMesh;
    }

    /// <summary>
    /// The drawn dial on the desk. Look comes only from state: halo, which arc it sits on,
    /// gnomon angle, plant stage and bloom, the 21 tiles, boil, and time.
    /// Each species has five cards (seed, sprout, young, leafy, full). Bloom is an overlay.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class DialView : MonoBehaviour, ICaptureState
    {
        public const int TilesPerArc = 7;
        public const int TileCount = 21;
        public const float OutlinePixels = 3f;
        public const float BoilPixels = 1f;
        public const float BloomCloseSeconds = 1.2f;

        // SVG dial face, degrees. 0 is +X (image right), 90 is +Z (image top, far side).
        public const float MorningArc0 = 242f;
        public const float MorningArc1 = 112f;
        public const float MiddayArc0 = 106f;
        public const float MiddayArc1 = 22f;
        public const float WindDownArc0 = 16f;
        public const float WindDownArc1 = -100f;

        [Header("State")]
        [Range(0f, 1f)] public float halo = 1f;
        public string haloTarget = "midday";
        public float gnomonDeg = 105f;
        public int stageMorning = 4;
        public int stageMidday = 4;
        public int stageWinddown = 4;
        public float bloomMorning;
        public float bloomMidday = 2f;
        public float bloomWinddown = 2f;
        public bool stageStrip;
        public int[] tiles =
        {
            1, 1, 3, 1, 2, 1, 1,
            1, 2, 3, 1, 1, 3, 4,
            3, 1, 1, 2, 1, 3, 4
        };
        /// <summary>Ink flood per tile, 0 at the nib to 1 full. Null means every tile is already full.</summary>
        public float[] tileFill;
        public bool boil = true;
        public float time = 1f;
        /// <summary>0 is blank paper, 1 is the finished drawing. The player default is already drawn.</summary>
        [Range(0f, 1f)] public float appear = 1f;
        /// <summary>False hides the three plant cards. The first run turns this on when the seeds land.</summary>
        public bool showPlants = true;
        [Range(0f, 1f)] public float waiting;
        public string waitingTarget = "midday";
        [Range(0f, 1f)] public float pulse;
        public int pulseArc = 1;
        public bool reducedMotion;

        [Header("Wired by Build")]
        public float faceY = 0.012f;
        public float faceRadius = 0.1472f;
        public Renderer haloRenderer;
        public Renderer poolRenderer;
        public Renderer contactRenderer;
        public Transform shadow;
        public Transform[] uprightCards;
        public Material[] boilMats;
        public Material[] plantMats;
        public Texture2D[] morningCards;
        public Texture2D[] middayCards;
        public Texture2D[] windDownCards;
        public Texture2D[] haloTextures;
        public Texture2D[] bloomTextures;
        public Renderer[] bloomRenderers;
        public Renderer[] stripRenderers;
        public MeshRenderer tileRenderer;

        int _haloArc = 1;
        readonly Dictionary<string, Texture2D> _haloMasks = new Dictionary<string, Texture2D>();

        Texture2D _stateTex;
        bool _hooked;
        const float TileRadius = 0.82f;
        readonly float[] _bloomVisual = { -1f, -1f, -1f };
        readonly float[] _bloomClose = { -1f, -1f, -1f };
        readonly float[] _bloomFold = { 1f, 1f, 1f };
        float _bloomClock = -1f;
        float _bloomDt;

        static readonly string[] ArcIds = { "morning", "midday", "winddown" };
        // Card spots read off the owner's frame, in units of the face radius. x is right, z is away.
        // In units of the face radius. Bases sit in the soil, foliage reaches the wash.
        static readonly Vector2[] PlantSpot =
        {
            new Vector2(-0.30f, -0.08f),
            new Vector2(0.16f, 0.18f),
            new Vector2(0.26f, -0.16f)
        };
        static readonly Vector2[] PlantSize =
        {
            new Vector2(0.064f, 0.096f),
            new Vector2(0.072f, 0.112f),
            new Vector2(0.052f, 0.082f)
        };

        public void ApplyCaptureState(IReadOnlyDictionary<string, string> state)
        {
            if (state != null)
            {
                foreach (var pair in state)
                    ApplyKey(pair.Key, pair.Value);
            }
            HookCamera();
            Apply();
        }

        public bool IsBloomClosing(int arc)
        {
            return arc >= 0 && arc < 3 && _bloomClose[arc] >= 0f;
        }

        public float BloomFoldAmount(int arc)
        {
            if (arc < 0 || arc >= 3) return 1f;
            return _bloomFold[arc];
        }

        /// <summary>-1 none, 0 bud, 1 open. During a close this stays on the open card until the fold finishes.</summary>
        public int ShownBloomCard(int arc)
        {
            if (arc < 0 || arc >= 3) return -1;
            float bloom = _bloomVisual[arc];
            if (bloom < 0.5f) return -1;
            if (bloom < 1.5f) return 0;
            return 1;
        }

        public void Apply()
        {
            _bloomDt = 0f;
            if (_bloomClock >= 0f)
            {
                _bloomDt = time - _bloomClock;
                if (_bloomDt < 0f) _bloomDt = 0f;
                if (_bloomDt > 0.1f) _bloomDt = 0.1f;
            }
            _bloomClock = time;

            EnsureStateTexture();
            WriteStateTexture();
            if (tileRenderer != null && _stateTex != null)
            {
                var block = new MaterialPropertyBlock();
                tileRenderer.GetPropertyBlock(block);
                block.SetTexture("_StateTex", _stateTex);
                tileRenderer.SetPropertyBlock(block);
            }

            int haloArc = ArcIndex(haloTarget);
            _haloArc = haloArc;
            ApplyPlant(0, stageMorning, bloomMorning);
            ApplyPlant(1, stageMidday, bloomMidday);
            ApplyPlant(2, stageWinddown, bloomWinddown);
            ApplyStrip();
            ApplyPulse();
            ApplyBloomFold();

            // Brightness only. The line stays a few pixels wide; a width pulse read as a throb.
            const float breathHz = Mathf.PI * 2f / 2.6f;
            // 0.40 to 1 over 2.6 s. The trough stays under the clip so the breath reads.
            float breathe = reducedMotion ? 1f : 0.70f + 0.30f * Mathf.Sin(time * breathHz);
            float amount = Mathf.Clamp01(halo) * breathe;
            int haloStage = haloArc == 0 ? stageMorning : haloArc == 1 ? stageMidday : stageWinddown;
            Texture2D[] haloSet = haloArc == 0 ? morningCards : haloArc == 1 ? middayCards : windDownCards;
            int stageCard = Mathf.Clamp(haloStage, 0, 4);
            int bloomCard = ShownBloomCard(haloArc);
            int bloomTex = haloArc * 2 + Mathf.Max(bloomCard, 0);
            Texture2D bloomTex2d = null;
            if (bloomCard >= 0 && bloomTextures != null && bloomTex < bloomTextures.Length)
                bloomTex2d = bloomTextures[bloomTex];
            if (haloRenderer != null)
            {
                haloRenderer.enabled = !stageStrip && amount > 0.01f;
                if (haloSet != null && stageCard < haloSet.Length && haloSet[stageCard] != null)
                {
                    var block = new MaterialPropertyBlock();
                    haloRenderer.GetPropertyBlock(block);
                    block.SetTexture("_MainTex", HaloMask(haloSet[stageCard], bloomTex2d));
                    haloRenderer.SetPropertyBlock(block);
                }
            }
            Material haloMat = haloRenderer != null ? haloRenderer.sharedMaterial : null;
            if (haloMat != null && amount > 0.01f)
            {
                // Gold core and a short falloff. Hot enough to glow, low enough that the
                // breath does not clip to white on every frame.
                haloMat.SetColor("_Color", new Color(1.15f, 0.86f, 0.32f) * amount);
                haloMat.SetColor("_Color2", new Color(0.40f, 0.26f, 0.08f) * amount);
                if (haloMat.HasProperty("_Silhouette")) haloMat.SetFloat("_Silhouette", 2.15f);
                if (haloMat.HasProperty("_Fit")) haloMat.SetFloat("_Fit", 1.18f);
            }
            if (poolRenderer != null)
            {
                poolRenderer.enabled = !stageStrip && amount > 0.01f;
                Material poolMat = poolRenderer.sharedMaterial;
                if (poolMat != null && amount > 0.01f)
                {
                    // A faint warm spot under the stem. The old disc covered the soil.
                    float poolPulse = reducedMotion ? 1f : 0.90f + 0.10f * Mathf.Sin(time * breathHz);
                    poolMat.SetColor("_Color", new Color(0.16f, 0.10f, 0.035f) * (amount * poolPulse));
                    poolMat.SetColor("_Color2", Color.black);
                    if (poolMat.HasProperty("_Falloff")) poolMat.SetFloat("_Falloff", 2.8f);
                    if (poolMat.HasProperty("_Focus")) poolMat.SetVector("_Focus", new Vector4(0.5f, 0.5f, 0.36f, 0f));
                    if (poolMat.HasProperty("_Silhouette")) poolMat.SetFloat("_Silhouette", 0f);
                }
            }
            if (contactRenderer != null)
                contactRenderer.enabled = !stageStrip && showPlants;
            ApplyIntro();

            if (shadow != null)
            {
                // 0 is image-right, 90 is image-far. The painted wash falls toward the near rim
                // (ref-1 at about 13:00), so +Z in the shadow mesh points at (cos, -sin).
                float rad = gnomonDeg * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(rad), 0f, -Mathf.Sin(rad));
                if (dir.sqrMagnitude < 1e-8f) dir = Vector3.forward;
                shadow.localRotation = Quaternion.LookRotation(dir, Vector3.up);
                // Clear the soil mound (peak is about 8 mm above the paper) or the wash is buried.
                shadow.localPosition = new Vector3(0f, faceY + 0.011f, 0f);
            }

            float boilPx = boil ? BoilPixels : 0f;
            if (boilMats != null)
            {
                for (int i = 0; i < boilMats.Length; i++)
                {
                    Material mat = boilMats[i];
                    if (mat == null) continue;
                    if (mat.HasProperty("_BoilTime")) mat.SetFloat("_BoilTime", time);
                    if (mat.HasProperty("_T")) mat.SetFloat("_T", time);
                    if (mat.HasProperty("_BoilPx")) mat.SetFloat("_BoilPx", boilPx);
                    if (mat.HasProperty("_Boil")) mat.SetFloat("_Boil", 0f);
                }
            }
            PlaceHalo(haloArc);
        }

        /// <summary>
        /// Builds the dial under this object from the drawn_dial model and one tile mesh.
        /// A second call replaces the children. The caller saves the generated meshes as assets.
        /// </summary>
        public void Build(GameObject model, Mesh tileMesh, DialLibrary library)
        {
            if (model == null) throw new InvalidOperationException("dial model is missing");
            if (tileMesh == null || !tileMesh.isReadable) throw new InvalidOperationException("tile mesh is not readable");
            if (library == null || library.Card == null || library.Cross == null)
                throw new InvalidOperationException("dial library is missing a card mesh");

            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyObject(transform.GetChild(i).gameObject);

            model.transform.SetParent(transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            Transform top = Require(model.transform, "DialTop");
            Transform side = Require(model.transform, "DialSide");
            Transform soil = Require(model.transform, "Soil");
            Transform pen = Require(model.transform, "Gnomon");
            SetMat(top, library.Face, false);
            SetMat(side, library.Rim, false);
            SetMat(soil, library.Soil, false);
            SetMat(pen, library.Gnomon, false);

            Renderer topRenderer = top.GetComponent<Renderer>();
            faceY = topRenderer.bounds.max.y;
            faceRadius = Mathf.Max(topRenderer.bounds.extents.x, topRenderer.bounds.extents.z);
            if (faceRadius < 0.05f) throw new InvalidOperationException("dial face radius is " + faceRadius.ToString("0.000"));

            morningCards = library.MorningCards;
            middayCards = library.MiddayCards;
            windDownCards = library.WindDownCards;
            haloTextures = library.Halos;
            bloomTextures = library.BloomCards;
            plantMats = new[] { library.Morning, library.Midday, library.WindDown };

            var cards = new List<Transform>();
            for (int arc = 0; arc < 3; arc++)
            {
                Vector2 spot = PlantSpot[arc];
                Vector3 pos = new Vector3(spot.x * faceRadius, faceY + 0.004f, spot.y * faceRadius);
                var card = Card(transform, ArcIds[arc], plantMats[arc], pos, PlantSize[arc], library.Cross);
                card.name = "plant." + ArcIds[arc];
                var box = card.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0.5f, 0.02f);
                box.size = new Vector3(1.05f, 1f, 0.42f);
                var target = card.AddComponent<IntentTarget>();
                target.Id = "plant." + ArcIds[arc];
                cards.Add(card.transform);
            }

            var blooms = new Renderer[3];
            for (int arc = 0; arc < 3; arc++)
            {
                Vector2 spot = PlantSpot[arc];
                Vector3 pos = new Vector3(spot.x * faceRadius, faceY + 0.0055f, spot.y * faceRadius);
                var bloomGo = Card(transform, "bloom." + ArcIds[arc], library.Bloom, pos, PlantSize[arc], library.Cross);
                var bloomRenderer = bloomGo.GetComponent<Renderer>();
                bloomRenderer.enabled = false;
                blooms[arc] = bloomRenderer;
            }
            bloomRenderers = blooms;

            var haloGo = Card(transform, "PinchHalo", library.Halo, Vector3.zero, Vector2.one, library.Card);
            haloRenderer = haloGo.GetComponent<Renderer>();
            cards.Add(haloGo.transform);
            uprightCards = cards.ToArray();

            var poolGo = Card(transform, "HaloPool", library.Pool, Vector3.zero, Vector2.one, library.PoolMesh);
            poolGo.transform.localRotation = Quaternion.identity;
            poolRenderer = poolGo.GetComponent<Renderer>();

            var contactGo = new GameObject("PlantContacts");
            contactGo.transform.SetParent(transform, false);
            contactGo.AddComponent<MeshFilter>().sharedMesh = BuildContactMesh();
            contactRenderer = contactGo.AddComponent<MeshRenderer>();
            contactRenderer.sharedMaterial = library.Contact;
            contactRenderer.shadowCastingMode = ShadowCastingMode.Off;
            contactRenderer.receiveShadows = false;

            BuildStrip(library);

            Mesh placed = CombineTiles(tileMesh);
            var tilesGo = new GameObject("Tiles");
            tilesGo.transform.SetParent(transform, false);
            tilesGo.AddComponent<MeshFilter>().sharedMesh = placed;
            tileRenderer = tilesGo.AddComponent<MeshRenderer>();
            tileRenderer.sharedMaterial = library.Tiles;
            tileRenderer.shadowCastingMode = ShadowCastingMode.Off;
            tileRenderer.receiveShadows = false;

            for (int i = 0; i < TileCount; i++)
            {
                int arc = i / TilesPerArc;
                int slot = i % TilesPerArc;
                float deg = ArcSlot(arc, slot);
                Vector3 pos = OnFace(deg, faceRadius * TileRadius, faceY + 0.001f);
                var proxy = new GameObject("tile." + ArcIds[arc] + "." + slot);
                proxy.transform.SetParent(transform, false);
                proxy.transform.localPosition = pos;
                proxy.transform.localRotation = TileRotation(deg);
                var box = proxy.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0.004f, 0f);
                box.size = new Vector3(0.014f, 0.01f, 0.012f);
                var target = proxy.AddComponent<IntentTarget>();
                target.Id = "tile." + ArcIds[arc] + "." + slot;
            }

            var catcher = new GameObject("TableShadowCatcher");
            catcher.transform.SetParent(transform, false);
            catcher.transform.localPosition = new Vector3(0f, -0.0015f, 0f);
            catcher.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            // A hair past the 30 cm rim. The fade ends inside this quad, so the edge itself adds nothing.
            catcher.transform.localScale = new Vector3(0.36f, 0.36f, 1f);
            catcher.AddComponent<MeshFilter>().sharedMesh = library.CatcherQuad;
            catcher.AddComponent<MeshRenderer>();
            SetMat(catcher.transform, library.Catcher, false);

            var shadowGo = new GameObject("GnomonShadow");
            shadowGo.transform.SetParent(transform, false);
            shadowGo.AddComponent<MeshFilter>().sharedMesh = library.ShadowMesh;
            shadowGo.AddComponent<MeshRenderer>();
            SetMat(shadowGo.transform, library.Shadow, false);
            shadow = shadowGo.transform;

            boilMats = new[]
            {
                library.Rim, library.Soil, library.Gnomon, library.Tiles,
                library.Morning, library.Midday, library.WindDown, library.Bloom, library.Halo
            };
        }

        public Mesh BuiltTileMesh => tileRenderer != null ? tileRenderer.GetComponent<MeshFilter>().sharedMesh : null;

        public Mesh BuiltContactMesh => contactRenderer != null ? contactRenderer.GetComponent<MeshFilter>().sharedMesh : null;

        public void Face(Camera cam)
        {
            if (cam == null || uprightCards == null) return;
            for (int i = 0; i < uprightCards.Length; i++)
            {
                Transform card = uprightCards[i];
                if (card == null) continue;
                Vector3 toCam = cam.transform.position - card.position;
                toCam.y = 0f;
                if (toCam.sqrMagnitude < 1e-8f) continue;
                float sway = 0f;
                if (boil && i < 3)
                    sway = Mathf.Sin(time * (Mathf.PI * 2f / 4f) + i * 1.7f) * 2f;
                card.rotation = Quaternion.LookRotation(toCam, Vector3.up) * Quaternion.Euler(sway, 0f, 0f);
            }
            if (bloomRenderers != null)
            {
                for (int i = 0; i < bloomRenderers.Length && i < 3; i++)
                {
                    if (bloomRenderers[i] == null || uprightCards[i] == null) continue;
                    bloomRenderers[i].transform.rotation = uprightCards[i].rotation;
                }
            }
            if (haloRenderer != null && _haloArc >= 0 && _haloArc < 3 && uprightCards[_haloArc] != null)
                haloRenderer.transform.rotation = uprightCards[_haloArc].rotation;
            if (stageStrip && stripRenderers != null)
            {
                for (int i = 0; i < stripRenderers.Length; i++)
                {
                    Renderer strip = stripRenderers[i];
                    if (strip == null) continue;
                    Vector3 to = cam.transform.position - strip.transform.position;
                    to.y = 0f;
                    if (to.sqrMagnitude < 1e-8f) continue;
                    strip.transform.rotation = Quaternion.LookRotation(to, Vector3.up);
                }
            }
        }

        void Awake()
        {
            // AfterSceneLoad runs once for the boot scene. A later load of Main, including PlayMode
            // reloads, still needs the controller, and it must be awake before the first render.
            if (!Application.isPlaying) return;
            if (GetComponent<SundialController>() == null)
                gameObject.AddComponent<SundialController>();
            if (GetComponent<DuskRitualController>() == null)
                gameObject.AddComponent<DuskRitualController>();
            if (GetComponent<StretchRitualController>() == null)
                gameObject.AddComponent<StretchRitualController>();
        }

        void OnEnable()
        {
            HookCamera();
            Apply();
        }

        void OnDisable()
        {
            if (!_hooked) return;
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            _hooked = false;
        }

        void HookCamera()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            _hooked = true;
        }

        void OnBeginCamera(ScriptableRenderContext context, Camera cam)
        {
            if (cam == null || cam.cameraType != CameraType.Game) return;
            Face(cam);
        }

        void ApplyKey(string key, string value)
        {
            switch (key)
            {
                case "halo": halo = ParseFloat(key, value); break;
                case "haloTarget": haloTarget = string.IsNullOrEmpty(value) ? "midday" : value; break;
                case "gnomonDeg": gnomonDeg = ParseFloat(key, value); break;
                case "time": time = ParseFloat(key, value); break;
                case "boil": boil = ParseBool(value); break;
                case "stage.morning": stageMorning = ParseStage(value); break;
                case "stage.midday": stageMidday = ParseStage(value); break;
                case "stage.winddown": stageWinddown = ParseStage(value); break;
                case "bloom.morning": bloomMorning = ParseFloat(key, value); break;
                case "bloom.midday": bloomMidday = ParseFloat(key, value); break;
                case "bloom.winddown": bloomWinddown = ParseFloat(key, value); break;
                case "tiles.morning": WriteTileDigits(0, value); break;
                case "tiles.midday": WriteTileDigits(7, value); break;
                case "tiles.winddown": WriteTileDigits(14, value); break;
                case "tiles": WriteTileDigits(0, value); break;
                case "stages": stageStrip = string.Equals(value, "strip", StringComparison.OrdinalIgnoreCase); break;
                case "waiting": waiting = Mathf.Clamp01(ParseFloat(key, value)); break;
                case "waitingTarget": waitingTarget = string.IsNullOrEmpty(value) ? "midday" : value; break;
                case "pulse": pulse = Mathf.Clamp01(ParseFloat(key, value)); break;
                case "pulseArc": pulseArc = Mathf.Clamp(Mathf.RoundToInt(ParseFloat(key, value)), 0, 2); break;
                case "reducedMotion": reducedMotion = ParseBool(value); break;
                default:
                    throw new FormatException("DialView has no state field '" + key + "'");
            }
        }

        /// <summary>
        /// Pencil, then ink, then washes. A finished dial (the default) is left alone so captures stay put.
        /// </summary>
        void ApplyIntro()
        {
            if (!Application.isPlaying) return;
            if (appear >= 0.999f)
            {
                EnableNamed("DialTop", true);
                EnableNamed("DialSide", true);
                EnableNamed("Gnomon", true);
                EnableNamed("Soil", true);
                if (tileRenderer != null) tileRenderer.enabled = true;
                if (shadow != null)
                {
                    Renderer drawn = shadow.GetComponent<Renderer>();
                    if (drawn != null) drawn.enabled = true;
                }
                TintFace(false);
                return;
            }
            bool ink = appear >= 1f / 3f;
            bool wash = appear >= 2f / 3f;
            EnableNamed("DialTop", ink);
            EnableNamed("DialSide", ink);
            EnableNamed("Gnomon", ink);
            EnableNamed("Soil", wash);
            if (tileRenderer != null) tileRenderer.enabled = wash;
            if (shadow != null)
            {
                Renderer shadowRenderer = shadow.GetComponent<Renderer>();
                if (shadowRenderer != null) shadowRenderer.enabled = false;
            }
            TintFace(ink && !wash);
        }

        void EnableNamed(string name, bool on)
        {
            Transform found = FindDeep(transform, name);
            if (found == null) return;
            Renderer renderer = found.GetComponent<Renderer>();
            if (renderer != null && renderer.enabled != on) renderer.enabled = on;
        }

        void TintFace(bool pencil)
        {
            Transform top = FindDeep(transform, "DialTop");
            if (top == null) return;
            Renderer renderer = top.GetComponent<Renderer>();
            if (renderer == null) return;
            if (!pencil)
            {
                renderer.SetPropertyBlock(null);
                return;
            }
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            var gray = new Color(0.62f, 0.58f, 0.52f, 1f);
            block.SetColor("_Lit", gray);
            block.SetColor("_Shade", gray * 0.9f);
            renderer.SetPropertyBlock(block);
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        void ApplyPlant(int arc, int stage, float bloom)
        {
            if (uprightCards == null || arc < 0 || arc >= 3 || uprightCards[arc] == null) return;
            Transform plant = uprightCards[arc];
            int card = Mathf.Clamp(stage, 0, 4);
            bloom = PresentBloom(arc, bloom);
            Texture2D[] set = arc == 0 ? morningCards : arc == 1 ? middayCards : windDownCards;
            Renderer renderer = plant.GetComponent<Renderer>();
            Color tint = PlantTint(arc);
            if (renderer != null)
            {
                renderer.enabled = !stageStrip && showPlants;
                if (set != null && card < set.Length && set[card] != null)
                    SetMain(renderer, set[card], tint);
            }
            Vector2 size = PlantSize[arc];
            // The drawing already grows on the shared canvas. The quad stays one size.
            plant.localScale = new Vector3(size.x, size.y, size.y);
            if (bloomRenderers == null || arc >= bloomRenderers.Length || bloomRenderers[arc] == null) return;
            Renderer bloomRenderer = bloomRenderers[arc];
            int which = bloom < 0.5f ? -1 : bloom < 1.5f ? 0 : 1;
            int tex = arc * 2 + which;
            bool show = !stageStrip && showPlants && which >= 0 && bloomTextures != null && tex < bloomTextures.Length && bloomTextures[tex] != null;
            bloomRenderer.enabled = show;
            Transform bloomTransform = bloomRenderer.transform;
            bloomTransform.localPosition = plant.localPosition + new Vector3(0f, 0.0015f, 0f);
            bloomTransform.localScale = plant.localScale;
            if (show) SetMain(bloomRenderer, bloomTextures[tex]);
        }

        /// <summary>
        /// An open flower that falls back to a bud folds shut over <see cref="BloomCloseSeconds"/>.
        /// Reduced motion swaps. The returned value is what the card should draw this frame.
        /// </summary>
        float PresentBloom(int arc, float target)
        {
            // Edit-mode captures set the flower once. Only play mode folds it shut.
            if (!Application.isPlaying)
            {
                _bloomVisual[arc] = target;
                _bloomClose[arc] = -1f;
                _bloomFold[arc] = 1f;
                return target;
            }
            if (_bloomVisual[arc] < 0f) _bloomVisual[arc] = target;
            bool open = _bloomVisual[arc] >= 1.5f && _bloomClose[arc] < 0f;
            bool bud = target >= 0.5f && target < 1.5f;
            if (open && bud)
            {
                if (reducedMotion)
                {
                    _bloomVisual[arc] = target;
                    _bloomFold[arc] = 1f;
                }
                else
                {
                    _bloomClose[arc] = 0f;
                }
            }

            if (_bloomClose[arc] >= 0f)
            {
                if (!bud || reducedMotion)
                {
                    _bloomClose[arc] = -1f;
                    _bloomVisual[arc] = target;
                    _bloomFold[arc] = 1f;
                    return target;
                }
                _bloomClose[arc] += _bloomDt;
                float u = Mathf.Clamp01(_bloomClose[arc] / BloomCloseSeconds);
                _bloomFold[arc] = Mathf.Lerp(1f, 0.38f, u);
                if (u >= 1f)
                {
                    _bloomClose[arc] = -1f;
                    _bloomVisual[arc] = target;
                    _bloomFold[arc] = 1f;
                    return target;
                }
                return 2f;
            }

            _bloomVisual[arc] = target;
            _bloomFold[arc] = 1f;
            return target;
        }

        void ApplyBloomFold()
        {
            if (bloomRenderers == null) return;
            for (int arc = 0; arc < bloomRenderers.Length && arc < 3; arc++)
            {
                Renderer bloomRenderer = bloomRenderers[arc];
                if (bloomRenderer == null) continue;
                float fold = _bloomFold[arc];
                if (fold >= 0.999f) continue;
                Transform bloomTransform = bloomRenderer.transform;
                Vector3 scale = bloomTransform.localScale;
                bloomTransform.localScale = new Vector3(scale.x * fold, scale.y * fold, scale.z);
            }
        }

        void ApplyStrip()
        {
            if (stripRenderers == null) return;
            Texture2D[][] rows = { morningCards, middayCards, windDownCards };
            int n = 0;
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 7; col++)
                {
                    if (n >= stripRenderers.Length) return;
                    Renderer plant = stripRenderers[n++];
                    if (plant != null)
                    {
                        plant.enabled = stageStrip;
                        int stage = col >= 5 ? 4 : col;
                        if (rows[row] != null && stage < rows[row].Length && rows[row][stage] != null)
                            SetMain(plant, rows[row][stage]);
                    }
                    if (col < 5) continue;
                    if (n >= stripRenderers.Length) return;
                    Renderer bloom = stripRenderers[n++];
                    if (bloom == null) continue;
                    bloom.enabled = stageStrip;
                    int tex = row * 2 + (col - 5);
                    if (bloomTextures != null && tex < bloomTextures.Length && bloomTextures[tex] != null)
                        SetMain(bloom, bloomTextures[tex]);
                }
            }
        }

        void BuildStrip(DialLibrary library)
        {
            var list = new List<Renderer>();
            // Near half of the soil, in front of the pen, so the seven columns stay in frame.
            // The soil is a disc. Keep every column inside it and left of the pinch.
            float spanX = 0.020f;
            float spanZ = 0.022f;
            var size = new Vector2(0.018f, 0.038f);
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 7; col++)
                {
                    var pos = new Vector3(-0.030f + (col - 3) * spanX, faceY + 0.006f, -0.022f - row * spanZ);
                    var go = Card(transform, "strip." + row + "." + col, plantMats[row], pos, size, library.Card);
                    var renderer = go.GetComponent<Renderer>();
                    renderer.enabled = false;
                    list.Add(renderer);
                    if (col < 5) continue;
                    var bloomGo = Card(transform, "strip." + row + "." + col + ".bloom", library.Bloom, pos + new Vector3(0f, 0.0015f, 0f), size, library.Card);
                    var bloomRenderer = bloomGo.GetComponent<Renderer>();
                    bloomRenderer.enabled = false;
                    list.Add(bloomRenderer);
                }
            }
            stripRenderers = list.ToArray();
        }

        void ApplyPulse()
        {
            if (pulse <= 0.0001f || stageStrip || uprightCards == null) return;
            int arc = pulseArc;
            if (arc < 0 || arc >= 3 || uprightCards[arc] == null) return;
            if (reducedMotion) return;
            float scale = 1f + 0.3f * Mathf.Sin(Mathf.Clamp01(pulse) * Mathf.PI);
            Transform plant = uprightCards[arc];
            plant.localScale = plant.localScale * scale;
            if (bloomRenderers != null && arc < bloomRenderers.Length && bloomRenderers[arc] != null)
                bloomRenderers[arc].transform.localScale = plant.localScale;
        }

        Color PlantTint(int arc)
        {
            Color tint = Color.white;
            int waitArc = TryArcIndex(waitingTarget);
            if (waiting > 0.01f && waitArc == arc)
            {
                // 2.6 s opacity breath. Reduced motion holds a steady warm step.
                float wave = reducedMotion ? 1f : 0.5f + 0.5f * Mathf.Sin(time * (Mathf.PI * 2f / 2.6f));
                float boost = 1f + 0.32f * wave * Mathf.Clamp01(waiting);
                tint = new Color(1.06f, 1.0f, 0.88f) * boost;
            }
            if (reducedMotion && pulseArc == arc && pulse > 0.02f && pulse < 0.999f)
                tint = new Color(1.25f, 1.12f, 0.85f);
            return tint;
        }

        static int TryArcIndex(string name)
        {
            if (string.IsNullOrEmpty(name)) return -1;
            string n = name.Trim().ToLowerInvariant();
            if (n == "morning" || n == "sunrise") return 0;
            if (n == "midday") return 1;
            if (n == "winddown" || n == "dusk") return 2;
            return -1;
        }

        static void SetMain(Renderer renderer, Texture2D texture)
        {
            SetMain(renderer, texture, Color.white);
        }

        static void SetMain(Renderer renderer, Texture2D texture, Color tint)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetTexture("_MainTex", texture);
            block.SetColor("_Color", tint);
            renderer.SetPropertyBlock(block);
        }

        Texture2D HaloMask(Texture2D plant, Texture2D extra)
        {
            string key = plant != null ? plant.name : "";
            if (extra != null) key = key + "+" + extra.name;
            Texture2D cached;
            if (_haloMasks.TryGetValue(key, out cached) && cached != null) return cached;
            Texture2D made = BakeHaloMask(plant, extra);
            _haloMasks[key] = made;
            return made;
        }

        /// <summary>
        /// Close the plant alpha (and the bloom, when it sticks out), then keep a thin
        /// ring just outside that shape. R is the core, G is the short falloff.
        /// </summary>
        static Texture2D BakeHaloMask(Texture2D plant, Texture2D extra)
        {
            if (plant == null) throw new InvalidOperationException("halo plant texture is missing");
            if (!plant.isReadable) throw new InvalidOperationException(plant.name + " is not readable");
            int w = plant.width;
            int h = plant.height;
            Color32[] plantPx = plant.GetPixels32();
            Color32[] extraPx = null;
            if (extra != null && extra.isReadable && extra.width == w && extra.height == h)
                extraPx = extra.GetPixels32();
            var on = new bool[w * h];
            for (int i = 0; i < on.Length; i++)
            {
                int a = plantPx[i].a;
                if (extraPx != null && extraPx[i].a > a) a = extraPx[i].a;
                on[i] = a >= 128;
            }
            // 12 px bridges neighbouring leaves. The ring then sits on that outer contour.
            const int closePx = 12;
            bool[] closed = CloseMask(on, w, h, closePx);
            int[] dist = DistanceToOn(closed, w, h);
            var pixels = new Color32[on.Length];
            const float corePx = 2.6f;
            const float glowPx = 5.4f;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (closed[i]) continue;
                float px = dist[i] / 3f;
                if (px <= corePx)
                    pixels[i] = new Color32(255, 0, 0, 255);
                else if (px <= glowPx)
                {
                    float t = 1f - (px - corePx) / (glowPx - corePx);
                    pixels[i] = new Color32(0, (byte)Mathf.RoundToInt(255f * t), 0, 255);
                }
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true)
            {
                name = "HaloMask_" + plant.name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }

        static bool[] CloseMask(bool[] on, int w, int h, int radiusPx)
        {
            int units = radiusPx * 3;
            int[] toPlant = DistanceToOn(on, w, h);
            var outside = new bool[on.Length];
            for (int i = 0; i < on.Length; i++) outside[i] = toPlant[i] > units;
            int[] toOutside = DistanceToOn(outside, w, h);
            var closed = new bool[on.Length];
            for (int i = 0; i < on.Length; i++) closed[i] = toOutside[i] >= units;
            return closed;
        }

        /// <summary>Chamfer distance to the nearest true pixel. 3 is a step, 4 is a diagonal.</summary>
        static int[] DistanceToOn(bool[] on, int w, int h)
        {
            const int inf = 1 << 20;
            var d = new int[on.Length];
            for (int i = 0; i < d.Length; i++) d[i] = on[i] ? 0 : inf;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    int v = d[i];
                    if (x > 0) v = Math.Min(v, d[i - 1] + 3);
                    if (y > 0) v = Math.Min(v, d[i - w] + 3);
                    if (x > 0 && y > 0) v = Math.Min(v, d[i - w - 1] + 4);
                    if (x + 1 < w && y > 0) v = Math.Min(v, d[i - w + 1] + 4);
                    d[i] = v;
                }
            }
            for (int y = h - 1; y >= 0; y--)
            {
                for (int x = w - 1; x >= 0; x--)
                {
                    int i = y * w + x;
                    int v = d[i];
                    if (x + 1 < w) v = Math.Min(v, d[i + 1] + 3);
                    if (y + 1 < h) v = Math.Min(v, d[i + w] + 3);
                    if (x + 1 < w && y + 1 < h) v = Math.Min(v, d[i + w + 1] + 4);
                    if (x > 0 && y + 1 < h) v = Math.Min(v, d[i + w - 1] + 4);
                    d[i] = v;
                }
            }
            return d;
        }

        void PlaceHalo(int arc)
        {
            if (haloRenderer == null || uprightCards == null || arc < 0 || arc >= 3 || uprightCards[arc] == null) return;
            Transform plant = uprightCards[arc];
            Transform haloTransform = haloRenderer.transform;
            haloTransform.localRotation = plant.localRotation;
            // The card's +Z faces the camera. A small push keeps the additive glow off the ink.
            Vector3 face = haloTransform.localRotation * Vector3.forward;
            haloTransform.localPosition = plant.localPosition + face * 0.0025f + Vector3.up * 0.001f;
            Vector3 plantScale = plant.localScale;
            // Same pivot as the plant (the base of the card). _Fit insets the texture so this
            // larger card is margin for the dilated glow, not a bigger drawing.
            const float haloScale = 1.18f;
            haloTransform.localScale = new Vector3(plantScale.x * haloScale, plantScale.y * haloScale, plantScale.z * haloScale);
            if (poolRenderer == null) return;
            Transform pool = poolRenderer.transform;
            pool.localRotation = Quaternion.identity;
            // The card pivot is the base of the plant. Keep the pool under that point.
            pool.localPosition = new Vector3(plant.localPosition.x, faceY + 0.009f, plant.localPosition.z);
            float w = Mathf.Max(0.024f, plantScale.x * 0.42f);
            pool.localScale = new Vector3(w, 1f, w * 0.46f);
        }

        Mesh BuildContactMesh()
        {
            var verts = new List<Vector3>(12);
            var uv = new List<Vector2>(12);
            var colors = new List<Color>(12);
            var tris = new List<int>(18);
            for (int arc = 0; arc < 3; arc++)
            {
                Vector2 spot = PlantSpot[arc];
                Vector3 center = new Vector3(spot.x * faceRadius, faceY + 0.008f, spot.y * faceRadius);
                float rx = PlantSize[arc].x * 1.15f;
                float rz = PlantSize[arc].x * 0.72f;
                int b = verts.Count;
                verts.Add(center + new Vector3(-rx, 0f, -rz));
                verts.Add(center + new Vector3(rx, 0f, -rz));
                verts.Add(center + new Vector3(rx, 0f, rz));
                verts.Add(center + new Vector3(-rx, 0f, rz));
                uv.Add(new Vector2(0f, 0f));
                uv.Add(new Vector2(1f, 0f));
                uv.Add(new Vector2(1f, 1f));
                uv.Add(new Vector2(0f, 1f));
                for (int v = 0; v < 4; v++) colors.Add(Color.white);
                tris.Add(b);
                tris.Add(b + 2);
                tris.Add(b + 1);
                tris.Add(b);
                tris.Add(b + 3);
                tris.Add(b + 2);
            }
            var mesh = new Mesh { name = "PlantContacts" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uv);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        void EnsureStateTexture()
        {
            if (_stateTex != null && _stateTex.width == TileCount) return;
            _stateTex = new Texture2D(TileCount, 1, TextureFormat.RGBA32, false, true)
            {
                name = "DialTileState",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
        }

        void WriteStateTexture()
        {
            if (_stateTex == null) return;
            if (tiles == null || tiles.Length != TileCount)
                throw new InvalidOperationException("DialView.tiles must hold 21 values");
            var pixels = new Color32[TileCount];
            for (int i = 0; i < TileCount; i++)
            {
                int state = Mathf.Clamp(tiles[i], 0, 4);
                int arc = i / TilesPerArc;
                float fill = 1f;
                if (tileFill != null && i < tileFill.Length) fill = Mathf.Clamp01(tileFill[i]);
                byte flood = (byte)Mathf.RoundToInt(fill * 255f);
                pixels[i] = new Color32((byte)Mathf.RoundToInt(state / 4f * 255f), (byte)Mathf.RoundToInt(arc / 2f * 255f), flood, 255);
            }
            _stateTex.SetPixels32(pixels);
            _stateTex.Apply(false, false);
        }

        Mesh CombineTiles(Mesh source)
        {
            // Flat cards on the rim. The bevelled cube read as a row of blocks standing
            // on edge, so each tile is a horizontal quad. The source mesh only has to exist.
            if (source == null || !source.isReadable) throw new InvalidOperationException("tile mesh is not readable");
            const float halfL = 0.006f;
            const float halfW = 0.0045f;
            var verts = new List<Vector3>(TileCount * 4);
            var normals = new List<Vector3>(TileCount * 4);
            var uv = new List<Vector2>(TileCount * 4);
            var nxy = new List<Vector2>(TileCount * 4);
            var nz = new List<Vector2>(TileCount * 4);
            var tileUv = new List<Vector2>(TileCount * 4);
            var colors = new List<Color>(TileCount * 4);
            var tris = new List<int>(TileCount * 6);
            var upNxy = new Vector2(0.5f, 1f);
            var upNz = new Vector2(0.5f, 0f);
            var upCol = new Color(0.5f, 1f, 0.5f, 1f);

            for (int i = 0; i < TileCount; i++)
            {
                int arc = i / TilesPerArc;
                int slot = i % TilesPerArc;
                float deg = ArcSlot(arc, slot);
                float rad = deg * Mathf.Deg2Rad;
                Vector3 center = OnFace(deg, faceRadius * TileRadius, faceY + 0.002f);
                var tangent = new Vector3(Mathf.Sin(rad), 0f, -Mathf.Cos(rad));
                var radial = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
                int bas = verts.Count;
                Vector3[] corner =
                {
                    center - tangent * halfL - radial * halfW,
                    center + tangent * halfL - radial * halfW,
                    center + tangent * halfL + radial * halfW,
                    center - tangent * halfL + radial * halfW
                };
                Vector2[] cornerUv =
                {
                    new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f)
                };
                for (int v = 0; v < 4; v++)
                {
                    verts.Add(corner[v]);
                    normals.Add(Vector3.up);
                    uv.Add(cornerUv[v]);
                    nxy.Add(upNxy);
                    nz.Add(upNz);
                    tileUv.Add(new Vector2(i, arc));
                    colors.Add(upCol);
                }
                tris.Add(bas + 0);
                tris.Add(bas + 2);
                tris.Add(bas + 1);
                tris.Add(bas + 0);
                tris.Add(bas + 3);
                tris.Add(bas + 2);
            }

            var mesh = new Mesh { name = "TilesPlaced" };
            mesh.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.SetUVs(1, nxy);
            mesh.SetUVs(2, nz);
            mesh.SetUVs(3, tileUv);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static float ArcMid(int arc)
        {
            float a0, a1;
            ArcEnds(arc, out a0, out a1);
            return a0 - Mathf.Repeat(a0 - a1, 360f) * 0.5f;
        }

        static float ArcSlot(int arc, int slot)
        {
            float a0, a1;
            ArcEnds(arc, out a0, out a1);
            float span = Mathf.Repeat(a0 - a1, 360f);
            float t = 0.12f + 0.76f * (slot / 6f);
            return a0 - span * t;
        }

        static void ArcEnds(int arc, out float a0, out float a1)
        {
            if (arc == 0) { a0 = MorningArc0; a1 = MorningArc1; }
            else if (arc == 1) { a0 = MiddayArc0; a1 = MiddayArc1; }
            else { a0 = WindDownArc0; a1 = WindDownArc1; }
        }

        static Vector3 OnFace(float deg, float radius, float y)
        {
            float rad = deg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(rad) * radius, y, Mathf.Sin(rad) * radius);
        }

        static Quaternion TileRotation(float deg)
        {
            float rad = deg * Mathf.Deg2Rad;
            // Long axis (local +X) follows the arc as the slot index increases (angle decreases).
            var tangent = new Vector3(Mathf.Sin(rad), 0f, -Mathf.Cos(rad));
            float yaw = Mathf.Atan2(tangent.x, tangent.z) * Mathf.Rad2Deg;
            return Quaternion.Euler(0f, yaw, 0f);
        }

        static int ArcIndex(string name)
        {
            if (string.IsNullOrEmpty(name)) return 1;
            string n = name.Trim().ToLowerInvariant();
            if (n == "morning" || n == "sunrise") return 0;
            if (n == "midday") return 1;
            if (n == "winddown" || n == "dusk") return 2;
            throw new FormatException("DialView haloTarget is not an arc: " + name);
        }

        static GameObject Card(Transform parent, string name, Material material, Vector3 pos, Vector2 size, Mesh mesh)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(size.x, size.y, size.y);
            return go;
        }

        static void SetMat(Transform t, Material material, bool cast)
        {
            var renderer = t.GetComponent<Renderer>();
            if (renderer == null) throw new InvalidOperationException(t.name + " has no renderer");
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = cast ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static Transform Require(Transform root, string name)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == name && all[i].GetComponent<MeshFilter>() != null) return all[i];
            }
            var names = new List<string>();
            for (int i = 0; i < all.Length; i++)
                if (all[i].GetComponent<MeshFilter>() != null) names.Add(all[i].name);
            throw new InvalidOperationException("drawn dial is missing mesh " + name + " (have " + string.Join(", ", names) + ")");
        }

        void WriteTileDigits(int start, string digits)
        {
            if (tiles == null || tiles.Length != TileCount)
                tiles = new int[TileCount];
            if (string.IsNullOrEmpty(digits)) throw new FormatException("DialView tiles are empty");
            int count = start == 0 && digits.Length == TileCount ? TileCount : TilesPerArc;
            if (digits.Length < count) throw new FormatException("DialView tiles need " + count + " digits: " + digits);
            for (int i = 0; i < count; i++)
            {
                char c = digits[i];
                if (c < '0' || c > '4') throw new FormatException("DialView tile digit is not 0..4: " + c);
                tiles[start + i] = c - '0';
            }
        }

        static int ParseStage(string text)
        {
            float value = ParseFloat("stage", text);
            return Mathf.Clamp(Mathf.RoundToInt(value), 0, 4);
        }

        static bool ParseBool(string text)
        {
            if (text == "1" || string.Equals(text, "on", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "true", StringComparison.OrdinalIgnoreCase))
                return true;
            if (text == "0" || string.Equals(text, "off", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "false", StringComparison.OrdinalIgnoreCase))
                return false;
            throw new FormatException("DialView boil is not on or off: " + text);
        }

        static float ParseFloat(string key, string text)
        {
            float value;
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                throw new FormatException("DialView state " + key + " is not a number: " + text);
            return value;
        }

        static void DestroyObject(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }
    }
}

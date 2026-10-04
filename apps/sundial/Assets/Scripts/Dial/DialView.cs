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
        /// <summary>Nine species, five stages each. Index is species * 5 + stage.</summary>
        public Texture2D[] SpeciesCards;
        public Texture2D[] BloomCards;
        public Texture2D[] Halos;
        public Mesh Card, Cross, ShadowMesh, CatcherQuad, PoolMesh;
    }

    /// <summary>
    /// The drawn dial on the desk. Look comes only from state: halo, which arc it sits on,
    /// gnomon angle, plant stage and bloom, the 63 tiles, boil, and time.
    /// Each species has five cards (seed, sprout, young, leafy, full). Bloom is an overlay.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class DialView : MonoBehaviour, ICaptureState
    {
        public const int ArcCount = 3;
        public const int RowsPerArc = 3;
        public const int DaysPerRow = 7;
        /// <summary>Three rows of seven days. Row 0 is the original one-habit arc.</summary>
        public const int TilesPerArc = 21;
        public const int TileCount = 63;
        /// <summary>Unoccupied row. The tile shader clips alpha 0, so the mesh can stay one draw.</summary>
        public const int HiddenTile = -1;
        public const float OutlinePixels = 3f;
        public const float BoilPixels = 1f;
        public const float BloomCloseSeconds = 1.2f;
        /// <summary>T-SUN-017 gold, retuned in T-SUN-031 so the same stroke reads on the brighter plants.</summary>
        public const float HaloClosePx = 5f;
        public const float HaloCorePx = 3.0f;
        public const float HaloGlowPx = 8.0f;
        public const float HaloFit = 1f;
        public const float HaloPush = 0.0035f;
        public const float BloomCardLift = 0.0015f;

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
        public int[] tiles = null;
        /// <summary>Nine plants, arc * 3 + row. Heroes also mirror stageMorning, stageMidday, stageWinddown.</summary>
        public int[] plantStage;
        /// <summary>False hides that plant. A one-habit dial leaves the inner rows off.</summary>
        public bool[] plantOn;
        /// <summary>Warm glance tint. Only the plants that are due, not the whole arc.</summary>
        public bool[] plantDue;
        /// <summary>Species index into <see cref="speciesCards"/>. -1 uses the slot's default species.</summary>
        public int[] plantSpecies;
        /// <summary>-1 follows <see cref="haloTarget"/>'s row-0 plant.</summary>
        public int haloSlot = -1;
        /// <summary>-1 pulses the row-0 plant of <see cref="pulseArc"/>.</summary>
        public int pulseSlot = -1;
        /// <summary>Ink flood per tile, 0 at the nib to 1 full. Null means every tile is already full.</summary>
        public float[] tileFill;
        public bool boil = true;
        public float time = 1f;
        /// <summary>0 is blank paper, 1 is the finished drawing. The player default is already drawn.</summary>
        [Range(0f, 1f)] public float appear = 1f;
        /// <summary>False hides the three plant cards. The first run turns this on when the seeds land.</summary>
        public bool showPlants = true;

        /// <summary>The week page is in front. The paper and the rim stay. The day drawing steps aside.</summary>
        public bool weekPage;
        [Range(0f, 1f)] public float waiting;
        public string waitingTarget = "midday";
        [Range(0f, 1f)] public float pulse;
        public int pulseArc = 1;
        public bool reducedMotion;

        /// <summary>Spike S1 material. Capture state variant=watercolour swaps the face to it. Default stays look A.</summary>
        public Material faceWatercolour;
        /// <summary>
        /// Spike S2 kit. Capture state variant=roomlight turns on the room-light cookie on every drawn material,
        /// swaps the gnomon shadow for a broad wash wedge, adds a short wash shadow per plant and moves the table
        /// contact away from the window. Default stays look A.
        /// </summary>
        public Texture2D roomCookie;
        public Material roomShadow;
        // Spike S3, T-SUN-049. variant=leafplant draws each of the three hero plants (morning, midday, evening: slots 0, 3 and 6)
        // as a drawn-leaf assembly (one mesh of cards cut from that species parts sheet) instead of the camera-facing card.
        // Resources/LeafPlant holds each material, atlas and parts json. Only the full stage has an assembly. Every other
        // stage, and every other plant, keeps the card.
        readonly LeafPlant[] _leaves = new LeafPlant[3];
        readonly bool[] _leafOn = new bool[3];
        Mesh _contactDefault;
        Mesh _contactSwapped;
        int _contactSwappedMask = -1;
        Vector3 _viewForward = new Vector3(0f, -0.60f, 0.80f);
        Texture2D _leafSilhouette;
        string _leafSilhouetteKey;
        /// <summary>The midday assembly (T-SUN-045 name). <see cref="LeafAssemblyFor"/> takes the arc.</summary>
        public LeafPlant LeafAssembly { get { return _leaves[1]; } }
        public LeafPlant LeafAssemblyFor(int arc) { return arc >= 0 && arc < 3 ? _leaves[arc] : null; }
        bool AnyLeafOn()
        {
            for (int i = 0; i < _leafOn.Length; i++)
                if (_leafOn[i]) return true;
            return false;
        }

        /// <summary>The assembly standing in for the card in this slot, or null when the card is drawn.</summary>
        LeafPlant LeafAt(int slot)
        {
            if (slot < 0 || slot % RowsPerArc != 0) return null;
            int arc = slot / RowsPerArc;
            return arc < 3 && _leafOn[arc] ? _leaves[arc] : null;
        }
        // Spike S4. variant=soilmound hides the old low soil disc and draws a raised heightfield mound with a top-down
        // painting, a ragged feathered edge and a few pebbles: one mesh, one draw. Resources/SoilMound holds the rest.
        SoilMound _mound;
        bool _moundApplied;
        // Spike S5. variant=halo2 draws the pinch halo as one smooth closed curve around the plant plus a ground ellipse, from
        // masks baked offline (Resources/Halo2), on one mesh in place of the card-sized halo. Look A keeps the card halo.
        Halo2 _halo2;
        bool _spillBlock;
        /// <summary>Strength of the warm spill on the soil under variant=halo2. 0 draws the ring alone (a capture reads the stroke without it).</summary>
        public float halo2Spill = 1f;
        /// <summary>Capture only. Draws the pinch halo alone (every other renderer in the scene off), so its light can be read on black.</summary>
        public bool isolateHalo;
        /// <summary>Capture only. Draws the dial and nothing else (every renderer outside this object off), so its silhouette is read on black.</summary>
        public bool isolateDial;
        /// <summary>Capture only. Draws the soil (the bed, or the old disc) and nothing else, to read its area on black.</summary>
        public bool isolateSoil;
        /// <summary>Capture only. 0 to 2 draws that arc's plants and no others; -1 draws them all.</summary>
        int plantsOnlyArc = -1;
        Texture2D _halo2Plant;
        Texture2D _halo2Bloom;
        public Halo2 Halo2Kit { get { return _halo2; } }
        public SoilMound Mound { get { return _mound; } }
        // T-SUN-052. variant=layout (combines with soilmound, leafplant, halo2 as layout+soilmound+...) scales the dial to the reference's
        // size against the hand, moves the plants and tiles, swaps the face for the remapped one (wider wash disc, narrower rim band),
        // and grows the soil bed over the face. The habit record (tiles, arcs, slots) is untouched. Look A keeps every one of these.
        bool _layoutOn;
        bool _layoutBaseKnown;
        Vector3 _layoutBaseScale = Vector3.one;
        Vector3 _layoutBasePos;
        Material _faceLayout;
        Material _faceLayoutWatercolour;
        Mesh _tilesDefault;
        Mesh _tilesLayout;
        Mesh _contactLayout;
        bool _moundLayout;
        bool _soilBaseKnown;
        Vector3 _soilBaseScale;
        Vector3 _soilBasePos;
        public bool LayoutOn { get { return _layoutOn; } }
        const string VariantDefault = "a";
        string _variant = VariantDefault;
        Material _faceDefault;
        Transform _roomShadowRoot;
        bool _roomLightApplied;
        Mesh _wedgeMesh;
        Transform _gnomonWedge;
        Transform[] _plantWedges;
        /// <summary>Direction the key light travels on the table plane (x right, z away). The window is upper left and behind.</summary>
        static readonly Vector2 RoomKeyTravel = new Vector2(0.45f, -0.64f).normalized;
        const float ContactAwayFromWindow = 0.012f;
        /// <summary>Plain flags on purpose. Any DontSave flag keeps the wedges out of FindObjectsByType, so out of the budget count.</summary>
        const HideFlags RuntimeOnly = HideFlags.None;

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
        public Texture2D[] speciesCards;
        public Transform[] plantSlots;
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
            // 1.14x the old 0.052 x 0.082 card. The filled leaves measure as a two-texel
            // shell, so the smaller card put the dusk contour at 1.44 px. This lands near 1.65.
            new Vector2(0.0593f, 0.0935f)
        };

        /// <summary>The spot of an arc's card in face radii: look A's, or the layout's when variant=layout is on.</summary>
        Vector2 Spot(int arc)
        {
            return _layoutOn ? DialLayout.PlantSpot[arc] : PlantSpot[arc];
        }

        float TileRadiusFor(int row, float lookARadius)
        {
            return _layoutOn ? DialLayout.TileRowRadius[Mathf.Clamp(row, 0, 2)] : lookARadius;
        }

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

            ApplyLayout();
            EnsureTiles();
            EnsurePlantArrays();
            EnsureStateTexture();
            WriteStateTexture();
            ApplyTileColliders();
            if (tileRenderer != null && _stateTex != null)
            {
                var block = new MaterialPropertyBlock();
                tileRenderer.GetPropertyBlock(block);
                block.SetTexture("_StateTex", _stateTex);
                tileRenderer.SetPropertyBlock(block);
            }

            int haloArc = ArcIndex(haloTarget);
            _haloArc = haloArc;
            ApplyPlants();
            ApplyLeafPlant();
            ApplyStrip();
            ApplyPulse();
            ApplyBloomFold();

            // Brightness only. The line stays a few pixels wide; a width pulse read as a throb.
            const float breathHz = Mathf.PI * 2f / 2.6f;
            // 0.40 to 1 over 2.6 s. The trough stays under the clip so the breath reads.
            float breathe = reducedMotion ? 1f : 0.70f + 0.30f * Mathf.Sin(time * breathHz);
            float amount = Mathf.Clamp01(halo) * breathe;
            int haloPlantSlot = haloSlot >= 0 ? haloSlot : haloArc * RowsPerArc;
            int haloRow = haloPlantSlot % RowsPerArc;
            int haloStage = plantStage != null && haloPlantSlot >= 0 && haloPlantSlot < plantStage.Length
                ? plantStage[haloPlantSlot]
                : (haloArc == 0 ? stageMorning : haloArc == 1 ? stageMidday : stageWinddown);
            int stageCard = Mathf.Clamp(haloStage, 0, 4);
            int bloomCard = haloRow == 0 ? ShownBloomCard(haloArc) : -1;
            int bloomTex = haloArc * 2 + Mathf.Max(bloomCard, 0);
            Texture2D bloomTex2d = null;
            if (bloomCard >= 0 && bloomTextures != null && bloomTex < bloomTextures.Length)
                bloomTex2d = bloomTextures[bloomTex];
            Texture2D haloCard = CardTex(haloPlantSlot, stageCard);
            if (LeafAt(haloPlantSlot) != null)
            {
                // The assembly already has its heads. The halo traces the plant that is drawn, from the camera that draws it.
                haloCard = LeafSilhouette(haloPlantSlot, haloCard);
                bloomTex2d = null;
            }
            _halo2Plant = haloCard;
            _halo2Bloom = bloomTex2d;
            if (haloRenderer != null)
            {
                haloRenderer.enabled = !stageStrip && amount > 0.01f;
                if (haloCard != null)
                {
                    var block = new MaterialPropertyBlock();
                    haloRenderer.GetPropertyBlock(block);
                    int bloomLift = 0;
                    if (bloomTex2d != null)
                    {
                        Transform haloPlant = HaloPlant();
                        float cardH = haloPlant != null ? haloPlant.localScale.y : 0.112f;
                        bloomLift = Mathf.RoundToInt(BloomCardLift / Mathf.Max(cardH, 0.02f) * haloCard.height);
                    }
                    block.SetTexture("_MainTex", HaloMask(haloCard, bloomTex2d, bloomLift));
                    haloRenderer.SetPropertyBlock(block);
                }
            }
            Material haloMat = haloRenderer != null ? haloRenderer.sharedMaterial : null;
            if (haloMat != null && amount > 0.01f)
            {
                // Gold core and a short falloff. Hot enough to glow, low enough that the
                // breath does not clip to white on every frame.
                // T-SUN-017 core gold. The falloff is a little stronger so the short glow
                // still reads against the saturated T-SUN-028 flowers.
                haloMat.SetColor("_Color", new Color(1.15f, 0.86f, 0.32f) * amount);
                haloMat.SetColor("_Color2", new Color(0.58f, 0.38f, 0.12f) * amount);
                if (haloMat.HasProperty("_Silhouette")) haloMat.SetFloat("_Silhouette", 2.15f);
                if (haloMat.HasProperty("_Fit")) haloMat.SetFloat("_Fit", HaloFit);
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
            if (weekPage) CoverDayDrawing();
            ApplyFaceVariant();
            ApplyRoomLight();
            ApplySoilMound();
            ApplyHalo2(amount);
            ApplyIsolate();
        }

        static bool ParseBoolLoose(string value)
        {
            return value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// variant=layout (T-SUN-052). Look A leaves all of this off. On: the dial scales about its centre, the tiles and the plant
        /// contact discs move to the layout radii and spots, and the old soil disc (when the mound is not drawn) becomes the bed.
        /// The face material, the mound and the plant cards read <see cref="_layoutOn"/> in their own Apply steps.
        /// </summary>
        void ApplyLayout()
        {
            bool want = HasVariant(IsLayoutVariant);
            if (!_layoutBaseKnown)
            {
                _layoutBaseScale = transform.localScale;
                _layoutBasePos = transform.localPosition;
                _layoutBaseKnown = true;
            }
            if (want != _layoutOn)
            {
                _layoutOn = want;
                transform.localScale = want ? _layoutBaseScale * DialLayout.Scale : _layoutBaseScale;
                transform.localPosition = want ? _layoutBasePos + DialLayout.Offset : _layoutBasePos;
                RebuildTilesForLayout();
                RebuildContactsForLayout();
            }
            ApplyOldSoilLayout();
        }

        void RebuildTilesForLayout()
        {
            if (tileRenderer == null) return;
            var filter = tileRenderer.GetComponent<MeshFilter>();
            if (filter == null) return;
            if (_tilesDefault == null) _tilesDefault = filter.sharedMesh;
            if (_tilesLayout != null) { DestroyObject(_tilesLayout); _tilesLayout = null; }
            if (_layoutOn)
            {
                _tilesLayout = CombineTiles(_tilesDefault);
                _tilesLayout.name = "TilesPlaced.Layout";
                _tilesLayout.hideFlags = HideFlags.DontSave;
                filter.sharedMesh = _tilesLayout;
            }
            else if (_tilesDefault != null)
            {
                filter.sharedMesh = _tilesDefault;
            }
            // The tile colliders are children named tile.*. They follow the same radii.
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                int index;
                if (!TryTileName(child.name, out index)) continue;
                int arc = index / TilesPerArc;
                int within = index % TilesPerArc;
                int row = within / DaysPerRow;
                float radius, t0, t1, halfL, halfW;
                RowLayout(row, out radius, out t0, out t1, out halfL, out halfW);
                float deg = RowSlot(arc, row, within % DaysPerRow);
                child.localPosition = OnFace(deg, faceRadius * TileRadiusFor(row, radius), faceY + 0.001f);
            }
        }

        void RebuildContactsForLayout()
        {
            if (contactRenderer == null) return;
            var filter = contactRenderer.GetComponent<MeshFilter>();
            if (filter == null) return;
            if (_contactDefault == null) _contactDefault = filter.sharedMesh;
            if (_contactLayout != null) { DestroyObject(_contactLayout); _contactLayout = null; }
            _contactSwappedMask = -1;
            if (_layoutOn)
            {
                _contactLayout = BuildContactMesh(0);
                _contactLayout.hideFlags = HideFlags.DontSave;
                filter.sharedMesh = _contactLayout;
            }
            else if (_contactDefault != null)
            {
                filter.sharedMesh = _contactDefault;
            }
        }

        /// <summary>Without the mound, the flat soil disc takes the bed's footprint (a squashed, shifted copy of itself).</summary>
        void ApplyOldSoilLayout()
        {
            Transform soil = FindDeep(transform, "Soil");
            if (soil == null) return;
            var filter = soil.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return;
            if (!_soilBaseKnown)
            {
                _soilBaseScale = soil.localScale;
                _soilBasePos = soil.localPosition;
                _soilBaseKnown = true;
            }
            bool bed = _layoutOn && !HasVariant(IsSoilMoundVariant) && filter.sharedMesh.bounds.size.sqrMagnitude > 1e-8f;
            if (!bed)
            {
                soil.localScale = _soilBaseScale;
                soil.localPosition = _soilBasePos;
                return;
            }
            // The disc's mesh lies in whichever local plane the importer left it in. Work in the parent's axes: which local axis
            // runs along the dial's x and which along its z, then stretch those two.
            Bounds b = filter.sharedMesh.bounds;
            Quaternion rot = soil.localRotation;
            Vector3[] dir = { rot * Vector3.right, rot * Vector3.up, rot * Vector3.forward };
            float[] ext = { b.extents.x * _soilBaseScale.x, b.extents.y * _soilBaseScale.y, b.extents.z * _soilBaseScale.z };
            float halfX = 0f;
            float halfZ = 0f;
            for (int i = 0; i < 3; i++)
            {
                halfX += Mathf.Abs(dir[i].x) * ext[i];
                halfZ += Mathf.Abs(dir[i].z) * ext[i];
            }
            halfX = Mathf.Max(halfX, 1e-4f);
            halfZ = Mathf.Max(halfZ, 1e-4f);
            float kx = DialLayout.BedHalfX * faceRadius / halfX;
            float kz = DialLayout.BedHalfZ * faceRadius / halfZ;
            var k = new float[3];
            for (int i = 0; i < 3; i++)
                k[i] = Mathf.Abs(dir[i].x) > 0.5f ? kx : Mathf.Abs(dir[i].z) > 0.5f ? kz : 1f;
            Vector3 scale = new Vector3(_soilBaseScale.x * k[0], _soilBaseScale.y * k[1], _soilBaseScale.z * k[2]);
            soil.localScale = scale;
            Vector3 centreNow = rot * Vector3.Scale(b.center, scale);
            Vector3 centreBase = _soilBasePos + rot * Vector3.Scale(b.center, _soilBaseScale);
            var target = new Vector3(0f, centreBase.y, DialLayout.BedCentreZ * faceRadius);
            soil.localPosition = target - centreNow;
        }

        /// <summary>The halo and nothing else, for the metrics (the gold clips on the bright paper, so it is read on black).</summary>
        void ApplyIsolate()
        {
            if (isolateSoil)
            {
                foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    bool soil = r.name == "Soil" || r.name == "SoilMound";
                    if (!soil) r.enabled = false;
                }
                return;
            }
            if (isolateDial)
            {
                foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    if (!r.transform.IsChildOf(transform)) r.enabled = false;
                }
                return;
            }
            if (!isolateHalo || haloRenderer == null) return;
            foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                bool keep = r == haloRenderer || (_halo2 != null && r == _halo2.Renderer);
                if (!keep) r.enabled = false;
            }
        }

        static bool IsHalo2Variant(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string n = name.Trim().ToLowerInvariant();
            return n == "halo2" || n == "halo-v2" || n == "halov2" || n == "s5";
        }

        /// <summary>
        /// Look A leaves the card halo alone. variant=halo2 turns it off and draws the baked hull ring and ground ellipse in
        /// its place, with a warm spill on the soil inside the ellipse. When the plant has no baked mask, or is the drawn-leaf
        /// assembly (whose analytic hull is a stub, see <see cref="HaloSphereUnion"/>), look A's halo stays.
        /// </summary>
        void ApplyHalo2(float amount)
        {
            if (!HasVariant(IsHalo2Variant))
            {
                if (_halo2 != null) _halo2.SetActive(false);
                ClearSpill();
                return;
            }
            if (haloRenderer == null) return;
            if (_halo2 == null)
            {
                var data = Resources.Load<TextAsset>(Halo2.ResourceJson);
                if (data == null)
                    throw new InvalidOperationException("variant=halo2 needs Resources/Halo2/halo2.json (run apps/sundial/Art/Scripts/halo_s5.py bake)");
                _halo2 = new Halo2(haloRenderer.transform, haloRenderer.sharedMaterial, data);
            }
            _halo2.Fit(haloRenderer.transform.localScale.y);
            bool wanted = haloRenderer.enabled;
            int slot = haloSlot >= 0 ? haloSlot : _haloArc * RowsPerArc;
            bool leaf = LeafAt(slot) != null;
            bool shown = false;
            Halo2MaskInfo info = null;
            if (wanted && !leaf)
            {
                string key = Halo2.KeyFor(_halo2Plant, _halo2Bloom);
                Texture2D mask;
                if (!_halo2.TryMask(key, out mask, out info) && _halo2Bloom != null)
                {
                    _halo2.Warn("no mask for " + key + ", the card without its bloom is used");
                    key = Halo2.KeyFor(_halo2Plant, null);
                }
                shown = _halo2.Show(key) && _halo2.TryMask(key, out mask, out info);
                if (!shown) _halo2.Warn("no baked halo for " + key + ", look A's halo is drawn");
            }
            _halo2.SetActive(shown);
            if (shown) haloRenderer.enabled = false;
            if (shown && poolRenderer != null && amount > 0.01f && halo2Spill > 0.001f) SpillOnSoil(info, amount * Mathf.Clamp01(halo2Spill));
            else ClearSpill();
        }

        /// <summary>The warm spot under the plant becomes a soft disc the size of the ground ellipse, on the soil.</summary>
        void SpillOnSoil(Halo2MaskInfo info, float amount)
        {
            Transform halo = haloRenderer.transform;
            Vector3 world = halo.TransformPoint(_halo2.GroundCentreLocal(info));
            Transform pool = poolRenderer.transform;
            pool.localRotation = Quaternion.identity;
            Vector3 local = transform.InverseTransformPoint(world);
            pool.localPosition = new Vector3(local.x, local.y - 0.0004f, local.z);
            float radius = _halo2.RadiusCardUnits(info) * halo.lossyScale.x * 0.92f;
            pool.localScale = new Vector3(radius * 2f, 1f, radius * 2f);
            // A property block, not the material: the asset stays look A's and switching back needs one clear.
            float pulse = reducedMotion ? 1f : 0.90f + 0.10f * Mathf.Sin(time * (Mathf.PI * 2f / 2.6f));
            var block = new MaterialPropertyBlock();
            poolRenderer.GetPropertyBlock(block);
            block.SetColor("_Color", new Color(0.70f, 0.43f, 0.15f) * (amount * pulse));
            block.SetFloat("_Falloff", 1.1f);
            block.SetVector("_Focus", new Vector4(0.5f, 0.5f, 0.5f, 0f));
            poolRenderer.SetPropertyBlock(block);
            _spillBlock = true;
        }

        void ClearSpill()
        {
            if (!_spillBlock || poolRenderer == null) return;
            poolRenderer.SetPropertyBlock(null);
            _spillBlock = false;
        }

        static bool IsSoilMoundVariant(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string n = name.Trim().ToLowerInvariant();
            return n == "soilmound" || n == "soil-mound" || n == "mound" || n == "soil" || n == "s4";
        }

        /// <summary>
        /// Look A leaves the soil disc alone. variant=soilmound turns it off and shows the mound in its place. The mound
        /// follows the same intro and week-page rules as the disc it replaces.
        /// </summary>
        void ApplySoilMound()
        {
            bool visible = !weekPage && (!Application.isPlaying || appear >= 2f / 3f);
            if (!HasVariant(IsSoilMoundVariant))
            {
                if (_moundApplied)
                {
                    if (_mound != null) _mound.SetActive(false);
                    EnableNamed("Soil", visible);
                    _moundApplied = false;
                }
                return;
            }
            if (_mound != null && _moundLayout != _layoutOn)
            {
                _mound.Destroy();
                _mound = null;
            }
            if (_mound == null)
            {
                var material = Resources.Load<Material>(SoilMound.ResourceMaterial);
                var data = Resources.Load<TextAsset>(SoilMound.ResourceData);
                if (material == null || data == null)
                    throw new InvalidOperationException("variant=soilmound needs Resources/SoilMound/Soil_Mound.mat and soil-mound.json (run SoilMoundSetup.Run)");
                _mound = new SoilMound(transform, material, data, faceY, _layoutOn ? new SoilBed(faceRadius) : null);
                _moundLayout = _layoutOn;
            }
            _mound.SetActive(visible);
            EnableNamed("Soil", false);
            _moundApplied = true;
        }

        static bool IsRoomLightVariant(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string n = name.Trim().ToLowerInvariant();
            return n == "roomlight" || n == "room-light" || n == "room" || n == "s2";
        }

        static bool IsWatercolourVariant(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string n = name.Trim().ToLowerInvariant();
            return n == "watercolour" || n == "watercolor" || n == "s1" || n == "b";
        }

        static bool IsLeafPlantVariant(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string n = name.Trim().ToLowerInvariant();
            return n == "leafplant" || n == "leaf-plant" || n == "leaf" || n == "drawnleaf" || n == "s3" || n == SprintLeafToken;
        }

        /// <summary>The leafplant token that is not the T-SUN-049 one: every hero plant but the morning one (see <see cref="SprintLeafArcs"/>).</summary>
        const string SprintLeafToken = "sprintleaf";
        /// <summary>
        /// T-SUN-050. <c>variant=sprint</c> is the sprint composite: the winners of the five spikes plus the layout pass, stacked.
        /// It expands to <see cref="SprintParts"/>. Anything joined to it is added on top (sprint+halo2 puts halo v2 in to compare).
        /// </summary>
        public const string SprintToken = "sprint";
        /// <summary>
        /// S1 watercolour and paper (with the layout face, see <see cref="ApplyFaceVariant"/>), S2 room light, S4 soil mound, S6 layout,
        /// S3b drawn leaves for the midday and evening plants, S5 halo v2 for the plant that stays a card. Halo v2 has no mask for a
        /// drawn-leaf plant, so those two keep the layout's closed halo stroke (see <see cref="ApplyHalo2"/>).
        /// </summary>
        public static readonly string[] SprintParts = { "layout", "watercolour", "roomlight", "soilmound", SprintLeafToken, "halo2" };
        /// <summary>Arcs drawn as assemblies under the sprint. The morning plant stays a card: its assembly lost to the card (T-SUN-049, 2 of 6).</summary>
        public static readonly bool[] SprintLeafArcs = { false, true, true };

        static bool IsSprintVariant(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name.Trim().ToLowerInvariant() == SprintToken;
        }

        /// <summary>The arc's hero plant is drawn as an assembly by the active variant. The plain leafplant token draws all three.</summary>
        bool LeafArcWanted(int arc)
        {
            bool all = false;
            bool sprint = false;
            foreach (string token in VariantTokens(_variant))
            {
                string n = token.Trim().ToLowerInvariant();
                if (n == SprintLeafToken) sprint = true;
                else if (IsLeafPlantVariant(n)) all = true;
            }
            if (all) return true;
            return sprint && arc >= 0 && arc < SprintLeafArcs.Length && SprintLeafArcs[arc];
        }

        static bool IsLayoutVariant(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string n = name.Trim().ToLowerInvariant();
            return n == "layout" || n == "s6" || n == "proportions";
        }

        static bool IsKnownToken(string n)
        {
            return n == VariantDefault || IsSprintVariant(n) || IsWatercolourVariant(n) || IsRoomLightVariant(n) || IsLeafPlantVariant(n) || IsSoilMoundVariant(n)
                   || IsHalo2Variant(n) || IsLayoutVariant(n);
        }

        /// <summary>A variant is one name or several joined with + (layout+soilmound+leafplant). Look A is the empty set. <c>sprint</c> stands for <see cref="SprintParts"/>.</summary>
        static string[] VariantTokens(string variant)
        {
            if (string.IsNullOrEmpty(variant)) return new string[0];
            var tokens = new List<string>();
            foreach (string raw in variant.Split(new[] { '+', ' ', ',', '|' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (IsSprintVariant(raw)) tokens.AddRange(SprintParts);
                else tokens.Add(raw);
            }
            return tokens.ToArray();
        }

        static bool IsKnownVariant(string name)
        {
            if (string.IsNullOrEmpty(name)) return true;
            foreach (string token in VariantTokens(name))
                if (!IsKnownToken(token.Trim().ToLowerInvariant())) return false;
            return true;
        }

        /// <summary>True when any token of the active variant is the kind <paramref name="is"/> names.</summary>
        bool HasVariant(Func<string, bool> @is)
        {
            foreach (string token in VariantTokens(_variant))
                if (@is(token)) return true;
            return false;
        }

        /// <summary>
        /// Look A leaves all of this off and the card draws. variant=leafplant hides each hero card and its bloom overlay and
        /// draws the assembly in their place: one mesh, one draw and one soft contact quad per plant.
        /// </summary>
        void ApplyLeafPlant()
        {
            for (int i = 0; i < _leafOn.Length; i++) _leafOn[i] = false;
            if (!HasVariant(IsLeafPlantVariant))
            {
                RestoreLeafPlant();
                return;
            }
            int skipMask = 0;
            for (int arc = 0; arc < 3; arc++)
            {
                int slot = arc * RowsPerArc;
                Transform plant = PlantTransform(slot);
                if (plant == null) continue;
                // The assembly is drawn from the species the arc hero plant shows by default.
                if (SpeciesOf(slot) != arc) continue;
                if (!LeafArcWanted(arc))
                {
                    if (_leaves[arc] != null) _leaves[arc].SetActive(false);
                    continue;
                }
                LeafPlant leaf = EnsureLeafPlant(arc);
                int stage = plantStage != null && slot < plantStage.Length ? Mathf.Clamp(plantStage[slot], 0, 4) : 4;
                bool on = SlotOn(slot) && showPlants && !stageStrip && !weekPage && stage == 4;
                leaf.SetActive(on);
                if (!on) continue;
                _leafOn[arc] = true;
                skipMask |= 1 << arc;
                Renderer card = plant.GetComponent<Renderer>();
                if (card != null) card.enabled = false;
                if (bloomRenderers != null && arc < bloomRenderers.Length && bloomRenderers[arc] != null) bloomRenderers[arc].enabled = false;
                int shown = ShownBloomCard(arc);
                leaf.Rebuild(shown < 0 ? LeafBloom.None : shown == 0 ? LeafBloom.Bud : LeafBloom.Open);
                float scale = PlantSize[arc].y > 1e-4f ? plant.localScale.y / PlantSize[arc].y : 1f;
                leaf.SetPose(plant.localPosition, scale);
                leaf.SetLook(time, PlantTint(slot));
            }
            SwapContact(skipMask);
        }

        int SpeciesOf(int slot)
        {
            if (plantSpecies != null && slot >= 0 && slot < plantSpecies.Length && plantSpecies[slot] >= 0)
                return plantSpecies[slot];
            return DefaultSpecies(slot);
        }

        LeafPlant EnsureLeafPlant(int arc)
        {
            if (_leaves[arc] != null) return _leaves[arc];
            LeafSpecies species = LeafSpecies.ForArc(arc);
            var material = Resources.Load<Material>(species.MaterialResource);
            var parts = Resources.Load<TextAsset>(species.PartsResource);
            if (material == null || parts == null)
                throw new InvalidOperationException("variant=leafplant needs Resources/" + species.MaterialResource + ".mat and " + species.PartsResource + ".json (run LeafPlantSetup.Run)");
            Material contact = contactRenderer != null ? contactRenderer.sharedMaterial : null;
            _leaves[arc] = new LeafPlant(transform, species, material, parts, contact);
            return _leaves[arc];
        }

        /// <summary>The halo mask follows the camera that draws the assembly, because the silhouette depends on the view.</summary>
        void RefreshLeafHalo()
        {
            if (haloRenderer == null || plantStage == null) return;
            int slot = haloSlot >= 0 ? haloSlot : _haloArc * RowsPerArc;
            if (LeafAt(slot) == null) return;
            Texture2D like = CardTex(slot, Mathf.Clamp(plantStage[slot], 0, 4));
            var block = new MaterialPropertyBlock();
            haloRenderer.GetPropertyBlock(block);
            block.SetTexture("_MainTex", HaloMask(LeafSilhouette(slot, like), null, 0));
            haloRenderer.SetPropertyBlock(block);
        }

        void RestoreLeafPlant()
        {
            for (int i = 0; i < _leaves.Length; i++)
                if (_leaves[i] != null) _leaves[i].SetActive(false);
            SwapContact(0);
        }

        /// <summary>Each assembly brings its own contact shadow. The old disc under a plant steps aside while its assembly is drawn.</summary>
        void SwapContact(int skipMask)
        {
            if (contactRenderer == null) return;
            var filter = contactRenderer.GetComponent<MeshFilter>();
            if (filter == null) return;
            if (_contactDefault == null) _contactDefault = filter.sharedMesh;
            Mesh plain = _layoutOn && _contactLayout != null ? _contactLayout : _contactDefault;
            if (skipMask != 0)
            {
                if (_contactSwapped == null || _contactSwappedMask != skipMask)
                {
                    if (_contactSwapped != null) DestroyObject(_contactSwapped);
                    _contactSwapped = BuildContactMesh(skipMask);
                    _contactSwapped.hideFlags = HideFlags.DontSave;
                    _contactSwappedMask = skipMask;
                }
                if (filter.sharedMesh != _contactSwapped) filter.sharedMesh = _contactSwapped;
            }
            else if (plain != null && filter.sharedMesh != plain)
            {
                filter.sharedMesh = plain;
            }
        }

        /// <summary>The assembly seen from the camera on an upright card, the size of the card texture it stands in for.</summary>
        Texture2D LeafSilhouette(int slot, Texture2D like)
        {
            int arc = slot / RowsPerArc;
            LeafPlant leaf = _leaves[arc];
            Vector3 local = transform.InverseTransformDirection(_viewForward);
            int shown = ShownBloomCard(arc);
            // Quantise the view so the bake is not repeated every frame the camera drifts a hair.
            string key = arc + ":" + Mathf.RoundToInt(local.x * 40f) + "," + Mathf.RoundToInt(local.y * 40f) + "," + Mathf.RoundToInt(local.z * 40f)
                         + "," + shown + "," + leaf.Triangles + (_layoutOn ? ",L" : "");
            if (_leafSilhouette != null && key == _leafSilhouetteKey) return _leafSilhouette;
            if (_leafSilhouette != null) DestroyObject(_leafSilhouette);
            Transform plant = PlantTransform(slot);
            float scale = PlantSize[arc].y > 1e-4f && plant != null ? plant.localScale.y / PlantSize[arc].y : 1f;
            int w = like != null ? like.width : 256;
            int h = like != null ? like.height : 512;
            if (_layoutOn)
            {
                // A bigger canvas, so the plant is not cut by its card, with the soil patch it stands in.
                Vector4 c = DialLayout.HaloCanvas;
                w = Mathf.RoundToInt(w * (c.z - c.x));
                h = Mathf.RoundToInt(h * (c.w - c.y));
                _leafSilhouette = leaf.BakeSilhouette(local, PlantSize[arc] * scale, w, h, scale, c, DialLayout.HaloBaseDisc);
            }
            else
            {
                _leafSilhouette = leaf.BakeSilhouette(local, PlantSize[arc] * scale, w, h, scale);
            }
            _leafSilhouette.name = "LeafPlantSilhouette." + key;
            _leafSilhouetteKey = key;
            return _leafSilhouette;
        }

        /// <summary>
        /// Look A is the prefab face. variant=watercolour uses the S1 material
        /// (keyword, control map, paper height, repaired dusk arc).
        /// </summary>
        void ApplyFaceVariant()
        {
            Transform top = FindDeep(transform, "DialTop");
            if (top == null) return;
            Renderer renderer = top.GetComponent<Renderer>();
            if (renderer == null) return;
            if (_faceDefault == null)
                _faceDefault = renderer.sharedMaterial;
            bool watercolour = HasVariant(IsWatercolourVariant);
            Material want = _faceDefault;
            if (watercolour && _layoutOn)
            {
                // The sprint face: the S1 paper, wash grain and pencil on the layout's remapped disc and band (T-SUN-050).
                if (_faceLayoutWatercolour == null) _faceLayoutWatercolour = Resources.Load<Material>(DialLayout.FaceWatercolourResource);
                if (_faceLayoutWatercolour == null)
                    throw new InvalidOperationException("variant=layout+watercolour needs Resources/" + DialLayout.FaceWatercolourResource + ".mat (run LayoutSetup.Run)");
                want = _faceLayoutWatercolour;
            }
            else if (watercolour)
            {
                if (faceWatercolour == null)
                    throw new InvalidOperationException("variant=watercolour needs DialView.faceWatercolour");
                want = faceWatercolour;
            }
            else if (_layoutOn)
            {
                if (_faceLayout == null) _faceLayout = Resources.Load<Material>(DialLayout.FaceResource);
                if (_faceLayout == null)
                    throw new InvalidOperationException("variant=layout needs Resources/" + DialLayout.FaceResource + ".mat (run LayoutSetup.Run)");
                want = _faceLayout;
            }
            if (want != null && renderer.sharedMaterial != want)
                renderer.sharedMaterial = want;
        }

        /// <summary>
        /// Look A leaves all of this off. variant=roomlight: the cookie multiply (global keyword), the wedge for the
        /// gnomon, one short wash per plant, and the table contact moved along the key light's travel.
        /// </summary>
        void ApplyRoomLight()
        {
            Transform catcher = FindDeep(transform, "TableShadowCatcher");
            Renderer catcherRenderer = catcher != null ? catcher.GetComponent<Renderer>() : null;
            if (!HasVariant(IsRoomLightVariant))
            {
                RoomLightGlobals.Clear();
                if (catcherRenderer != null) catcherRenderer.SetPropertyBlock(null);
                if (_roomShadowRoot != null) _roomShadowRoot.gameObject.SetActive(false);
                if (_roomLightApplied)
                {
                    // Give look A its shadow wash back. Apply only enables it in play mode.
                    _roomLightApplied = false;
                    if (shadow != null && !weekPage)
                    {
                        Renderer old = shadow.GetComponent<Renderer>();
                        if (old != null) old.enabled = true;
                    }
                }
                return;
            }
            if (roomCookie == null || roomShadow == null)
                throw new InvalidOperationException("variant=roomlight needs DialView.roomCookie and DialView.roomShadow");
            RoomLightGlobals.Apply(roomCookie, CookieWorldToDial(), RoomLightGlobals.Amplitude);
            _roomLightApplied = true;

            if (catcherRenderer != null)
            {
                Vector3 shift = transform.TransformVector(new Vector3(RoomKeyTravel.x, 0f, RoomKeyTravel.y)) * ContactAwayFromWindow;
                var block = new MaterialPropertyBlock();
                catcherRenderer.GetPropertyBlock(block);
                block.SetVector("_Offset", new Vector4(shift.x, 0f, shift.z, 0f));
                catcherRenderer.SetPropertyBlock(block);
            }

            EnsureRoomShadows();
            _roomShadowRoot.gameObject.SetActive(true);
            // The old shadow wash and the plant contact discs give way to the wedges.
            bool gnomonVisible = !weekPage && (!Application.isPlaying || appear >= 0.999f);
            if (shadow != null)
            {
                Renderer old = shadow.GetComponent<Renderer>();
                if (old != null) old.enabled = false;
            }
            if (contactRenderer != null) contactRenderer.enabled = false;

            float rad = gnomonDeg * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Cos(rad), 0f, -Mathf.Sin(rad));
            if (dir.sqrMagnitude < 1e-8f) dir = Vector3.forward;
            Quaternion aim = Quaternion.LookRotation(dir, Vector3.up);

            _gnomonWedge.localRotation = aim;
            _gnomonWedge.localPosition = new Vector3(0f, faceY + 0.0145f, 0f) + dir.normalized * 0.004f;
            _gnomonWedge.localScale = new Vector3(0.115f, 1f, 0.115f);
            _gnomonWedge.gameObject.SetActive(gnomonVisible && !stageStrip);

            for (int i = 0; i < _plantWedges.Length; i++)
            {
                Transform wedge = _plantWedges[i];
                Transform slot = plantSlots != null && i < plantSlots.Length ? plantSlots[i] : null;
                Renderer card = slot != null ? slot.GetComponent<Renderer>() : null;
                // A hero plant drawn as an assembly has its card hidden, but it still stands there and still throws its wash.
                bool standing = (card != null && card.enabled) || LeafAt(i) != null;
                bool on = standing && showPlants && !stageStrip && !weekPage;
                wedge.gameObject.SetActive(on);
                if (!on) continue;
                Vector3 baseLocal = slot.localPosition;
                wedge.localRotation = aim;
                wedge.localPosition = new Vector3(baseLocal.x, faceY + 0.0125f, baseLocal.z);
                // One short wash. Length follows the plant's height, width its card.
                wedge.localScale = new Vector3(slot.localScale.x * 0.62f, 1f, slot.localScale.y * 0.36f);
            }
        }

        /// <summary>
        /// The cookie is a pattern on the table, authored for the dial at its look A size. The layout scales the dial about its centre,
        /// so the cookie is read through the dial's look A transform (its layout-free scale and position), not the scaled one:
        /// otherwise the room's light and shade would stretch by 1 / <see cref="DialLayout.Scale"/> against the plate (T-SUN-050).
        /// </summary>
        Matrix4x4 CookieWorldToDial()
        {
            if (!_layoutOn || !_layoutBaseKnown) return transform.worldToLocalMatrix;
            Matrix4x4 parent = transform.parent != null ? transform.parent.localToWorldMatrix : Matrix4x4.identity;
            Matrix4x4 lookA = parent * Matrix4x4.TRS(_layoutBasePos, transform.localRotation, _layoutBaseScale);
            return lookA.inverse;
        }

        void EnsureRoomShadows()
        {
            if (_roomShadowRoot != null) return;
            var root = new GameObject("RoomLightShadows") { hideFlags = RuntimeOnly };
            root.transform.SetParent(transform, false);
            _roomShadowRoot = root.transform;
            _wedgeMesh = new Mesh { name = "RoomShadowWedge", hideFlags = HideFlags.DontSave };
            _wedgeMesh.SetVertices(new[]
            {
                new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f),
                new Vector3(0.5f, 0f, 1f), new Vector3(-0.5f, 0f, 1f)
            });
            _wedgeMesh.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) });
            _wedgeMesh.SetColors(new[] { Color.white, Color.white, Color.white, Color.white });
            _wedgeMesh.SetNormals(new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            _wedgeMesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            _wedgeMesh.RecalculateBounds();
            _gnomonWedge = NewWedge("GnomonWedge");
            _plantWedges = new Transform[ArcCount * RowsPerArc];
            for (int i = 0; i < _plantWedges.Length; i++)
                _plantWedges[i] = NewWedge("PlantWedge." + i);
        }

        Transform NewWedge(string name)
        {
            var go = new GameObject(name) { hideFlags = RuntimeOnly };
            go.transform.SetParent(_roomShadowRoot, false);
            go.AddComponent<MeshFilter>().sharedMesh = _wedgeMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = roomShadow;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go.transform;
        }

        /// <summary>
        /// The second page covers the day. Plants, tiles and the gnomon would poke through a flat sheet,
        /// so they step aside until the page turns back. The face and the rim stay.
        /// </summary>
        void CoverDayDrawing()
        {
            if (tileRenderer != null) tileRenderer.enabled = false;
            if (haloRenderer != null) haloRenderer.enabled = false;
            if (poolRenderer != null) poolRenderer.enabled = false;
            if (contactRenderer != null) contactRenderer.enabled = false;
            if (shadow != null)
            {
                Renderer drawn = shadow.GetComponent<Renderer>();
                if (drawn != null) drawn.enabled = false;
            }
            EnableNamed("Gnomon", false);
            EnableNamed("Soil", false);
            if (uprightCards != null)
            {
                for (int i = 0; i < uprightCards.Length; i++)
                {
                    if (uprightCards[i] == null) continue;
                    Renderer renderer = uprightCards[i].GetComponent<Renderer>();
                    if (renderer != null) renderer.enabled = false;
                }
            }
            if (bloomRenderers != null)
            {
                for (int i = 0; i < bloomRenderers.Length; i++)
                {
                    if (bloomRenderers[i] != null) bloomRenderers[i].enabled = false;
                }
            }
            if (stripRenderers != null)
            {
                for (int i = 0; i < stripRenderers.Length; i++)
                {
                    if (stripRenderers[i] != null) stripRenderers[i].enabled = false;
                }
            }
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
            speciesCards = library.SpeciesCards;
            haloTextures = library.Halos;
            bloomTextures = library.BloomCards;
            plantMats = new[] { library.Morning, library.Midday, library.WindDown };

            var cards = new List<Transform>();
            plantSlots = new Transform[ArcCount * RowsPerArc];
            for (int arc = 0; arc < ArcCount; arc++)
            {
                Vector2 spot = Spot(arc);
                Vector3 pos = new Vector3(spot.x * faceRadius, faceY + 0.004f, spot.y * faceRadius);
                var card = Card(transform, ArcIds[arc], plantMats[arc], pos, PlantSize[arc], library.Cross);
                card.name = "plant." + ArcIds[arc];
                var box = card.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0.5f, 0.02f);
                box.size = new Vector3(1.05f, 1f, 0.42f);
                var target = card.AddComponent<IntentTarget>();
                target.Id = "plant." + ArcIds[arc];
                cards.Add(card.transform);
                plantSlots[arc * RowsPerArc] = card.transform;
            }
            for (int arc = 0; arc < ArcCount; arc++)
            {
                for (int row = 1; row < RowsPerArc; row++)
                {
                    Vector2 spot = Spot(arc);
                    Vector3 pos = new Vector3(spot.x * faceRadius, faceY + 0.004f, spot.y * faceRadius);
                    string id = "plant." + ArcIds[arc] + ".r" + row;
                    var card = Card(transform, id, plantMats[arc], pos, PlantSize[arc] * 0.62f, library.Cross);
                    var box = card.AddComponent<BoxCollider>();
                    box.center = new Vector3(0f, 0.5f, 0.02f);
                    box.size = new Vector3(1.05f, 1f, 0.42f);
                    box.enabled = false;
                    var target = card.AddComponent<IntentTarget>();
                    target.Id = id;
                    var renderer = card.GetComponent<Renderer>();
                    if (renderer != null) renderer.enabled = false;
                    cards.Add(card.transform);
                    plantSlots[arc * RowsPerArc + row] = card.transform;
                }
            }

            var blooms = new Renderer[3];
            for (int arc = 0; arc < 3; arc++)
            {
                Vector2 spot = Spot(arc);
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
                int within = i % TilesPerArc;
                int row = within / DaysPerRow;
                int day = within % DaysPerRow;
                float deg = RowSlot(arc, row, day);
                float radius;
                float t0;
                float t1;
                float halfL;
                float halfW;
                RowLayout(row, out radius, out t0, out t1, out halfL, out halfW);
                radius = TileRadiusFor(row, radius);
                Vector3 pos = OnFace(deg, faceRadius * radius, faceY + 0.001f);
                string id = row == 0
                    ? "tile." + ArcIds[arc] + "." + day
                    : "tile." + ArcIds[arc] + ".r" + row + "." + day;
                var proxy = new GameObject(id);
                proxy.transform.SetParent(transform, false);
                proxy.transform.localPosition = pos;
                proxy.transform.localRotation = TileRotation(deg);
                var box = proxy.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0.004f, 0f);
                box.size = new Vector3(halfL * 2.2f, 0.01f, halfW * 2.4f);
                var target = proxy.AddComponent<IntentTarget>();
                target.Id = id;
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
            Vector3 view = cam.transform.forward;
            bool moved = (view - _viewForward).sqrMagnitude > 1e-4f;
            _viewForward = view;
            if (moved && AnyLeafOn()) RefreshLeafHalo();
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
            Transform haloPlant = HaloPlant();
            if (haloRenderer != null && haloPlant != null)
                haloRenderer.transform.rotation = haloPlant.rotation;
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
            if (GetComponent<GratitudeRitualController>() == null)
                gameObject.AddComponent<GratitudeRitualController>();
            if (GetComponent<FocusBlockController>() == null)
                gameObject.AddComponent<FocusBlockController>();
            if (GetComponent<WeekDialController>() == null)
                gameObject.AddComponent<WeekDialController>();
            if (GetComponent<RimScrubController>() == null)
                gameObject.AddComponent<RimScrubController>();
            if (GetComponent<ArcTimesController>() == null)
                gameObject.AddComponent<ArcTimesController>();
        }

        void OnEnable()
        {
            HookCamera();
            Apply();
        }

        void OnDestroy()
        {
            for (int i = 0; i < _leaves.Length; i++)
                if (_leaves[i] != null) _leaves[i].Destroy();
            if (_mound != null) _mound.Destroy();
            if (_halo2 != null) _halo2.Destroy();
            if (_contactSwapped != null) DestroyObject(_contactSwapped);
            if (_leafSilhouette != null) DestroyObject(_leafSilhouette);
            if (_tilesLayout != null) DestroyObject(_tilesLayout);
            if (_contactLayout != null) DestroyObject(_contactLayout);
        }

        void OnDisable()
        {
            if (HasVariant(IsRoomLightVariant)) RoomLightGlobals.Clear();
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
            if (ApplyRowKey(key, value)) return;
            switch (key)
            {
                case "halo": halo = ParseFloat(key, value); break;
                // Off hides the three hero plant cards (and their contact shadows), so a capture can read where the plants are.
                case "plants":
                    // 0 hides the plants, 1 shows them, an arc name shows that arc's plants alone (a capture reads one plant's outline).
                    if (TryArcIndex(value) >= 0) { showPlants = true; plantsOnlyArc = TryArcIndex(value); }
                    else { showPlants = ParseBool(value); plantsOnlyArc = -1; }
                    break;
                case "spill": halo2Spill = ParseFloat(key, value); break;
                case "isolate":
                    isolateDial = string.Equals(value, "dial", StringComparison.OrdinalIgnoreCase);
                    isolateSoil = string.Equals(value, "soil", StringComparison.OrdinalIgnoreCase);
                    isolateHalo = string.Equals(value, "halo", StringComparison.OrdinalIgnoreCase) || (!isolateDial && !isolateSoil && ParseBoolLoose(value));
                    break;
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
                case "tiles.morning": WriteTileDigits(TileIndex(0, 0, 0), value); break;
                case "tiles.midday": WriteTileDigits(TileIndex(1, 0, 0), value); break;
                case "tiles.winddown": WriteTileDigits(TileIndex(2, 0, 0), value); break;
                case "tiles": WriteTileDigits(0, value); break;
                case "rows":
                    if (value == "3") ShowAllRows();
                    else if (value == "1") ShowHeroRows();
                    break;
                case "stages": stageStrip = string.Equals(value, "strip", StringComparison.OrdinalIgnoreCase); break;
                case "waiting": waiting = Mathf.Clamp01(ParseFloat(key, value)); break;
                case "waitingTarget": waitingTarget = string.IsNullOrEmpty(value) ? "midday" : value; break;
                case "pulse": pulse = Mathf.Clamp01(ParseFloat(key, value)); break;
                case "pulseArc": pulseArc = Mathf.Clamp(Mathf.RoundToInt(ParseFloat(key, value)), 0, 2); break;
                case "reducedMotion": reducedMotion = ParseBool(value); break;
                case "variant":
                    if (!IsKnownVariant(value))
                        throw new FormatException("DialView variant is not a, sprint, watercolour, roomlight, leafplant, soilmound, halo2 or layout (join with +): " + value);
                    _variant = string.IsNullOrEmpty(value) ? VariantDefault : value.Trim();
                    break;
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

        void ApplyPlants()
        {
            EnsurePlantArrays();
            plantStage[0] = stageMorning;
            plantStage[3] = stageMidday;
            plantStage[6] = stageWinddown;
            for (int slot = 0; slot < ArcCount * RowsPerArc; slot++)
            {
                int arc = slot / RowsPerArc;
                int row = slot % RowsPerArc;
                float bloom = row == 0 ? BloomFor(arc) : 0f;
                ApplyPlantSlot(slot, plantStage[slot], bloom);
            }
        }

        float BloomFor(int arc)
        {
            if (arc == 0) return bloomMorning;
            if (arc == 1) return bloomMidday;
            return bloomWinddown;
        }

        void ApplyPlantSlot(int slot, int stage, float bloom)
        {
            Transform plant = PlantTransform(slot);
            if (plant == null) return;
            int arc = slot / RowsPerArc;
            int row = slot % RowsPerArc;
            bool on = SlotOn(slot);
            int card = Mathf.Clamp(stage, 0, 4);
            if (row == 0) bloom = PresentBloom(arc, bloom);
            Renderer renderer = plant.GetComponent<Renderer>();
            Color tint = PlantTint(slot);
            Texture2D tex = CardTex(slot, card);
            if (renderer != null)
            {
                renderer.enabled = on;
                if (on && tex != null) SetMain(renderer, tex, tint);
            }
            Collider box = plant.GetComponent<Collider>();
            if (box != null) box.enabled = on;
            PlacePlant(slot, plant);
            if (row != 0 || bloomRenderers == null || arc >= bloomRenderers.Length || bloomRenderers[arc] == null) return;
            Renderer bloomRenderer = bloomRenderers[arc];
            int which = bloom < 0.5f ? -1 : bloom < 1.5f ? 0 : 1;
            int bloomTex = arc * 2 + which;
            bool show = on && which >= 0 && bloomTextures != null && bloomTex < bloomTextures.Length && bloomTextures[bloomTex] != null;
            bloomRenderer.enabled = show;
            Transform bloomTransform = bloomRenderer.transform;
            bloomTransform.localPosition = plant.localPosition + new Vector3(0f, 0.0015f, 0f);
            bloomTransform.localScale = plant.localScale;
            if (show) SetMain(bloomRenderer, bloomTextures[bloomTex]);
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
            if (pulse <= 0.0001f || stageStrip) return;
            int slot = pulseSlot >= 0 ? pulseSlot : pulseArc * RowsPerArc;
            Transform plant = PlantTransform(slot);
            if (plant == null || reducedMotion) return;
            float scale = 1f + 0.3f * Mathf.Sin(Mathf.Clamp01(pulse) * Mathf.PI);
            plant.localScale = plant.localScale * scale;
            int arc = slot / RowsPerArc;
            int row = slot % RowsPerArc;
            if (row == 0 && bloomRenderers != null && arc < bloomRenderers.Length && bloomRenderers[arc] != null)
                bloomRenderers[arc].transform.localScale = plant.localScale;
        }

        Color PlantTint(int slot)
        {
            Color tint = Color.white;
            bool warm = false;
            if (AnyDue())
                warm = plantDue != null && slot >= 0 && slot < plantDue.Length && plantDue[slot];
            else
            {
                int waitArc = TryArcIndex(waitingTarget);
                warm = waiting > 0.01f && slot / RowsPerArc == waitArc && slot % RowsPerArc == 0;
            }
            if (warm)
            {
                // 2.6 s breath. Reduced motion holds a steady warm step.
                // One waiting hero stays on the old pale lift. Several due plants go gold,
                // or a nine-plant face cannot say which ones are waiting.
                float wave = reducedMotion ? 1f : 0.5f + 0.5f * Mathf.Sin(time * (Mathf.PI * 2f / 2.6f));
                if (AnyDue())
                    tint = Color.Lerp(new Color(1.08f, 1.0f, 0.82f), new Color(1.34f, 1.05f, 0.48f), 0.45f + 0.55f * wave);
                else
                    tint = new Color(1.06f, 1.0f, 0.88f) * (1f + 0.32f * wave * Mathf.Clamp01(waiting));
            }
            int pulseAt = pulseSlot >= 0 ? pulseSlot : pulseArc * RowsPerArc;
            if (reducedMotion && pulseAt == slot && pulse > 0.02f && pulse < 0.999f)
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

        Texture2D HaloMask(Texture2D plant, Texture2D extra, int bloomLiftPx)
        {
            string key = plant != null ? plant.name : "";
            if (extra != null) key = key + "+" + extra.name + "@" + bloomLiftPx;
            // The layout closes the plant's gaps with a wider brush, so the line hugs the plant as one outline (look A: HaloClosePx).
            int closePx = _layoutOn ? DialLayout.HaloClosePx : Mathf.RoundToInt(HaloClosePx);
            if (closePx != Mathf.RoundToInt(HaloClosePx)) key = key + "#c" + closePx;
            Texture2D cached;
            if (_haloMasks.TryGetValue(key, out cached) && cached != null) return cached;
            Texture2D made = BakeHaloMask(plant, extra, bloomLiftPx, closePx);
            _haloMasks[key] = made;
            return made;
        }

        /// <summary>
        /// Close the plant alpha (and the bloom, lifted to where that card is drawn),
        /// then keep a thin ring just outside that shape. R is the core, G is the short falloff.
        /// </summary>
        static Texture2D BakeHaloMask(Texture2D plant, Texture2D extra, int bloomLiftPx, int closePx)
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
            int lift = bloomLiftPx > 0 ? bloomLiftPx : 0;
            for (int y = 0; y < h; y++)
            {
                int sy = y - lift;
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    int a = plantPx[i].a;
                    if (extraPx != null && sy >= 0 && sy < h)
                    {
                        int ea = extraPx[sy * w + x].a;
                        if (ea > a) a = ea;
                    }
                    on[i] = a >= 128;
                }
            }
            // A short close bridges a hairline gap. A wide close turned the flowering plant
            // into one blob, and the outline stopped reading as a stroke around the leaves.
            bool[] closed = CloseMask(on, w, h, closePx);
            int[] dist = DistanceToOn(closed, w, h);
            var pixels = new Color32[on.Length];
            float corePx = HaloCorePx;
            float glowPx = HaloGlowPx;
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
            Transform plant = HaloPlant();
            if (plant == null && arc >= 0 && arc < ArcCount && uprightCards != null && arc < uprightCards.Length)
                plant = uprightCards[arc];
            if (haloRenderer == null || plant == null) return;
            Transform haloTransform = haloRenderer.transform;
            haloTransform.localRotation = plant.localRotation;
            // The card's +Z faces the camera. The push clears the plant's depth write so the
            // gold sits on the silhouette instead of behind the leaves. Same scale as the plant:
            // a larger card bulged past the drawing and hid the core.
            Vector3 face = haloTransform.localRotation * Vector3.forward;
            haloTransform.localPosition = plant.localPosition + face * HaloPush + Vector3.up * 0.001f;
            Vector3 plantScale = plant.localScale;
            haloTransform.localScale = new Vector3(plantScale.x * HaloFit, plantScale.y * HaloFit, plantScale.z * HaloFit);
            if (_layoutOn && LeafAt(haloSlot >= 0 ? haloSlot : arc * RowsPerArc) != null)
            {
                // The canvas is bigger than the card; the quad grows to match and the base stays where the plant's base is.
                Vector4 c = DialLayout.HaloCanvas;
                haloTransform.localScale = new Vector3(plantScale.x * HaloFit * (c.z - c.x), plantScale.y * HaloFit * (c.w - c.y), plantScale.z * HaloFit);
                haloTransform.localPosition += Vector3.up * (c.y * plantScale.y * HaloFit);
            }
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
            return BuildContactMesh(0);
        }

        /// <summary>The contact discs under the three hero plants. Bit n of the mask leaves out arc n disc (its assembly has its own).</summary>
        Mesh BuildContactMesh(int skipMask)
        {
            var verts = new List<Vector3>(12);
            var uv = new List<Vector2>(12);
            var colors = new List<Color>(12);
            var tris = new List<int>(18);
            for (int arc = 0; arc < 3; arc++)
            {
                if ((skipMask & (1 << arc)) != 0) continue;
                Vector2 spot = Spot(arc);
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
            EnsureTiles();
            if (tiles == null || tiles.Length != TileCount)
                throw new InvalidOperationException("DialView.tiles must hold 63 values");
            var pixels = new Color32[TileCount];
            for (int i = 0; i < TileCount; i++)
            {
                int raw = tiles[i];
                bool hidden = raw < 0;
                int state = hidden ? 0 : Mathf.Clamp(raw, 0, 4);
                int arc = i / TilesPerArc;
                float fill = 1f;
                if (tileFill != null && i < tileFill.Length) fill = Mathf.Clamp01(tileFill[i]);
                byte flood = (byte)Mathf.RoundToInt(fill * 255f);
                byte alpha = hidden ? (byte)0 : (byte)255;
                pixels[i] = new Color32((byte)Mathf.RoundToInt(state / 4f * 255f), (byte)Mathf.RoundToInt(arc / 2f * 255f), flood, alpha);
            }
            _stateTex.SetPixels32(pixels);
            _stateTex.Apply(false, false);
        }

        Mesh CombineTiles(Mesh source)
        {
            if (source == null || !source.isReadable) throw new InvalidOperationException("tile mesh is not readable");
            return PlaceTiles();
        }

        Mesh PlaceTiles()
        {
            // One combined mesh, one draw. Each tile lies flat on the rim: 12 x 9 mm,
            // top 2 mm above the paper, 1.8 mm of solid thickness (the bottom clears the
            // face by 0.2 mm). Sides are marked with UV3.y = 0 so the tile shader inks them.
            // Vertex order per tile is 4 top corners, then 4 sides of 4. The A2 measure reads that.
            // The source mesh only has to exist; the bevelled cube stood on edge.
            const float topRaise = 0.002f;
            const float clear = 0.0002f;
            const int vertsPerTile = 20;
            var verts = new List<Vector3>(TileCount * vertsPerTile);
            var normals = new List<Vector3>(TileCount * vertsPerTile);
            var uv = new List<Vector2>(TileCount * vertsPerTile);
            var nxy = new List<Vector2>(TileCount * vertsPerTile);
            var nz = new List<Vector2>(TileCount * vertsPerTile);
            var tileUv = new List<Vector2>(TileCount * vertsPerTile);
            var colors = new List<Color>(TileCount * vertsPerTile);
            var tris = new List<int>(TileCount * 30);
            var upCol = new Color(0.5f, 1f, 0.5f, 1f);

            for (int i = 0; i < TileCount; i++)
            {
                int arc = i / TilesPerArc;
                int within = i % TilesPerArc;
                int row = within / DaysPerRow;
                float radius;
                float t0;
                float t1;
                float halfL;
                float halfW;
                RowLayout(row, out radius, out t0, out t1, out halfL, out halfW);
                radius = TileRadiusFor(row, radius);
                float deg = RowSlot(arc, row, within % DaysPerRow);
                float rad = deg * Mathf.Deg2Rad;
                float topY = faceY + topRaise;
                float botY = faceY + clear;
                Vector3 center = OnFace(deg, faceRadius * radius, topY);
                var tangent = new Vector3(Mathf.Sin(rad), 0f, -Mathf.Cos(rad));
                var radial = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
                Vector3[] top =
                {
                    center - tangent * halfL - radial * halfW,
                    center + tangent * halfL - radial * halfW,
                    center + tangent * halfL + radial * halfW,
                    center - tangent * halfL + radial * halfW
                };
                var bot = new Vector3[4];
                for (int v = 0; v < 4; v++) bot[v] = new Vector3(top[v].x, botY, top[v].z);
                Vector2[] cornerUv =
                {
                    new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f)
                };
                AddTileFace(verts, normals, uv, nxy, nz, tileUv, colors, tris, top, Vector3.up, cornerUv, i, 1f, upCol);
                AddTileSide(verts, normals, uv, nxy, nz, tileUv, colors, tris, top, bot, 0, 1, -radial, i, upCol);
                AddTileSide(verts, normals, uv, nxy, nz, tileUv, colors, tris, top, bot, 1, 2, tangent, i, upCol);
                AddTileSide(verts, normals, uv, nxy, nz, tileUv, colors, tris, top, bot, 2, 3, radial, i, upCol);
                AddTileSide(verts, normals, uv, nxy, nz, tileUv, colors, tris, top, bot, 3, 0, -tangent, i, upCol);
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

        static void AddTileFace(
            List<Vector3> verts, List<Vector3> normals, List<Vector2> uv, List<Vector2> nxy, List<Vector2> nz,
            List<Vector2> tileUv, List<Color> colors, List<int> tris,
            Vector3[] corner, Vector3 normal, Vector2[] cornerUv, int tile, float topFlag, Color col)
        {
            int bas = verts.Count;
            Vector2 nxyV, nzV;
            PackNormal(normal, out nxyV, out nzV);
            for (int v = 0; v < 4; v++)
            {
                verts.Add(corner[v]);
                normals.Add(normal);
                uv.Add(cornerUv[v]);
                nxy.Add(nxyV);
                nz.Add(nzV);
                tileUv.Add(new Vector2(tile, topFlag));
                colors.Add(col);
            }
            tris.Add(bas + 0);
            tris.Add(bas + 2);
            tris.Add(bas + 1);
            tris.Add(bas + 0);
            tris.Add(bas + 3);
            tris.Add(bas + 2);
        }

        static void AddTileSide(
            List<Vector3> verts, List<Vector3> normals, List<Vector2> uv, List<Vector2> nxy, List<Vector2> nz,
            List<Vector2> tileUv, List<Color> colors, List<int> tris,
            Vector3[] top, Vector3[] bot, int a, int b, Vector3 outward, int tile, Color col)
        {
            Vector3[] corner = { bot[a], bot[b], top[b], top[a] };
            Vector2[] sideUv =
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f)
            };
            AddTileFace(verts, normals, uv, nxy, nz, tileUv, colors, tris, corner, outward.normalized, sideUv, tile, 0f, col);
        }

        static void PackNormal(Vector3 normal, out Vector2 nxy, out Vector2 nz)
        {
            Vector3 n = normal.sqrMagnitude > 1e-8f ? normal.normalized : Vector3.up;
            nxy = new Vector2(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f);
            nz = new Vector2(n.z * 0.5f + 0.5f, 0f);
        }

        static float ArcMid(int arc)
        {
            float a0, a1;
            ArcEnds(arc, out a0, out a1);
            return a0 - Mathf.Repeat(a0 - a1, 360f) * 0.5f;
        }

        public static int TileIndex(int arc, int row, int day)
        {
            if (arc < 0) arc = 0;
            if (arc > 2) arc = 2;
            if (row < 0) row = 0;
            if (row > 2) row = 2;
            if (day < 0) day = 0;
            if (day > 6) day = 6;
            return arc * TilesPerArc + row * DaysPerRow + day;
        }

        static void RowLayout(int row, out float radius, out float t0, out float t1, out float halfL, out float halfW)
        {
            if (row <= 0)
            {
                radius = TileRadius;
                t0 = 0.12f;
                t1 = 0.88f;
                halfL = 0.006f;
                halfW = 0.0045f;
                return;
            }
            if (row == 1)
            {
                radius = 0.70f;
                t0 = 0.20f;
                t1 = 0.80f;
                halfL = 0.0046f;
                halfW = 0.0034f;
                return;
            }
            radius = 0.58f;
            t0 = 0.28f;
            t1 = 0.72f;
            halfL = 0.0036f;
            halfW = 0.0028f;
        }

        static float RowSlot(int arc, int row, int day)
        {
            float a0, a1;
            ArcEnds(arc, out a0, out a1);
            float span = Mathf.Repeat(a0 - a1, 360f);
            float t0, t1, radius, halfL, halfW;
            RowLayout(row, out radius, out t0, out t1, out halfL, out halfW);
            float t = day >= 6 ? t1 : t0 + (t1 - t0) * (day / 6f);
            return a0 - span * t;
        }

        static float ArcSlot(int arc, int slot)
        {
            return RowSlot(arc, 0, slot);
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
            EnsureTiles();
            if (string.IsNullOrEmpty(digits)) throw new FormatException("DialView tiles are empty");
            if (start == 0 && digits.Length == 21)
            {
                WriteLegacyRow(digits);
                return;
            }
            int count = start == 0 && digits.Length == TileCount ? TileCount : DaysPerRow;
            if (digits.Length < count) throw new FormatException("DialView tiles need " + count + " digits: " + digits);
            for (int i = 0; i < count; i++)
                tiles[start + i] = Digit(digits[i]);
        }

        void WriteLegacyRow(string digits)
        {
            for (int i = 0; i < 21; i++)
            {
                int arc = i / DaysPerRow;
                int day = i % DaysPerRow;
                tiles[TileIndex(arc, 0, day)] = Digit(digits[i]);
            }
        }

        static int Digit(char c)
        {
            if (c < '0' || c > '4') throw new FormatException("DialView tile digit is not 0..4: " + c);
            return c - '0';
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

        public static int[] DefaultTiles()
        {
            var next = new int[TileCount];
            for (int i = 0; i < TileCount; i++) next[i] = HiddenTile;
            int[] row0 = { 1, 1, 3, 1, 2, 1, 1, 1, 2, 3, 1, 1, 3, 4, 3, 1, 1, 2, 1, 3, 4 };
            for (int i = 0; i < row0.Length; i++)
            {
                int arc = i / DaysPerRow;
                int day = i % DaysPerRow;
                next[TileIndex(arc, 0, day)] = row0[i];
            }
            return next;
        }

        public void EnsureTiles()
        {
            if (tiles != null && tiles.Length == TileCount) return;
            int[] next = DefaultTiles();
            if (tiles != null && tiles.Length == 21)
            {
                for (int i = 0; i < TileCount; i++) next[i] = HiddenTile;
                for (int i = 0; i < 21; i++)
                {
                    int arc = i / DaysPerRow;
                    int day = i % DaysPerRow;
                    next[TileIndex(arc, 0, day)] = tiles[i];
                }
            }
            tiles = next;
        }

        public void EnsurePlantArrays()
        {
            if (plantStage == null || plantStage.Length != 9)
            {
                plantStage = new int[9];
                for (int i = 0; i < plantStage.Length; i++) plantStage[i] = 4;
            }
            if (plantOn == null || plantOn.Length != 9)
            {
                plantOn = new bool[9];
                plantOn[0] = true;
                plantOn[3] = true;
                plantOn[6] = true;
            }
            if (plantDue == null || plantDue.Length != 9) plantDue = new bool[9];
            if (plantSpecies == null || plantSpecies.Length != 9)
            {
                plantSpecies = new int[9];
                for (int i = 0; i < plantSpecies.Length; i++) plantSpecies[i] = -1;
            }
        }

        void ShowAllRows()
        {
            EnsurePlantArrays();
            for (int i = 0; i < plantOn.Length; i++) plantOn[i] = true;
        }

        void ShowHeroRows()
        {
            EnsurePlantArrays();
            for (int i = 0; i < plantOn.Length; i++) plantOn[i] = i % RowsPerArc == 0;
        }

        bool ApplyRowKey(string key, string value)
        {
            string arc;
            int row;
            bool hasRow;
            if (SplitSlot(key, "tiles.", out arc, out row, out hasRow) && hasRow)
            {
                int arcIndex = TryArcIndex(arc);
                if (arcIndex < 0) return false;
                WriteTileDigits(TileIndex(arcIndex, row, 0), value);
                return true;
            }
            if (SplitSlot(key, "stage.", out arc, out row, out hasRow) && hasRow)
            {
                int arcIndex = TryArcIndex(arc);
                if (arcIndex < 0) return false;
                EnsurePlantArrays();
                plantStage[arcIndex * RowsPerArc + row] = ParseStage(value);
                return true;
            }
            if (SplitSlot(key, "due.", out arc, out row, out hasRow))
            {
                int arcIndex = TryArcIndex(arc);
                if (arcIndex < 0) return false;
                EnsurePlantArrays();
                bool on = ParseBool(value);
                if (hasRow) plantDue[arcIndex * RowsPerArc + row] = on;
                else
                {
                    for (int r = 0; r < RowsPerArc; r++) plantDue[arcIndex * RowsPerArc + r] = on;
                }
                TouchDue();
                return true;
            }
            return false;
        }

        void TouchDue()
        {
            bool any = false;
            int arc = 1;
            if (plantDue != null)
            {
                for (int i = 0; i < plantDue.Length; i++)
                {
                    if (!plantDue[i]) continue;
                    any = true;
                    arc = i / RowsPerArc;
                    break;
                }
            }
            waiting = any ? 1f : waiting;
            if (any && arc >= 0 && arc < ArcIds.Length) waitingTarget = ArcIds[arc];
        }

        static bool SplitSlot(string key, string prefix, out string arc, out int row, out bool hasRow)
        {
            arc = null;
            row = 0;
            hasRow = false;
            if (string.IsNullOrEmpty(key) || !key.StartsWith(prefix, StringComparison.Ordinal)) return false;
            string rest = key.Substring(prefix.Length);
            int dot = rest.IndexOf('.');
            if (dot < 0)
            {
                arc = rest;
                return TryArcIndex(arc) >= 0;
            }
            arc = rest.Substring(0, dot);
            string tail = rest.Substring(dot + 1);
            if (tail.Length == 1 && (tail[0] == '1' || tail[0] == '2') && TryArcIndex(arc) >= 0)
            {
                row = tail[0] - '0';
                hasRow = true;
                return true;
            }
            return false;
        }

        bool SlotOn(int slot)
        {
            if (stageStrip || !showPlants) return false;
            if (plantsOnlyArc >= 0 && slot / RowsPerArc != plantsOnlyArc) return false;
            int row = slot % RowsPerArc;
            if (plantOn == null || plantOn.Length != 9) return row == 0;
            return slot >= 0 && slot < plantOn.Length && plantOn[slot];
        }

        bool AnyDue()
        {
            if (plantDue == null) return false;
            for (int i = 0; i < plantDue.Length; i++)
            {
                if (plantDue[i]) return true;
            }
            return false;
        }

        Transform PlantTransform(int slot)
        {
            if (plantSlots != null && slot >= 0 && slot < plantSlots.Length && plantSlots[slot] != null)
                return plantSlots[slot];
            int arc = slot / RowsPerArc;
            int row = slot % RowsPerArc;
            if (row == 0 && uprightCards != null && arc >= 0 && arc < ArcCount && arc < uprightCards.Length)
                return uprightCards[arc];
            return null;
        }

        Transform HaloPlant()
        {
            int slot = haloSlot >= 0 ? haloSlot : _haloArc * RowsPerArc;
            return PlantTransform(slot);
        }

        void PlacePlant(int slot, Transform plant)
        {
            int arc = slot / RowsPerArc;
            int row = slot % RowsPerArc;
            if (arc < 0 || arc >= PlantSpot.Length) return;
            bool crowded = SlotOn(arc * RowsPerArc + 1) || SlotOn(arc * RowsPerArc + 2);
            Vector2 spot = Spot(arc);
            float len = spot.magnitude;
            Vector2 radial = len > 1e-4f ? spot / len : new Vector2(0f, 1f);
            Vector2 tangent = new Vector2(radial.y, -radial.x);
            float pull = 0f;
            float along = 0f;
            float scale = 1f;
            if (crowded)
            {
                pull = row == 0 ? 0.04f : 0.06f;
                if (row == 1) along = 0.34f;
                if (row == 2) along = -0.34f;
                scale = row == 0 ? 0.78f : 0.62f;
            }
            else if (row > 0)
            {
                pull = 0.06f;
                along = row == 1 ? 0.34f : -0.34f;
                scale = 0.62f;
            }
            Vector2 at = spot - radial * pull + tangent * along;
            plant.localPosition = new Vector3(at.x * faceRadius, faceY + 0.004f, at.y * faceRadius);
            Vector2 size = PlantSize[arc] * scale;
            plant.localScale = new Vector3(size.x, size.y, size.y);
        }

        Texture2D CardTex(int slot, int stage)
        {
            stage = Mathf.Clamp(stage, 0, 4);
            int species = DefaultSpecies(slot);
            if (plantSpecies != null && slot >= 0 && slot < plantSpecies.Length && plantSpecies[slot] >= 0)
                species = plantSpecies[slot];
            if (speciesCards != null)
            {
                int index = species * 5 + stage;
                if (index >= 0 && index < speciesCards.Length && speciesCards[index] != null)
                    return speciesCards[index];
            }
            int arc = slot / RowsPerArc;
            int row = slot % RowsPerArc;
            if (row != 0) return null;
            Texture2D[] set = arc == 0 ? morningCards : arc == 1 ? middayCards : windDownCards;
            if (set != null && stage < set.Length) return set[stage];
            return null;
        }

        static int DefaultSpecies(int slot)
        {
            switch (slot)
            {
                case 0: return 0;
                case 1: return 3;
                case 2: return 4;
                case 3: return 1;
                case 4: return 5;
                case 5: return 6;
                case 6: return 2;
                case 7: return 7;
                default: return 8;
            }
        }

        void ApplyTileColliders()
        {
            EnsureTiles();
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                int index;
                if (!TryTileName(child.name, out index)) continue;
                var box = child.GetComponent<Collider>();
                if (box == null) continue;
                box.enabled = index >= 0 && index < tiles.Length && tiles[index] >= 0;
            }
        }

        static bool TryTileName(string name, out int index)
        {
            index = -1;
            if (string.IsNullOrEmpty(name) || !name.StartsWith("tile.", StringComparison.Ordinal)) return false;
            string rest = name.Substring("tile.".Length);
            string[] bits = rest.Split('.');
            if (bits.Length == 2)
            {
                int arc = TryArcIndex(bits[0]);
                int day;
                if (arc < 0 || !int.TryParse(bits[1], out day) || day < 0 || day > 6) return false;
                index = TileIndex(arc, 0, day);
                return true;
            }
            if (bits.Length == 3 && bits[1].Length == 2 && bits[1][0] == 'r')
            {
                int arc = TryArcIndex(bits[0]);
                int row = bits[1][1] - '0';
                int day;
                if (arc < 0 || (row != 1 && row != 2) || !int.TryParse(bits[2], out day) || day < 0 || day > 6) return false;
                index = TileIndex(arc, row, day);
                return true;
            }
            return false;
        }

        static void DestroyObject(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }
    }
}

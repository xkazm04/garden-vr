using UnityEngine;

namespace GardenVR.Sundial
{
    /// <summary>A stem on a cubic spline: base, top, and how many leaf nodes it carries.</summary>
    public sealed class LeafStemDef
    {
        public Vector3 Base, Top;
        public int Nodes;
        public float Phase;

        public LeafStemDef(Vector3 b, Vector3 t, int nodes, float phase)
        {
            Base = b;
            Top = t;
            Nodes = nodes;
            Phase = phase;
        }
    }

    /// <summary>A flower or bud head on a stem. Face heads are face-on flowers, the others are cards that point along Dir (or along the stem).</summary>
    public sealed class LeafHeadDef
    {
        public int Stem;
        public float T;
        public Vector3 Dir;
        public bool Face;
        public float Scale;
        public bool AlongStem;

        public LeafHeadDef(int stem, float t, Vector3 dir, bool face, float scale = 1f, bool alongStem = false)
        {
            Stem = stem;
            T = t;
            Dir = dir;
            Face = face;
            Scale = scale;
            AlongStem = alongStem;
        }
    }

    /// <summary>An extra bud among the open heads: the plant is still opening.</summary>
    public sealed class LeafExtraBud
    {
        public int Stem, BudIndex;
        public float T, Scale, Roll, Phase;
        public Vector3 Dir;

        public LeafExtraBud(int stem, float t, Vector3 dir, float scale, float roll, float phase, int budIndex)
        {
            Stem = stem;
            T = t;
            Dir = dir;
            Scale = scale;
            Roll = roll;
            Phase = phase;
            BudIndex = budIndex;
        }
    }

    public enum LeafLayout
    {
        /// <summary>Opposite leaf pairs along the stems, turning 90 degrees per node.</summary>
        Opposite = 0,
        /// <summary>A clump of narrow blades fanned out from the base, and bare flowering stems.</summary>
        Basal = 1
    }

    /// <summary>
    /// What <see cref="LeafPlant"/> builds, per plant on the dial. T-SUN-049: the builder is species-agnostic, the species
    /// is data (sheet, scales, stem splines, head list, card caps). Lengths are metres in the dial's frame (x right, y up,
    /// z away from the viewer), before <see cref="Scale"/>.
    /// </summary>
    public sealed class LeafSpecies
    {
        /// <summary>The cap on drawn cards per plant (the dossier said 50, T-SUN-049 raised it to 60 to bush out midday).</summary>
        public const int CardCap = 60;

        public string Id;
        public int Arc;
        public int Seed;
        public float Scale = 1f;
        public float PlantHeight;
        // In the scaled (final) units, because normals are taken from the scaled vertices.
        public Vector3 EllipsoidCentre, EllipsoidRadius;
        public float LeafMetresPerPx, FaceFlowerMetresPerPx, SideFlowerMetresPerPx, BudMetresPerPx, TuftMetresPerPx, StemMetresPerPx;
        public float BudHeadScale = 1f;
        public LeafLayout Layout;
        public float NodeStart = 0.10f, NodeEnd = 0.78f;
        public float PitchBase = 72f, PitchTop = 46f;
        public float LeafDroop = 0.30f, LeafCup = 0.10f;
        public int SmallLeaves;
        public int BasalLeaves;
        public int StemRibbons = 1;
        public LeafStemDef[] Stems;
        public LeafHeadDef[] Heads;
        public LeafExtraBud[] ExtraBuds;
        public int TuftCount = 3;
        public bool TuftCrossed = true;
        public float[] TuftAngle = { 205f, 20f, 300f };
        public float[] TuftRadius = { 0.013f, 0.015f, 0.012f };
        public float ContactRx = 0.027f, ContactRz = 0.017f;

        public string MaterialResource { get { return "LeafPlant/Leaf_" + char.ToUpperInvariant(Id[0]) + Id.Substring(1); } }
        public string PartsResource { get { return "LeafPlant/" + Id + "-parts"; } }
        public string ObjectName { get { return "LeafPlant." + Id; } }

        public bool UsesFaceFlowers
        {
            get
            {
                foreach (LeafHeadDef h in Heads)
                    if (h.Face) return true;
                return false;
            }
        }

        public static readonly LeafSpecies Morning = BuildMorning();
        public static readonly LeafSpecies Midday = BuildMidday();
        public static readonly LeafSpecies Evening = BuildEvening();
        public static readonly LeafSpecies[] ByArc = { Morning, Midday, Evening };

        public static LeafSpecies ForArc(int arc)
        {
            return arc >= 0 && arc < ByArc.Length ? ByArc[arc] : null;
        }

        // The coral phlox-like herb of T-SUN-045. Authored at a plant 0.112 high and scaled to the 0.078 the card plant draws.
        // T-SUN-049: leaves start lower (shorter bare stems), a second small leaf class sits between the nodes, one stem
        // ribbon per stem. 60 cards in all.
        static LeafSpecies BuildMidday()
        {
            const float scale = 0.72f;
            return new LeafSpecies
            {
                Id = "midday",
                Arc = 1,
                Seed = 45,
                Scale = scale,
                PlantHeight = 0.112f,
                EllipsoidCentre = new Vector3(0f, 0.052f, 0f) * scale,
                EllipsoidRadius = new Vector3(0.044f, 0.064f, 0.044f) * scale,
                LeafMetresPerPx = 0.000125f,
                FaceFlowerMetresPerPx = 0.000125f,
                SideFlowerMetresPerPx = 0.00017f,
                BudMetresPerPx = 0.00013f,
                TuftMetresPerPx = 0.00015f,
                StemMetresPerPx = 0.000162f,
                BudHeadScale = 1.15f,
                Layout = LeafLayout.Opposite,
                NodeStart = 0.05f,
                NodeEnd = 0.80f,
                SmallLeaves = 11,
                StemRibbons = 1,
                Stems = new[]
                {
                    new LeafStemDef(new Vector3(-0.006f, 0f, 0.003f), new Vector3(-0.022f, 0.070f, 0.005f), 6, 0.00f),
                    new LeafStemDef(new Vector3(0.005f, 0f, -0.003f), new Vector3(0.010f, 0.092f, -0.004f), 5, 0.31f),
                    new LeafStemDef(new Vector3(0.001f, 0f, 0.007f), new Vector3(0.026f, 0.062f, 0.010f), 5, 0.62f)
                },
                Heads = new[]
                {
                    new LeafHeadDef(0, 1.00f, new Vector3(-0.2f, 1f, -0.1f), true, 1.00f),
                    new LeafHeadDef(1, 1.00f, new Vector3(0.1f, 1f, -0.2f), true, 1.00f),
                    new LeafHeadDef(2, 1.00f, new Vector3(0.45f, 1f, -0.1f), false, 1.00f),
                    new LeafHeadDef(0, 0.74f, new Vector3(-0.75f, 0.7f, -0.2f), false, 0.85f),
                    new LeafHeadDef(1, 0.78f, new Vector3(0.7f, 0.75f, -0.25f), false, 0.85f),
                    new LeafHeadDef(2, 0.76f, new Vector3(0.3f, 0.6f, -0.9f), true, 0.84f)
                },
                ExtraBuds = new[]
                {
                    new LeafExtraBud(1, 0.90f, new Vector3(-0.35f, 1f, -0.1f), 1.0f, 8f, 0.4f, 1),
                    new LeafExtraBud(0, 0.86f, new Vector3(0.25f, 1f, -0.3f), 0.9f, -12f, 0.7f, 2)
                }
            };
        }

        // Sunrise: an olive herb with many oval leaves and sprays of tiny buttercup flowers. Authored at its final size.
        static LeafSpecies BuildMorning()
        {
            return new LeafSpecies
            {
                Id = "morning",
                Arc = 0,
                Seed = 49,
                Scale = 1f,
                PlantHeight = 0.065f,
                EllipsoidCentre = new Vector3(0f, 0.032f, 0f),
                EllipsoidRadius = new Vector3(0.034f, 0.042f, 0.031f),
                LeafMetresPerPx = 0.000080f,
                FaceFlowerMetresPerPx = 0.000090f,
                SideFlowerMetresPerPx = 0.000110f,
                BudMetresPerPx = 0.000100f,
                TuftMetresPerPx = 0.000085f,
                StemMetresPerPx = 0.000090f,
                BudHeadScale = 1.0f,
                Layout = LeafLayout.Opposite,
                NodeStart = 0.04f,
                NodeEnd = 0.86f,
                PitchBase = 66f,
                PitchTop = 40f,
                LeafDroop = 0.22f,
                LeafCup = 0.12f,
                SmallLeaves = 0,
                StemRibbons = 1,
                Stems = new[]
                {
                    new LeafStemDef(new Vector3(-0.006f, 0f, 0.003f), new Vector3(-0.018f, 0.048f, 0.004f), 6, 0.00f),
                    new LeafStemDef(new Vector3(0.005f, 0f, -0.003f), new Vector3(0.006f, 0.054f, -0.004f), 6, 0.25f),
                    new LeafStemDef(new Vector3(0.001f, 0f, 0.007f), new Vector3(0.020f, 0.044f, 0.008f), 5, 0.50f),
                    new LeafStemDef(new Vector3(-0.002f, 0f, -0.006f), new Vector3(-0.004f, 0.036f, -0.007f), 4, 0.75f)
                },
                Heads = new[]
                {
                    new LeafHeadDef(0, 1.00f, new Vector3(-0.25f, 1f, -0.1f), false, 1.00f),
                    new LeafHeadDef(1, 1.00f, new Vector3(0.1f, 1f, -0.2f), true, 1.00f),
                    new LeafHeadDef(2, 1.00f, new Vector3(0.3f, 1f, -0.1f), false, 1.00f),
                    new LeafHeadDef(3, 1.00f, new Vector3(0.0f, 1f, -0.25f), true, 0.90f),
                    new LeafHeadDef(0, 0.74f, new Vector3(-0.8f, 0.7f, -0.2f), false, 0.85f),
                    new LeafHeadDef(1, 0.78f, new Vector3(0.75f, 0.75f, -0.25f), true, 0.85f),
                    new LeafHeadDef(2, 0.72f, new Vector3(0.8f, 0.6f, -0.3f), false, 0.85f),
                    new LeafHeadDef(0, 0.60f, new Vector3(0.3f, 0.6f, -0.9f), true, 0.80f)
                },
                ExtraBuds = new[]
                {
                    new LeafExtraBud(1, 0.90f, new Vector3(-0.35f, 1f, -0.1f), 1.0f, 8f, 0.4f, 1),
                    new LeafExtraBud(2, 0.86f, new Vector3(0.25f, 1f, -0.3f), 0.9f, -12f, 0.7f, 2)
                },
                TuftCount = 2,
                TuftAngle = new[] { 205f, 20f },
                TuftRadius = new[] { 0.012f, 0.014f },
                ContactRx = 0.026f,
                ContactRz = 0.016f
            };
        }

        // Dusk: lavender. A clump of narrow blades, seven bare stems, a violet spike on each.
        static LeafSpecies BuildEvening()
        {
            return new LeafSpecies
            {
                Id = "evening",
                Arc = 2,
                Seed = 51,
                Scale = 1f,
                PlantHeight = 0.070f,
                EllipsoidCentre = new Vector3(0f, 0.032f, 0f),
                EllipsoidRadius = new Vector3(0.036f, 0.042f, 0.034f),
                LeafMetresPerPx = 0.000100f,
                FaceFlowerMetresPerPx = 0.0001f,
                SideFlowerMetresPerPx = 0.000078f,
                BudMetresPerPx = 0.000090f,
                TuftMetresPerPx = 0.00009f,
                StemMetresPerPx = 0.00011f,
                BudHeadScale = 1.0f,
                Layout = LeafLayout.Basal,
                BasalLeaves = 40,
                LeafDroop = 0.20f,
                LeafCup = 0.05f,
                StemRibbons = 1,
                Stems = new[]
                {
                    new LeafStemDef(new Vector3(-0.010f, 0f, 0.004f), new Vector3(-0.020f, 0.038f, 0.006f), 0, 0.00f),
                    new LeafStemDef(new Vector3(-0.004f, 0f, -0.004f), new Vector3(-0.010f, 0.044f, -0.004f), 0, 0.15f),
                    new LeafStemDef(new Vector3(0.002f, 0f, 0.006f), new Vector3(0.002f, 0.046f, 0.008f), 0, 0.30f),
                    new LeafStemDef(new Vector3(0.006f, 0f, -0.002f), new Vector3(0.013f, 0.042f, 0.000f), 0, 0.45f),
                    new LeafStemDef(new Vector3(0.011f, 0f, 0.004f), new Vector3(0.021f, 0.036f, 0.006f), 0, 0.60f),
                    new LeafStemDef(new Vector3(-0.007f, 0f, -0.008f), new Vector3(-0.015f, 0.034f, -0.010f), 0, 0.75f),
                    new LeafStemDef(new Vector3(0.008f, 0f, -0.009f), new Vector3(0.016f, 0.032f, -0.010f), 0, 0.90f)
                },
                Heads = new[]
                {
                    new LeafHeadDef(0, 0.80f, Vector3.up, false, 1.00f, true),
                    new LeafHeadDef(1, 0.80f, Vector3.up, false, 1.05f, true),
                    new LeafHeadDef(2, 0.80f, Vector3.up, false, 1.10f, true),
                    new LeafHeadDef(3, 0.80f, Vector3.up, false, 1.00f, true),
                    new LeafHeadDef(4, 0.80f, Vector3.up, false, 0.95f, true),
                    new LeafHeadDef(5, 0.80f, Vector3.up, false, 0.90f, true),
                    new LeafHeadDef(6, 0.80f, Vector3.up, false, 0.92f, true)
                },
                ExtraBuds = new[]
                {
                    new LeafExtraBud(2, 0.55f, new Vector3(-0.35f, 1f, -0.1f), 0.8f, 8f, 0.4f, 1),
                    new LeafExtraBud(3, 0.50f, new Vector3(0.35f, 1f, -0.2f), 0.75f, -12f, 0.7f, 2)
                },
                TuftCount = 2,
                TuftAngle = new[] { 200f, 335f },
                TuftRadius = new[] { 0.012f, 0.013f },
                ContactRx = 0.028f,
                ContactRz = 0.018f
            };
        }
    }
}

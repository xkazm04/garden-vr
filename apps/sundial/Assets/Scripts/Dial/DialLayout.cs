using UnityEngine;

namespace GardenVR.Sundial
{
    /// <summary>
    /// T-SUN-052. The proportions of <c>variant=layout</c>: dial size against the hand, the soil bed that fills the face inside the
    /// arcs, the rim band and where the plants and tiles stand. All lengths are fractions of the face radius unless a name says
    /// metres. The habit record is untouched: the same 63 tiles on the same arcs and the same nine plant slots, only their radii
    /// and spots move. Look A (the default) never reads any of this.
    /// </summary>
    public static class DialLayout
    {
        public const string FaceResource = "Layout/Dial_Face_Layout";
        /// <summary>T-SUN-050. The same remap applied to the S1 face and its control map, so the watercolour paper and the layout stack.</summary>
        public const string FaceWatercolourResource = "Layout/Dial_Face_Layout_S1";

        /// <summary>
        /// Uniform scale of the whole dial about its centre. The DialG1 major axis of the silhouette is 1148.9 px at 1.0 and the
        /// reference's is 1008.3 px, a ratio of 0.8777; the measured silhouette at 0.8777 was 0.9925 of the reference, so 0.8843. The hand matte is the same pixels in both frames, so this is also the
        /// dial-to-knuckle-span ratio.
        /// </summary>
        public const float Scale = 0.8843f;

        /// <summary>Metres, in the dial's own frame before the scale. Sets the silhouette centre on the reference's.</summary>
        public static readonly Vector3 Offset = new Vector3(-0.0011f, 0f, -0.0015f);

        /// <summary>Card spots, x right, z away, in face radii. Morning and dusk stay inside their arcs. Midday stands 26 degrees clear of its arc (at -4 degrees, the arc starts at 22) so that it is on the bed, whose far edge is at z +0.02; its tiles, wash and slot are unchanged. Look A: (-0.30,-0.08) (0.16,0.18) (0.26,-0.16).</summary>
        public static readonly Vector2[] PlantSpot =
        {
            new Vector2(-0.46f, -0.20f),
            new Vector2(0.26f, -0.02f),
            new Vector2(0.46f, -0.36f)
        };

        /// <summary>
        /// The painted wash disc ends here and the rim band runs from here to the edge. Look A: the washes end at 0.731 and the shader
        /// ink ring sits at 0.862, a band 0.27 wide, most of it bare paper.
        /// </summary>
        public const float InnerRing = 0.82f;
        /// <summary>The shader ink ring, on the painted line at the inner edge of the band (look A: 0.862).</summary>
        public const float InkRing = 0.822f;

        /// <summary>Tile row radii (look A: 0.82, 0.70, 0.58). Row 0 sits in the band; the two inner rows stay on the wash.</summary>
        public static readonly float[] TileRowRadius = { 0.885f, 0.775f, 0.700f };

        // The bed is an ellipse in the dial plane, centred nearer the viewer than the gnomon: wide at the sides, shallow in depth,
        // so the washes stay as a far band and the near rim carries the pebbles. Look A's mound is a 0.50 radius circle at the centre.
        public const float BedCentreZ = -0.39f;
        public const float BedHalfX = 0.70f;
        public const float BedHalfZ = 0.41f;
        /// <summary>The mound's pebbles are scaled up with the bed (look A: 1).</summary>
        public const float PebbleScale = 1.6f;

        /// <summary>The affine map from the baked mound (a circle of <paramref name="moundRadius"/> metres at the origin) to the bed.</summary>
        public static void BedMap(float faceRadius, float moundRadius, out float sx, out float sz, out float oz)
        {
            sx = BedHalfX * faceRadius / moundRadius;
            sz = BedHalfZ * faceRadius / moundRadius;
            oz = BedCentreZ * faceRadius;
        }

        /// <summary>
        /// T-SUN-051. The same map for a baked bed that is not a circle: its box (half width <paramref name="halfX"/>, near and far z, metres)
        /// goes onto the box of the bed ellipse, so the crescent fills the layout's bed from its near rim to its far edge.
        /// </summary>
        public static void BedBoxMap(float faceRadius, float halfX, float z0, float z1, out float sx, out float sz, out float oz)
        {
            sx = BedHalfX * faceRadius / halfX;
            sz = 2f * BedHalfZ * faceRadius / (z1 - z0);
            oz = (BedCentreZ - BedHalfZ) * faceRadius - z0 * sz;
        }

        /// <summary>True when the point (face radii) is inside the bed ellipse.</summary>
        public static bool InsideBed(Vector2 p)
        {
            float u = p.x / BedHalfX;
            float v = (p.y - BedCentreZ) / BedHalfZ;
            return u * u + v * v < 1f;
        }

        /// <summary>
        /// Halo close radius in card texels (look A: 5). A wider close joins the leaves and flowers into one outline that follows the
        /// plant, instead of a scribble around every leaf. The stroke stays the T-SUN-031 lock (3 px core, 8 px glow, same gold).
        /// </summary>
        public const int HaloClosePx = 14;
        /// <summary>The halo canvas for a drawn-leaf plant, in card widths and heights from the card base: the plant is wider than its card.</summary>
        public static readonly Vector4 HaloCanvas = new Vector4(-0.95f, -0.16f, 0.95f, 1.12f);
        /// <summary>The soil patch the ring wraps under the base, as a flattened ellipse (card widths, card heights).</summary>
        public static readonly Vector2 HaloBaseDisc = new Vector2(0.36f, 0.05f);
    }
}

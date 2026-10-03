using System.Collections.Generic;
using GardenVR.Core;
using UnityEngine;

namespace GardenVR.Sundial
{
    /// <summary>
    /// The week page, drawn in the dial's ink and wash. Four rows of seven for each arc.
    /// A kept day takes that arc's wash. A late day is pencil hatch. A miss is a pale mark.
    /// Today is an open ring. A day before the habit existed is blank.
    /// Nothing here is red, and nothing counts a run of days.
    /// </summary>
    public static class WeekPageInk
    {
        public const int Size = 384;
        public const float MarkRadius = 0.0044f;
        public const float ColGap = 0.0132f;
        public const float RowGap = 0.0115f;

        public static readonly Color Paper = new Color(0.957f, 0.937f, 0.902f, 1f);
        public static readonly Color Ink = new Color(0.165f, 0.149f, 0.133f, 1f);
        public static readonly Color Pencil = new Color(0.541f, 0.506f, 0.471f, 1f);
        public static readonly Color MissWash = new Color(0.773f, 0.745f, 0.698f, 1f);

        /// <summary>Morning gold, midday coral, dusk lilac. The same washes as the day page.</summary>
        public static readonly Color[] ArcWash =
        {
            new Color(0.965f, 0.871f, 0.718f, 1f),
            new Color(0.957f, 0.714f, 0.631f, 1f),
            new Color(0.792f, 0.690f, 0.773f, 1f)
        };

        /// <summary>Far to near: morning, midday, wind-down. Dial local Z, metres.</summary>
        public static readonly float[] BandZ = { 0.048f, -0.004f, -0.056f };

        public const string Caption = "Four weeks";

        public static int ArcOf(HabitDef habit)
        {
            ArcId arc;
            if (habit == null || !SundialRules.TryArc(habit.Group, out arc)) return -1;
            if (arc == ArcId.Morning) return 0;
            if (arc == ArcId.Midday) return 1;
            return 2;
        }

        /// <summary>The words beside that plant. A habit we do not know by name keeps its arc, and no tally.</summary>
        public static string PlantLine(HabitDef habit)
        {
            if (habit == null) return "";
            if (habit.Id == "water" || habit.PresetKey == "water") return "Water";
            if (habit.Id == "top3" || habit.PresetKey == "top3") return "Top three";
            if (habit.Id == "breaths" || habit.PresetKey == "breaths") return "Breaths";
            int arc = ArcOf(habit);
            if (arc == 0) return "Morning";
            if (arc == 1) return "Midday";
            if (arc == 2) return "Wind down";
            return "";
        }

        public static HabitDef FirstOfArc(IList<HabitDef> habits, int arc)
        {
            if (habits == null) return null;
            for (int i = 0; i < habits.Count; i++)
            {
                HabitDef habit = habits[i];
                if (habit == null || string.IsNullOrEmpty(habit.Id)) continue;
                if (ArcOf(habit) == arc) return habit;
            }
            return null;
        }

        public static bool TryMark(int arc, int week, int day, out float x, out float z)
        {
            x = 0f;
            z = 0f;
            if (arc < 0 || arc > 2 || week < 0 || week >= WeekRecord.Weeks || day < 0 || day >= WeekRecord.DaysPerWeek)
                return false;
            x = (day - 3) * ColGap;
            z = BandZ[arc] + (1.5f - week) * RowGap;
            return true;
        }

        public static Color32[] Paint(float diameter, WeekDial dial, IList<HabitDef> habits)
        {
            int n = Size;
            var pixels = new Color32[n * n];
            if (diameter < 0.05f) diameter = 0.30f;
            float rim = diameter * 0.5f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    DialOf(diameter, x, y, out float dx, out float dz);
                    float reach = Mathf.Sqrt(dx * dx + dz * dz);
                    if (reach > rim * 0.992f) continue;
                    float edge = Mathf.Clamp01((rim - reach) / 0.0035f);
                    float grain = 0.988f + 0.012f * Mathf.Abs(Mathf.Sin(dx * 170f + dz * 130f));
                    Color paper = Paper * grain;
                    float vig = Mathf.Clamp01((reach - rim * 0.80f) / (rim * 0.20f));
                    paper = Color.Lerp(paper, Ink, vig * vig * 0.07f);
                    float rings = Band(reach, rim * 0.90f, 0.00105f) * 0.32f + Band(reach, rim * 0.965f, 0.00075f) * 0.26f;
                    paper = Color.Lerp(paper, Pencil, rings);
                    paper.a = edge;
                    pixels[y * n + x] = To32(paper);
                }
            }

            for (int arc = 0; arc < 3; arc++)
            {
                if (FirstOfArc(habits, arc) == null) continue;
                Ellipse(pixels, diameter, 0.002f, BandZ[arc], 0.055f, 0.028f, ArcWash[arc], 0.24f);
            }

            if (dial != null && dial.Plants != null)
            {
                for (int arc = 0; arc < 3; arc++)
                {
                    HabitDef habit = FirstOfArc(habits, arc);
                    if (habit == null || dial.For(habit.Id) == null) continue;
                    WeekRecord row = dial.For(habit.Id);
                    if (row.Days == null) continue;
                    for (int week = 0; week < WeekRecord.Weeks; week++)
                    {
                        for (int day = 0; day < WeekRecord.DaysPerWeek; day++)
                        {
                            int index = week * WeekRecord.DaysPerWeek + day;
                            if (index >= row.Days.Length) continue;
                            float mx, mz;
                            if (!TryMark(arc, week, day, out mx, out mz)) continue;
                            DrawMark(pixels, diameter, mx, mz, row.Days[index], arc);
                        }
                    }
                }
            }
            return pixels;
        }

        public static Color Sample(Color32[] pixels, float diameter, float x, float z)
        {
            if (pixels == null || pixels.Length != Size * Size || diameter < 0.05f)
                return new Color(0f, 0f, 0f, 0f);
            float u = x / diameter + 0.5f;
            float v = -z / diameter + 0.5f;
            int px = Mathf.Clamp(Mathf.RoundToInt(u * (Size - 1)), 0, Size - 1);
            int py = Mathf.Clamp(Mathf.RoundToInt(v * (Size - 1)), 0, Size - 1);
            Color32 c = pixels[py * Size + px];
            return new Color(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f);
        }

        /// <summary>A saturated red mark. The arc washes are warm and do not count.</summary>
        public static bool ShameRed(Color32[] pixels)
        {
            if (pixels == null) return false;
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 p = pixels[i];
                if (p.a < 24) continue;
                float r = p.r / 255f;
                float g = p.g / 255f;
                float b = p.b / 255f;
                if (r > 0.55f && g < 0.32f && b < 0.32f && r > g * 1.8f && r > b * 1.8f)
                    return true;
            }
            return false;
        }

        static void DrawMark(Color32[] pixels, float diameter, float x, float z, TileState state, int arc)
        {
            Color wash = arc >= 0 && arc < ArcWash.Length ? ArcWash[arc] : Paper;
            switch (state)
            {
                case TileState.Kept:
                    Disc(pixels, diameter, x, z, MarkRadius, wash, 0.94f);
                    Circle(pixels, diameter, x, z, MarkRadius * 0.86f, 0.00115f, Ink, 0.92f);
                    break;
                case TileState.Late:
                    Disc(pixels, diameter, x, z, MarkRadius, Pencil, 0.82f);
                    Hatch(pixels, diameter, x, z, MarkRadius);
                    Circle(pixels, diameter, x, z, MarkRadius * 0.90f, 0.0009f, Ink, 0.5f);
                    break;
                case TileState.Missed:
                    Disc(pixels, diameter, x, z, MarkRadius * 0.78f, MissWash, 0.72f);
                    Circle(pixels, diameter, x, z, MarkRadius * 0.78f, 0.0007f, Pencil, 0.4f);
                    break;
                case TileState.Today:
                    Circle(pixels, diameter, x, z, MarkRadius * 0.92f, 0.00125f, Ink, 0.9f);
                    break;
            }
        }

        static void Hatch(Color32[] pixels, float diameter, float cx, float cz, float radius)
        {
            int n = Size;
            int minX, minY, maxX, maxY;
            Bounds(diameter, cx, cz, radius, out minX, out minY, out maxX, out maxY);
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    DialOf(diameter, x, y, out float dx, out float dz);
                    float dist = Mathf.Sqrt((dx - cx) * (dx - cx) + (dz - cz) * (dz - cz));
                    if (dist > radius || dist < radius * 0.38f) continue;
                    float stripe = Mathf.Repeat((dx - cx) + (dz - cz), 0.0025f);
                    if (stripe > 0.00085f) continue;
                    int i = y * n + x;
                    Color under = From32(pixels[i]);
                    pixels[i] = To32(Over(under, Ink, 0.55f));
                }
            }
        }

        static void Disc(Color32[] pixels, float diameter, float cx, float cz, float radius, Color ink, float alpha)
        {
            int n = Size;
            int minX, minY, maxX, maxY;
            Bounds(diameter, cx, cz, radius, out minX, out minY, out maxX, out maxY);
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    DialOf(diameter, x, y, out float dx, out float dz);
                    float dist = Mathf.Sqrt((dx - cx) * (dx - cx) + (dz - cz) * (dz - cz));
                    if (dist > radius) continue;
                    float edge = Mathf.Clamp01((radius - dist) / 0.0008f);
                    int i = y * n + x;
                    Color under = From32(pixels[i]);
                    if (under.a < 0.01f) continue;
                    pixels[i] = To32(Over(under, ink, alpha * edge));
                }
            }
        }

        static void Circle(Color32[] pixels, float diameter, float cx, float cz, float radius, float half, Color ink, float alpha)
        {
            int n = Size;
            int minX, minY, maxX, maxY;
            Bounds(diameter, cx, cz, radius + half, out minX, out minY, out maxX, out maxY);
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    DialOf(diameter, x, y, out float dx, out float dz);
                    float dist = Mathf.Sqrt((dx - cx) * (dx - cx) + (dz - cz) * (dz - cz));
                    float weight = Band(dist, radius, half);
                    if (weight <= 0f) continue;
                    int i = y * n + x;
                    Color under = From32(pixels[i]);
                    if (under.a < 0.01f) continue;
                    pixels[i] = To32(Over(under, ink, alpha * weight));
                }
            }
        }

        static void Ellipse(Color32[] pixels, float diameter, float cx, float cz, float rx, float rz, Color ink, float alpha)
        {
            int n = Size;
            int minX, minY, maxX, maxY;
            Bounds(diameter, cx, cz, Mathf.Max(rx, rz), out minX, out minY, out maxX, out maxY);
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    DialOf(diameter, x, y, out float dx, out float dz);
                    float nx = (dx - cx) / rx;
                    float nz = (dz - cz) / rz;
                    float d = nx * nx + nz * nz;
                    if (d > 1f) continue;
                    float edge = Mathf.Clamp01((1f - d) / 0.35f);
                    int i = y * n + x;
                    Color under = From32(pixels[i]);
                    if (under.a < 0.01f) continue;
                    pixels[i] = To32(Over(under, ink, alpha * edge));
                }
            }
        }

        static void Bounds(float diameter, float cx, float cz, float radius, out int minX, out int minY, out int maxX, out int maxY)
        {
            float u0 = (cx - radius) / diameter + 0.5f;
            float u1 = (cx + radius) / diameter + 0.5f;
            float v0 = -((cz + radius) / diameter) + 0.5f;
            float v1 = -((cz - radius) / diameter) + 0.5f;
            minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(u0, u1) * (Size - 1)) - 1, 0, Size - 1);
            maxX = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(u0, u1) * (Size - 1)) + 1, 0, Size - 1);
            minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(v0, v1) * (Size - 1)) - 1, 0, Size - 1);
            maxY = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(v0, v1) * (Size - 1)) + 1, 0, Size - 1);
        }

        static void DialOf(float diameter, int px, int py, out float x, out float z)
        {
            float u = px / (float)(Size - 1);
            float v = py / (float)(Size - 1);
            x = (u - 0.5f) * diameter;
            z = -(v - 0.5f) * diameter;
        }

        static float Band(float reach, float radius, float half)
        {
            float d = Mathf.Abs(reach - radius);
            if (d >= half) return 0f;
            float t = 1f - d / half;
            return t * t * (3f - 2f * t);
        }

        static Color Over(Color under, Color ink, float alpha)
        {
            alpha = Mathf.Clamp01(alpha) * Mathf.Clamp01(under.a > 0f ? 1f : 0f);
            return new Color(
                under.r + (ink.r - under.r) * alpha,
                under.g + (ink.g - under.g) * alpha,
                under.b + (ink.b - under.b) * alpha,
                under.a);
        }

        static Color32 To32(Color c)
        {
            return new Color32(
                (byte)Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255f),
                (byte)Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255f),
                (byte)Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255f),
                (byte)Mathf.RoundToInt(Mathf.Clamp01(c.a) * 255f));
        }

        static Color From32(Color32 c)
        {
            return new Color(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f);
        }
    }
}

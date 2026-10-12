using GardenVR.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace GardenVR.Sundial
{
    public sealed partial class DialView
    {
        readonly string[] _profileLines = new string[9];
        readonly string[] _profileTiles = new string[63];
        readonly LifeZone[] _profileZones = new LifeZone[9];
        readonly Renderer[] _zoneDots = new Renderer[9];
        TextMesh _profileLabel;
        Material _profileDotMaterial;
        int _profileLook = -1, _profileDay = -1;
        static Texture2D _zoneInk;

        // Field Notebook artboard palette, tools/artboards/frames.mjs FN.
        public static Color ZoneColor(LifeZone zone)
        {
            switch (zone)
            {
                case LifeZone.Body: return new Color32(111, 158, 106, 255);
                case LifeZone.Work: return new Color32(184, 132, 64, 255);
                case LifeZone.Connection: return new Color32(201, 112, 127, 255);
                default: return new Color32(95, 132, 179, 255);
            }
        }

        static string ZoneName(LifeZone zone)
        {
            switch (zone)
            {
                case LifeZone.Body: return "Body";
                case LifeZone.Work: return "Work";
                case LifeZone.Connection: return "Connection";
                default: return "Mind";
            }
        }

        public string ProfileLabel => _profileLabel != null && _profileLabel.gameObject.activeInHierarchy ? _profileLabel.text : "";
        public Renderer ZoneDot(int slot) => _zoneDots[slot];

        public void SetProfile(int slot, HabitDef habit, Ledger ledger, GardenDay today, PlantState plant)
        {
            _profileZones[slot] = HabitProfiles.ZoneOf(habit);
            string line = WeekPageInk.PlantLine(habit) + "\n" + ZoneName(_profileZones[slot]);
            line += "\nkept " + MonthlyRecord.KeptDays(ledger, habit.Id, GardenMonth.Of(today)) + " days this month";
            HabitSchedule schedule = HabitProfiles.ScheduleOf(habit);
            if (schedule.Kind == ScheduleKind.TimesPerWeek)
                line += "\n" + SundialRules.KeptThisWeek(habit, ledger, today) + " of " + schedule.TimesPerWeek + " this week";
            _profileLines[slot] = line;
            for (int day = 0; day < 7; day++)
                _profileTiles[slot * 7 + day] = TileLabels.For(plant.Window[day]);
        }

        public void LookAtProfile(string id)
        {
            _profileLook = -1;
            _profileDay = -1;
            string arc;
            int row, tile;
            if (SundialArcs.TryPlant(id, out arc, out row))
                _profileLook = SundialArcs.Index(arc) * 3 + row;
            else if (!string.IsNullOrEmpty(id) && TryTileName(id, out tile))
            {
                _profileLook = tile / 7;
                _profileDay = tile % 7;
            }
        }

        void ApplyProfiles()
        {
            for (int slot = 0; slot < 9; slot++)
            {
                Transform plant = PlantTransform(slot);
                bool on = plant != null && SlotOn(slot) && showPlants && !weekPage && !stageStrip && _profileLines[slot] != null;
                if (on && _zoneDots[slot] == null)
                {
                    var dot = new GameObject("zone.dot." + slot);
                    dot.transform.SetParent(transform, false);
                    dot.AddComponent<MeshFilter>().sharedMesh = InkLetter.Quad();
                    var renderer = dot.AddComponent<MeshRenderer>();
                    if (_profileDotMaterial == null)
                    {
                        _profileDotMaterial = new Material(InkLetter.Card()) { name = "ProfileZoneInk", renderQueue = 3110 };
                        _profileDotMaterial.SetFloat("_ZTest", (float)CompareFunction.Always);
                    }
                    renderer.sharedMaterial = _profileDotMaterial;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    _zoneDots[slot] = renderer;
                }
                if (_zoneDots[slot] == null) continue;
                _zoneDots[slot].enabled = on;
                if (!on) continue;
                Transform dotTransform = _zoneDots[slot].transform;
                dotTransform.localPosition = plant.localPosition + new Vector3(0f, 0.003f, -0.014f);
                dotTransform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                dotTransform.localScale = Vector3.one * 0.008f;
                InkLetter.Tint(_zoneDots[slot], ZoneInk(), ZoneColor(_profileZones[slot]));
            }
            bool show = _profileLook >= 0 && SlotOn(_profileLook) && showPlants && !weekPage && !stageStrip;
            if (!show) { if (_profileLabel != null) _profileLabel.gameObject.SetActive(false); return; }
            if (_profileLabel == null)
            {
                var label = new GameObject("HabitProfileLabel");
                label.transform.SetParent(transform, false);
                _profileLabel = label.AddComponent<TextMesh>();
                _profileLabel.richText = false;
                InkLetter.AttachNote(label.transform, new Vector2(0.135f, 0.061f));
            }
            string text = _profileLines[_profileLook];
            if (_profileDay >= 0) text += "\n" + _profileTiles[_profileLook * 7 + _profileDay];
            _profileLabel.gameObject.SetActive(true);
            InkLetter.Apply(_profileLabel, text, 48, 0.0015f);
            // Keep the longest accepted name on the paper at the fixed dial framing.
            float width = _profileLabel.GetComponent<Renderer>().localBounds.size.x;
            if (width > 0.090f) _profileLabel.characterSize *= 0.090f / width;
            _profileLabel.transform.localPosition = new Vector3(0.12f, faceY + 0.09f, 0.04f);
            if (Camera.main != null)
                _profileLabel.transform.rotation = Camera.main.transform.rotation;
        }

        static Texture2D ZoneInk()
        {
            if (_zoneInk != null) return _zoneInk;
            const int size = 64;
            _zoneInk = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "ZonePencilDot" };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - 31.5f, dy = y - 31.5f;
                float radius = Mathf.Sqrt(dx * dx + dy * dy);
                float edge = 26f + 0.65f * Mathf.Sin(Mathf.Atan2(dy, dx) * 7f);
                float grain = 0.90f + 0.10f * Mathf.Abs(Mathf.Sin(x * 13f + y * 7f));
                float shade = radius > edge - 2f ? 0.25f : grain;
                pixels[y * size + x] = new Color(shade, shade, shade, Mathf.Clamp01(edge - radius));
            }
            _zoneInk.SetPixels(pixels);
            _zoneInk.Apply(false, true);
            return _zoneInk;
        }
    }
}

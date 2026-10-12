using System.Collections.Generic;
using GardenVR.Core;
using UnityEngine;

namespace GardenVR.Sundial
{
    public sealed partial class DialView
    {
        static readonly LifeZone[] RecordZones = { LifeZone.Body, LifeZone.Mind, LifeZone.Work, LifeZone.Connection };
        readonly float?[] _monthFill = new float?[4];
        readonly TextMesh[] _monthTabs = new TextMesh[4];
        readonly Texture2D[] _monthPapers = new Texture2D[4];
        readonly float[] _paintedFill = { -1f, -1f, -1f, -1f };
        bool _recordBound;

        public float? MonthlyFill(LifeZone zone) => _monthFill[System.Array.IndexOf(RecordZones, zone)];
        public TextMesh MonthlyTab(LifeZone zone) => _monthTabs[System.Array.IndexOf(RecordZones, zone)];

        public void SetMonthlyRecord(Ledger ledger, IReadOnlyList<HabitDef> habits, GardenDay today)
        {
            _recordBound = habits != null && habits.Count > 0;
            for (int i = 0; i < 4; i++)
                _monthFill[i] = ZoneRecord.Fill(ledger, habits, RecordZones[i], GardenMonth.Of(today), today);
        }

        public void SetQuantityProfile(int slot, HabitDef habit, QuantityTally tally, GardenDay today)
        {
            int? target = HabitProfiles.CleanTarget(habit.Target);
            if (!target.HasValue) return;
            _profileLines[slot] += "\n" + tally.Count(habit.Id, today.Index) + " of " + target.Value;
            for (int day = 0; day < 7; day++)
                _profileTiles[slot * 7 + day] = tally.Count(habit.Id, today.Index - 6 + day) + " of " + target.Value;
        }

        void ApplyMonthlyRecord()
        {
            bool show = _recordBound && showPlants && !weekPage && !stageStrip;
            for (int i = 0; i < 4; i++)
            {
                if (show && _monthTabs[i] == null)
                {
                    var go = new GameObject("zone.tab." + RecordZones[i]);
                    go.transform.SetParent(transform, false);
                    _monthTabs[i] = go.AddComponent<TextMesh>();
                    InkLetter.Apply(_monthTabs[i], ZoneName(RecordZones[i]), 48, 0.00125f);
                    InkLetter.AttachNote(go.transform, new Vector2(0.048f, 0.029f));
                }
                var tab = _monthTabs[i];
                if (tab == null) continue;
                tab.gameObject.SetActive(show);
                if (!show) continue;
                float x = (i - 1.5f) * 0.049f;
                // Four shared onboarding/record tabs sit above the settings ear, along the front rim.
                tab.transform.localPosition = new Vector3(x, faceY + 0.025f, -faceRadius * 1.05f);
                if (Camera.main != null) tab.transform.rotation = Camera.main.transform.rotation;
                float fill = _monthFill[i] ?? 0f;
                if (Mathf.Approximately(_paintedFill[i], fill)) continue;
                PaintTab(i, fill);
                _paintedFill[i] = fill;
            }
        }

        // Pencil strokes are clipped to the paper and the core-provided fill, never a percentage label.
        void PaintTab(int index, float fill)
        {
            Texture2D paper = InkLetter.Note(0.048f / 0.029f);
            if (_monthPapers[index] == null)
                _monthPapers[index] = new Texture2D(paper.width, paper.height, TextureFormat.RGBA32, false)
                    { name = "MonthlyPencil" + RecordZones[index], wrapMode = TextureWrapMode.Clamp };
            Color[] pixels = paper.GetPixels();
            Color pencil = ZoneColor(RecordZones[index]);
            int left = 22, right = paper.width - 25;
            for (int y = 22; y < paper.height - 25; y++)
            for (int x = left; x < left + (right - left) * fill; x++)
            {
                float stroke = Mathf.Repeat(x - y * 0.65f + Mathf.Sin(y * 0.31f) * 0.55f, 9f);
                if (stroke < 1.7f)
                    pixels[y * paper.width + x] = Color.Lerp(pixels[y * paper.width + x], pencil, 0.58f);
            }
            _monthPapers[index].SetPixels(pixels);
            _monthPapers[index].Apply(false, false);
            InkLetter.Tint(_monthTabs[index].transform.Find("paper").GetComponent<Renderer>(), _monthPapers[index], Color.white);
        }

        void DestroyMonthlyRecord()
        {
            foreach (var paper in _monthPapers) if (paper != null) DestroyObject(paper);
        }
    }
}

using System.Collections.Generic;
using System.Globalization;
using GardenVR.Core;
using GardenVR.Input;
using UnityEngine;

namespace GardenVR.Sundial
{
    public sealed partial class WeekDialController
    {
        readonly List<GameObject> _journalChrome = new List<GameObject>();
        string _journalKey;
        public int JournalWeek { get; private set; } = 3;
        public string JournalText { get; private set; } = "";
        bool JournalIntent(HandIntent intent)
        {
            if (!OnWeekPage || Blocked() || intent.Kind != HandIntentKind.Pinch) return false;
            if (intent.TargetId == "journal.older" && JournalWeek > 0) { JournalWeek--; return true; }
            if (intent.TargetId == "journal.newer" && JournalWeek < 3) { JournalWeek++; return true; }
            return false;
        }
        void PlaceJournal()
        {
            bool show = OnWeekPage && !Blocked() && !(_sundial.Settings != null && _sundial.Settings.Open);
            int end = Service.Today().Index - (3 - JournalWeek) * WeekRecord.DaysPerWeek;
            var entries = Service.Save.Journal.WeekEnding(end);
            bool full = entries.Count > 2;
            string text = "Journal\n" + Date(end - 6) + " to " + Date(end);
            foreach (var entry in entries) text += full
                ? "\n\n" + ReflectionPaper.Wrap(Date(entry.Day) + "  " + entry.Line, 52)
                : "\n\n" + Date(entry.Day) + "\n" + ReflectionPaper.Wrap(entry.Line, 25);
            if (entries.Count == 0) text += "\n\nNo lines kept this week";
            JournalText = show ? text : "";
            string key = show + ":" + text;
            if (_journalKey != key)
            {
                foreach (var go in _journalChrome) if (go != null) { go.SetActive(false); Destroy(go); }
                _journalChrome.Clear(); _journalKey = key;
                if (show)
                {
                    float y = _sundial.View.faceY;
                    _journalChrome.Add(ReflectionPaper.Note(transform, "JournalWeek", text,
                        full ? new Vector3(0, y + .075f, -.04f) : new Vector3(-.105f, y + .06f, -.005f),
                        full ? new Vector2(.21f, .17f) : new Vector2(.095f, .12f), false));
                    if (JournalWeek > 0) _journalChrome.Add(ReflectionPaper.Note(transform, "journal.older", "Earlier week",
                        new Vector3(-.13f, y + .008f, -.053f), new Vector2(.045f, .020f), true));
                    if (JournalWeek < 3) _journalChrome.Add(ReflectionPaper.Note(transform, "journal.newer", "Later week",
                        new Vector3(-.078f, y + .008f, -.053f), new Vector2(.045f, .020f), true));
                }
            }
            foreach (var go in _journalChrome) if (go != null) ReflectionPaper.Face(go.transform);
        }
        static string Date(int day) => new GardenDay(day).CivilDate.ToString("d MMM", CultureInfo.InvariantCulture);
    }
}

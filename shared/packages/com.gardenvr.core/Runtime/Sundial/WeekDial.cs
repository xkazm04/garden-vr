using System;
using System.Collections.Generic;

namespace GardenVR.Core
{
    /// <summary>
    /// One habit across the last four weeks. <see cref="Days"/> has 28 tiles, oldest first.
    /// Index 0 is 27 days before today. Index 27 is today.
    /// Week 0 is the oldest week. Week 3 holds today.
    /// This is a record of days. It does not count a run, and a miss is only that day's tile.
    /// </summary>
    public sealed class WeekRecord
    {
        public const int Weeks = 4;
        public const int DaysPerWeek = 7;
        public const int SpanDays = 28;

        public string HabitId;
        public TileState[] Days;

        public TileState At(int week, int dayOfWeek)
        {
            if (Days == null) throw new InvalidOperationException("days");
            if (week < 0 || week >= Weeks) throw new ArgumentOutOfRangeException(nameof(week));
            if (dayOfWeek < 0 || dayOfWeek >= DaysPerWeek) throw new ArgumentOutOfRangeException(nameof(dayOfWeek));
            return Days[week * DaysPerWeek + dayOfWeek];
        }
    }

    /// <summary>
    /// The second dial page. Four weeks for each habit, oldest day first.
    /// The page is a record. It has no run count and a miss leaves every other day as it was.
    /// </summary>
    public sealed class WeekDial
    {
        public List<WeekRecord> Plants = new List<WeekRecord>();

        public static WeekDial Read(IReadOnlyList<HabitDef> habits, Ledger ledger, GardenDay today)
        {
            if (ledger == null) throw new ArgumentNullException(nameof(ledger));
            var dial = new WeekDial();
            if (habits == null) return dial;
            int todayIndex = today.Index;
            int first = todayIndex - (WeekRecord.SpanDays - 1);
            for (int h = 0; h < habits.Count; h++)
            {
                HabitDef habit = habits[h];
                if (habit == null || string.IsNullOrEmpty(habit.Id)) continue;
                var days = new TileState[WeekRecord.SpanDays];
                for (int i = 0; i < days.Length; i++)
                    days[i] = SundialRules.TileOn(habit, ledger, first + i, todayIndex);
                dial.Plants.Add(new WeekRecord { HabitId = habit.Id, Days = days });
            }
            return dial;
        }

        public WeekRecord For(string habitId)
        {
            if (Plants == null || string.IsNullOrEmpty(habitId)) return null;
            for (int i = 0; i < Plants.Count; i++)
            {
                WeekRecord row = Plants[i];
                if (row != null && row.HabitId == habitId) return row;
            }
            return null;
        }

        public string ToJson()
        {
            var root = new JsonObject();
            root.Set("weeks", JsonValue.Number(WeekRecord.Weeks));
            root.Set("span", JsonValue.Number(WeekRecord.SpanDays));
            var plants = new JsonArray();
            if (Plants != null)
            {
                for (int i = 0; i < Plants.Count; i++)
                {
                    WeekRecord row = Plants[i];
                    if (row == null) continue;
                    var obj = new JsonObject();
                    obj.Set("habit", JsonValue.String(row.HabitId ?? ""));
                    var tiles = new JsonArray();
                    if (row.Days != null)
                    {
                        for (int d = 0; d < row.Days.Length; d++)
                            tiles.Add(JsonValue.String(row.Days[d].ToString()));
                    }
                    obj.Set("tiles", tiles);
                    plants.Add(obj);
                }
            }
            root.Set("plants", plants);
            return Json.Write(root);
        }
    }
}

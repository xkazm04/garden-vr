using System;
using System.Collections.Generic;

namespace GardenVR.Core
{
    /// <summary>
    /// Oracle for the dial at one minute. <see cref="ToJson"/> is the shape the dev overlay and the tests read:
    /// now, arc, gnomonDeg, plants[{habit, window, windowKept, lifetimeKept, stage, bloom, dueNow, canBackfill}].
    /// </summary>
    public sealed class SundialState
    {
        public int Now;
        public ArcId? Arc;
        public float GnomonDeg;
        public List<PlantState> Plants = new List<PlantState>();

        public static SundialState Capture(IReadOnlyList<HabitDef> habits, Ledger ledger, GardenDay today, int nowMin)
        {
            return Capture(habits, ledger, today, nowMin, null);
        }

        /// <summary>
        /// Same picture as <see cref="Capture(IReadOnlyList{HabitDef}, Ledger, GardenDay, int)"/>,
        /// read against <paramref name="times"/>. A null schedule is the plan default.
        /// The gnomon stays on the clock. Only the arc and due flags follow the boundaries.
        /// </summary>
        public static SundialState Capture(IReadOnlyList<HabitDef> habits, Ledger ledger, GardenDay today, int nowMin, ArcTimes times)
        {
            if (ledger == null) throw new ArgumentNullException(nameof(ledger));
            ArcTimes schedule = times ?? ArcTimes.Default;
            var state = new SundialState();
            state.Now = nowMin;
            state.Arc = schedule.ArcAt(nowMin);
            state.GnomonDeg = SundialRules.GnomonAngleDeg(nowMin);
            if (habits == null) return state;
            for (int i = 0; i < habits.Count; i++)
            {
                if (habits[i] == null) continue;
                state.Plants.Add(SundialRules.Plant(habits[i], ledger, today, nowMin, schedule));
            }
            return state;
        }

        public string ToJson()
        {
            var root = new JsonObject();
            root.Set("now", JsonValue.Number(Now));
            root.Set("arc", Arc.HasValue ? JsonValue.String(Arc.Value.ToString()) : JsonValue.Null());
            root.Set("gnomonDeg", JsonValue.Number((double)GnomonDeg));
            var plants = new JsonArray();
            if (Plants != null)
            {
                for (int i = 0; i < Plants.Count; i++)
                    plants.Add(PlantJson(Plants[i]));
            }
            root.Set("plants", plants);
            return Json.Write(root);
        }

        static JsonObject PlantJson(PlantState plant)
        {
            if (plant == null) throw new ArgumentException("plant");
            var obj = new JsonObject();
            obj.Set("habit", JsonValue.String(plant.HabitId ?? ""));
            var window = new JsonArray();
            if (plant.Window != null)
            {
                for (int i = 0; i < plant.Window.Length; i++)
                    window.Add(JsonValue.String(plant.Window[i].ToString()));
            }
            obj.Set("window", window);
            obj.Set("windowKept", JsonValue.Number(plant.WindowKept));
            obj.Set("lifetimeKept", JsonValue.Number(plant.LifetimeKept));
            obj.Set("stage", JsonValue.String(plant.Stage.ToString()));
            obj.Set("bloom", JsonValue.String(plant.Bloom.ToString()));
            obj.Set("dueNow", JsonValue.Bool(plant.DueNow));
            obj.Set("canBackfill", JsonValue.Bool(plant.CanBackfillYesterday));
            return obj;
        }
    }
}

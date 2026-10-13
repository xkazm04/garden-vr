using System;
using System.IO;
using UnityEngine;

namespace GardenVR.Sundial.Tests.PlayMode
{
    /// <summary>
    /// Single route for every directory a PlayMode test writes task evidence into. By default that is a
    /// per-run folder under Application.temporaryCachePath, so running the suite never rewrites the
    /// committed orchestration/runs/sundial/T-SUN-xxx files. An agent producing evidence for its task sets
    /// GARDENVR_EVIDENCE_DIR=1 (the committed task folder) or GARDENVR_EVIDENCE_DIR=&lt;path&gt; (that path
    /// plus the task id). The older GARDEN_RUN_DIR override in a few tests still wins there.
    /// </summary>
    static class SundialEvidence
    {
        public static string Dir(string taskId)
        {
            string env = Environment.GetEnvironmentVariable("GARDENVR_EVIDENCE_DIR");
            string dir;
            if (string.IsNullOrEmpty(env) || env == "0") dir = Path.Combine(Application.temporaryCachePath, "evidence", "sundial", taskId);
            else if (env == "1") dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "orchestration", "runs", "sundial", taskId));
            else dir = Path.Combine(env, taskId);
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}

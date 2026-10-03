using System.Globalization;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace GardenVR.Terrarium.Tests
{
    public class EtchContrastTests
    {
        [Test]
        public void Etch_RenderedContrast_AtLeastFourPointFive()
        {
            Texture2D capture;
            int hits;
            float ratio = EtchContrast.MeasureRendered(out capture, out hits);
            try
            {
                if (capture != null)
                {
                    string repo = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
                    string dir = Path.Combine(repo, "orchestration", "runs", "terrarium", "T-TER-012");
                    Directory.CreateDirectory(dir);
                    File.WriteAllBytes(Path.Combine(dir, "etched-text.png"), capture.EncodeToPNG());
                    File.WriteAllText(Path.Combine(dir, "etched-contrast.txt"),
                        "contrast=" + ratio.ToString("0.00", CultureInfo.InvariantCulture) + "\n" +
                        "hits=" + hits.ToString(CultureInfo.InvariantCulture) + "\n" +
                        "minimum=4.5\n" +
                        "ink=#BFF5DD at 70%\n" +
                        "background=#0E1A1C\n");
                }
                Assert.Greater(hits, 40, "the etched line did not render any glyph pixels");
                Assert.GreaterOrEqual(ratio, EtchContrast.MinimumRatio,
                    "etch.text against night.room is below 4.5:1");
            }
            finally
            {
                if (capture != null) Object.DestroyImmediate(capture);
            }
        }
    }
}

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
                    string dir = Path.Combine(repo, "orchestration", "runs", "terrarium", "T-TER-024");
                    Directory.CreateDirectory(dir);
                    File.WriteAllBytes(Path.Combine(dir, "contrast-sample.png"), capture.EncodeToPNG());
                    TMPro.TMP_FontAsset font = EtchContrast.Font();
                    File.WriteAllText(Path.Combine(dir, "etched-contrast.txt"),
                        "contrast=" + ratio.ToString("0.00", CultureInfo.InvariantCulture) + "\n" +
                        "hits=" + hits.ToString(CultureInfo.InvariantCulture) + "\n" +
                        "minimum=4.5\n" +
                        "ink=#BFF5DD at 70%\n" +
                        "background=#0E1A1C\n" +
                        "font=" + (font != null ? font.name : "missing") + "\n");
                }
                TMPro.TMP_FontAsset face = EtchContrast.Font();
                Assert.IsNotNull(face, "the etched face did not load");
                StringAssert.Contains("Cormorant", face.name);
                StringAssert.DoesNotContain("Liberation", face.name);
                string licence = Path.Combine(Application.dataPath, "Fonts", "CormorantGaramond", "OFL.txt");
                Assert.IsTrue(File.Exists(licence), "OFL licence missing");
                StringAssert.Contains("SIL Open Font License", File.ReadAllText(licence));
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

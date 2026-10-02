using System.Globalization;
using System.Text;

namespace GardenVR.Capture
{
    public static class CaptureJson
    {
        public static string Quote(string text)
        {
            if (text == null) text = "";
            var sb = new StringBuilder(text.Length + 2);
            sb.Append('"');
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\\' || c == '"') sb.Append('\\').Append(c);
                else if (c == '\n') sb.Append("\\n");
                else if (c == '\r') sb.Append("\\r");
                else if (c == '\t') sb.Append("\\t");
                else if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                else sb.Append(c);
            }
            sb.Append('"');
            return sb.ToString();
        }

        public static string Num(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return "0";
            return value.ToString("0.######", CultureInfo.InvariantCulture);
        }

        public static string Check(ImageStats stats)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"meanLuma\": ").Append(Num(stats.MeanLuma)).Append(",\n");
            sb.Append("  \"blackFrac\": ").Append(Num(stats.BlackFrac)).Append(",\n");
            sb.Append("  \"whiteFrac\": ").Append(Num(stats.WhiteFrac)).Append(",\n");
            sb.Append("  \"magentaFrac\": ").Append(Num(stats.MagentaFrac)).Append(",\n");
            sb.Append("  \"width\": ").Append(stats.Width).Append(",\n");
            sb.Append("  \"height\": ").Append(stats.Height).Append(",\n");
            sb.Append("  \"sha256\": ").Append(Quote(stats.Sha256 ?? "")).Append("\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        public static string Diff(DiffResult diff)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"meanAbs\": ").Append(Num(diff.MeanAbs)).Append(",\n");
            sb.Append("  \"outsideMask\": ").Append(Num(diff.OutsideMask)).Append(",\n");
            sb.Append("  \"insideMask\": ").Append(Num(diff.InsideMask)).Append(",\n");
            sb.Append("  \"width\": ").Append(diff.Width).Append(",\n");
            sb.Append("  \"height\": ").Append(diff.Height).Append("\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        public static string Budget(BudgetReport report)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"renderers\": ").Append(report.Renderers).Append(",\n");
            sb.Append("  \"drawsEst\": ").Append(report.DrawsEst).Append(",\n");
            sb.Append("  \"shadowCasters\": ").Append(report.ShadowCasters).Append(",\n");
            sb.Append("  \"transparentDraws\": ").Append(report.TransparentDraws).Append(",\n");
            sb.Append("  \"tris\": ").Append(report.Tris).Append(",\n");
            sb.Append("  \"textures\": [");
            if (report.Textures != null)
            {
                for (int i = 0; i < report.Textures.Count; i++)
                {
                    TextureBudget tex = report.Textures[i];
                    if (i > 0) sb.Append(',');
                    sb.Append("\n    {\"name\": ").Append(Quote(tex.Name ?? ""));
                    sb.Append(", \"width\": ").Append(tex.Width);
                    sb.Append(", \"height\": ").Append(tex.Height).Append('}');
                }
                if (report.Textures.Count > 0) sb.Append("\n  ");
            }
            sb.Append("],\n");
            sb.Append("  \"texMemAstc6x6MB\": ").Append(Num(report.TexMemAstc6x6MB)).Append(",\n");
            sb.Append("  \"transparentMeanLayers\": ").Append(Num(report.TransparentMeanLayers)).Append(",\n");
            sb.Append("  \"transparentCoverage\": ").Append(Num(report.TransparentCoverage)).Append(",\n");
            sb.Append("  \"framing\": ").Append(Quote(report.Framing ?? "")).Append(",\n");
            sb.Append("  \"target\": ").Append(Quote(report.Target ?? "")).Append(",\n");
            sb.Append("  \"overdrawPng\": ").Append(Quote(report.OverdrawPng ?? "")).Append("\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        public static string StringMap(System.Collections.Generic.IReadOnlyDictionary<string, string> map)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            bool first = true;
            if (map != null)
            {
                foreach (var pair in map)
                {
                    if (!first) sb.Append(",\n");
                    first = false;
                    sb.Append("  ").Append(Quote(pair.Key)).Append(": ").Append(Quote(pair.Value));
                }
            }
            if (!first) sb.Append('\n');
            sb.Append("}\n");
            return sb.ToString();
        }
    }
}

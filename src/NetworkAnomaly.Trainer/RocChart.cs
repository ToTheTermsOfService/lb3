using System.Globalization;
using System.Security;
using System.Text;

namespace NetworkAnomaly.Trainer;

public static class RocChart
{
    private const int Width = 520, Height = 480, Left = 60, Top = 20, Size = 400;

    public static void WriteSvg(string path, IEnumerable<(string Name, string Color, List<(double Fpr, double Tpr)> Points)> curves)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<svg xmlns='http://www.w3.org/2000/svg' width='{Width}' height='{Height}' font-family='sans-serif' font-size='12'>");
        sb.AppendLine($"<rect width='{Width}' height='{Height}' fill='white'/>");
        AppendGrid(sb);
        AppendCurves(sb, curves);
        sb.AppendLine("</svg>");
        File.WriteAllText(path, sb.ToString());
    }

    private static string X(double fpr) => (Left + fpr * Size).ToString("F1", CultureInfo.InvariantCulture);
    private static string Y(double tpr) => (Top + (1 - tpr) * Size).ToString("F1", CultureInfo.InvariantCulture);

    private static void AppendGrid(StringBuilder sb)
    {
        for (int k = 0; k <= 10; k += 2)
        {
            double v = k / 10.0;
            sb.AppendLine($"<line x1='{X(v)}' y1='{Y(0)}' x2='{X(v)}' y2='{Y(1)}' stroke='#e5e5e5'/>");
            sb.AppendLine($"<line x1='{X(0)}' y1='{Y(v)}' x2='{X(1)}' y2='{Y(v)}' stroke='#e5e5e5'/>");
            sb.AppendLine($"<text x='{X(v)}' y='{Top + Size + 16}' text-anchor='middle'>{v:0.0}</text>".Replace(',', '.'));
            sb.AppendLine($"<text x='{Left - 8}' y='{Y(v)}' text-anchor='end' dy='4'>{v:0.0}</text>".Replace(',', '.'));
        }
        sb.AppendLine($"<line x1='{X(0)}' y1='{Y(0)}' x2='{X(1)}' y2='{Y(1)}' stroke='#999' stroke-dasharray='4 4'/>");
        sb.AppendLine($"<rect x='{Left}' y='{Top}' width='{Size}' height='{Size}' fill='none' stroke='#333'/>");
        sb.AppendLine($"<text x='{Left + Size / 2}' y='{Height - 18}' text-anchor='middle'>False positive rate (normal, позначений як аномалія)</text>");
        sb.AppendLine($"<text transform='translate(16 {Top + Size / 2}) rotate(-90)' text-anchor='middle'>True positive rate (виявлені атаки)</text>");
    }

    private static void AppendCurves(StringBuilder sb, IEnumerable<(string Name, string Color, List<(double Fpr, double Tpr)> Points)> curves)
    {
        int legendY = Top + Size - 20;
        foreach (var (name, color, points) in curves)
        {
            string poly = string.Join(' ', points.Select(p => $"{X(p.Fpr)},{Y(p.Tpr)}"));
            sb.AppendLine($"<polyline points='{poly}' fill='none' stroke='{color}' stroke-width='2'/>");
            sb.AppendLine($"<line x1='{Left + 150}' y1='{legendY}' x2='{Left + 175}' y2='{legendY}' stroke='{color}' stroke-width='3'/>");
            sb.AppendLine($"<text x='{Left + 182}' y='{legendY + 4}'>{SecurityElement.Escape(name)}</text>");
            legendY -= 18;
        }
    }
}

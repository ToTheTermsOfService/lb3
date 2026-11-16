using System.Globalization;
using System.Text;

namespace NetworkAnomaly.Trainer;

/// <summary>Метрики, які ML.NET не дає напряму: AUC, довірчі інтервали, перцентилі, ROC.</summary>
public static class Stats
{
    /// <summary>AUC ROC як імовірність того, що випадкова атака має вищий Score, ніж випадковий normal (Манна–Уітні, нічиї = 0.5).</summary>
    public static double Auc(IReadOnlyList<(float Score, bool Attack)> xs)
    {
        var ordered = xs.OrderBy(x => x.Score).ToList();
        double rankSumPos = 0;
        int nPos = 0, i = 0;
        while (i < ordered.Count)
        {
            int j = i;
            while (j + 1 < ordered.Count && ordered[j + 1].Score == ordered[i].Score) j++;
            double avgRank = (i + j) / 2.0 + 1;
            for (int k = i; k <= j; k++)
                if (ordered[k].Attack) { rankSumPos += avgRank; nPos++; }
            i = j + 1;
        }
        int nNeg = ordered.Count - nPos;
        if (nPos == 0 || nNeg == 0) return double.NaN;
        return (rankSumPos - nPos * (nPos + 1) / 2.0) / ((double)nPos * nNeg);
    }

    /// <summary>95% довірчий інтервал Вілсона для частки k/n.</summary>
    public static (double Lo, double Hi) Wilson(int k, int n, double z = 1.96)
    {
        if (n == 0) return (double.NaN, double.NaN);
        double p = (double)k / n, z2 = z * z;
        double center = (p + z2 / (2 * n)) / (1 + z2 / n);
        double half = z * Math.Sqrt(p * (1 - p) / n + z2 / (4.0 * n * n)) / (1 + z2 / n);
        return (Math.Max(0, center - half), Math.Min(1, center + half));
    }

    /// <summary>Перцентиль (метод "nearest rank").</summary>
    public static float Percentile(IEnumerable<float> values, double p)
    {
        var s = values.OrderBy(v => v).ToArray();
        int idx = Math.Clamp((int)Math.Ceiling(p * s.Length) - 1, 0, s.Length - 1);
        return s[idx];
    }

    public static List<(double Fpr, double Tpr)> Roc(IReadOnlyList<(float Score, bool Attack)> xs)
    {
        int pos = xs.Count(x => x.Attack), neg = xs.Count - pos;
        var pts = new List<(double, double)> { (0, 0) };
        int tp = 0, fp = 0;
        foreach (var g in xs.GroupBy(x => x.Score).OrderByDescending(g => g.Key))
        {
            tp += g.Count(x => x.Attack);
            fp += g.Count(x => !x.Attack);
            pts.Add(((double)fp / neg, (double)tp / pos));
        }
        return pts;
    }

    /// <summary>Проста SVG-діаграма ROC-кривих (без зовнішніх бібліотек).</summary>
    public static void WriteRocSvg(string path, IEnumerable<(string Name, string Color, List<(double Fpr, double Tpr)> Points)> curves)
    {
        const int W = 520, H = 480, L = 60, T = 20, S = 400;
        string X(double f) => (L + f * S).ToString("F1", CultureInfo.InvariantCulture);
        string Y(double t) => (T + (1 - t) * S).ToString("F1", CultureInfo.InvariantCulture);
        var sb = new StringBuilder();
        sb.AppendLine($"<svg xmlns='http://www.w3.org/2000/svg' width='{W}' height='{H}' font-family='sans-serif' font-size='12'>");
        sb.AppendLine($"<rect width='{W}' height='{H}' fill='white'/>");
        for (int k = 0; k <= 10; k += 2)
        {
            double v = k / 10.0;
            sb.AppendLine($"<line x1='{X(v)}' y1='{Y(0)}' x2='{X(v)}' y2='{Y(1)}' stroke='#e5e5e5'/>");
            sb.AppendLine($"<line x1='{X(0)}' y1='{Y(v)}' x2='{X(1)}' y2='{Y(v)}' stroke='#e5e5e5'/>");
            sb.AppendLine($"<text x='{X(v)}' y='{T + S + 16}' text-anchor='middle'>{v:0.0}</text>".Replace(',', '.'));
            sb.AppendLine($"<text x='{L - 8}' y='{Y(v)}' text-anchor='end' dy='4'>{v:0.0}</text>".Replace(',', '.'));
        }
        sb.AppendLine($"<line x1='{X(0)}' y1='{Y(0)}' x2='{X(1)}' y2='{Y(1)}' stroke='#999' stroke-dasharray='4 4'/>");
        sb.AppendLine($"<rect x='{L}' y='{T}' width='{S}' height='{S}' fill='none' stroke='#333'/>");
        sb.AppendLine($"<text x='{L + S / 2}' y='{H - 18}' text-anchor='middle'>False positive rate (normal, позначений як аномалія)</text>");
        sb.AppendLine($"<text transform='translate(16 {T + S / 2}) rotate(-90)' text-anchor='middle'>True positive rate (виявлені атаки)</text>");
        int legendY = T + S - 20;
        foreach (var (name, color, pts) in curves)
        {
            string poly = string.Join(' ', pts.Select(p => $"{X(p.Fpr)},{Y(p.Tpr)}"));
            sb.AppendLine($"<polyline points='{poly}' fill='none' stroke='{color}' stroke-width='2'/>");
            sb.AppendLine($"<line x1='{L + 150}' y1='{legendY}' x2='{L + 175}' y2='{legendY}' stroke='{color}' stroke-width='3'/>");
            sb.AppendLine($"<text x='{L + 182}' y='{legendY + 4}'>{System.Security.SecurityElement.Escape(name)}</text>");
            legendY -= 18;
        }
        sb.AppendLine("</svg>");
        File.WriteAllText(path, sb.ToString());
    }
}

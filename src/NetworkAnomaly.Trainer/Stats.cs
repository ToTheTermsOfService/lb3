namespace NetworkAnomaly.Trainer;

public static class Stats
{
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

    public static (double Lo, double Hi) Wilson(int k, int n, double z = 1.96)
    {
        if (n == 0) return (double.NaN, double.NaN);
        double p = (double)k / n, z2 = z * z;
        double center = (p + z2 / (2 * n)) / (1 + z2 / n);
        double half = z * Math.Sqrt(p * (1 - p) / n + z2 / (4.0 * n * n)) / (1 + z2 / n);
        return (Math.Max(0, center - half), Math.Min(1, center + half));
    }

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
}

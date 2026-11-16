using NetworkAnomaly.Common;

namespace NetworkAnomaly.Trainer;

public static class ConsoleReport
{
    public static void Header(string title) => Console.WriteLine($"\n===== {title} =====");

    public static void Split(string name, List<DeltaRow> rows) =>
        Console.WriteLine($"  {name,-11} normal: {rows.Count(r => !r.IsAttack),4}   атаки: {rows.Count(r => r.IsAttack),5}");

    public static void Rates(string name, int tp, int fn, int fp, int tn)
    {
        var tprCi = Stats.Wilson(tp, tp + fn);
        var fprCi = Stats.Wilson(fp, fp + tn);
        double prec = tp / (double)Math.Max(1, tp + fp), rec = tp / (double)Math.Max(1, tp + fn);
        double f1 = prec + rec == 0 ? 0 : 2 * prec * rec / (prec + rec);
        double bal = (rec + tn / (double)Math.Max(1, fp + tn)) / 2;
        double acc = (tp + tn) / (double)(tp + fn + fp + tn);
        Console.WriteLine($"{name}:");
        Console.WriteLine($"  TPR (виявлені атаки)  = {tp}/{tp + fn} = {rec:P2}  95% ДІ [{tprCi.Lo:P2}; {tprCi.Hi:P2}]");
        Console.WriteLine($"  FPR (хибні тривоги)   = {fp}/{fp + tn} = {fp / (double)Math.Max(1, fp + tn):P2}  95% ДІ [{fprCi.Lo:P2}; {fprCi.Hi:P2}]");
        Console.WriteLine($"  Precision = {prec:F4}  F1 = {f1:F4}  Balanced accuracy = {bal:F4}  Accuracy = {acc:F4}");
    }

    public static void Rates(string name, IEnumerable<DeltaRow> rows, Func<DeltaRow, bool> flagged)
    {
        int tp = 0, fn = 0, fp = 0, tn = 0;
        foreach (var r in rows)
        {
            bool f = flagged(r);
            if (r.IsAttack) { if (f) tp++; else fn++; }
            else { if (f) fp++; else tn++; }
        }
        Rates(name, tp, fn, fp, tn);
    }
}

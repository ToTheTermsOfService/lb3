using NetworkAnomaly.Common;

namespace NetworkAnomaly.Trainer;

public static class HyperparameterSearch
{
    private static readonly string[] Preprocessings = ["none", "log", "meanvar", "log+meanvar"];
    private static readonly int[] Ranks = [1, 2, 3, 5, 8, 12];

    public static (string Prep, int Rank) Run(AnomalyModelFactory factory, List<DeltaRow> train, List<DeltaRow> validation)
    {
        ConsoleReport.Header("3. Абляція передобробки й рангу (validation; модель навчена на train normal)");
        Console.WriteLine("TPR@1%FPR: частка атак validation вище порогу = 99-й перцентиль Score на normal validation.");
        Console.WriteLine($"  {"Передобробка",-12} {"Rank",4} {"AUC",8} {"TPR@1%FPR",10} {"NaN у Score",12}");

        var grid = new List<(string Prep, int Rank, double Auc, double Tpr)>();
        foreach (var prep in Preprocessings)
            foreach (var rank in Ranks)
            {
                var model = factory.Fit(prep, rank, train);
                var (scores, nan) = factory.ScoreWithNaNCount(model.WithoutFix, validation);
                var pairs = AnomalyModelFactory.Pairs(scores, validation);
                float thr = Stats.Percentile(pairs.Where(p => !p.Attack).Select(p => p.Score), Settings.ThresholdPercentile);
                double tpr = pairs.Where(p => p.Attack).Average(p => p.Score > thr ? 1.0 : 0.0);
                double auc = Stats.Auc(pairs);
                grid.Add((prep, rank, auc, tpr));
                Console.WriteLine($"  {prep,-12} {rank,4} {auc,8:F4} {tpr,10:P1} {nan,12}");
            }

        var best = grid.OrderByDescending(g => Math.Round(g.Auc, 3)).ThenByDescending(g => g.Tpr).ThenBy(g => g.Rank).First();
        Console.WriteLine($"Обрано: передобробка = {best.Prep}, Rank = {best.Rank} (найвищий AUC на validation; за рівності — вищий TPR, менший ранг).");
        return (best.Prep, best.Rank);
    }
}

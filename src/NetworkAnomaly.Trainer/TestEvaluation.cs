using Microsoft.ML;
using NetworkAnomaly.Common;

namespace NetworkAnomaly.Trainer;

public static class TestEvaluation
{
    public static List<(float Score, bool Attack)> Run(MLContext ml, ITransformer model, List<DeltaRow> test)
    {
        ConsoleReport.Header("5. Оцінювання на test");
        var scored = model.Transform(ml.Data.LoadFromEnumerable(test.Select(LabeledInput.From)));
        var metrics = ml.AnomalyDetection.Evaluate(scored, falsePositiveCount: 1);
        var preds = ml.Data.CreateEnumerable<AnomalyPrediction>(scored, false).ToList();
        var results = test.Zip(preds).ToList();
        var pairs = results.Select(x => (Score: x.Second.Score, Attack: x.First.IsAttack)).ToList();

        int tp = results.Count(x => x.First.IsAttack && x.Second.IsAnomaly);
        int fn = results.Count(x => x.First.IsAttack && !x.Second.IsAnomaly);
        int fp = results.Count(x => !x.First.IsAttack && x.Second.IsAnomaly);
        int tn = results.Count(x => !x.First.IsAttack && !x.Second.IsAnomaly);

        Console.WriteLine($"ML.NET Evaluate: AUC = {metrics.AreaUnderRocCurve:F4}, detection rate @ 1 FP = {metrics.DetectionRateAtFalsePositiveCount:F4}");
        Console.WriteLine($"Власний AUC (перевірка): {Stats.Auc(pairs):F4}");
        ConsoleReport.Rates("PCA (фінальна модель)", tp, fn, fp, tn);
        Console.WriteLine("\nМатриця помилок:");
        Console.WriteLine($"                  прогноз: аномалія   прогноз: норма");
        Console.WriteLine($"  факт: атака     {tp,17}   {fn,14}");
        Console.WriteLine($"  факт: normal    {fp,17}   {tn,14}");

        PrintByClass(results);
        PrintErrors(results);
        return pairs;
    }

    private static void PrintByClass(List<(DeltaRow First, AnomalyPrediction Second)> results)
    {
        Console.WriteLine("\nЗа класами (test):");
        foreach (var g in results.GroupBy(x => x.First.Class).OrderBy(g => g.Key))
        {
            int k = g.Count(x => x.Second.IsAnomaly), n = g.Count();
            var ci = Stats.Wilson(k, n);
            string what = g.Key == Schema.NormalClass ? "<- хибні тривоги" : "";
            Console.WriteLine($"  {g.Key,-12} {k,4}/{n,-4} {k / (double)n,7:P1}  95% ДІ [{ci.Lo:P1}; {ci.Hi:P1}]  медіана Score {Stats.Percentile(g.Select(x => x.Second.Score), 0.5):F4} {what}");
        }
    }

    private static void PrintErrors(List<(DeltaRow First, AnomalyPrediction Second)> results)
    {
        var missed = results.Where(x => x.First.IsAttack && !x.Second.IsAnomaly).ToList();
        Console.WriteLine($"\nПропущені атаки ({missed.Count}):");
        foreach (var (r, p) in missed.Take(20))
            Console.WriteLine($"  рядок {r.Line,5}  {r.SessionId,-14} Score {p.Score:F4}  tcpCurrEstab={r.Features[13]}, ΔtcpInSegs={r.Features[9]}, ΔifInOctets={r.Features[0]}");
        if (missed.Count > 20) Console.WriteLine($"  ... ще {missed.Count - 20}");

        var falseAlarms = results.Where(x => !x.First.IsAttack && x.Second.IsAnomaly).ToList();
        Console.WriteLine($"Хибні тривоги ({falseAlarms.Count}):");
        foreach (var (r, p) in falseAlarms)
            Console.WriteLine($"  рядок {r.Line,5}  {r.SessionId,-14} Score {p.Score:F4}");
    }
}

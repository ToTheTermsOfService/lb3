using NetworkAnomaly.Common;

namespace NetworkAnomaly.Trainer;

public static class Baselines
{
    public static (string ZName, List<(float Score, bool Attack)> ZPairs) Run(
        DataSplit split, IReadOnlyDictionary<int, RawPoll> pollByLine, string[] featureNames)
    {
        ConsoleReport.Header("6. Базові рішення на test");
        var test = split.Test;
        int nAtt = test.Count(r => r.IsAttack), nNorm = test.Count - nAtt;
        ConsoleReport.Rates("Усе — аномалія", nAtt, 0, nNorm, 0);

        SessionConstantsRule(split.Train, test, pollByLine, featureNames);
        return ZScoreDetector(split);
    }

    private static void SessionConstantsRule(List<DeltaRow> train, List<DeltaRow> test,
        IReadOnlyDictionary<int, RawPoll> pollByLine, string[] featureNames)
    {
        var trainPolls = train.Select(r => pollByLine[r.Line]).ToList();
        var reference = trainPolls[0].Values;
        var constIdx = Enumerable.Range(0, Schema.FeatureCount)
            .Where(i => trainPolls.All(p => p.Values[i] == reference[i])).ToArray();
        Console.WriteLine($"Константні в сирих значеннях train normal: {string.Join(", ", constIdx.Select(i => featureNames[i]))}");
        ConsoleReport.Rates("Правило «константи сесії» (сирі значення)", test,
            r => constIdx.Any(i => pollByLine[r.Line].Values[i] != reference[i]));
    }

    private static (string, List<(float Score, bool Attack)>) ZScoreDetector(DataSplit split)
    {
        var logTrain = split.Train.Select(r => r.Features.Select(v => Math.Log(v + 1.0)).ToArray()).ToList();
        var mu = Enumerable.Range(0, Schema.FeatureCount).Select(i => logTrain.Average(x => x[i])).ToArray();
        var sd = Enumerable.Range(0, Schema.FeatureCount)
            .Select(i => Math.Sqrt(logTrain.Average(x => (x[i] - mu[i]) * (x[i] - mu[i])))).ToArray();

        float ZScore(DeltaRow r, bool useMax, double floor)
        {
            var z = Enumerable.Range(0, Schema.FeatureCount)
                .Select(i => Math.Abs((Math.Log(r.Features[i] + 1.0) - mu[i]) / Math.Max(floor, sd[i])));
            return (float)(useMax ? z.Max() : z.Average());
        }

        Console.WriteLine("Детектор z-оцінок (без PCA), підбір на validation:");
        var best = (UseMax: true, Floor: 0.0, Auc: -1.0);
        foreach (var useMax in new[] { true, false })
            foreach (var floor in new[] { 1e-3, 0.1, 1.0 })
            {
                double auc = Stats.Auc(split.Validation.Select(r => (ZScore(r, useMax, floor), r.IsAttack)).ToList());
                Console.WriteLine($"  {(useMax ? "max" : "mean")} |z|, σ ≥ {floor,-6} AUC(val) = {auc:F4}");
                if (auc > best.Auc) best = (useMax, floor, auc);
            }

        string name = $"{(best.UseMax ? "max" : "mean")} |z| (σ ≥ {best.Floor})";
        float Z(DeltaRow r) => ZScore(r, best.UseMax, best.Floor);
        float thr = Stats.Percentile(split.Validation.Where(r => !r.IsAttack).Select(Z), Settings.ThresholdPercentile);
        var pairs = split.Test.Select(r => (Score: Z(r), Attack: r.IsAttack)).ToList();
        ConsoleReport.Rates($"Детектор {name}", split.Test, r => Z(r) > thr);
        Console.WriteLine($"  AUC = {Stats.Auc(pairs):F4}");
        return (name, pairs);
    }
}

using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;
using Microsoft.ML.Transforms;
using NetworkAnomaly.Common;

namespace NetworkAnomaly.Trainer;

public sealed record FittedModel(ITransformer Full, ITransformer WithoutFix);

public sealed class AnomalyModelFactory(MLContext ml)
{
    public MLContext Ml => ml;

    public FittedModel Fit(string prep, int rank, IEnumerable<DeltaRow> rows, float? threshold = null)
    {
        var data = ml.Data.LoadFromEnumerable(rows.Select(LabeledInput.From));
        var prepT = Preprocessing(prep).Fit(data);
        var pca = ml.AnomalyDetection.Trainers.RandomizedPca(new RandomizedPcaTrainer.Options
        {
            FeatureColumnName = "NormFeatures",
            Rank = rank,
            Oversampling = 20,
            EnsureZeroMean = true,
            Seed = Settings.Seed,
        }).Fit(prepT.Transform(data));
        if (threshold is float t) pca = ml.AnomalyDetection.ChangeModelThreshold(pca, t);
        var noFix = new TransformerChain<ITransformer>(prepT, pca);
        var fixNaN = ml.Transforms.ReplaceMissingValues("Score",
            replacementMode: MissingValueReplacingEstimator.ReplacementMode.DefaultValue).Fit(noFix.Transform(data));
        return new FittedModel(new TransformerChain<ITransformer>(prepT, pca, fixNaN), noFix);
    }

    public float[] Score(ITransformer model, IEnumerable<DeltaRow> rows) => ScoreWithNaNCount(model, rows).Scores;

    public (float[] Scores, int NaN) ScoreWithNaNCount(ITransformer model, IEnumerable<DeltaRow> rows)
    {
        var raw = ml.Data.CreateEnumerable<AnomalyPrediction>(
            model.Transform(ml.Data.LoadFromEnumerable(rows.Select(LabeledInput.From))), false).Select(p => p.Score).ToArray();
        return (raw.Select(s => float.IsNaN(s) ? 0f : s).ToArray(), raw.Count(float.IsNaN));
    }

    public static List<(float Score, bool Attack)> Pairs(float[] scores, IReadOnlyList<DeltaRow> rows) =>
        scores.Zip(rows, (s, r) => (s, r.IsAttack)).ToList();

    private IEstimator<ITransformer> Preprocessing(string kind) => kind switch
    {
        "none" => ml.Transforms.CopyColumns("NormFeatures", "Features"),
        "log" => ml.Transforms.Expression("NormFeatures", "x => log(x + 1)", "Features"),
        "meanvar" => ml.Transforms.NormalizeMeanVariance("NormFeatures", "Features"),
        "log+meanvar" => ml.Transforms.Expression("LogFeatures", "x => log(x + 1)", "Features")
                           .Append(ml.Transforms.NormalizeMeanVariance("NormFeatures", "LogFeatures")),
        _ => throw new ArgumentException(kind),
    };
}

using Microsoft.ML.Data;

namespace NetworkAnomaly.Common;

public class LabeledInput
{
    [VectorType(Schema.FeatureCount)]
    public float[] Features { get; set; } = [];

    public float Label { get; set; }

    public static LabeledInput From(DeltaRow r) => new() { Features = r.Features, Label = r.IsAttack ? 1f : 0f };
}

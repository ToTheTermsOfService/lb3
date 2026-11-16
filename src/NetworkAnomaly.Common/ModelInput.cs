using Microsoft.ML.Data;

namespace NetworkAnomaly.Common;

public class ModelInput
{
    [VectorType(Schema.FeatureCount)]
    public float[] Features { get; set; } = [];
}

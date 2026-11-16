using Microsoft.ML.Data;

namespace NetworkAnomaly.Common;

public class AnomalyPrediction
{
    [ColumnName("PredictedLabel")]
    public bool IsAnomaly { get; set; }

    public float Score { get; set; }
}

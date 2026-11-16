using Microsoft.ML.Data;

namespace NetworkAnomaly.Common;

public static class Schema
{
    /// <summary>Кількість SNMP MIB-ознак.</summary>
    public const int FeatureCount = 34;
    public const string NormalClass = "normal";

    /// <summary>
    /// tcpCurrEstab — gauge (поточна кількість з'єднань), а не лічильник: для нього береться саме значення, не приріст.
    /// </summary>
    public static readonly int[] GaugeIndices = [13];
}

/// <summary>Один SNMP-опит пристрою: сирі значення лічильників (double, щоб не втратити точність 32-бітних лічильників).</summary>
public sealed record RawPoll(int Line, double[] Values, string? Class, string? Session);

/// <summary>Вектор приростів між двома сусідніми опитами однієї сесії.</summary>
public sealed record DeltaRow(int Line, string SessionId, float[] Features, string? Class)
{
    public bool IsAttack => Class is not null && Class != Schema.NormalClass;
}

/// <summary>Вхід моделі — лише ознаки. Саме з цією схемою модель серіалізується, мітка клієнту не потрібна.</summary>
public class ModelInput
{
    [VectorType(Schema.FeatureCount)]
    public float[] Features { get; set; } = [];
}

/// <summary>Вхід для оцінювання: ознаки + мітка (1 — атака). Evaluator ML.NET вимагає мітку типу Single.</summary>
public class LabeledInput
{
    [VectorType(Schema.FeatureCount)]
    public float[] Features { get; set; } = [];

    public float Label { get; set; }

    public static LabeledInput From(DeltaRow r) => new() { Features = r.Features, Label = r.IsAttack ? 1f : 0f };
}

/// <summary>Вихід RandomizedPcaTrainer.</summary>
public class AnomalyPrediction
{
    /// <summary>true — Score вищий за поріг моделі.</summary>
    [ColumnName("PredictedLabel")]
    public bool IsAnomaly { get; set; }

    /// <summary>Відносна помилка реконструкції PCA, діапазон [0, 1].</summary>
    public float Score { get; set; }
}

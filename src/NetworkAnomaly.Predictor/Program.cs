using System.Globalization;
using System.Text;
using Microsoft.ML;
using NetworkAnomaly.Common;
using NetworkAnomaly.Predictor;

Console.OutputEncoding = Encoding.UTF8;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
var ml = new MLContext(seed: 1);

try
{
    string modelPath = ProjectPaths.ModelFile;
    if (!File.Exists(modelPath))
        return Fail($"Модель не знайдено: {modelPath}\nСпершу запустіть NetworkAnomaly.Trainer.");

    ITransformer model = ml.Model.Load(modelPath, out var inputSchema);
    Console.WriteLine($"Модель завантажено: {modelPath}");
    Console.WriteLine("Вхідна схема моделі: " + string.Join(", ", inputSchema.Select(c => $"{c.Name}: {c.Type}")));

    string csvPath = args.Length > 0 ? args[0] : ProjectPaths.HoldoutFile;
    if (!File.Exists(csvPath))
        return Fail($"Файл даних не знайдено: {csvPath}");

    var (_, polls) = SnmpCsv.Read(csvPath);
    var sessions = SnmpSessions.Split(polls);
    var rows = sessions.SelectMany((s, i) => DeltaFeatures.Build(s, s[0].Session ?? $"S{i + 1}")).ToList();
    bool labeled = polls.All(p => !string.IsNullOrEmpty(p.Class));
    Console.WriteLine($"\nФайл: {csvPath}");
    Console.WriteLine($"Опитів: {polls.Count}, сесій: {sessions.Count}, векторів приростів: {rows.Count}, мітки класу: {(labeled ? "є (буде перевірка)" : "немає")}");
    if (rows.Count == 0) return Fail("Замало опитів: для приростів потрібно щонайменше 2 опити в сесії.");

    var engine = ml.Model.CreatePredictionEngine<ModelInput, AnomalyPrediction>(model);
    PredictionOutput.Sample(engine, rows, labeled);

    var view = ml.Data.LoadFromEnumerable(rows.Select(r => new ModelInput { Features = r.Features }));
    var preds = ml.Data.CreateEnumerable<AnomalyPrediction>(model.Transform(view), reuseRowObject: false).ToList();
    var results = rows.Zip(preds).ToList();
    PredictionOutput.BySession(results);
    if (labeled) PredictionOutput.LabelCheck(results);
    return 0;
}
catch (Exception ex) when (ex is InvalidDataException or IOException or FormatException or InvalidOperationException)
{
    return Fail($"Помилка: {ex.Message}");
}

static int Fail(string message)
{
    Console.Error.WriteLine(message);
    return 1;
}

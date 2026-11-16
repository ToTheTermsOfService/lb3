using System.Globalization;
using System.Text;
using Microsoft.ML;
using NetworkAnomaly.Common;
using NetworkAnomaly.Trainer;

Console.OutputEncoding = Encoding.UTF8;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
var ml = new MLContext(seed: Settings.Seed);
var factory = new AnomalyModelFactory(ml);

var (featureNames, polls) = SnmpCsv.Read(ProjectPaths.DataFile);
var sessions = Session.FromPolls(polls);
var pollByLine = polls.ToDictionary(p => p.Line);
PrintSessions(sessions, polls.Count);

var split = DataSplit.ByTime(sessions);
ConsoleReport.Header("2. Розбиття (за часом, без перемішування)");
ConsoleReport.Split("train", split.Train);
ConsoleReport.Split("validation", split.Validation);
ConsoleReport.Split("test", split.Test);

var best = HyperparameterSearch.Run(factory, split.Train, split.Validation);

ConsoleReport.Header("4. Фінальна модель");
var untuned = factory.Fit(best.Prep, best.Rank, split.Train);
var valNormalScores = factory.Score(untuned.Full, split.Validation.Where(r => !r.IsAttack));
float threshold = Stats.Percentile(valNormalScores, Settings.ThresholdPercentile);
var trainScores = factory.Score(untuned.Full, split.Train);
Console.WriteLine($"Score на train normal:      медіана {Stats.Percentile(trainScores, 0.5):F4}, 99% {Stats.Percentile(trainScores, 0.99):F4}");
Console.WriteLine($"Score на validation normal: медіана {Stats.Percentile(valNormalScores, 0.5):F4}, 99% {threshold:F4}");
Console.WriteLine($"Поріг аномалії = {threshold:F4} (99-й перцентиль на validation normal: поріг визначається на даних, яких PCA не бачив).");

var final = factory.Fit(best.Prep, best.Rank, split.Train, threshold);
var (_, nanTrain) = factory.ScoreWithNaNCount(final.WithoutFix, split.Train);
Console.WriteLine($"Записів train із Score = NaN до заміни: {nanTrain} з {split.Train.Count} (див. звіт, розділ про NaN).");

var testPairs = TestEvaluation.Run(ml, final.Full, split.Test);
var (zName, zPairs) = Baselines.Run(split, pollByLine, featureNames);
var rocAbsolute = Generalization.Run(factory, sessions, split.Test, pollByLine, best);

ConsoleReport.Header("8. Збереження");
Directory.CreateDirectory(ProjectPaths.ReportsDir);
string rocPath = Path.Combine(ProjectPaths.ReportsDir, "roc_test.svg");
RocChart.WriteSvg(rocPath, [
    ("PCA, прирости (фінальна)", "#1f77b4", Stats.Roc(testPairs)),
    ($"z-оцінки: {zName}", "#2ca02c", Stats.Roc(zPairs)),
    ("PCA, абсолютні значення (P2)", "#d62728", rocAbsolute),
]);
Console.WriteLine($"ROC-криві: {rocPath}");

SnmpCsv.Write(ProjectPaths.HoldoutFile, featureNames, split.Holdout);
Console.WriteLine($"Відкладені тестові опити для Predictor: {ProjectPaths.HoldoutFile} ({split.Holdout.Count} опитів)");

var inputSchema = ml.Data.LoadFromEnumerable(new List<ModelInput>()).Schema;
Directory.CreateDirectory(Path.GetDirectoryName(ProjectPaths.ModelFile)!);
ml.Model.Save(final.Full, inputSchema, ProjectPaths.ModelFile);
Console.WriteLine($"Модель: {ProjectPaths.ModelFile} ({new FileInfo(ProjectPaths.ModelFile).Length / 1024.0:F1} КБ)");
return 0;

static void PrintSessions(List<Session> sessions, int pollCount)
{
    ConsoleReport.Header("1. Дані");
    Console.WriteLine($"Файл: {ProjectPaths.DataFile}");
    Console.WriteLine($"Опитів: {pollCount}, ознак: {Schema.FeatureCount}. Рядки впорядковані в часі й утворюють {sessions.Count} сесій:");
    Console.WriteLine($"  {"Сесія",-14} {"Рядки CSV",-12} {"Опитів",7} {"Приростів",10}");
    foreach (var s in sessions)
        Console.WriteLine($"  {s.Id,-14} {s.Polls[0].Line + "-" + s.Polls[^1].Line,-12} {s.Polls.Count,7} {s.Deltas.Count,10}");
    Console.WriteLine("Ознаки моделі: прирости лічильників між сусідніми опитами (переповнення 2^32 і скидання оброблено),");
    Console.WriteLine("для gauge tcpCurrEstab — поточне значення. Перший опит кожної сесії приросту не має.");
}

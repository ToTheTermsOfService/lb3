// Частина 2: завантаження (десеріалізація) моделі та її використання для нових SNMP-опитів.
// Запуск: dotnet run --project src/NetworkAnomaly.Predictor [шлях_до_csv]
//   CSV: 34 колонки лічильників у порядку часу; необов'язкові колонки "class" (для перевірки) і "session".
//   За замовчуванням — Data/holdout_test.csv (тестові опити, яких модель не бачила під час навчання та підбору).
//   Шлях до моделі можна перевизначити змінною оточення NAD_MODEL.
using System.Globalization;
using System.Text;
using Microsoft.ML;
using NetworkAnomaly.Common;

Console.OutputEncoding = Encoding.UTF8;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
var ml = new MLContext(seed: 1);

try
{
    // ---------- 1. Десеріалізація ----------
    string modelPath = ProjectPaths.ModelFile;
    if (!File.Exists(modelPath))
        return Fail($"Модель не знайдено: {modelPath}\nСпершу запустіть NetworkAnomaly.Trainer.");

    ITransformer model = ml.Model.Load(modelPath, out var inputSchema);
    Console.WriteLine($"Модель завантажено: {modelPath}");
    Console.WriteLine("Вхідна схема моделі: " + string.Join(", ", inputSchema.Select(c => $"{c.Name}: {c.Type}")));
    var engine = ml.Model.CreatePredictionEngine<ModelInput, AnomalyPrediction>(model);

    // ---------- 2. Дані: опити -> сесії -> прирости ----------
    string csvPath = args.Length > 0 ? args[0] : ProjectPaths.HoldoutFile;
    if (!File.Exists(csvPath))
        return Fail($"Файл даних не знайдено: {csvPath}");

    var (_, polls) = SnmpData.ReadCsv(csvPath);
    var sessions = SnmpData.SplitSessions(polls);
    var rows = sessions.SelectMany((s, i) => SnmpData.ToDeltas(s, s[0].Session ?? $"S{i + 1}")).ToList();
    bool labeled = polls.All(p => !string.IsNullOrEmpty(p.Class));
    Console.WriteLine($"\nФайл: {csvPath}");
    Console.WriteLine($"Опитів: {polls.Count}, сесій: {sessions.Count}, векторів приростів: {rows.Count}, мітки класу: {(labeled ? "є (буде перевірка)" : "немає")}");
    if (rows.Count == 0) return Fail("Замало опитів: для приростів потрібно щонайменше 2 опити в сесії.");

    // ---------- 3. Одиночні прогнози через PredictionEngine ----------
    var rnd = new Random(7);
    var sample = rows.OrderBy(_ => rnd.Next()).Take(15).OrderBy(r => r.Line).ToList();
    Console.WriteLine($"\nPredictionEngine, {sample.Count} випадкових записів:");
    Console.WriteLine($"  {"Рядок",6} {"Сесія",-14} {"Прогноз",-9} {"Score",7}" + (labeled ? "  Справжній клас" : ""));
    foreach (var r in sample)
    {
        var p = engine.Predict(new ModelInput { Features = r.Features });
        string verdict = p.IsAnomaly ? "АНОМАЛІЯ" : "норма";
        string check = labeled ? $"  {r.Class,-11} {(p.IsAnomaly == r.IsAttack ? "вірно" : "ПОМИЛКА")}" : "";
        Console.WriteLine($"  {r.Line,6} {r.SessionId,-14} {verdict,-9} {p.Score,7:F4}{check}");
    }

    // ---------- 4. Пакетний прогноз ----------
    var view = ml.Data.LoadFromEnumerable(rows.Select(r => new ModelInput { Features = r.Features }));
    var preds = ml.Data.CreateEnumerable<AnomalyPrediction>(model.Transform(view), reuseRowObject: false).ToList();
    var results = rows.Zip(preds).ToList();

    Console.WriteLine("\nПакетний прогноз за сесіями:");
    Console.WriteLine($"  {"Сесія",-14} {"Векторів",9} {"Аномалій",9} {"Частка",8}  Перша аномалія (рядок CSV)");
    foreach (var g in results.GroupBy(x => x.First.SessionId))
    {
        int k = g.Count(x => x.Second.IsAnomaly);
        var first = g.FirstOrDefault(x => x.Second.IsAnomaly).First;
        Console.WriteLine($"  {g.Key,-14} {g.Count(),9} {k,9} {k / (double)g.Count(),8:P1}  {(first is null ? "—" : first.Line.ToString())}");
    }
    Console.WriteLine($"Усього аномалій: {preds.Count(p => p.IsAnomaly)} з {preds.Count}");

    if (labeled)
    {
        int tp = results.Count(x => x.First.IsAttack && x.Second.IsAnomaly), pos = results.Count(x => x.First.IsAttack);
        int fp = results.Count(x => !x.First.IsAttack && x.Second.IsAnomaly), neg = results.Count - pos;
        Console.WriteLine("\nПеревірка за мітками (лише для демонстрації; повне оцінювання — у Trainer):");
        if (pos > 0) Console.WriteLine($"  Виявлено атак:  {tp}/{pos} = {tp / (double)pos:P1}");
        if (neg > 0) Console.WriteLine($"  Хибні тривоги:  {fp}/{neg} = {fp / (double)neg:P1}");
    }
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

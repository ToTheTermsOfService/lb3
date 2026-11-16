// Частина 1: тренування, оцінювання та серіалізація моделі виявлення аномалій (RandomizedPcaTrainer).
using System.Globalization;
using System.Text;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;
using Microsoft.ML.Transforms;
using NetworkAnomaly.Common;
using NetworkAnomaly.Trainer;

Console.OutputEncoding = Encoding.UTF8;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
const int Seed = 42;
const double ThresholdPercentile = 0.99; // поріг = 99-й перцентиль Score на відкладеному normal (ціль ≈1% хибних тривог)
var ml = new MLContext(seed: Seed);

// =====================================================================
// 1. Дані: опити -> сесії -> прирости
// =====================================================================
var (featureNames, polls) = SnmpData.ReadCsv(ProjectPaths.DataFile);
var perClass = new Dictionary<string, int>();
var sessions = SnmpData.SplitSessions(polls).Select(s =>
{
    string cls = s[0].Class!;
    perClass[cls] = perClass.GetValueOrDefault(cls) + 1;
    string id = $"{cls}#{perClass[cls]}";
    return new Session(id, cls, s, SnmpData.ToDeltas(s, id));
}).ToList();
var pollByLine = polls.ToDictionary(p => p.Line);

Header("1. Дані");
Console.WriteLine($"Файл: {ProjectPaths.DataFile}");
Console.WriteLine($"Опитів: {polls.Count}, ознак: {Schema.FeatureCount}. Рядки впорядковані в часі й утворюють {sessions.Count} сесій:");
Console.WriteLine($"  {"Сесія",-14} {"Рядки CSV",-12} {"Опитів",7} {"Приростів",10}");
foreach (var s in sessions)
    Console.WriteLine($"  {s.Id,-14} {s.Polls[0].Line + "-" + s.Polls[^1].Line,-12} {s.Polls.Count,7} {s.Deltas.Count,10}");
Console.WriteLine("Ознаки моделі: прирости лічильників між сусідніми опитами (переповнення 2^32 і скидання оброблено),");
Console.WriteLine("для gauge tcpCurrEstab — поточне значення. Перший опит кожної сесії приросту не має.");

// =====================================================================
// 2. Часове розбиття всередині кожної сесії
// =====================================================================
// normal: перші 60% -> train, наступні 20% -> validation, останні 20% -> test.
// атаки (у тренуванні не беруть участі): перша половина -> validation, друга -> test.
var train = new List<DeltaRow>();
var val = new List<DeltaRow>();
var test = new List<DeltaRow>();
var holdout = new List<(RawPoll, string)>();
foreach (var s in sessions)
{
    int n = s.Deltas.Count, testStart;
    if (s.IsNormal)
    {
        int a = (int)(n * 0.6), b = (int)(n * 0.8);
        train.AddRange(s.Deltas.GetRange(0, a));
        val.AddRange(s.Deltas.GetRange(a, b - a));
        test.AddRange(s.Deltas.GetRange(b, n - b));
        testStart = b;
    }
    else
    {
        int a = n / 2;
        val.AddRange(s.Deltas.GetRange(0, a));
        test.AddRange(s.Deltas.GetRange(a, n - a));
        testStart = a;
    }
    // приріст з індексом d рахується з опитів d і d+1, тож для тестових приростів потрібні опити з testStart
    holdout.AddRange(s.Polls.Skip(testStart).Select(p => (p, s.Id)));
}

Header("2. Розбиття (за часом, без перемішування)");
PrintSplit("train", train);
PrintSplit("validation", val);
PrintSplit("test", test);

// =====================================================================
// 3. Підбір передобробки та рангу на validation
// =====================================================================
string[] preps = ["none", "log", "meanvar", "log+meanvar"];
int[] ranks = [1, 2, 3, 5, 8, 12];

Header("3. Абляція передобробки й рангу (validation; модель навчена на train normal)");
Console.WriteLine("TPR@1%FPR: частка атак validation вище порогу = 99-й перцентиль Score на normal validation.");
Console.WriteLine($"  {"Передобробка",-12} {"Rank",4} {"AUC",8} {"TPR@1%FPR",10} {"NaN у Score",12}");
var grid = new List<(string Prep, int Rank, double Auc, double Tpr)>();
foreach (var prep in preps)
    foreach (var rank in ranks)
    {
        var m = Fit(prep, rank, train);
        var (scores, nan) = ScoreWithNaNCount(m.WithoutFix, val);
        var pairs = Pairs(scores, val);
        float thr = Stats.Percentile(pairs.Where(p => !p.Attack).Select(p => p.Score), ThresholdPercentile);
        double tpr = pairs.Where(p => p.Attack).Average(p => p.Score > thr ? 1.0 : 0.0);
        double auc = Stats.Auc(pairs);
        grid.Add((prep, rank, auc, tpr));
        Console.WriteLine($"  {prep,-12} {rank,4} {auc,8:F4} {tpr,10:P1} {nan,12}");
    }
var best = grid.OrderByDescending(g => Math.Round(g.Auc, 3)).ThenByDescending(g => g.Tpr).ThenBy(g => g.Rank).First();
Console.WriteLine($"Обрано: передобробка = {best.Prep}, Rank = {best.Rank} (найвищий AUC на validation; за рівності — вищий TPR, менший ранг).");

// =====================================================================
// 4. Фінальна модель: поріг з normal validation
// =====================================================================
Header("4. Фінальна модель");
var trainedNoThreshold = Fit(best.Prep, best.Rank, train);
var valNormal = val.Where(r => !r.IsAttack).ToList();
var valNormalScores = Score(trainedNoThreshold.Full, valNormal);
float threshold = Stats.Percentile(valNormalScores, ThresholdPercentile);
var trainScores = Score(trainedNoThreshold.Full, train);
Console.WriteLine($"Score на train normal:      медіана {Stats.Percentile(trainScores, 0.5):F4}, 99% {Stats.Percentile(trainScores, 0.99):F4}");
Console.WriteLine($"Score на validation normal: медіана {Stats.Percentile(valNormalScores, 0.5):F4}, 99% {threshold:F4}");
Console.WriteLine($"Поріг аномалії = {threshold:F4} (99-й перцентиль на validation normal: поріг визначається на даних, яких PCA не бачив).");

var final = Fit(best.Prep, best.Rank, train, threshold);
var (_, nanTrain) = ScoreWithNaNCount(final.WithoutFix, train);
Console.WriteLine($"Записів train із Score = NaN до заміни: {nanTrain} з {train.Count} (див. звіт, розділ про NaN).");

// =====================================================================
// 5. Оцінювання на test (один раз)
// =====================================================================
Header("5. Оцінювання на test");
var testView = ml.Data.LoadFromEnumerable(test.Select(LabeledInput.From));
var scoredTest = final.Full.Transform(testView);
var mlMetrics = ml.AnomalyDetection.Evaluate(scoredTest, falsePositiveCount: 1);
var preds = ml.Data.CreateEnumerable<AnomalyPrediction>(scoredTest, false).ToList();
var testPairs = test.Zip(preds, (r, p) => (Score: p.Score, Attack: r.IsAttack)).ToList();

int tp = 0, fn = 0, fp = 0, tn = 0;
foreach (var (r, p) in test.Zip(preds))
    if (r.IsAttack) { if (p.IsAnomaly) tp++; else fn++; }
    else { if (p.IsAnomaly) fp++; else tn++; }

Console.WriteLine($"ML.NET Evaluate: AUC = {mlMetrics.AreaUnderRocCurve:F4}, detection rate @ 1 FP = {mlMetrics.DetectionRateAtFalsePositiveCount:F4}");
Console.WriteLine($"Власний AUC (перевірка): {Stats.Auc(testPairs):F4}");
PrintRates("PCA (фінальна модель)", tp, fn, fp, tn);
Console.WriteLine("\nМатриця помилок:");
Console.WriteLine($"                  прогноз: аномалія   прогноз: норма");
Console.WriteLine($"  факт: атака     {tp,17}   {fn,14}");
Console.WriteLine($"  факт: normal    {fp,17}   {tn,14}");

Console.WriteLine("\nЗа класами (test):");
foreach (var g in test.Zip(preds).GroupBy(x => x.First.Class).OrderBy(g => g.Key))
{
    int k = g.Count(x => x.Second.IsAnomaly), n = g.Count();
    var ci = Stats.Wilson(k, n);
    string what = g.Key == Schema.NormalClass ? "<- хибні тривоги" : "";
    Console.WriteLine($"  {g.Key,-12} {k,4}/{n,-4} {k / (double)n,7:P1}  95% ДІ [{ci.Lo:P1}; {ci.Hi:P1}]  медіана Score {Stats.Percentile(g.Select(x => x.Second.Score), 0.5):F4} {what}");
}

var missed = test.Zip(preds).Where(x => x.First.IsAttack && !x.Second.IsAnomaly).ToList();
Console.WriteLine($"\nПропущені атаки ({missed.Count}):");
foreach (var (r, p) in missed.Take(20))
    Console.WriteLine($"  рядок {r.Line,5}  {r.SessionId,-14} Score {p.Score:F4}  tcpCurrEstab={r.Features[13]}, ΔtcpInSegs={r.Features[9]}, ΔifInOctets={r.Features[0]}");
if (missed.Count > 20) Console.WriteLine($"  ... ще {missed.Count - 20}");
var falseAlarms = test.Zip(preds).Where(x => !x.First.IsAttack && x.Second.IsAnomaly).ToList();
Console.WriteLine($"Хибні тривоги ({falseAlarms.Count}):");
foreach (var (r, p) in falseAlarms)
    Console.WriteLine($"  рядок {r.Line,5}  {r.SessionId,-14} Score {p.Score:F4}");

// =====================================================================
// 6. Базові рішення (baseline) на тому ж test
// =====================================================================
Header("6. Базові рішення на test");
int nAtt = test.Count(r => r.IsAttack), nNorm = test.Count - nAtt;
PrintRates("Усе — аномалія", nAtt, 0, nNorm, 0);

// Правило «відбиток сесії»: ознаки, що константні в сирих значеннях train normal; аномалія, якщо хоч одна інша.
var trainNormalPolls = train.Select(r => pollByLine[r.Line]).ToList();
var constIdx = Enumerable.Range(0, Schema.FeatureCount)
    .Where(i => trainNormalPolls.All(p => p.Values[i] == trainNormalPolls[0].Values[i])).ToArray();
bool ConstRule(DeltaRow r) => constIdx.Any(i => pollByLine[r.Line].Values[i] != trainNormalPolls[0].Values[i]);
Console.WriteLine($"Константні в сирих значеннях train normal: {string.Join(", ", constIdx.Select(i => featureNames[i]))}");
PrintRates("Правило «константи сесії» (сирі значення)", Count(test, r => r.IsAttack && ConstRule(r)), Count(test, r => r.IsAttack && !ConstRule(r)),
    Count(test, r => !r.IsAttack && ConstRule(r)), Count(test, r => !r.IsAttack && !ConstRule(r)));

// Простий детектор без PCA: z-оцінки log-приростів відносно train normal, агрегація max або mean.
// Нижня межа σ (sdFloor) потрібна, бо частина ознак у train константна; агрегацію і межу обираємо на validation.
var logTrain = train.Select(r => r.Features.Select(v => Math.Log(v + 1.0)).ToArray()).ToList();
var mu = Enumerable.Range(0, Schema.FeatureCount).Select(i => logTrain.Average(x => x[i])).ToArray();
var sdRaw = Enumerable.Range(0, Schema.FeatureCount)
    .Select(i => Math.Sqrt(logTrain.Average(x => (x[i] - mu[i]) * (x[i] - mu[i])))).ToArray();
float ZScore(DeltaRow r, bool useMax, double floor)
{
    var z = Enumerable.Range(0, Schema.FeatureCount).Select(i => Math.Abs((Math.Log(r.Features[i] + 1.0) - mu[i]) / Math.Max(floor, sdRaw[i])));
    return (float)(useMax ? z.Max() : z.Average());
}
Console.WriteLine("Детектор z-оцінок (без PCA), підбір на validation:");
var zBest = (UseMax: true, Floor: 0.0, Auc: -1.0);
foreach (var useMax in new[] { true, false })
    foreach (var floor in new[] { 1e-3, 0.1, 1.0 })
    {
        double auc = Stats.Auc(val.Select(r => (ZScore(r, useMax, floor), r.IsAttack)).ToList());
        Console.WriteLine($"  {(useMax ? "max" : "mean")} |z|, σ ≥ {floor,-6} AUC(val) = {auc:F4}");
        if (auc > zBest.Auc) zBest = (useMax, floor, auc);
    }
string zName = $"{(zBest.UseMax ? "max" : "mean")} |z| (σ ≥ {zBest.Floor})";
float Z(DeltaRow r) => ZScore(r, zBest.UseMax, zBest.Floor);
float zThr = Stats.Percentile(valNormal.Select(Z), ThresholdPercentile);
var zPairs = test.Select(r => (Score: Z(r), Attack: r.IsAttack)).ToList();
PrintRates($"Детектор {zName}", Count(test, r => r.IsAttack && Z(r) > zThr), Count(test, r => r.IsAttack && Z(r) <= zThr),
    Count(test, r => !r.IsAttack && Z(r) > zThr), Count(test, r => !r.IsAttack && Z(r) <= zThr));
Console.WriteLine($"  AUC = {Stats.Auc(zPairs):F4}");

// =====================================================================
// 7. Узагальнення: інші протоколи розбиття, абсолютні значення vs прирости
// =====================================================================
Header("7. Узагальнення на нові дані: абсолютні значення (перша версія) vs прирости");
var normalSessions = sessions.Where(s => s.IsNormal).ToList();
var testAttacks = test.Where(r => r.IsAttack).ToList();
List<DeltaRow> Abs(IEnumerable<RawPoll> ps, string sid) => ps.Select(p => new DeltaRow(p.Line, sid, p.Values.Select(v => (float)v).ToArray(), p.Class)).ToList();
var testAttackPollsAbs = testAttacks.Select(r => Abs([pollByLine[r.Line]], r.SessionId)[0]).ToList();

Console.WriteLine("Атаки в усіх протоколах — test-половини атакувальних сесій. Поріг — 99-й перцентиль на вказаних normal.");
Console.WriteLine($"  {"Протокол",-38} {"Ознаки",-24} {"AUC",7} {"FPR (normal)",22} {"TPR",8}");
var rocAbs = new List<(double, double)>();
foreach (var useDeltas in new[] { false, true })
{
    string prep = useDeltas ? best.Prep : "log";
    int rank = useDeltas ? best.Rank : 8;
    string feat = useDeltas ? $"прирости ({prep}, r={rank})" : "абсолютні (log, r=8)";
    List<DeltaRow> Rows(Session s) => useDeltas ? s.Deltas : Abs(s.Polls, s.Id);
    var attacks = useDeltas ? testAttacks : testAttackPollsAbs;

    // P1: випадкове 80/20, поріг на train (як у першій версії)
    var rnd = new Random(Seed);
    var allNormal = normalSessions.SelectMany(Rows).OrderBy(_ => rnd.Next()).ToList();
    int cut = (int)(allNormal.Count * 0.8);
    Run("P1 випадкове 80/20, поріг на train", feat, prep, rank, allNormal.Take(cut).ToList(), allNormal.Take(cut).ToList(), allNormal.Skip(cut).ToList(), attacks);

    // P2: часове 60/20/20 у кожній сесії
    var (tr, th, te) = (new List<DeltaRow>(), new List<DeltaRow>(), new List<DeltaRow>());
    foreach (var s in normalSessions)
    {
        var rows = Rows(s); int n = rows.Count, a = (int)(n * 0.6), b = (int)(n * 0.8);
        tr.AddRange(rows.GetRange(0, a)); th.AddRange(rows.GetRange(a, b - a)); te.AddRange(rows.GetRange(b, n - b));
    }
    var p2 = Run("P2 часове 60/20/20", feat, prep, rank, tr, th, te, attacks);
    if (!useDeltas) rocAbs = Stats.Roc(p2);

    // P3/P4: навчання на одній сесії normal, тест на іншій
    for (int k = 0; k < 2; k++)
    {
        var src = Rows(normalSessions[k]); var dst = Rows(normalSessions[1 - k]);
        int a = (int)(src.Count * 0.8);
        Run($"P{3 + k} {normalSessions[k].Id} -> {normalSessions[1 - k].Id}", feat, prep, rank,
            src.GetRange(0, a), src.GetRange(a, src.Count - a), dst, attacks);
    }
}

// =====================================================================
// 8. ROC-криві та серіалізація
// =====================================================================
Header("8. Збереження");
Directory.CreateDirectory(ProjectPaths.ReportsDir);
string rocPath = Path.Combine(ProjectPaths.ReportsDir, "roc_test.svg");
Stats.WriteRocSvg(rocPath, [
    ("PCA, прирости (фінальна)", "#1f77b4", Stats.Roc(testPairs)),
    ($"z-оцінки: {zName}", "#2ca02c", Stats.Roc(zPairs)),
    ("PCA, абсолютні значення (P2)", "#d62728", rocAbs),
]);
Console.WriteLine($"ROC-криві: {rocPath}");

SnmpData.WriteCsv(ProjectPaths.HoldoutFile, featureNames, holdout);
Console.WriteLine($"Відкладені тестові опити для Predictor: {ProjectPaths.HoldoutFile} ({holdout.Count} опитів)");

// Модель зберігається зі схемою лише з Features: мітка для використання не потрібна.
var inputSchema = ml.Data.LoadFromEnumerable(new List<ModelInput>()).Schema;
Directory.CreateDirectory(Path.GetDirectoryName(ProjectPaths.ModelFile)!);
ml.Model.Save(final.Full, inputSchema, ProjectPaths.ModelFile);
Console.WriteLine($"Модель: {ProjectPaths.ModelFile} ({new FileInfo(ProjectPaths.ModelFile).Length / 1024.0:F1} КБ)");
return 0;

// =====================================================================
// Допоміжні функції
// =====================================================================
IEstimator<ITransformer> Prep(string kind) => kind switch
{
    "none" => ml.Transforms.CopyColumns("NormFeatures", "Features"),
    "log" => ml.Transforms.Expression("NormFeatures", "x => log(x + 1)", "Features"),
    "meanvar" => ml.Transforms.NormalizeMeanVariance("NormFeatures", "Features"),
    "log+meanvar" => ml.Transforms.Expression("LogFeatures", "x => log(x + 1)", "Features")
                       .Append(ml.Transforms.NormalizeMeanVariance("NormFeatures", "LogFeatures")),
    _ => throw new ArgumentException(kind),
};

// Конвеєр: передобробка -> RandomizedPca (-> поріг) -> заміна NaN у Score на 0.
FittedModel Fit(string prep, int rank, IEnumerable<DeltaRow> rows, float? thr = null)
{
    var data = ml.Data.LoadFromEnumerable(rows.Select(LabeledInput.From));
    var prepT = Prep(prep).Fit(data);
    var pca = ml.AnomalyDetection.Trainers.RandomizedPca(new RandomizedPcaTrainer.Options
    {
        FeatureColumnName = "NormFeatures",
        Rank = rank,
        Oversampling = 20,
        EnsureZeroMean = true,
        Seed = Seed,
    }).Fit(prepT.Transform(data));
    if (thr is float t) pca = ml.AnomalyDetection.ChangeModelThreshold(pca, t);
    var noFix = new TransformerChain<ITransformer>(prepT, pca);
    var fixNaN = ml.Transforms.ReplaceMissingValues("Score",
        replacementMode: MissingValueReplacingEstimator.ReplacementMode.DefaultValue).Fit(noFix.Transform(data));
    return new FittedModel(new TransformerChain<ITransformer>(prepT, pca, fixNaN), noFix);
}

float[] Score(ITransformer m, IEnumerable<DeltaRow> rows) => ScoreWithNaNCount(m, rows).Scores;

(float[] Scores, int NaN) ScoreWithNaNCount(ITransformer m, IEnumerable<DeltaRow> rows)
{
    var raw = ml.Data.CreateEnumerable<AnomalyPrediction>(
        m.Transform(ml.Data.LoadFromEnumerable(rows.Select(LabeledInput.From))), false).Select(p => p.Score).ToArray();
    return (raw.Select(s => float.IsNaN(s) ? 0f : s).ToArray(), raw.Count(float.IsNaN));
}

List<(float Score, bool Attack)> Pairs(float[] scores, IReadOnlyList<DeltaRow> rows) =>
    scores.Zip(rows, (s, r) => (s, r.IsAttack)).ToList();

List<(float Score, bool Attack)> Run(string protocol, string feat, string prep, int rank,
    List<DeltaRow> fitRows, List<DeltaRow> thresholdRows, List<DeltaRow> normalTest, List<DeltaRow> attackTest)
{
    var m = Fit(prep, rank, fitRows);
    float thr = Stats.Percentile(Score(m.Full, thresholdRows), ThresholdPercentile);
    var rows = normalTest.Concat(attackTest).ToList();
    var pairs = Pairs(Score(m.Full, rows), rows);
    int fpc = pairs.Count(p => !p.Attack && p.Score > thr), nn = normalTest.Count;
    double tpr = pairs.Where(p => p.Attack).Average(p => p.Score > thr ? 1.0 : 0.0);
    var ci = Stats.Wilson(fpc, nn);
    Console.WriteLine($"  {protocol,-38} {feat,-24} {Stats.Auc(pairs),7:F4} {$"{fpc}/{nn} = {fpc / (double)nn:P1} [{ci.Lo:P0};{ci.Hi:P0}]",22} {tpr,8:P1}");
    return pairs;
}

static int Count(IEnumerable<DeltaRow> rows, Func<DeltaRow, bool> f) => rows.Count(f);

static void PrintRates(string name, int tp, int fn, int fp, int tn)
{
    var tprCi = Stats.Wilson(tp, tp + fn);
    var fprCi = Stats.Wilson(fp, fp + tn);
    double prec = tp / (double)Math.Max(1, tp + fp), rec = tp / (double)Math.Max(1, tp + fn);
    double f1 = prec + rec == 0 ? 0 : 2 * prec * rec / (prec + rec);
    double bal = (rec + tn / (double)Math.Max(1, fp + tn)) / 2;
    double acc = (tp + tn) / (double)(tp + fn + fp + tn);
    Console.WriteLine($"{name}:");
    Console.WriteLine($"  TPR (виявлені атаки)  = {tp}/{tp + fn} = {rec:P2}  95% ДІ [{tprCi.Lo:P2}; {tprCi.Hi:P2}]");
    Console.WriteLine($"  FPR (хибні тривоги)   = {fp}/{fp + tn} = {fp / (double)Math.Max(1, fp + tn):P2}  95% ДІ [{fprCi.Lo:P2}; {fprCi.Hi:P2}]");
    Console.WriteLine($"  Precision = {prec:F4}  F1 = {f1:F4}  Balanced accuracy = {bal:F4}  Accuracy = {acc:F4}");
}

static void PrintSplit(string name, List<DeltaRow> rows) =>
    Console.WriteLine($"  {name,-11} normal: {rows.Count(r => !r.IsAttack),4}   атаки: {rows.Count(r => r.IsAttack),5}");

static void Header(string title) => Console.WriteLine($"\n===== {title} =====");

record Session(string Id, string Class, List<RawPoll> Polls, List<DeltaRow> Deltas)
{
    public bool IsNormal => Class == Schema.NormalClass;
}

record FittedModel(ITransformer Full, ITransformer WithoutFix);

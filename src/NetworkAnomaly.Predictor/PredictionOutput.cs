using Microsoft.ML;
using NetworkAnomaly.Common;

namespace NetworkAnomaly.Predictor;

public static class PredictionOutput
{
    public static void Sample(PredictionEngine<ModelInput, AnomalyPrediction> engine, List<DeltaRow> rows, bool labeled)
    {
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
    }

    public static void BySession(List<(DeltaRow First, AnomalyPrediction Second)> results)
    {
        Console.WriteLine("\nПакетний прогноз за сесіями:");
        Console.WriteLine($"  {"Сесія",-14} {"Векторів",9} {"Аномалій",9} {"Частка",8}  Перша аномалія (рядок CSV)");
        foreach (var g in results.GroupBy(x => x.First.SessionId))
        {
            int k = g.Count(x => x.Second.IsAnomaly);
            var first = g.FirstOrDefault(x => x.Second.IsAnomaly).First;
            Console.WriteLine($"  {g.Key,-14} {g.Count(),9} {k,9} {k / (double)g.Count(),8:P1}  {(first is null ? "—" : first.Line.ToString())}");
        }
        Console.WriteLine($"Усього аномалій: {results.Count(x => x.Second.IsAnomaly)} з {results.Count}");
    }

    public static void LabelCheck(List<(DeltaRow First, AnomalyPrediction Second)> results)
    {
        int tp = results.Count(x => x.First.IsAttack && x.Second.IsAnomaly), pos = results.Count(x => x.First.IsAttack);
        int fp = results.Count(x => !x.First.IsAttack && x.Second.IsAnomaly), neg = results.Count - pos;
        Console.WriteLine("\nПеревірка за мітками (лише для демонстрації; повне оцінювання — у Trainer):");
        if (pos > 0) Console.WriteLine($"  Виявлено атак:  {tp}/{pos} = {tp / (double)pos:P1}");
        if (neg > 0) Console.WriteLine($"  Хибні тривоги:  {fp}/{neg} = {fp / (double)neg:P1}");
    }
}

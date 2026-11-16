using NetworkAnomaly.Common;

namespace NetworkAnomaly.Trainer;

public static class Generalization
{
    public static List<(double Fpr, double Tpr)> Run(AnomalyModelFactory factory, List<Session> sessions,
        List<DeltaRow> test, IReadOnlyDictionary<int, RawPoll> pollByLine, (string Prep, int Rank) best)
    {
        ConsoleReport.Header("7. Узагальнення на нові дані: абсолютні значення (перша версія) vs прирости");
        var normalSessions = sessions.Where(s => s.IsNormal).ToList();
        var testAttacks = test.Where(r => r.IsAttack).ToList();
        var testAttacksAbs = testAttacks.Select(r => Absolute([pollByLine[r.Line]], r.SessionId)[0]).ToList();

        Console.WriteLine("Атаки в усіх протоколах — test-половини атакувальних сесій. Поріг — 99-й перцентиль на вказаних normal.");
        Console.WriteLine($"  {"Протокол",-38} {"Ознаки",-24} {"AUC",7} {"FPR (normal)",22} {"TPR",8}");

        var rocAbsolute = new List<(double, double)>();
        foreach (var useDeltas in new[] { false, true })
        {
            string prep = useDeltas ? best.Prep : "log";
            int rank = useDeltas ? best.Rank : 8;
            string feat = useDeltas ? $"прирости ({prep}, r={rank})" : "абсолютні (log, r=8)";
            List<DeltaRow> Rows(Session s) => useDeltas ? s.Deltas : Absolute(s.Polls, s.Id);
            var attacks = useDeltas ? testAttacks : testAttacksAbs;
            List<(float Score, bool Attack)> Run(string protocol, List<DeltaRow> fit, List<DeltaRow> thr, List<DeltaRow> normalTest) =>
                Protocol(factory, protocol, feat, prep, rank, fit, thr, normalTest, attacks);

            var rnd = new Random(Settings.Seed);
            var shuffled = normalSessions.SelectMany(Rows).OrderBy(_ => rnd.Next()).ToList();
            int cut = (int)(shuffled.Count * 0.8);
            var fitRandom = shuffled.Take(cut).ToList();
            Run("P1 випадкове 80/20, поріг на train", fitRandom, fitRandom, shuffled.Skip(cut).ToList());

            var (tr, th, te) = (new List<DeltaRow>(), new List<DeltaRow>(), new List<DeltaRow>());
            foreach (var s in normalSessions)
            {
                var rows = Rows(s);
                int n = rows.Count, a = (int)(n * 0.6), b = (int)(n * 0.8);
                tr.AddRange(rows.GetRange(0, a));
                th.AddRange(rows.GetRange(a, b - a));
                te.AddRange(rows.GetRange(b, n - b));
            }
            var p2 = Run("P2 часове 60/20/20", tr, th, te);
            if (!useDeltas) rocAbsolute = Stats.Roc(p2);

            for (int k = 0; k < 2; k++)
            {
                var src = Rows(normalSessions[k]);
                int a = (int)(src.Count * 0.8);
                Run($"P{3 + k} {normalSessions[k].Id} -> {normalSessions[1 - k].Id}",
                    src.GetRange(0, a), src.GetRange(a, src.Count - a), Rows(normalSessions[1 - k]));
            }
        }
        return rocAbsolute;
    }

    private static List<DeltaRow> Absolute(IEnumerable<RawPoll> polls, string sessionId) =>
        polls.Select(p => new DeltaRow(p.Line, sessionId, p.Values.Select(v => (float)v).ToArray(), p.Class)).ToList();

    private static List<(float Score, bool Attack)> Protocol(AnomalyModelFactory factory, string protocol, string feat,
        string prep, int rank, List<DeltaRow> fitRows, List<DeltaRow> thresholdRows, List<DeltaRow> normalTest, List<DeltaRow> attackTest)
    {
        var model = factory.Fit(prep, rank, fitRows);
        float thr = Stats.Percentile(factory.Score(model.Full, thresholdRows), Settings.ThresholdPercentile);
        var rows = normalTest.Concat(attackTest).ToList();
        var pairs = AnomalyModelFactory.Pairs(factory.Score(model.Full, rows), rows);
        int fp = pairs.Count(p => !p.Attack && p.Score > thr), nn = normalTest.Count;
        double tpr = pairs.Where(p => p.Attack).Average(p => p.Score > thr ? 1.0 : 0.0);
        var ci = Stats.Wilson(fp, nn);
        Console.WriteLine($"  {protocol,-38} {feat,-24} {Stats.Auc(pairs),7:F4} {$"{fp}/{nn} = {fp / (double)nn:P1} [{ci.Lo:P0};{ci.Hi:P0}]",22} {tpr,8:P1}");
        return pairs;
    }
}

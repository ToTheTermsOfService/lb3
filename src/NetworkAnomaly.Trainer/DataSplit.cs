using NetworkAnomaly.Common;

namespace NetworkAnomaly.Trainer;

public sealed class DataSplit
{
    public List<DeltaRow> Train { get; } = [];
    public List<DeltaRow> Validation { get; } = [];
    public List<DeltaRow> Test { get; } = [];
    public List<(RawPoll Poll, string Session)> Holdout { get; } = [];

    public static DataSplit ByTime(IEnumerable<Session> sessions)
    {
        var split = new DataSplit();
        foreach (var s in sessions)
        {
            int n = s.Deltas.Count, testStart;
            if (s.IsNormal)
            {
                int a = (int)(n * 0.6), b = (int)(n * 0.8);
                split.Train.AddRange(s.Deltas.GetRange(0, a));
                split.Validation.AddRange(s.Deltas.GetRange(a, b - a));
                split.Test.AddRange(s.Deltas.GetRange(b, n - b));
                testStart = b;
            }
            else
            {
                int a = n / 2;
                split.Validation.AddRange(s.Deltas.GetRange(0, a));
                split.Test.AddRange(s.Deltas.GetRange(a, n - a));
                testStart = a;
            }
            split.Holdout.AddRange(s.Polls.Skip(testStart).Select(p => (p, s.Id)));
        }
        return split;
    }
}

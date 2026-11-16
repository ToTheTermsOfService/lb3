namespace NetworkAnomaly.Common;

public static class SnmpSessions
{
    private static readonly int[] ResetMarkers = [9, 16, 20];

    public static List<List<RawPoll>> Split(IReadOnlyList<RawPoll> polls)
    {
        var sessions = new List<List<RawPoll>>();
        List<RawPoll>? current = null;
        for (int i = 0; i < polls.Count; i++)
        {
            if (current is null || StartsNewSession(polls[i - 1], polls[i]))
            {
                current = [];
                sessions.Add(current);
            }
            current.Add(polls[i]);
        }
        return sessions;
    }

    private static bool StartsNewSession(RawPoll prev, RawPoll p) =>
        p.Session is not null
            ? p.Session != prev.Session
            : p.Class != prev.Class || ResetMarkers.Any(k => p.Values[k] < prev.Values[k]);
}

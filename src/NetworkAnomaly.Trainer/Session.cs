using NetworkAnomaly.Common;

namespace NetworkAnomaly.Trainer;

public sealed record Session(string Id, string Class, List<RawPoll> Polls, List<DeltaRow> Deltas)
{
    public bool IsNormal => Class == Schema.NormalClass;

    public static List<Session> FromPolls(IReadOnlyList<RawPoll> polls)
    {
        var perClass = new Dictionary<string, int>();
        return SnmpSessions.Split(polls).Select(s =>
        {
            string cls = s[0].Class!;
            perClass[cls] = perClass.GetValueOrDefault(cls) + 1;
            string id = $"{cls}#{perClass[cls]}";
            return new Session(id, cls, s, DeltaFeatures.Build(s, id));
        }).ToList();
    }
}

namespace NetworkAnomaly.Common;

public sealed record RawPoll(int Line, double[] Values, string? Class, string? Session);

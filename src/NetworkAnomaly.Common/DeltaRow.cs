namespace NetworkAnomaly.Common;

public sealed record DeltaRow(int Line, string SessionId, float[] Features, string? Class)
{
    public bool IsAttack => Class is not null && Class != Schema.NormalClass;
}

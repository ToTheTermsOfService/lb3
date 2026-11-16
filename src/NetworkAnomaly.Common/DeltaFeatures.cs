namespace NetworkAnomaly.Common;

public static class DeltaFeatures
{
    private const double Counter32 = 4294967296.0;

    public static List<DeltaRow> Build(IReadOnlyList<RawPoll> session, string sessionId)
    {
        var rows = new List<DeltaRow>(Math.Max(0, session.Count - 1));
        for (int t = 1; t < session.Count; t++)
            rows.Add(new DeltaRow(session[t].Line, sessionId, Delta(session[t - 1].Values, session[t].Values), session[t].Class));
        return rows;
    }

    private static float[] Delta(double[] prev, double[] cur)
    {
        var f = new float[Schema.FeatureCount];
        for (int i = 0; i < Schema.FeatureCount; i++)
        {
            if (i == Schema.TcpCurrEstabIndex)
            {
                f[i] = (float)cur[i];
                continue;
            }
            double d = cur[i] - prev[i];
            if (d < 0) d = prev[i] >= Counter32 / 2 ? d + Counter32 : cur[i];
            f[i] = (float)d;
        }
        return f;
    }
}

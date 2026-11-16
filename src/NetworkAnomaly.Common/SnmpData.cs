using System.Globalization;
using System.Text;

namespace NetworkAnomaly.Common;

/// <summary>Читання/запис CSV з SNMP-опитами та перетворення кумулятивних лічильників у прирости.</summary>
public static class SnmpData
{
    private const double Counter32 = 4294967296.0; // 2^32

    /// <summary>
    /// Читає CSV: перші 34 колонки — лічильники; необов'язкові колонки "class" (мітка) і "session" (ідентифікатор сесії).
    /// </summary>
    public static (string[] FeatureNames, List<RawPoll> Polls) ReadCsv(string path)
    {
        using var reader = new StreamReader(path);
        string header = reader.ReadLine() ?? throw new InvalidDataException("Порожній файл.");
        var cols = header.Split(',').Select(c => c.Trim()).ToArray();
        if (cols.Length < Schema.FeatureCount)
            throw new InvalidDataException($"Очікується щонайменше {Schema.FeatureCount} колонок, знайдено {cols.Length}.");

        int classIdx = Array.FindIndex(cols, c => c.Equals("class", StringComparison.OrdinalIgnoreCase));
        int sessionIdx = Array.FindIndex(cols, c => c.Equals("session", StringComparison.OrdinalIgnoreCase));
        var names = cols.Take(Schema.FeatureCount).ToArray();

        var polls = new List<RawPoll>();
        int line = 1;
        while (reader.ReadLine() is { } text)
        {
            line++;
            if (string.IsNullOrWhiteSpace(text)) continue;
            var parts = text.Split(',');
            var values = new double[Schema.FeatureCount];
            for (int i = 0; i < Schema.FeatureCount; i++)
                if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]))
                    throw new InvalidDataException($"Рядок {line}, колонка {names[i]}: '{parts[i]}' не є числом.");
            polls.Add(new RawPoll(line, values,
                classIdx >= 0 ? parts[classIdx].Trim() : null,
                sessionIdx >= 0 ? parts[sessionIdx].Trim() : null));
        }
        return (names, polls);
    }

    /// <summary>
    /// Ділить послідовність опитів на сесії. Якщо є колонка session — за нею.
    /// Інакше нова сесія починається при зміні класу або скиданні лічильників, що не переповнюються
    /// (tcpInSegs, udpInDatagrams, ipInReceives зменшилися).
    /// </summary>
    public static List<List<RawPoll>> SplitSessions(IReadOnlyList<RawPoll> polls)
    {
        int[] resetMarkers = [9, 16, 20];
        var sessions = new List<List<RawPoll>>();
        List<RawPoll>? current = null;
        for (int i = 0; i < polls.Count; i++)
        {
            var p = polls[i];
            bool newSession = current is null;
            if (!newSession)
            {
                var prev = polls[i - 1];
                newSession = p.Session is not null
                    ? p.Session != prev.Session
                    : p.Class != prev.Class || resetMarkers.Any(k => p.Values[k] < prev.Values[k]);
            }
            if (newSession) { current = []; sessions.Add(current); }
            current!.Add(p);
        }
        return sessions;
    }

    /// <summary>
    /// Прирости між сусідніми опитами сесії. Перший опит сесії приросту не має і відкидається.
    /// Від'ємний приріст: якщо попереднє значення було у верхній половині діапазону — переповнення 2^32,
    /// інакше — скидання лічильника (приріст = поточне значення).
    /// </summary>
    public static List<DeltaRow> ToDeltas(IReadOnlyList<RawPoll> session, string sessionId)
    {
        var rows = new List<DeltaRow>(Math.Max(0, session.Count - 1));
        for (int t = 1; t < session.Count; t++)
        {
            var prev = session[t - 1].Values;
            var cur = session[t].Values;
            var f = new float[Schema.FeatureCount];
            for (int i = 0; i < Schema.FeatureCount; i++)
            {
                if (Schema.GaugeIndices.Contains(i)) { f[i] = (float)cur[i]; continue; }
                double d = cur[i] - prev[i];
                if (d < 0) d = prev[i] >= Counter32 / 2 ? d + Counter32 : cur[i];
                f[i] = (float)d;
            }
            rows.Add(new DeltaRow(session[t].Line, sessionId, f, session[t].Class));
        }
        return rows;
    }

    /// <summary>Записує опити у CSV того ж формату + колонка session.</summary>
    public static void WriteCsv(string path, string[] featureNames, IEnumerable<(RawPoll Poll, string Session)> rows)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', featureNames) + ",class,session");
        foreach (var (p, s) in rows)
            sb.AppendLine(string.Join(',', p.Values.Select(v => v.ToString("R", CultureInfo.InvariantCulture))) + $",{p.Class},{s}");
        File.WriteAllText(path, sb.ToString());
    }
}

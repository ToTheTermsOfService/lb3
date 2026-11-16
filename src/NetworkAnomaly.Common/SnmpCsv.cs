using System.Globalization;
using System.Text;

namespace NetworkAnomaly.Common;

public static class SnmpCsv
{
    public static (string[] FeatureNames, List<RawPoll> Polls) Read(string path)
    {
        using var reader = new StreamReader(path);
        string header = reader.ReadLine() ?? throw new InvalidDataException("Порожній файл.");
        var cols = header.Split(',').Select(c => c.Trim()).ToArray();
        if (cols.Length < Schema.FeatureCount)
            throw new InvalidDataException($"Очікується щонайменше {Schema.FeatureCount} колонок, знайдено {cols.Length}.");

        int classIdx = IndexOf(cols, "class");
        int sessionIdx = IndexOf(cols, "session");
        var names = cols.Take(Schema.FeatureCount).ToArray();

        var polls = new List<RawPoll>();
        int line = 1;
        while (reader.ReadLine() is { } text)
        {
            line++;
            if (string.IsNullOrWhiteSpace(text)) continue;
            var parts = text.Split(',');
            polls.Add(new RawPoll(line, ParseValues(parts, names, line),
                classIdx >= 0 ? parts[classIdx].Trim() : null,
                sessionIdx >= 0 ? parts[sessionIdx].Trim() : null));
        }
        return (names, polls);
    }

    public static void Write(string path, string[] featureNames, IEnumerable<(RawPoll Poll, string Session)> rows)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', featureNames) + ",class,session");
        foreach (var (p, s) in rows)
            sb.AppendLine(string.Join(',', p.Values.Select(v => v.ToString("R", CultureInfo.InvariantCulture))) + $",{p.Class},{s}");
        File.WriteAllText(path, sb.ToString());
    }

    private static int IndexOf(string[] cols, string name) =>
        Array.FindIndex(cols, c => c.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static double[] ParseValues(string[] parts, string[] names, int line)
    {
        var values = new double[Schema.FeatureCount];
        for (int i = 0; i < Schema.FeatureCount; i++)
            if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]))
                throw new InvalidDataException($"Рядок {line}, колонка {names[i]}: '{parts[i]}' не є числом.");
        return values;
    }
}

namespace NetworkAnomaly.Common;

public static class ProjectPaths
{
    public static string SolutionRoot { get; } = FindRoot();

    public static string DataFile => Path.Combine(SolutionRoot, "Data", "network_anomaly.csv");
    public static string HoldoutFile => Path.Combine(SolutionRoot, "Data", "holdout_test.csv");
    public static string ModelFile => Environment.GetEnvironmentVariable("NAD_MODEL")
                                      ?? Path.Combine(SolutionRoot, "Models", "network_anomaly_model.zip");
    public static string ReportsDir => Path.Combine(SolutionRoot, "Reports");

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "NetworkAnomalyDetection.sln")))
                return dir.FullName;
        throw new DirectoryNotFoundException(
            "Не знайдено NetworkAnomalyDetection.sln вище за каталог програми. Запускайте з каталогу рішення.");
    }
}

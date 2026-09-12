namespace SageToolbox.Models;

public sealed class SqlInstanceInfo
{
    public string ProductVersion { get; init; } = string.Empty;
    public string ProductLevel { get; init; } = string.Empty;
    public string Edition { get; init; } = string.Empty;
    public bool WindowsAuthOnly { get; init; }
    public string DataPath { get; init; } = string.Empty;
    public string LogPath { get; init; } = string.Empty;
    public string BackupPath { get; init; } = string.Empty;
    public int FilestreamLevel { get; init; }
    public string? FilestreamShareName { get; init; }

    private static readonly Dictionary<int, string> YearByMajorVersion = new()
    {
        [17] = "2025", [16] = "2022", [15] = "2019", [14] = "2017",
        [13] = "2016", [12] = "2014", [11] = "2012", [10] = "2008 R2/2008"
    };

    public string VersionLabel
    {
        get
        {
            var majorText = ProductVersion.Split('.').FirstOrDefault();
            var year = int.TryParse(majorText, out var major) && YearByMajorVersion.TryGetValue(major, out var y)
                ? $"SQL Server {y}"
                : "SQL Server";
            return string.IsNullOrEmpty(ProductLevel) ? $"{year} ({ProductVersion})" : $"{year} {ProductLevel} ({ProductVersion})";
        }
    }

    public string AuthModeLabel => WindowsAuthOnly ? "Windows uniquement" : "Mixte (SQL Server + Windows)";

    // Le nom de partage est le signal le plus fiable (présent seulement si FILESTREAM est
    // réellement actif) : le niveau numérique renvoyé par FilestreamConfiguredLevel ne
    // suit pas exactement l'échelle documentée 0/1/2 sur toutes les versions (une valeur
    // 3 a été observée sur une instance SQL Server 2025 avec l'accès complet actif).
    public string FilestreamLabel => !string.IsNullOrEmpty(FilestreamShareName)
        ? $"Activé — partage \\\\{FilestreamShareName}"
        : FilestreamLevel > 0 ? "Activé (T-SQL uniquement)" : "Désactivé";
}

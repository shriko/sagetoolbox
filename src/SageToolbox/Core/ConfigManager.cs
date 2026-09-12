using System.IO;
using System.Text.Json;

namespace SageToolbox.Core;

public sealed class AppConfig
{
    public string? LastInstance { get; set; }
    public string? LastDatabase { get; set; }
    public string Theme { get; set; } = "System";
    public string? LastRestoreBakFolder { get; set; }
    public string? LastRestoreConnFilesFolder { get; set; }
    public bool LastRestoreReplaceExisting { get; set; }
    public string? LastBackupFolder { get; set; }
    public bool LastBackupUseCompression { get; set; } = true;
}

/// <summary>
/// Persistance de la config utilisateur hors du répertoire d'installation
/// (%APPDATA%\SageToolbox\config.json), avec migration depuis l'ancien
/// emplacement %APPDATA%\AIG_Toolbox si l'utilisateur vient de la V2.
/// </summary>
public sealed class ConfigManager
{
    private static readonly Lazy<ConfigManager> LazyInstance = new(() => new ConfigManager());
    public static ConfigManager Instance => LazyInstance.Value;

    private readonly string _configPath;
    public AppConfig Config { get; private set; }

    private ConfigManager()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "SageToolbox");
        Directory.CreateDirectory(dir);
        _configPath = Path.Combine(dir, "config.json");

        MigrateFromLegacyLocation(appData);
        Config = Load();
    }

    private void MigrateFromLegacyLocation(string appData)
    {
        if (File.Exists(_configPath)) return;

        var legacyPath = Path.Combine(appData, "AIG_Toolbox", "config.json");
        if (!File.Exists(legacyPath)) return;

        try
        {
            File.Copy(legacyPath, _configPath);
        }
        catch
        {
            // Migration best-effort : si elle échoue, on repart d'une config par défaut.
        }
    }

    private AppConfig Load()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json);
                if (cfg != null) return cfg;
            }
        }
        catch
        {
            // Config corrompue ou illisible -> on repart d'une config par défaut.
        }
        return new AppConfig();
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(Config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, json);
        }
        catch (Exception ex)
        {
            Logger.Instance.Warning($"Impossible d'enregistrer la configuration : {ex.Message}");
        }
    }

    public void SaveLastConnection(string instance, string database)
    {
        Config.LastInstance = instance;
        Config.LastDatabase = database;
        Save();
    }
}

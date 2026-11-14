using System.IO;
using System.Text;

namespace SageToolbox.Enrichissement;

/// <summary>
/// Journal d'audit texte (%APPDATA%\SageToolbox\mises_a_jour_tiers.txt) : horodatage, base et valeurs avant/après par
/// tiers mis à jour, distinct du journal applicatif, pour permettre un retour arrière manuel.
/// </summary>
public sealed class AuditLogger
{
    private readonly string _logPath;

    public AuditLogger(string? directory = null)
    {
        var dir = directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SageToolbox");
        Directory.CreateDirectory(dir);
        _logPath = Path.Combine(dir, "mises_a_jour_tiers.txt");
    }

    public string FilePath => _logPath;

    public void LogUpdates(string databaseLabel, IEnumerable<UpdateResult> results)
    {
        var sb = new StringBuilder();
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        foreach (var result in results)
        {
            sb.AppendLine($"[{timestamp}] {databaseLabel} — CT_Num={result.CtNum} — {(result.Success ? "OK" : $"ÉCHEC ({result.ErrorMessage})")}");
            foreach (var (column, (before, after)) in result.Changes)
                sb.AppendLine($"    {column}: '{before}' -> '{after}'");
        }

        File.AppendAllText(_logPath, sb.ToString(), Encoding.UTF8);
    }
}

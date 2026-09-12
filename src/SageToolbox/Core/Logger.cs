using System.IO;

namespace SageToolbox.Core;

/// <summary>
/// Journalisation double : événement pour la console de log UI (thread-safe, l'abonné
/// est responsable de re-poster sur le thread UI via son Dispatcher) + fichier persistant
/// sous %APPDATA%\SageToolbox\sage_toolbox.log.
/// </summary>
public sealed class Logger
{
    private static readonly Lazy<Logger> LazyInstance = new(() => new Logger());
    public static Logger Instance => LazyInstance.Value;

    private readonly string _logFilePath;
    private readonly object _fileLock = new();

    public event Action<LogEntry>? EntryLogged;

    private Logger()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SageToolbox");
        Directory.CreateDirectory(dir);
        _logFilePath = Path.Combine(dir, "sage_toolbox.log");
    }

    public void Info(string message) => Log(LogLevel.Info, message);
    public void Success(string message) => Log(LogLevel.Success, message);
    public void Warning(string message) => Log(LogLevel.Warning, message);
    public void Error(string message) => Log(LogLevel.Error, message);

    public void Log(LogLevel level, string message)
    {
        var entry = new LogEntry(DateTime.Now, level, message);
        WriteToFile(entry);
        EntryLogged?.Invoke(entry);
    }

    private void WriteToFile(LogEntry entry)
    {
        lock (_fileLock)
        {
            try
            {
                File.AppendAllText(_logFilePath, entry.Formatted + Environment.NewLine);
            }
            catch
            {
                // La persistance du log ne doit jamais faire échouer l'opération en cours.
            }
        }
    }
}

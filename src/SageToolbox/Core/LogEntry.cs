namespace SageToolbox.Core;

public enum LogLevel
{
    Info,
    Success,
    Warning,
    Error
}

public sealed record LogEntry(DateTime Timestamp, LogLevel Level, string Message)
{
    public string Icon => Level switch
    {
        LogLevel.Info => "ℹ️",
        LogLevel.Success => "✅",
        LogLevel.Warning => "⚠️",
        LogLevel.Error => "❌",
        _ => string.Empty
    };

    public string Formatted => $"[{Timestamp:HH:mm:ss}] {Icon} {Message}";
}

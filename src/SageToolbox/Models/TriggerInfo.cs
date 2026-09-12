namespace SageToolbox.Models;

public sealed class TriggerInfo
{
    public string TableName { get; init; } = string.Empty;
    public string TriggerName { get; init; } = string.Empty;
}

public sealed class IntegrityIssue
{
    public string Controle { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
}

namespace ProjectCallisto.Application.Reports.Models;

public record PresenceInsight
{
    public InsightType Type { get; init; }
    public string Message { get; init; } = string.Empty;
    public InsightSeverity Severity { get; init; }
    public double Value { get; init; }
}

public enum InsightType
{
    HighOvertime,
    HighAwayTime,
    HighOfflineDuringWorkingHours
}

public enum InsightSeverity
{
    Warning,
    Alert
}

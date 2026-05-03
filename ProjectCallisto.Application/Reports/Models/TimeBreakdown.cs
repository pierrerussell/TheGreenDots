namespace ProjectCallisto.Application.Reports.Models;

public record TimeBreakdown
{
    public double TotalHours { get; init; }
    public double AvailableHours { get; init; }
    public double BusyHours { get; init; }
    public double AwayHours { get; init; }
    public double DoNotDisturbHours { get; init; }
    public double OfflineHours { get; init; }

    // Percentages (calculated from hours)
    public int AvailablePercent { get; init; }
    public int BusyPercent { get; init; }
    public int AwayPercent { get; init; }
    public int DoNotDisturbPercent { get; init; }
    public int OfflinePercent { get; init; }
}

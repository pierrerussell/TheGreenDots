using ProjectCallisto.Application.Reports.Models;

namespace ProjectCallisto.Application.Reports;

public interface IReportCalculationService
{
    Task<DailyReportData> CalculateDailyReportAsync(
        Guid organisationId,
        CancellationToken ct);

    Task<WeeklyReportData> CalculateWeeklyReportAsync(
        Guid organisationId,
        CancellationToken ct);

    Task<MonthlyReportData> CalculateMonthlyReportAsync(
        Guid organisationId,
        CancellationToken ct);
}

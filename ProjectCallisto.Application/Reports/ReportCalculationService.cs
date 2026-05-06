using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectCallisto.Application.Reports.Models;
using ProjectCallisto.Domain.Organisations;
using ProjectCallisto.EfCore;

namespace ProjectCallisto.Application.Reports;

public class ReportCalculationService : IReportCalculationService
{
    private readonly AppDbContext _dbContext;
    private readonly IPresenceBreakdownCalculator _calculator;
    private readonly IInsightDetectionService _insightDetector;
    private readonly ILogger<ReportCalculationService> _logger;

    public ReportCalculationService(
        AppDbContext dbContext,
        IPresenceBreakdownCalculator calculator,
        IInsightDetectionService insightDetector,
        ILogger<ReportCalculationService> logger)
    {
        _dbContext = dbContext;
        _calculator = calculator;
        _insightDetector = insightDetector;
        _logger = logger;
    }

    public async Task<DailyReportData> CalculateDailyReportAsync(
        Guid organisationId,
        CancellationToken ct)
    {
        // Fetch organisation with related data
        var organisation = await FetchOrganisationWithDependencies(organisationId, ct);

        // Calculate date range: Yesterday 00:00:00 to 23:59:59 in org's timezone
        var (periodStart, periodEnd) = CalculateDailyDateRange(organisation.Timezone!);

        // Calculate report
        var employees = await CalculateEmployeeBreakdowns(
            organisation,
            periodStart,
            periodEnd,
            ct);

        return new DailyReportData
        {
            OrganisationId = organisation.Id,
            OrganisationName = organisation.Name,
            StartDate = periodStart,
            EndDate = periodEnd,
            Timezone = organisation.Timezone!,
            Employees = employees,
            TotalMembers = employees.Count
        };
    }

    public async Task<WeeklyReportData> CalculateWeeklyReportAsync(
        Guid organisationId,
        CancellationToken ct)
    {
        // Fetch organisation with related data
        var organisation = await FetchOrganisationWithDependencies(organisationId, ct);

        // Calculate date range: Last Monday 00:00:00 to Sunday 23:59:59 in org's timezone
        var (periodStart, periodEnd) = CalculateWeeklyDateRange(organisation.Timezone!);

        // Calculate report
        var employees = await CalculateEmployeeBreakdowns(
            organisation,
            periodStart,
            periodEnd,
            ct);

        return new WeeklyReportData
        {
            OrganisationId = organisation.Id,
            OrganisationName = organisation.Name,
            StartDate = periodStart,
            EndDate = periodEnd,
            Timezone = organisation.Timezone!,
            WorkingHours = organisation.WorkingHours,
            Employees = employees,
            TotalMembers = employees.Count
        };
    }

    public async Task<MonthlyReportData> CalculateMonthlyReportAsync(
        Guid organisationId,
        CancellationToken ct)
    {
        // Fetch organisation with related data
        var organisation = await FetchOrganisationWithDependencies(organisationId, ct);

        // Calculate date range: First day of last month to last day of last month
        var (periodStart, periodEnd) = CalculateMonthlyDateRange(organisation.Timezone!);

        // Calculate report
        var employees = await CalculateEmployeeBreakdowns(
            organisation,
            periodStart,
            periodEnd,
            ct);

        return new MonthlyReportData
        {
            OrganisationId = organisation.Id,
            OrganisationName = organisation.Name,
            StartDate = periodStart,
            EndDate = periodEnd,
            Timezone = organisation.Timezone!,
            WorkingHours = organisation.WorkingHours,
            Employees = employees,
            TotalMembers = employees.Count
        };
    }

    private async Task<Organisation> FetchOrganisationWithDependencies(
        Guid organisationId,
        CancellationToken ct)
    {
        var organisation = await _dbContext.Organisations
            .Include(o => o.WorkingHours)
            .Include(o => o.Subscription)
            .FirstOrDefaultAsync(o => o.Id == organisationId, ct);

        if (organisation == null)
        {
            throw new InvalidOperationException($"Organisation with ID {organisationId} not found");
        }
        
        // Default to UTC if no timezone is configured
        if (string.IsNullOrEmpty(organisation.Timezone))
        {
            _logger.LogWarning(
                "Organisation {OrgId} ({OrgName}) has no timezone configured. Defaulting to UTC. " +
                "Please configure timezone in organisation settings.",
                organisationId, organisation.Name);

            organisation.Timezone = "UTC";
        }

        return organisation;
    }

    private async Task<List<EmployeePresenceBreakdown>> CalculateEmployeeBreakdowns(
        Organisation organisation,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken ct)
    {
        // Fetch all assigned tenant members
        var members = await _dbContext.TenantMembers
            .Where(m => m.OrganisationId == organisation.Id && m.IsAssignedSeat)
            .ToListAsync(ct);

        if (members.Count == 0)
        {
            return new List<EmployeePresenceBreakdown>();
        }

        var memberIds = members.Select(m => m.Id).ToList();

        // Fetch ALL PresenceHistory records for all members in the period (single bulk query)
        var allPresenceRecords = await _dbContext.Set<PresenceHistory>()
            .Where(p => memberIds.Contains(p.TenantMemberId) &&
                        p.RecordedAt >= periodStart &&
                        p.RecordedAt <= periodEnd)
            .OrderBy(p => p.TenantMemberId)
            .ThenBy(p => p.RecordedAt)
            .ToListAsync(ct);

        _logger.LogInformation(
            "Fetched {RecordCount} presence records for {MemberCount} members between {Start} and {End}",
            allPresenceRecords.Count, members.Count, periodStart, periodEnd);

        // Group records by TenantMemberId
        var recordsByMember = allPresenceRecords
            .GroupBy(p => p.TenantMemberId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Process each employee
        var employeeBreakdowns = new List<EmployeePresenceBreakdown>();

        foreach (var member in members)
        {
            // Get all records for this member
            var memberRecords = recordsByMember.ContainsKey(member.Id)
                ? recordsByMember[member.Id]
                : new List<PresenceHistory>();

            // Calculate working hours breakdown - clips segments to only count time within working hour windows
            // Handles records that span working hour boundaries (e.g., 8:30 AM - 9:30 AM when work starts at 9:00 AM)
            var workingHoursBreakdown = _calculator.CalculateForWorkingHours(
                memberRecords,
                organisation.WorkingHours!,
                organisation.Timezone!,
                periodStart,
                periodEnd);

            // Calculate full period breakdown (all records) - fills entire period with segments
            var fullPeriodBreakdown = _calculator.Calculate(
                memberRecords,
                periodStart,
                periodEnd);

            // Calculate overtime
            var overtimeHours = fullPeriodBreakdown.TotalHours - workingHoursBreakdown.TotalHours;

            // Detect insights
            var insights = _insightDetector.DetectInsights(
                workingHoursBreakdown,
                fullPeriodBreakdown,
                organisation.WorkingHours!);

            // Build employee breakdown
            employeeBreakdowns.Add(new EmployeePresenceBreakdown
            {
                TenantMemberId = member.Id,
                DisplayName = member.DisplayName,
                Email = member.Email,
                JobTitle = member.JobTitle,
                WorkingHoursBreakdown = workingHoursBreakdown,
                FullWeekBreakdown = fullPeriodBreakdown,
                OvertimeHours = overtimeHours,
                Insights = insights,
                PresenceRecords = memberRecords // Include raw records for timeline generation
            });
        }

        return employeeBreakdowns;
    }

    private (DateTimeOffset periodStart, DateTimeOffset periodEnd) CalculateDailyDateRange(string timezone)
    {
        var tzInfo = TimeZoneInfo.FindSystemTimeZoneById(timezone);
        var nowInOrgTz = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tzInfo);

        // Yesterday in org's timezone
        var yesterday = nowInOrgTz.Date.AddDays(-1);

        // Start: Yesterday at 00:00:00
        var periodStart = new DateTimeOffset(
            yesterday.Year,
            yesterday.Month,
            yesterday.Day,
            0, 0, 0,
            tzInfo.GetUtcOffset(yesterday));

        // End: Yesterday at 23:59:59
        var periodEnd = new DateTimeOffset(
            yesterday.Year,
            yesterday.Month,
            yesterday.Day,
            23, 59, 59,
            tzInfo.GetUtcOffset(yesterday.AddHours(23)));

        _logger.LogInformation(
            "Daily report date range: {Start} to {End} (Timezone: {Timezone}, Now in TZ: {NowInTz}, Yesterday: {Yesterday})",
            periodStart, periodEnd, timezone, nowInOrgTz, yesterday);

        return (periodStart, periodEnd);
    }

    private (DateTimeOffset periodStart, DateTimeOffset periodEnd) CalculateWeeklyDateRange(string timezone)
    {
        var tzInfo = TimeZoneInfo.FindSystemTimeZoneById(timezone);
        var nowInOrgTz = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tzInfo);

        // Find last Monday
        var today = nowInOrgTz.Date;
        var daysSinceMonday = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var lastMonday = today.AddDays(-daysSinceMonday - 7);

        // Find last Sunday
        var lastSunday = lastMonday.AddDays(6);

        // Start: Last Monday at 00:00:00
        var periodStart = new DateTimeOffset(
            lastMonday.Year,
            lastMonday.Month,
            lastMonday.Day,
            0, 0, 0,
            tzInfo.GetUtcOffset(lastMonday));

        // End: Last Sunday at 23:59:59
        var periodEnd = new DateTimeOffset(
            lastSunday.Year,
            lastSunday.Month,
            lastSunday.Day,
            23, 59, 59,
            tzInfo.GetUtcOffset(lastSunday.AddHours(23)));

        return (periodStart, periodEnd);
    }

    private (DateTimeOffset periodStart, DateTimeOffset periodEnd) CalculateMonthlyDateRange(string timezone)
    {
        var tzInfo = TimeZoneInfo.FindSystemTimeZoneById(timezone);
        var nowInOrgTz = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tzInfo);

        // First day of last month
        var today = nowInOrgTz.Date;
        var firstDayOfCurrentMonth = new DateTime(today.Year, today.Month, 1);
        var firstDayOfLastMonth = firstDayOfCurrentMonth.AddMonths(-1);

        // Last day of last month
        var lastDayOfLastMonth = firstDayOfCurrentMonth.AddDays(-1);

        // Start: First day of last month at 00:00:00
        var periodStart = new DateTimeOffset(
            firstDayOfLastMonth.Year,
            firstDayOfLastMonth.Month,
            firstDayOfLastMonth.Day,
            0, 0, 0,
            tzInfo.GetUtcOffset(firstDayOfLastMonth));

        // End: Last day of last month at 23:59:59
        var periodEnd = new DateTimeOffset(
            lastDayOfLastMonth.Year,
            lastDayOfLastMonth.Month,
            lastDayOfLastMonth.Day,
            23, 59, 59,
            tzInfo.GetUtcOffset(lastDayOfLastMonth.AddHours(23)));

        return (periodStart, periodEnd);
    }
}

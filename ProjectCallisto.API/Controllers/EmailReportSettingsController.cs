using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectCallisto.Domain.Organisations;
using ProjectCallisto.Domain.Users;
using ProjectCallisto.EfCore;

namespace ProjectCallisto.API.Controllers;

[Authorize]
[ApiController]
[Route("api/organisations/{orgId:guid}/email-report-settings")]
public class EmailReportSettingsController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public EmailReportSettingsController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    [Authorize(Policy = nameof(Permission.ManageSettings))]
    public async Task<IActionResult> GetSettings(Guid orgId)
    {
        var settings = await _dbContext.EmailReportSettings
            .Include(s => s.Recipients)
            .FirstOrDefaultAsync(s => s.OrganisationId == orgId);

        if (settings == null)
        {
            // Return defaults
            return Ok(new EmailReportSettingsResponse(
                null,
                false,
                "weekly",
                "monday",
                1,
                new TimeOnly(9, 0),
                Array.Empty<RecipientResponse>(),
                null
            ));
        }

        return Ok(EmailReportSettingsResponse.FromEntity(settings));
    }

    [HttpPut]
    [Authorize(Policy = nameof(Permission.ManageSettings))]
    public async Task<IActionResult> UpdateSettings(
        Guid orgId,
        [FromBody] UpdateEmailReportSettingsRequest request)
    {
        // Validation
        if (request.Frequency == "weekly" && request.DayOfWeek == null)
            return BadRequest("DayOfWeek is required for weekly reports");

        if (request.Frequency == "monthly" && request.DayOfMonth == null)
            return BadRequest("DayOfMonth is required for monthly reports");

        if (request.DayOfMonth.HasValue && (request.DayOfMonth < 1 || request.DayOfMonth > 28))
            return BadRequest("DayOfMonth must be between 1 and 28");

        var settings = await _dbContext.EmailReportSettings
            .Include(s => s.Recipients)
            .FirstOrDefaultAsync(s => s.OrganisationId == orgId);

        if (settings == null)
        {
            settings = new EmailReportSettings(orgId);
            await _dbContext.EmailReportSettings.AddAsync(settings);
        }

        // Update settings
        settings.IsEnabled = request.IsEnabled;
        settings.Frequency = ParseFrequency(request.Frequency);
        settings.DayOfWeek = !string.IsNullOrEmpty(request.DayOfWeek)
            ? ParseDayOfWeek(request.DayOfWeek)
            : null;
        settings.DayOfMonth = request.DayOfMonth;
        settings.TimeOfDay = request.TimeOfDay;
        settings.UpdatedAt = DateTimeOffset.UtcNow;

        // Update recipients (simple replace strategy)
        _dbContext.EmailRecipients.RemoveRange(settings.Recipients);
        settings.Recipients = request.Recipients
            .Select(r => new EmailRecipient(settings.Id, r.Email, r.Name))
            .ToList();

        await _dbContext.SaveChangesAsync();

        return Ok(EmailReportSettingsResponse.FromEntity(settings));
    }

    private static ReportFrequency ParseFrequency(string frequency) => frequency.ToLowerInvariant() switch
    {
        "daily" => ReportFrequency.Daily,
        "weekly" => ReportFrequency.Weekly,
        "monthly" => ReportFrequency.Monthly,
        _ => throw new ArgumentException($"Invalid frequency: {frequency}")
    };

    private static DayOfWeek ParseDayOfWeek(string day) => day.ToLowerInvariant() switch
    {
        "monday" => DayOfWeek.Monday,
        "tuesday" => DayOfWeek.Tuesday,
        "wednesday" => DayOfWeek.Wednesday,
        "thursday" => DayOfWeek.Thursday,
        "friday" => DayOfWeek.Friday,
        "saturday" => DayOfWeek.Saturday,
        "sunday" => DayOfWeek.Sunday,
        _ => throw new ArgumentException($"Invalid day: {day}")
    };

    private async Task<User?> GetCurrentUserAsync()
    {
        var subjectId = User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
        if (subjectId == null) return null;
        return await _dbContext.Users.FirstOrDefaultAsync(u => u.SubjectId == subjectId);
    }
}

public record EmailReportSettingsResponse(
    Guid? Id,
    bool IsEnabled,
    string Frequency,
    string? DayOfWeek,
    int? DayOfMonth,
    TimeOnly TimeOfDay,
    RecipientResponse[] Recipients,
    DateTimeOffset? LastSentAt)
{
    public static EmailReportSettingsResponse FromEntity(EmailReportSettings settings)
    {
        return new EmailReportSettingsResponse(
            settings.Id,
            settings.IsEnabled,
            settings.Frequency.ToString().ToLowerInvariant(),
            settings.DayOfWeek?.ToString().ToLowerInvariant(),
            settings.DayOfMonth,
            settings.TimeOfDay,
            settings.Recipients.Select(r => new RecipientResponse(r.Id, r.Email, r.Name)).ToArray(),
            settings.LastSentAt
        );
    }
}

public record UpdateEmailReportSettingsRequest(
    bool IsEnabled,
    string Frequency,
    string? DayOfWeek,
    int? DayOfMonth,
    TimeOnly TimeOfDay,
    RecipientRequest[] Recipients
);

public record RecipientRequest(string Email, string? Name);
public record RecipientResponse(Guid Id, string Email, string? Name);

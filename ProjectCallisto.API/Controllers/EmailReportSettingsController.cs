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
            .Where(s => s.OrganisationId == orgId)
            .ToListAsync();

        return Ok(settings.Select(EmailReportSettingsResponse.FromEntity).ToArray());
    }

    [HttpPost("initialize")]
    [Authorize(Policy = nameof(Permission.ManageSettings))]
    public async Task<IActionResult> InitializeSettings(Guid orgId)
    {
        // Get existing settings
        var existingSettings = await _dbContext.EmailReportSettings
            .Include(s => s.Recipients)
            .Where(s => s.OrganisationId == orgId)
            .ToListAsync();

        var existingFrequencies = existingSettings.Select(s => s.Frequency).ToHashSet();

        // Create missing settings with defaults (disabled)
        var allFrequencies = new[] { ReportFrequency.Daily, ReportFrequency.Weekly, ReportFrequency.Monthly };

        foreach (var frequency in allFrequencies)
        {
            if (!existingFrequencies.Contains(frequency))
            {
                var newSetting = new EmailReportSettings(orgId)
                {
                    IsEnabled = false,
                    Frequency = frequency,
                    TimeOfDay = new TimeOnly(9, 0)
                };

                // Set frequency-specific defaults
                if (frequency == ReportFrequency.Weekly)
                {
                    newSetting.DayOfWeek = DayOfWeek.Monday;
                }
                else if (frequency == ReportFrequency.Monthly)
                {
                    newSetting.DayOfMonth = 1;
                }

                await _dbContext.EmailReportSettings.AddAsync(newSetting);
                existingSettings.Add(newSetting);
            }
        }

        await _dbContext.SaveChangesAsync();

        // Return all settings (existing + newly created)
        return Ok(existingSettings.Select(EmailReportSettingsResponse.FromEntity).ToArray());
    }

    [HttpPost]
    [Authorize(Policy = nameof(Permission.ManageSettings))]
    public async Task<IActionResult> CreateSettings(
        Guid orgId,
        [FromBody] CreateEmailReportSettingsRequest request)
    {
        // Validation
        if (request.Frequency == "weekly" && request.DayOfWeek == null)
            return BadRequest("DayOfWeek is required for weekly reports");

        if (request.Frequency == "monthly" && request.DayOfMonth == null)
            return BadRequest("DayOfMonth is required for monthly reports");

        if (request.DayOfMonth.HasValue && (request.DayOfMonth < 1 || request.DayOfMonth > 28))
            return BadRequest("DayOfMonth must be between 1 and 28");

        var settings = new EmailReportSettings(orgId);

        // Set properties
        settings.IsEnabled = request.IsEnabled;
        settings.Frequency = ParseFrequency(request.Frequency);
        settings.DayOfWeek = !string.IsNullOrEmpty(request.DayOfWeek)
            ? ParseDayOfWeek(request.DayOfWeek)
            : null;
        settings.DayOfMonth = request.DayOfMonth;
        settings.TimeOfDay = request.TimeOfDay;

        // Add recipients
        settings.Recipients = request.Recipients
            .Select(r => new EmailRecipient(settings.Id, r.Email, r.Name))
            .ToList();

        await _dbContext.EmailReportSettings.AddAsync(settings);
        await _dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(GetSettings), new { orgId }, EmailReportSettingsResponse.FromEntity(settings));
    }

    [HttpPut("{settingId:guid}")]
    [Authorize(Policy = nameof(Permission.ManageSettings))]
    public async Task<IActionResult> UpdateSettings(
        Guid orgId,
        Guid settingId,
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
            .FirstOrDefaultAsync(s => s.Id == settingId && s.OrganisationId == orgId);

        if (settings == null)
            return NotFound();

        // Update settings
        settings.IsEnabled = request.IsEnabled;
        settings.Frequency = ParseFrequency(request.Frequency);
        settings.DayOfWeek = !string.IsNullOrEmpty(request.DayOfWeek)
            ? ParseDayOfWeek(request.DayOfWeek)
            : null;
        settings.DayOfMonth = request.DayOfMonth;
        settings.TimeOfDay = request.TimeOfDay;
        settings.UpdatedAt = DateTimeOffset.UtcNow;

        // Delete all existing recipients for this setting (separate query to avoid tracking issues)
        var existingRecipients = await _dbContext.EmailRecipients
            .Where(r => r.EmailReportSettingsId == settingId)
            .ToListAsync();

        _dbContext.EmailRecipients.RemoveRange(existingRecipients);
        await _dbContext.SaveChangesAsync(); // Save deletion first

        // Add new recipients
        var newRecipients = request.Recipients
            .Select(r => new EmailRecipient(settings.Id, r.Email, r.Name))
            .ToList();

        await _dbContext.EmailRecipients.AddRangeAsync(newRecipients);
        await _dbContext.SaveChangesAsync(); // Save addition second

        return Ok(EmailReportSettingsResponse.FromEntity(settings));
    }

    [HttpDelete("{settingId:guid}")]
    [Authorize(Policy = nameof(Permission.ManageSettings))]
    public async Task<IActionResult> DeleteSettings(Guid orgId, Guid settingId)
    {
        var settings = await _dbContext.EmailReportSettings
            .FirstOrDefaultAsync(s => s.Id == settingId && s.OrganisationId == orgId);

        if (settings == null)
            return NotFound();

        _dbContext.EmailReportSettings.Remove(settings);
        await _dbContext.SaveChangesAsync();

        return NoContent();
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

public record CreateEmailReportSettingsRequest(
    bool IsEnabled,
    string Frequency,
    string? DayOfWeek,
    int? DayOfMonth,
    TimeOnly TimeOfDay,
    RecipientRequest[] Recipients
);

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

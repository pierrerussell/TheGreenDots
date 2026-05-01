using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectCallisto.Domain.Organisations;
using ProjectCallisto.Domain.Users;
using ProjectCallisto.EfCore;

namespace ProjectCallisto.API.Controllers;

[Authorize]
[ApiController]
[Route("api/organisations/{orgId:guid}/working-hours")]
public class WorkingHoursController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public WorkingHoursController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    [Authorize(Policy = nameof(Permission.ViewDashboard))]
    public async Task<IActionResult> GetWorkingHours(Guid orgId)
    {
        var workingHours = await _dbContext.WorkingHours
            .FirstOrDefaultAsync(wh => wh.OrganisationId == orgId);

        if (workingHours == null)
        {
            // Return defaults if not configured
            return Ok(new WorkingHoursResponse(
                null,
                new TimeOnly(9, 0),
                new TimeOnly(17, 0),
                new[] { "monday", "tuesday", "wednesday", "thursday", "friday" }
            ));
        }

        return Ok(WorkingHoursResponse.FromEntity(workingHours));
    }

    [HttpPut]
    [Authorize(Policy = nameof(Permission.ManageSettings))]
    public async Task<IActionResult> UpdateWorkingHours(
        Guid orgId,
        [FromBody] UpdateWorkingHoursRequest request)
    {
        // Validation
        if (request.StartTime >= request.EndTime)
            return BadRequest("Start time must be before end time");

        if (request.WorkingDays.Length == 0)
            return BadRequest("At least one working day must be selected");

        var workingHours = await _dbContext.WorkingHours
            .FirstOrDefaultAsync(wh => wh.OrganisationId == orgId);

        if (workingHours == null)
        {
            // Create new
            workingHours = new WorkingHours(orgId);
            await _dbContext.WorkingHours.AddAsync(workingHours);
        }

        // Update
        workingHours.StartTime = request.StartTime;
        workingHours.EndTime = request.EndTime;
        workingHours.WorkingDays = ParseWorkingDays(request.WorkingDays);
        workingHours.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync();

        return Ok(WorkingHoursResponse.FromEntity(workingHours));
    }

    private static WorkingDaysFlags ParseWorkingDays(string[] days)
    {
        var flags = WorkingDaysFlags.None;
        foreach (var day in days)
        {
            flags |= day.ToLowerInvariant() switch
            {
                "monday" => WorkingDaysFlags.Monday,
                "tuesday" => WorkingDaysFlags.Tuesday,
                "wednesday" => WorkingDaysFlags.Wednesday,
                "thursday" => WorkingDaysFlags.Thursday,
                "friday" => WorkingDaysFlags.Friday,
                "saturday" => WorkingDaysFlags.Saturday,
                "sunday" => WorkingDaysFlags.Sunday,
                _ => WorkingDaysFlags.None
            };
        }
        return flags;
    }

    private async Task<User?> GetCurrentUserAsync()
    {
        var subjectId = User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
        if (subjectId == null) return null;
        return await _dbContext.Users.FirstOrDefaultAsync(u => u.SubjectId == subjectId);
    }
}

public record WorkingHoursResponse(
    Guid? Id,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string[] WorkingDays)
{
    public static WorkingHoursResponse FromEntity(WorkingHours wh)
    {
        var days = new List<string>();
        if (wh.WorkingDays.HasFlag(WorkingDaysFlags.Monday)) days.Add("monday");
        if (wh.WorkingDays.HasFlag(WorkingDaysFlags.Tuesday)) days.Add("tuesday");
        if (wh.WorkingDays.HasFlag(WorkingDaysFlags.Wednesday)) days.Add("wednesday");
        if (wh.WorkingDays.HasFlag(WorkingDaysFlags.Thursday)) days.Add("thursday");
        if (wh.WorkingDays.HasFlag(WorkingDaysFlags.Friday)) days.Add("friday");
        if (wh.WorkingDays.HasFlag(WorkingDaysFlags.Saturday)) days.Add("saturday");
        if (wh.WorkingDays.HasFlag(WorkingDaysFlags.Sunday)) days.Add("sunday");

        return new WorkingHoursResponse(wh.Id, wh.StartTime, wh.EndTime, days.ToArray());
    }
}

public record UpdateWorkingHoursRequest(
    TimeOnly StartTime,
    TimeOnly EndTime,
    string[] WorkingDays
);

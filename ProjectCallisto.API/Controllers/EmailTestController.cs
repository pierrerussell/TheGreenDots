using System.Text;
using Microsoft.AspNetCore.Mvc;
using ProjectCallisto.Application.Emails;
using ProjectCallisto.Application.Queues;

namespace ProjectCallisto.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestEmailController : ControllerBase
{
  private readonly IQueueService<EmailMessage> _queueService;
  private readonly ILogger<TestEmailController> _logger;

  public TestEmailController(
      IQueueService<EmailMessage> queueService,
      ILogger<TestEmailController> logger)
  {
      _queueService = queueService;
      _logger = logger;
  }

  [HttpPost("send")]
  public async Task<IActionResult> SendTestEmail([FromBody] SendTestEmailRequest request)
  {
      try
      {
          // Generate sample CSV data
          byte[]? csvData = null;
          if (request.IncludeCsv)
          {
              var csv = new StringBuilder();
              csv.AppendLine("Date,Project,Hours,Description");
              csv.AppendLine("2026-04-21,Project Alpha,8.5,Development work");
              csv.AppendLine("2026-04-22,Project Beta,6.0,Code review");
              csv.AppendLine("2026-04-23,Project Alpha,7.5,Bug fixes");
              csv.AppendLine("2026-04-24,Project Gamma,8.0,Feature implementation");
              csv.AppendLine("2026-04-25,Project Beta,5.5,Documentation");

              csvData = Encoding.UTF8.GetBytes(csv.ToString());
          }

          var emailMessage = new EmailMessage
          {
              To = request.ToEmail,
              TemplateId = request.TemplateId,
              TemplateData = new Dictionary<string, object>
              {
                  { "userName", request.UserName ?? "Test User" },
                  { "reportWeek", "Week of April 21, 2026" },
                  { "totalHours", 35.5 },
                  { "projectCount", 3 },
                  { "topProject", "Project Alpha" }
              },
              CsvAttachment = csvData,
              CsvFileName = request.IncludeCsv ? "weekly-report.csv" : null
          };

          await _queueService.EnqueueAsync(emailMessage);

          _logger.LogInformation("Test email queued for {Email}", request.ToEmail);

          return Ok(new
          {
              message = "Test email queued successfully",
              queueName = "email-queue",
              recipient = request.ToEmail,
              templateId = request.TemplateId,
              includedCsv = request.IncludeCsv
          });
      }
      catch (Exception ex)
      {
          _logger.LogError(ex, "Failed to queue test email");
          return StatusCode(500, new { error = ex.Message });
      }
  }
}

public record SendTestEmailRequest(
  string ToEmail,
  string TemplateId = "weekly-report",
  string? UserName = null,
  bool IncludeCsv = true
);
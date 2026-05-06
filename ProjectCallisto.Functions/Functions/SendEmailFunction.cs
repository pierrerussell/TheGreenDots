using System.Text.Json;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using ProjectCallisto.Application.Emails;
using Microsoft.Azure.Functions.Worker.Extensions;
namespace ProjectCallisto.Functions.Functions;

public class SendEmailFunction
{
    private readonly IEmailService _emailService;
    private readonly ILogger<SendEmailFunction> _logger;

    public SendEmailFunction(ILogger<SendEmailFunction> logger, IEmailService emailService)
    {
        _logger = logger;
        _emailService = emailService;
    }

    [Function("SendEmail")]
    public async Task Run(
        [QueueTrigger("email-queue", Connection = "AzureQueue")]
        QueueMessage message
    )
    {
        _logger.LogInformation("Processing email: {MessageId}", message.MessageId);
        try
        {
            var emailMessage = JsonSerializer.Deserialize<EmailMessage>(message.MessageText);
            if (emailMessage == null)
                throw new InvalidOperationException("Deserialization failed");

            if (string.IsNullOrEmpty(emailMessage.To) || !emailMessage.To.Contains("@"))
                throw new ArgumentException($"Invalid email address: {emailMessage.To}");

            Stream? csvStream = null;
            if (emailMessage.CsvAttachment != null)
            {
                csvStream = new MemoryStream(emailMessage.CsvAttachment);
            }

            // Determine if this is a templated or HTML email
            if (!string.IsNullOrEmpty(emailMessage.HtmlBody) && !string.IsNullOrEmpty(emailMessage.Subject))
            {
                // Send HTML email
                await _emailService.SendHtmlEmailAsync(
                    emailMessage.To,
                    emailMessage.Subject,
                    emailMessage.HtmlBody,
                    csvStream,
                    emailMessage.CsvFileName
                );
                _logger.LogInformation("HTML email sent to {To} with subject: {Subject}", emailMessage.To, emailMessage.Subject);
            }
            else if (!string.IsNullOrEmpty(emailMessage.TemplateId))
            {
                // Send templated email (legacy)
                if (!Guid.TryParse(emailMessage.TemplateId, out _))
                    throw new ArgumentException($"Invalid template ID: {emailMessage.TemplateId}");

                await _emailService.SendTemplatedEmailAsync(
                    emailMessage.To,
                    emailMessage.TemplateId,
                    emailMessage.TemplateData ?? new Dictionary<string, object>(),
                    csvStream,
                    emailMessage.CsvFileName
                );
                _logger.LogInformation("Template email sent to {To} with template: {TemplateId}", emailMessage.To, emailMessage.TemplateId);
            }
            else
            {
                throw new InvalidOperationException("Email message must have either HtmlBody+Subject or TemplateId");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email: {MessageId}", message.MessageId);
            throw; // Let the function retry according to the queue's retry policy
        }
    }
}
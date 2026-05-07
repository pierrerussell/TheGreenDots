using System.Text.Json;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using ProjectCallisto.Application.Emails;
using ProjectCallisto.Application.Validation;
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
        _logger.LogInformation(
            "Processing email. MessageId: {MessageId}, DequeueCount: {DequeueCount}",
            message.MessageId,
            message.DequeueCount);

        try
        {
            var emailMessage = JsonSerializer.Deserialize<EmailMessage>(message.MessageText);
            if (emailMessage == null)
            {
                _logger.LogError("Email message deserialization failed. MessageId: {MessageId}", message.MessageId);
                throw new InvalidOperationException("Deserialization failed");
            }

            // Validate email address to prevent header injection attacks
            EmailValidator.ValidateOrThrow(emailMessage.To, nameof(emailMessage.To));

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
                _logger.LogInformation(
                    "HTML email sent successfully. MessageId: {MessageId}, To: {To}, Subject: {Subject}",
                    message.MessageId,
                    emailMessage.To,
                    emailMessage.Subject);
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
        catch (ArgumentException ex)
        {
            // Validation errors - don't retry (will fail again)
            _logger.LogError(ex,
                "Email validation failed. MessageId: {MessageId}, DequeueCount: {DequeueCount}",
                message.MessageId,
                message.DequeueCount);

            // Don't throw - message will be moved to poison queue after max dequeue attempts
            throw;
        }
        catch (InvalidOperationException ex)
        {
            // Business logic errors - may be transient, allow retry
            _logger.LogError(ex,
                "Email processing error. MessageId: {MessageId}, DequeueCount: {DequeueCount}",
                message.MessageId,
                message.DequeueCount);

            throw; // Retry with exponential backoff
        }
        catch (Exception ex)
        {
            // Unexpected errors - log with full context
            _logger.LogError(ex,
                "Unexpected error sending email. MessageId: {MessageId}, DequeueCount: {DequeueCount}, " +
                "ExceptionType: {ExceptionType}",
                message.MessageId,
                message.DequeueCount,
                ex.GetType().Name);

            throw; // Retry with exponential backoff
        }
    }
}
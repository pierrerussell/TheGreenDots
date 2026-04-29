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
        [QueueTrigger("email-queue", Connection = "AzureQueue:ConnectionString")]
        QueueMessage message
    )
    {
        _logger.LogInformation("Processing email: {MessageId}", message.MessageId);
        try
        {
            var emailMessage = JsonSerializer.Deserialize<EmailMessage>(message.MessageText);
            if (emailMessage == null)
            {
                _logger.LogError("Failed to deserialize message");
                return;
            }

            Stream? csvStream = null;
            if (emailMessage.CsvAttachment != null)
            {
                csvStream = new MemoryStream(emailMessage.CsvAttachment);
            }

            await _emailService.SendTemplatedEmailAsync(
                emailMessage.To,
                emailMessage.TemplateId,
                emailMessage.TemplateData,
                csvStream,
                emailMessage.CsvFileName
            );
            _logger.LogInformation("Email sent to {To}", emailMessage.To);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email: {MessageId}", message.MessageId);
            throw; // Let the function retry according to the queue's retry policy
        }
    }
}
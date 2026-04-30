using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectCallisto.Application.Emails;
using Resend;
using EmailMessage = Resend.EmailMessage;


namespace ProjectCallisto.Resend;

public class ResendEmailService : IEmailService
{
    private readonly IResend _resend;
    private readonly IOptions<ResendOptions> _options;
    private readonly ILogger<ResendEmailService> _logger;
    
    public ResendEmailService(IResend resend, IOptions<ResendOptions> options, ILogger<ResendEmailService> logger)
    {
        _resend = resend;
        _options = options;
        _logger = logger;
    }
    
    public async Task SendTemplatedEmailAsync(string to, string templateId, Dictionary<string, object> templateData, Stream? csvAttachment = null,
        string? csvFileName = null)
    {
        try
        {
            var message = new EmailMessage
            {
                From = _options.Value.FromEmail,
                To = to,
                Subject = "",
                Template = new EmailMessageTemplate()
                {
                    TemplateId = new Guid(templateId),
                    Variables = templateData
                }
            };
            if (csvAttachment != null && !string.IsNullOrEmpty(csvFileName))
            {
                using var memoryStream = new MemoryStream();
                await csvAttachment.CopyToAsync(memoryStream);

                message.Attachments = new List<EmailAttachment>()
                {
                    new EmailAttachment()
                    {
                        Content = memoryStream.ToArray(),
                        ContentType = "text/csv",
                        Filename = csvFileName
                    }
                };
            }

            var response = await _resend.EmailSendAsync(message);
            _logger.LogInformation(
                "Email sent. ID: {EmailId}, Template: {TemplateId}",
                response.Content, templateId);
        }
        catch (Exception ex)
        {
            _logger.LogError( ex, "Failed to send email to {To}", to);
            throw;
        }
    }
}
namespace ProjectCallisto.Application.Emails;

public interface IEmailService
{
    Task SendTemplatedEmailAsync(string to, string templateId, Dictionary<string, object> templateData, Stream? csvAttachment = null, string? csvFileName = null);
}
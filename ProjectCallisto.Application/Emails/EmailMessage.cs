namespace ProjectCallisto.Application.Emails;

public class EmailMessage
{
    public string To { get; set; } = string.Empty;

    // Template-based email (legacy)
    public string? TemplateId { get; set; }
    public Dictionary<string, object>? TemplateData { get; set; }

    // HTML-based email (new)
    public string? Subject { get; set; }
    public string? HtmlBody { get; set; }

    // Attachments
    public byte[]? CsvAttachment { get; set; }
    public string? CsvFileName { get; set; }
}
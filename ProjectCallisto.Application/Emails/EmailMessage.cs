namespace ProjectCallisto.Application.Emails;

public class EmailMessage
{
    public string To { get; set; } = string.Empty;
    public string TemplateId { get; set; } = string.Empty;
    public Dictionary<string, object> TemplateData { get; set; } = new();
    public byte[]? CsvAttachment { get; set; }
    public string? CsvFileName { get; set; }
}
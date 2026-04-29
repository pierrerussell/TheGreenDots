namespace ProjectCallisto.AzureQueue;

public class AzureQueueOptions
{
    public string StorageAccountName { get; set; } = string.Empty;
    public string EmailQueueName { get; set; } = "email-queue";
}
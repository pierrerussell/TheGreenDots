using System.Text;
using System.Text.Json;
using Azure.Identity;
using Azure.Storage.Queues;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectCallisto.Application.Emails;
using ProjectCallisto.Application.Queues;
using ProjectCallisto.Application.Reports;

namespace ProjectCallisto.AzureQueue;

public class AzureQueueService<T> : IQueueService<T> where T : class
{
    private readonly QueueClient _queueClient;
    private readonly ILogger<AzureQueueService<T>> _logger;
    
    public AzureQueueService(IOptions<AzureQueueOptions> options, ILogger<AzureQueueService<T>> logger)
    {
        _logger = logger;

        // Determine queue name based on message type
        var queueName = typeof(T).Name switch
        {
            nameof(EmailMessage) => options.Value.EmailQueueName,
            nameof(ReportCalculationJob) => options.Value.ReportCalculationQueueName, // ADD
            _ => throw new InvalidOperationException($"No queue configured for type {typeof(T).Name}")
        };

        // Use DefaultAzureCredential (works locally with az login and in Azure with Managed Identity)
        var queueUri = new Uri($"https://{options.Value.StorageAccountName}.queue.core.windows.net/{queueName}");
        _queueClient = new QueueClient(queueUri, new DefaultAzureCredential());
    }
    
    public async Task EnqueueAsync(T message)
    {
        try
        {
            var json = JsonSerializer.Serialize(message);
            var bytes = Encoding.UTF8.GetBytes(json);
            var base64 = Convert.ToBase64String(bytes);

            await _queueClient.SendMessageAsync(base64);
            _logger.LogInformation("Message enqueued to {QueueName}", _queueClient.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue message");
            throw;
        }
    }
}
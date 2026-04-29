using System.Text;
using System.Text.Json;
using Azure.Storage.Queues;
using Microsoft.Extensions.Logging;
using ProjectCallisto.Application.Queues;

namespace ProjectCallisto.AzureQueue;

public class AzureQueueService<T> : IQueueService<T> where T : class
{
    private readonly QueueClient _queueClient;
    private readonly ILogger<AzureQueueService<T>> _logger;
    
    public AzureQueueService(QueueClient queueClient, ILogger<AzureQueueService<T>> logger)
    {
        _queueClient = queueClient;
        _logger = logger;
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
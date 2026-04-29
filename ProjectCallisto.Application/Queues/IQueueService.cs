namespace ProjectCallisto.Application.Queues;

public interface IQueueService<T> where T : class
{
    Task EnqueueAsync(T message);

}
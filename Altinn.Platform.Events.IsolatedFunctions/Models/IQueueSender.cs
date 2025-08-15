public interface IQueueSender
{
    Task SendMessageAsync(string text, TimeSpan? visibility = null, TimeSpan? ttl = null, CancellationToken ct = default);
}

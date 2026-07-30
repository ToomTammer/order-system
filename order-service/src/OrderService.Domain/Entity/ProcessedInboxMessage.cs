namespace OrderService.Domain.Entity;
public class ProcessedInboxMessage
{
    public Guid MessageId { get; private set; }
    public DateTimeOffset ProcessedAt { get; private set; }

    private ProcessedInboxMessage() { }

    public static ProcessedInboxMessage Create(Guid messageId) => new()
    {
        MessageId = messageId,
        ProcessedAt = DateTimeOffset.UtcNow,
    };
}

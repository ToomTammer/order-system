namespace OrderService.Domain.Entity;
public class OutboxMessage
{
    public Guid Id { get; private set; }
    public Guid AggregateId { get; private set; }
    public string EventType { get; private set; } = default!;
    public string Payload { get; private set; } = default!;
    public Guid CorrelationId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public int Attempts { get; private set; }

    private OutboxMessage() { }

    public static OutboxMessage Create(Guid aggregateId, string eventType, string payload, Guid correlationId) => new()
    {
        Id = Guid.NewGuid(),
        AggregateId = aggregateId,
        EventType = eventType,
        Payload = payload,
        CorrelationId = correlationId,
        CreatedAt = DateTimeOffset.UtcNow,
        Attempts = 0,
    };

    public void MarkProcessed() => ProcessedAt = DateTimeOffset.UtcNow;
    public void IncrementAttempts() => Attempts++;
}

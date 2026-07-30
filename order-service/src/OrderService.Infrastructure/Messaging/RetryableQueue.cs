using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RabbitMQ.Client;

namespace OrderService.Infrastructure.Messaging;

public static class RetryableQueue
{
    public const int MaxAttempts = 5;
    public static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    public static void Declare(IModel channel, string queueName, string exchange, params string[] routingKeys)
    {
        var retryQueue = queueName + ".retry";
        var dlq = queueName + ".dlq";

        channel.QueueDeclare(queueName, durable: true, exclusive: false, autoDelete: false, arguments: new Dictionary<string, object>
        {
            ["x-dead-letter-exchange"] = "",
            ["x-dead-letter-routing-key"] = retryQueue,
        });
        foreach (var routingKey in routingKeys)
            channel.QueueBind(queueName, exchange, routingKey);

        channel.QueueDeclare(retryQueue, durable: true, exclusive: false, autoDelete: false, arguments: new Dictionary<string, object>
        {
            ["x-message-ttl"] = (int)RetryDelay.TotalMilliseconds,
            ["x-dead-letter-exchange"] = "",
            ["x-dead-letter-routing-key"] = queueName,
        });

        channel.QueueDeclare(dlq, durable: true, exclusive: false, autoDelete: false);
    }

    public static long GetDeathCount(IBasicProperties props, string queueName)
    {
        if (props.Headers is null || !props.Headers.TryGetValue("x-death", out var raw) || raw is not List<object> deaths)
            return 0;

        foreach (var entry in deaths)
        {
            if (entry is not Dictionary<string, object> death) continue;
            var queueMatches = death.TryGetValue("queue", out var q) && q is byte[] qBytes &&
                                System.Text.Encoding.UTF8.GetString(qBytes) == queueName;
            if (queueMatches && death.TryGetValue("count", out var count) && count is long countValue)
                return countValue;
        }
        return 0;
    }

    public static void SendToDeadLetterQueue(IModel channel, string queueName, IBasicProperties props, ReadOnlyMemory<byte> body)
    {
        channel.BasicPublish(exchange: "", routingKey: queueName + ".dlq", basicProperties: props, body: body);
    }
}
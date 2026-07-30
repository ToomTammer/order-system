using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderService.Infrastructure.Persistence;
using RabbitMQ.Client;
using Serilog.Context;

namespace OrderService.Infrastructure.Messaging;

public class OutboxDispatcherService(
    IServiceScopeFactory scopeFactory,
    IConnection rabbitConnection,
    ILogger<OutboxDispatcherService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private const int BatchSize = 20;
    private const int MaxAttemptsBeforeGivingUpOnThisPoll = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "outbox dispatch batch failed, will retry next poll");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task DispatchBatchAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var messages = await db.OutboxMessages
            .FromSqlRaw(
                """
                SELECT * FROM outbox_messages
                WHERE processed_at IS NULL AND attempts < {0}
                ORDER BY created_at
                LIMIT {1}
                FOR UPDATE SKIP LOCKED
                """,
                MaxAttemptsBeforeGivingUpOnThisPoll,
                BatchSize)
            .ToListAsync(ct);

        if (messages.Count == 0)
        {
            await transaction.CommitAsync(ct);
            return;
        }

        using var channel = rabbitConnection.CreateModel();
        channel.ConfirmSelect();

        foreach (var message in messages)
        {
            using var _ = LogContext.PushProperty("CorrelationId", message.CorrelationId);
            try
            {
                var props = channel.CreateBasicProperties();
                props.Persistent = true;
                props.Headers = new Dictionary<string, object>
                {
                    ["x-message-id"] = message.Id.ToString(),
                    ["x-correlation-id"] = message.CorrelationId.ToString(),
                };

                var body = System.Text.Encoding.UTF8.GetBytes(message.Payload);
                channel.BasicPublish(
                    exchange: RabbitMqConnector.OrdersExchange,
                    routingKey: message.EventType,
                    basicProperties: props,
                    body: body);
                channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(5));

                message.MarkProcessed();
                logger.LogInformation(
                    "published outbox message {MessageId} ({EventType}) for aggregate {AggregateId}",
                    message.Id, message.EventType, message.AggregateId);
            }
            catch (Exception ex)
            {
                message.IncrementAttempts();
                logger.LogWarning(ex, "failed to publish outbox message {MessageId}, attempt {Attempts}", message.Id, message.Attempts);
            }
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
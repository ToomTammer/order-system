using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderService.Application.Events;
using OrderService.Application.Orders;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog.Context;

namespace OrderService.Infrastructure.Messaging;

public class OrderEventsConsumer(
    IServiceScopeFactory scopeFactory,
    IConnection rabbitConnection,
    ILogger<OrderEventsConsumer> logger) : BackgroundService
{
    private const string QueueName = "order-service.stock-results";

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var channel = rabbitConnection.CreateModel();
        RetryableQueue.Declare(channel, QueueName, RabbitMqConnector.OrdersExchange, EventTypes.StockReserved, EventTypes.StockFailed);
        channel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);

        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += (_, delivery) => OnMessageReceived(channel, delivery);
        channel.BasicConsume(QueueName, autoAck: false, consumer);

        logger.LogInformation("consuming {Queue} for StockReserved/StockFailed", QueueName);

        stoppingToken.Register(() => channel.Close());
        return Task.CompletedTask;
    }

    private void OnMessageReceived(IModel channel, BasicDeliverEventArgs delivery)
    {
        var correlationId = GetHeader(delivery.BasicProperties, "x-correlation-id") is { } c ? Guid.Parse(c) : Guid.NewGuid();
        using var _ = LogContext.PushProperty("CorrelationId", correlationId);

        try
        {
            var messageId = GetHeader(delivery.BasicProperties, "x-message-id") is { } m ? Guid.Parse(m) : Guid.NewGuid();

            using var doc = JsonDocument.Parse(delivery.Body.ToArray());
            var root = doc.RootElement;
            var orderId = root.GetProperty("orderId").GetGuid();
            var reserved = delivery.RoutingKey == EventTypes.StockReserved;
            var reason = reserved ? null : root.TryGetProperty("reason", out var r) ? r.GetString() : null;

            using var scope = scopeFactory.CreateScope();
            var orderWorkflow = scope.ServiceProvider.GetRequiredService<IOrderWorkflow>();
            orderWorkflow.HandleStockResultAsync(orderId, reserved, reason, messageId, correlationId).GetAwaiter().GetResult();

            channel.BasicAck(delivery.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            var attempts = RetryableQueue.GetDeathCount(delivery.BasicProperties, QueueName);
            if (attempts + 1 >= RetryableQueue.MaxAttempts)
            {
                logger.LogError(ex, "giving up on stock result message after {Attempts} attempts, sending to DLQ", attempts + 1);
                RetryableQueue.SendToDeadLetterQueue(channel, QueueName, delivery.BasicProperties, delivery.Body);
                channel.BasicAck(delivery.DeliveryTag, multiple: false);
            }
            else
            {
                logger.LogWarning(ex, "failed to process stock result message (attempt {Attempts}), will retry via {Queue}.retry", attempts + 1, QueueName);
                channel.BasicNack(delivery.DeliveryTag, multiple: false, requeue: false);
            }
        }
    }

    private static string? GetHeader(IBasicProperties props, string key) =>
        props.Headers is not null && props.Headers.TryGetValue(key, out var value) && value is byte[] bytes
            ? Encoding.UTF8.GetString(bytes)
            : null;
}
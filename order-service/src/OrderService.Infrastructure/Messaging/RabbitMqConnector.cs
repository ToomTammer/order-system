using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RabbitMQ.Client;

namespace OrderService.Infrastructure.Messaging;

public static class RabbitMqConnector
{
    public const string OrdersExchange = "orders";
    public const string DeadLetterExchange = "orders.dlx";

    public static IConnection ConnectWithRetry(string host, int port, string user, string password, int maxAttempts = 10, int delayMilliseconds = 2000)
    {
        var factory = new ConnectionFactory
        {
            HostName = host,
            Port = port,
            UserName = user,
            Password = password,
            DispatchConsumersAsync = false,
            // restart to recover from a transient RabbitMQ outage otherwise.
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
        };

        Exception? lastError = null;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var connection = factory.CreateConnection();
                using var channel = connection.CreateModel();
                DeclareTopology(channel);
                return connection;
            }
            catch (Exception ex)
            {
                lastError = ex;
                Thread.Sleep(delayMilliseconds);
            }
        }

        throw new InvalidOperationException($"Could not connect to RabbitMQ at {host}:{port} after {maxAttempts} attempts.", lastError);
    }

    public static void DeclareTopology(IModel channel)
    {
        channel.ExchangeDeclare(OrdersExchange, ExchangeType.Topic, durable: true);
        channel.ExchangeDeclare(DeadLetterExchange, ExchangeType.Fanout, durable: true);
    }
}
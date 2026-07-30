using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OrderService.Application.Events;
using OrderService.Application.Orders;
using OrderService.Domain.Entity;
using OrderService.Infrastructure.Messaging;
using OrderService.Infrastructure.Persistence;
using RabbitMQ.Client;
using static OrderService.Domain.Configs;

namespace OrderService.IntegrationTests;
[Collection("Integration")]
public class OutboxAndInboxIntegrationTests(IntegrationTestFixture fixture)
{
    private OrderWorkflow CreateWorkflow(OrderDbContext db) => new(
        new PostgresOrderRepository(db),
        new PostgresOutboxRepository(db),
        new PostgresInboxRepository(db),
        new EfUnitOfWork(db),
        NullLogger<OrderWorkflow>.Instance);

    private OrderDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<OrderDbContext>().UseNpgsql(fixture.Postgres.GetConnectionString()).Options);

    private static async Task<Guid> CreateUserAsync(OrderDbContext db)
    {
        var user = User.Create($"user-{Guid.NewGuid()}", "irrelevant-hash");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    [Fact]
    public async Task CreatingAnOrder_StagesOutboxRow_AndTheRealDispatcher_PublishesItToRabbitMq()
    {
        await using var db = CreateDbContext();
        var userId = await CreateUserAsync(db);
        var order = await CreateWorkflow(db).CreateOrderAsync(userId, "sku-int-test", 2, Guid.NewGuid());

        var stagedRow = await db.OutboxMessages.SingleAsync(m => m.AggregateId == order.Id);
        Assert.Equal(EventTypes.OrderCreated, stagedRow.EventType);
        Assert.Null(stagedRow.ProcessedAt);

        var factory = new ConnectionFactory { Uri = new Uri(fixture.RabbitMq.GetConnectionString()) };
        using var rabbitConnection = factory.CreateConnection();
        using var channel = rabbitConnection.CreateModel();
        RabbitMqConnector.DeclareTopology(channel);

        var testQueue = "test.order-created." + Guid.NewGuid();
        channel.QueueDeclare(testQueue, durable: false, exclusive: false, autoDelete: true);
        channel.QueueBind(testQueue, RabbitMqConnector.OrdersExchange, EventTypes.OrderCreated);

        var services = new ServiceCollection();
        services.AddDbContext<OrderDbContext>(o => o.UseNpgsql(fixture.Postgres.GetConnectionString()));
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

    
        var dispatcher = new OutboxDispatcherService(scopeFactory, rabbitConnection, NullLogger<OutboxDispatcherService>.Instance);
        using var cts = new CancellationTokenSource();
        await dispatcher.StartAsync(cts.Token);

        string? matchingBody = null;
        for (var attempt = 0; attempt < 40 && matchingBody is null; attempt++)
        {
            var result = channel.BasicGet(testQueue, autoAck: true);
            if (result is null)
            {
                await Task.Delay(250);
                continue;
            }
            var body = Encoding.UTF8.GetString(result.Body.ToArray());
            if (body.Contains(order.Id.ToString())) matchingBody = body;
        }
        await dispatcher.StopAsync(cts.Token);

        Assert.NotNull(matchingBody);
        Assert.Contains("\"orderId\"", matchingBody);

        await using var verifyDb = CreateDbContext();
        var processedRow = await verifyDb.OutboxMessages.SingleAsync(m => m.AggregateId == order.Id);
        Assert.NotNull(processedRow.ProcessedAt);
    }

    [Fact]
    public async Task HandleStockResultAsync_DuplicateMessageId_NeverDoubleTransitionsOrEmitsADuplicateOutboxRow()
    {
        Guid orderId;
        await using (var db = CreateDbContext())
        {
            var userId = await CreateUserAsync(db);
            var order = await CreateWorkflow(db).CreateOrderAsync(userId, "sku-int-test", 1, Guid.NewGuid());
            orderId = order.Id;
        }

        var messageId = Guid.NewGuid();

        await using (var db1 = CreateDbContext())
            await CreateWorkflow(db1).HandleStockResultAsync(orderId, reserved: true, failureReason: null, messageId, Guid.NewGuid());
        await using (var db2 = CreateDbContext())
            await CreateWorkflow(db2).HandleStockResultAsync(orderId, reserved: true, failureReason: null, messageId, Guid.NewGuid());

        await using var verifyDb = CreateDbContext();
        var confirmedRows = await verifyDb.OutboxMessages
            .Where(m => m.AggregateId == orderId && m.EventType == EventTypes.OrderConfirmed)
            .ToListAsync();
        Assert.Single(confirmedRows);

        var inboxRows = await verifyDb.ProcessedInboxMessages.Where(m => m.MessageId == messageId).ToListAsync();
        Assert.Single(inboxRows);

        var refreshedOrder = await verifyDb.Orders.SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Confirmed, refreshedOrder.Status);
    }
}

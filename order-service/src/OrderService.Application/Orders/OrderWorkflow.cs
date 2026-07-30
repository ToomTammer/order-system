using System.Text.Json;
using Microsoft.Extensions.Logging;
using OrderService.Application.Events;
using OrderService.Domain;
using OrderService.Domain.Entity;
using OrderService.Domain.IRepository;
using static OrderService.Domain.Configs;

namespace OrderService.Application.Orders;

public class OrderWorkflow(
    IOrderRepository orderRepository,
    IOutboxRepository outboxRepository,
    IInboxRepository inboxRepository,
    IUnitOfWork unitOfWork,
    ILogger<OrderWorkflow> logger) : IOrderWorkflow
{
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 20;

    public async Task<Order> CreateOrderAsync(Guid userId, string productId, int quantity, Guid correlationId, CancellationToken ct = default)
    {
        try
        {
            var order = Order.Create(userId, productId, quantity);
            orderRepository.Add(order);

            var payload = JsonSerializer.Serialize(
                new { orderId = order.Id, userId = order.UserId, productId = order.ProductId, quantity = order.Quantity },
                EventJsonOptions.CamelCase);
            outboxRepository.Add(OutboxMessage.Create(order.Id, EventTypes.OrderCreated, payload, correlationId));

            await unitOfWork.SaveChangesAsync(ct);
            return order;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while creating order");
            throw;
        }
        
    }

    public Task<Order?> GetOrderAsync(Guid orderId, Guid callerUserId, CancellationToken ct = default) =>
        orderRepository.GetByIdForUserAsync(orderId, callerUserId, ct);

    public async Task<PagedResult<Order>> ListMyOrdersAsync(
        Guid callerUserId,
        OrderStatus? statusFilter,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize <= 0 ? DefaultPageSize : pageSize, 1, MaxPageSize);

        var (items, totalCount) = await orderRepository.ListForUserAsync(callerUserId, statusFilter, page, pageSize, ct);
        return new PagedResult<Order>(items, page, pageSize, totalCount);
    }

    public async Task HandleStockResultAsync(
        Guid orderId,
        bool reserved,
        string? failureReason,
        Guid messageId,
        Guid correlationId,
        CancellationToken ct = default)
    {
        var order = await orderRepository.GetByIdAsync(orderId, ct);
        if (order is null)
        {
            logger.LogWarning("received stock result for unknown order {OrderId}, correlation {CorrelationId}, ignoring", orderId, correlationId);
            return;
        }

        inboxRepository.Add(ProcessedInboxMessage.Create(messageId));
        order.UpdateStatus(reserved ? OrderStatus.Confirmed : OrderStatus.Failed);

        var eventType = reserved ? EventTypes.OrderConfirmed : EventTypes.OrderFailed;
        var payload = JsonSerializer.Serialize(
            new
            {
                orderId = order.Id,
                userId = order.UserId,
                productId = order.ProductId,
                status = eventType
            },
            EventJsonOptions.CamelCase);
        outboxRepository.Add(OutboxMessage.Create(order.Id, eventType, payload, correlationId));

        var committed = await unitOfWork.TrySaveChangesAsync(ct);
        if (!committed)
        {
            logger.LogInformation(
                "stock result message {MessageId} for order {OrderId}, correlation {CorrelationId}, already processed, skipping duplicate delivery",
                messageId, orderId, correlationId);
        }
    }
}

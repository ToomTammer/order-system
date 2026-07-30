using OrderService.Domain;
using OrderService.Domain.Entity;
using static OrderService.Domain.Configs;

namespace OrderService.Application.Orders;

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public interface IOrderWorkflow
{
    Task<Order> CreateOrderAsync(Guid userId, string productId, int quantity, Guid correlationId, CancellationToken ct = default);
    Task<Order?> GetOrderAsync(Guid orderId, Guid callerUserId, CancellationToken ct = default);

    Task<PagedResult<Order>> ListMyOrdersAsync(
        Guid callerUserId,
        OrderStatus? statusFilter,
        int page,
        int pageSize,
        CancellationToken ct = default);
    Task HandleStockResultAsync(
        Guid orderId,
        bool reserved,
        string? failureReason,
        Guid messageId,
        Guid correlationId,
        CancellationToken ct = default);
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OrderService.Domain.Entity;
using static OrderService.Domain.Configs;

namespace OrderService.Domain.IRepository;
public interface IOrderRepository
{
    void Add(Order order);
    Task<Order?> GetByIdForUserAsync(Guid orderId, Guid userId, CancellationToken ct = default);

    Task<(IReadOnlyList<Order> Items, int TotalCount)> ListForUserAsync(
        Guid userId,
        OrderStatus? statusFilter,
        int page,
        int pageSize,
        CancellationToken ct = default);
    Task<Order?> GetByIdAsync(Guid orderId, CancellationToken ct = default);
}
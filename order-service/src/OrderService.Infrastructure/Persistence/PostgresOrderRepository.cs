using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrderService.Domain.Entity;
using OrderService.Domain.IRepository;
using static OrderService.Domain.Configs;

namespace OrderService.Infrastructure.Persistence;

public class PostgresOrderRepository(OrderDbContext db) : IOrderRepository
{
    public void Add(Order order) => db.Orders.Add(order);

    public Task<Order?> GetByIdForUserAsync(Guid orderId, Guid userId, CancellationToken ct = default) =>
        db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId, ct);

    public async Task<(IReadOnlyList<Order> Items, int TotalCount)> ListForUserAsync(
        Guid userId,
        OrderStatus? statusFilter,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = db.Orders.Where(o => o.UserId == userId);
        if (statusFilter is not null)
            query = query.Where(o => o.Status == statusFilter.Value);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public Task<Order?> GetByIdAsync(Guid orderId, CancellationToken ct = default) =>
        db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct);
}
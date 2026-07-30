using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OrderService.Domain.Entity;
using OrderService.Domain.IRepository;
using static OrderService.Domain.Configs;

namespace OrderService.Tests.Fakes;

public class InMemoryOrderRepository : IOrderRepository
{
    private readonly List<Order> _orders = [];
    public void Add(Order order) => _orders.Add(order);

    public Task<Order?> GetByIdForUserAsync(Guid orderId, Guid userId, CancellationToken ct = default) =>
        Task.FromResult(_orders.FirstOrDefault(o => o.Id == orderId && o.UserId == userId));

    public Task<Order?> GetByIdAsync(Guid orderId, CancellationToken ct = default) =>
        Task.FromResult(_orders.FirstOrDefault(o => o.Id == orderId));

    public Task<(IReadOnlyList<Order>, int)> ListForUserAsync(
        Guid userId, OrderStatus? statusFilter, int page, int pageSize, CancellationToken ct = default)
    {
        var all = _orders.Where(o => o.UserId == userId).OrderByDescending(o => o.CreatedAt).ToList();
        var page1 = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult<(IReadOnlyList<Order>, int)>((page1, all.Count));
    }
}
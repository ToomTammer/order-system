using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrderService.Domain.Entity;
using OrderService.Infrastructure.Persistence;

namespace OrderService.IntegrationTests;

[Collection("Integration")]
public class OrderRepositoryIntegrationTests(IntegrationTestFixture fixture)
{
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
    public async Task GetByIdForUserAsync_ReturnsNull_ForAnotherUsersOrder()
    {
        await using var db = CreateDbContext();
        var ownerId = await CreateUserAsync(db);
        var attackerId = await CreateUserAsync(db);

        var order = Order.Create(ownerId, "sku-int-test", 1);
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        await using var queryDb = CreateDbContext();
        var repo = new PostgresOrderRepository(queryDb);

        Assert.NotNull(await repo.GetByIdForUserAsync(order.Id, ownerId));
        Assert.Null(await repo.GetByIdForUserAsync(order.Id, attackerId));
    }

    [Fact]
    public async Task ListForUserAsync_OnlyReturnsCallersOwnOrders()
    {
        await using var db = CreateDbContext();
        var userA = await CreateUserAsync(db);
        var userB = await CreateUserAsync(db);

        db.Orders.Add(Order.Create(userA, "sku-a1", 1));
        db.Orders.Add(Order.Create(userA, "sku-a2", 1));
        db.Orders.Add(Order.Create(userB, "sku-b1", 1));
        await db.SaveChangesAsync();

        await using var queryDb = CreateDbContext();
        var repo = new PostgresOrderRepository(queryDb);
        var (items, totalCount) = await repo.ListForUserAsync(userA, statusFilter: null, page: 1, pageSize: 20);

        Assert.Equal(2, totalCount);
        Assert.All(items, o => Assert.Equal(userA, o.UserId));
    }
}
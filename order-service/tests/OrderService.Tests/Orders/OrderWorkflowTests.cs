using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using OrderService.Application.Orders;
using OrderService.Tests.Fakes;

namespace OrderService.Tests.Orders
{
    public class OrderWorkflowTests
    {
        private static OrderWorkflow CreateSut(out InMemoryOrderRepository repo, out InMemoryOutboxRepository outbox) =>
        CreateSut(out repo, out outbox, out _);

        private static OrderWorkflow CreateSut(
            out InMemoryOrderRepository repo,
            out InMemoryOutboxRepository outbox,
            out InMemoryInboxRepository inbox)
        {
            repo = new InMemoryOrderRepository();
            outbox = new InMemoryOutboxRepository();
            inbox = new InMemoryInboxRepository();
            return new OrderWorkflow(repo, outbox, inbox, new InMemoryUnitOfWork(), NullLogger<OrderWorkflow>.Instance);
        }
        
        [Fact]
        public async Task GetOrderAsync_ReturnsNull_WhenOrderBelongsToAnotherUser()
        {
            var sut = CreateSut(out _, out _);
            var ownerId = Guid.NewGuid();
            var attackerId = Guid.NewGuid();

            var order = await sut.CreateOrderAsync(ownerId, "sku-1", 2, Guid.NewGuid());

            Assert.NotNull(await sut.GetOrderAsync(order.Id, ownerId));
            Assert.Null(await sut.GetOrderAsync(order.Id, attackerId));
        }
    }
}
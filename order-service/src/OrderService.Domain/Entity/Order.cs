using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static OrderService.Domain.Configs;

namespace OrderService.Domain.Entity
{
    
    public class Order
    {
        public Guid Id { get; private set; }
        public Guid UserId { get; private set; }
        public string ProductId { get; private set; } = default!;
        public int Quantity { get; private set; }
        public OrderStatus Status { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset UpdatedAt { get; private set; }

        private Order() { }

        public static Order Create(Guid userId, string productId, int quantity)
        {
            if (userId == Guid.Empty) throw new ArgumentException("userId is required", nameof(userId));
            if (string.IsNullOrWhiteSpace(productId)) throw new ArgumentException("productId is required", nameof(productId));
            if (quantity <= 0) throw new ArgumentException("quantity must be positive", nameof(quantity));

            var now = DateTimeOffset.UtcNow;
            return new Order
            {
                Id = Guid.NewGuid(), UserId = userId, ProductId = productId, Quantity = quantity,
                Status = OrderStatus.Pending, CreatedAt = now, UpdatedAt = now,
            };
        }

        public void UpdateStatus(OrderStatus newStatus)
        {
            if (Status != OrderStatus.Pending) return;
            Status = newStatus;
            UpdatedAt = DateTimeOffset.UtcNow;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OrderService.Domain.Entity;
using OrderService.Domain.IRepository;

namespace OrderService.Infrastructure.Persistence;

public class PostgresOutboxRepository(OrderDbContext db) : IOutboxRepository
{
    public void Add(OutboxMessage message) => db.OutboxMessages.Add(message);
}
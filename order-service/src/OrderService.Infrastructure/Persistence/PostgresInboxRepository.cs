using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OrderService.Domain.Entity;
using OrderService.Domain.IRepository;

namespace OrderService.Infrastructure.Persistence;

public class PostgresInboxRepository(OrderDbContext db) : IInboxRepository
{
    public void Add(ProcessedInboxMessage message) => db.ProcessedInboxMessages.Add(message);
}

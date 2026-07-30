using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OrderService.Domain.Entity;
using OrderService.Domain.IRepository;

namespace OrderService.Tests.Fakes;

public class InMemoryOutboxRepository : IOutboxRepository
{
    private readonly List<OutboxMessage> _messages = [];
    public void Add(OutboxMessage message) => _messages.Add(message);
}
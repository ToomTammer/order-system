using System.Collections.Generic;
using OrderService.Domain.Entity;
using OrderService.Domain.IRepository;

namespace OrderService.Tests.Fakes;

public class InMemoryInboxRepository : IInboxRepository
{
    private readonly List<ProcessedInboxMessage> _messages = [];
    public void Add(ProcessedInboxMessage message) => _messages.Add(message);
}

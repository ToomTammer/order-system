using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OrderService.Domain.Entity;

namespace OrderService.Domain.IRepository;
public interface IOutboxRepository
{
    void Add(OutboxMessage message);
}
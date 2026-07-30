using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OrderService.Application;

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct = default);
    Task<bool> TrySaveChangesAsync(CancellationToken ct = default);
}
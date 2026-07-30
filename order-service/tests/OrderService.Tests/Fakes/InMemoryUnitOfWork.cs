using System.Threading;
using System.Threading.Tasks;
using OrderService.Application;

namespace OrderService.Tests.Fakes;

public class InMemoryUnitOfWork : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task<bool> TrySaveChangesAsync(CancellationToken ct = default) => Task.FromResult(true);
}

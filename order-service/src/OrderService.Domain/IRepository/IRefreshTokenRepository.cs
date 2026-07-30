using OrderService.Domain.Entity;

namespace OrderService.Domain.IRepository;

public interface IRefreshTokenRepository
{
    void Add(RefreshToken token);
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default);
}

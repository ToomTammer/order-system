namespace OrderService.Application.Auth;

public record RegisterResult(Guid UserId, string Username);
public record LoginResult(AccessToken AccessToken, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);

public interface IAuthWorkflow
{
    Task<RegisterResult> RegisterAsync(string username, string password, CancellationToken ct = default);
    Task<LoginResult> LoginAsync(string username, string password, CancellationToken ct = default);
    Task<LoginResult> RefreshAsync(string refreshToken, CancellationToken ct = default);
}

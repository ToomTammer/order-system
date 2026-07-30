using OrderService.Domain;
using OrderService.Domain.Entity;
using OrderService.Domain.IRepository;

namespace OrderService.Application.Auth;

public class AuthWorkflow(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IPasswordHasher passwordHasher,
    ITokenGenerator tokenGenerator,
    IUnitOfWork unitOfWork) : IAuthWorkflow
{
    private const int MinPasswordLength = 8;
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    public async Task<RegisterResult> RegisterAsync(string username, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("username is required", nameof(username));
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinPasswordLength)
            throw new ArgumentException($"password must be at least {MinPasswordLength} characters", nameof(password));

        if (await userRepository.ExistsByUsernameAsync(username, ct))
            throw new UsernameTakenException(username);

        var passwordHash = passwordHasher.HashPassword(password);
        var user = User.Create(username, passwordHash);
        await userRepository.AddAsync(user, ct);

        return new RegisterResult(user.Id, user.Username);
    }

    public async Task<LoginResult> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        var user = await userRepository.GetByUsernameAsync(username, ct);
        if (user is null)
            throw new InvalidCredentialsException();

        if (user.IsLockedOut)
            throw new AccountLockedException(user.LockedUntil!.Value);

        if (!passwordHasher.VerifyPassword(user.PasswordHash, password))
        {
            user.RecordFailedLogin();
            await unitOfWork.SaveChangesAsync(ct);
            throw new InvalidCredentialsException();
        }

        user.RecordSuccessfulLogin();
        return await IssueTokenPairAsync(user, ct);
    }

    public async Task<LoginResult> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var tokenHash = RefreshTokenHasher.Hash(refreshToken);
        var existing = await refreshTokenRepository.GetByTokenHashAsync(tokenHash, ct);
        if (existing is null || !existing.IsActive)
            throw new InvalidRefreshTokenException();

        var user = await userRepository.GetByIdAsync(existing.UserId, ct);
        if (user is null)
            throw new InvalidRefreshTokenException();

        // Rotation: the presented token is single-use — revoked here, in the
        // same unit of work as issuing its replacement below.
        existing.Revoke();

        return await IssueTokenPairAsync(user, ct);
    }

    private async Task<LoginResult> IssueTokenPairAsync(User user, CancellationToken ct)
    {
        var accessToken = tokenGenerator.GenerateAccessToken(user);

        var rawRefreshToken = RefreshTokenHasher.GenerateRawToken();
        var expiresAt = DateTimeOffset.UtcNow.Add(RefreshTokenLifetime);
        refreshTokenRepository.Add(RefreshToken.Create(user.Id, RefreshTokenHasher.Hash(rawRefreshToken), expiresAt));

        await unitOfWork.SaveChangesAsync(ct);

        return new LoginResult(accessToken, rawRefreshToken, expiresAt);
    }
}

namespace OrderService.Application.Auth;

public class UsernameTakenException(string username) : Exception($"Username '{username}' is already taken.");
public class InvalidCredentialsException() : Exception("Invalid username or password.");
public class InvalidRefreshTokenException() : Exception("Refresh token is invalid, expired, or already used.");
public class AccountLockedException(DateTimeOffset lockedUntil)
    : Exception($"Account is temporarily locked until {lockedUntil:O} due to too many failed login attempts.")
{
    public DateTimeOffset LockedUntil { get; } = lockedUntil;
}

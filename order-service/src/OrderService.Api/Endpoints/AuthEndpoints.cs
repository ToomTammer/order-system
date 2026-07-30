using OrderService.Api.Configurations;
using OrderService.Application.Auth;

namespace OrderService.Api.Endpoints;

public record RegisterRequest(string Username, string Password);
public record LoginRequest(string Username, string Password);
public record RefreshRequest(string RefreshToken);

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/auth");

        group.MapPost("/register", async (RegisterRequest request, IAuthWorkflow authWorkflow, CancellationToken ct) =>
        {
            try
            {
                var result = await authWorkflow.RegisterAsync(request.Username, request.Password, ct);
                return Results.Created($"/auth/{result.UserId}", new { userId = result.UserId, username = result.Username });
            }
            catch (ArgumentException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
            catch (UsernameTakenException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
            }
        }).RequireRateLimiting(RateLimiting.AuthPolicy);

        group.MapPost("/login", async (LoginRequest request, IAuthWorkflow authWorkflow, CancellationToken ct) =>
        {
            try
            {
                var result = await authWorkflow.LoginAsync(request.Username, request.Password, ct);
                return Results.Ok(ToTokenPairDto(result));
            }
            catch (InvalidCredentialsException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status401Unauthorized);
            }
            catch (AccountLockedException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status423Locked);
            }
        }).RequireRateLimiting(RateLimiting.AuthPolicy);

        group.MapPost("/refresh", async (RefreshRequest request, IAuthWorkflow authWorkflow, CancellationToken ct) =>
        {
            try
            {
                var result = await authWorkflow.RefreshAsync(request.RefreshToken, ct);
                return Results.Ok(ToTokenPairDto(result));
            }
            catch (InvalidRefreshTokenException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status401Unauthorized);
            }
        });
    }

    private static object ToTokenPairDto(LoginResult result) => new
    {
        accessToken = result.AccessToken.Value,
        expiresAt = result.AccessToken.ExpiresAt,
        refreshToken = result.RefreshToken,
        refreshTokenExpiresAt = result.RefreshTokenExpiresAt,
    };
}

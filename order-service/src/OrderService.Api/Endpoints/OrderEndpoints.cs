using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using OrderService.Application.Orders;
using OrderService.Domain.Entity;
using static OrderService.Domain.Configs;

namespace OrderService.Api.Endpoints;

public record CreateOrderRequest(string ProductId, int Quantity);

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/orders").RequireAuthorization();

        group.MapPost("/", async (CreateOrderRequest request, ClaimsPrincipal caller, IOrderWorkflow orderWorkflow, HttpContext httpContext, CancellationToken ct) =>
        {
            var userId = caller.GetUserId();
            try
            {
                var order = await orderWorkflow.CreateOrderAsync(userId, request.ProductId, request.Quantity, httpContext.GetCorrelationId(), ct);
                return Results.Created($"/orders/{order.Id}", ToDto(order));
            }
            catch (ArgumentException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
        });

        group.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal caller, IOrderWorkflow orderWorkflow, CancellationToken ct) =>
        {
            var userId = caller.GetUserId();
            var order = await orderWorkflow.GetOrderAsync(id, userId, ct);
            return order is null ? Results.NotFound() : Results.Ok(ToDto(order));
        });

        group.MapGet("/", async (ClaimsPrincipal caller, IOrderWorkflow orderWorkflow, CancellationToken ct, int? page, int? pageSize, string? status) =>
        {
            OrderStatus? statusFilter = null;
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (!Enum.TryParse<OrderStatus>(status, ignoreCase: true, out var parsed))
                    return Results.Problem(
                        detail: $"status must be one of: {string.Join(", ", Enum.GetNames<OrderStatus>())}",
                        statusCode: StatusCodes.Status400BadRequest);
                statusFilter = parsed;
            }

            var userId = caller.GetUserId();
            var result = await orderWorkflow.ListMyOrdersAsync(
                userId,
                statusFilter,
                page: page ?? 1,
                pageSize: pageSize ?? OrderWorkflow.DefaultPageSize,
                ct);

            return Results.Ok(new
            {
                items = result.Items.Select(ToDto),
                page = result.Page,
                pageSize = result.PageSize,
                totalCount = result.TotalCount,
            });
        });
    }

    private static object ToDto(Order order) => new
    {
        id = order.Id,
        productId = order.ProductId,
        quantity = order.Quantity,
        status = order.Status.ToString(),
        createdAt = order.CreatedAt,
        updatedAt = order.UpdatedAt,
    };

    private static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.Parse(sub!);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Serilog.Context;

namespace OrderService.Api;
public static class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemsKey = "CorrelationId";

    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var header) && Guid.TryParse(header, out var parsed)
                ? parsed
                : Guid.NewGuid();

            context.Items[ItemsKey] = correlationId;
            context.Response.Headers[HeaderName] = correlationId.ToString();

            using (LogContext.PushProperty("CorrelationId", correlationId))
            {
                await next();
            }
        });

    public static Guid GetCorrelationId(this HttpContext context) =>
        context.Items.TryGetValue(ItemsKey, out var value) && value is Guid id ? id : Guid.NewGuid();
}
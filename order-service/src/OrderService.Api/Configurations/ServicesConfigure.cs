using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OrderService.Infrastructure.Auth;
using OrderService.Infrastructure.HealthChecks;
using OrderService.Infrastructure.Messaging;
using OrderService.Infrastructure.Persistence;
using Serilog;

namespace OrderService.Api.Configurations
{
    public static class ServicesConfigure
    {
        public static void BasicConfigure(this WebApplicationBuilder builder)
        {
            builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
            var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
    
            if (!builder.Environment.IsDevelopment())
            {
                var connectionString = builder.Configuration.GetConnectionString("Postgres") ?? "";
                var rabbitPasswordCfg = builder.Configuration["RabbitMq:Password"] ?? "";
                if (jwtOptions.SigningKey == JwtOptions.DevOnlySigningKey)
                    throw new InvalidOperationException(
                        "Refusing to start: Jwt:SigningKey is still the dev-only default outside a Development " +
                        "environment. Set a real signing key via your deploy target's secrets manager (e.g. a Railway " +
                        "environment variable)");
                if (connectionString.Contains("Password=postgres", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        "Refusing to start: ConnectionStrings:Postgres still uses the docker-compose dev password " +
                        "outside a Development environment. Set a real DB credential via your deploy target's secrets manager.");
                if (rabbitPasswordCfg == "guest")
                    throw new InvalidOperationException(
                        "Refusing to start: RabbitMq:Password is still the default \"guest\" outside a Development " +
                        "environment. Set a real RabbitMQ credential via your deploy target's secrets manager.");
            }
    
            builder.PostgresDatabaseConfigure();
            builder.RegisterAuthen();
            builder.Services.RegisterDependency();
            builder.OtelConfigure();
            builder.RabbitMqConfigure();
            builder.SwaggerConfigure();

            builder.Services.AddHealthChecks()
                .AddCheck<PostgresHealthCheck>("postgres", tags: ["ready"])
                .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: ["ready"]);
        }

        public static void SwaggerConfigure(this WebApplicationBuilder builder)
        {
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "OrderService API", Version = "v1" });

                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                });
                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
                        },
                        Array.Empty<string>()
                    },
                });
            });
        }

        public static void PostgresDatabaseConfigure(this WebApplicationBuilder builder)
        {
            builder.Services.AddDbContext<OrderDbContext>(opts =>
                opts.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));
        }

        public static void OtelConfigure(this WebApplicationBuilder builder)
        {
            var otlpEndpoint = builder.Configuration["Otel:OtlpEndpoint"];
            builder.Services.AddOpenTelemetry()
                .ConfigureResource(r => r.AddService("order-service"))
                .WithTracing(tracing =>
                {
                    tracing.AddAspNetCoreInstrumentation().AddConsoleExporter();
                    if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                        tracing.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
                });
        }
        
        public static void RabbitMqConfigure(this WebApplicationBuilder builder)
        {
            var rabbitHost = builder.Configuration["RabbitMq:Host"] ?? "localhost";
            var rabbitPort = int.Parse(builder.Configuration["RabbitMq:Port"] ?? "5672");
            var rabbitUser = builder.Configuration["RabbitMq:Username"] ?? "guest";
            var rabbitPassword = builder.Configuration["RabbitMq:Password"] ?? "guest";

            Log.Information("Connecting to RabbitMQ at {Host}:{Port}", rabbitHost, rabbitPort);

            var rabbitConnection = RabbitMqConnector.ConnectWithRetry(rabbitHost, rabbitPort, rabbitUser, rabbitPassword);

            Log.Information("RabbitMQ connected, orders exchange declared");
            
            builder.Services.AddSingleton(rabbitConnection);
            builder.Services.AddHostedService<OutboxDispatcherService>();
            builder.Services.AddHostedService<OrderEventsConsumer>();

        }
    }
    
    
}
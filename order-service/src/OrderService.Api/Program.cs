using OrderService.Api.Configurations;
using OrderService.Infrastructure.Auth;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .Enrich.FromLogContext()
    .CreateLogger();
try
{

var builder = WebApplication.CreateBuilder(args);

builder.BasicConfigure();
builder.Host.UseSerilog();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();


if (!builder.Environment.IsDevelopment())
{
    var connectionString = builder.Configuration.GetConnectionString("Postgres") ?? "";
    var rabbitPasswordCfg = builder.Configuration["RabbitMq:Password"] ?? "";
    if (jwtOptions.SigningKey == JwtOptions.DevOnlySigningKey)
        throw new InvalidOperationException(
            "Refusing to start: Jwt:SigningKey is still the dev-only default outside a Development environment.");
    if (connectionString.Contains("Password=postgres", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException(
            "Refusing to start: ConnectionStrings:Postgres still uses the docker-compose dev password");
    if (rabbitPasswordCfg == "guest")
        throw new InvalidOperationException(
            "Refusing to start: RabbitMq:Password is still the default \"guest\" outside a Development");
}

var app = builder.Build();

app.AppConfigure();

app.MapGet("/", () => "Hello World!");

app.Run();

}
catch (Exception ex)
{
    Log.Fatal(ex, "order-service terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

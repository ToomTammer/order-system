using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using OrderService.Application;
using OrderService.Application.Auth;
using OrderService.Application.Orders;
using OrderService.Domain.IRepository;
using OrderService.Infrastructure.Auth;
using OrderService.Infrastructure.Persistence;

namespace OrderService.Api.Configurations
{
    public static class DependencyConfigure
    {
        public static void RegisterDependency(this IServiceCollection services)
        {
            services.AddScoped<IOrderRepository, PostgresOrderRepository>();
            services.AddScoped<IOutboxRepository, PostgresOutboxRepository>();
            services.AddScoped<IInboxRepository, PostgresInboxRepository>();
            services.AddScoped<IUserRepository, PostgresUserRepository>();
            services.AddScoped<IRefreshTokenRepository, PostgresRefreshTokenRepository>();
            services.AddScoped<IUnitOfWork, EfUnitOfWork>();
            services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();
            services.AddSingleton<ITokenGenerator, JwtTokenGenerator>();
            services.AddScoped<IAuthWorkflow, AuthWorkflow>();
            services.AddScoped<IOrderWorkflow, OrderWorkflow>();
        }
    }
    
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OrderService.Api.Endpoints;
using OrderService.Infrastructure.Auth;

namespace OrderService.Api.Configurations
{
    public static class ApplicationConfigure
    {
        public static void AppConfigure(this WebApplication app)
        {
            app.UseCorrelationId();
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseAuthentication();
            app.UseAuthorization();
            app.UseRateLimiter();

            app.MapHealthEndpoints();
            app.MapAuthEndpoints();
            app.MapOrderEndpoints();
    
        }
    }
    
}
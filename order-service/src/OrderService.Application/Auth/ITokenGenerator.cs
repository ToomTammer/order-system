using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OrderService.Domain.Entity;

namespace OrderService.Application.Auth;

public record AccessToken(string Value, DateTimeOffset ExpiresAt);

public interface ITokenGenerator
{
    AccessToken GenerateAccessToken(User user);
}
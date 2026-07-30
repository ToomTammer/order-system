using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using OrderService.Application.Auth;
using OrderService.Domain.Entity;

namespace OrderService.Infrastructure.Auth;

public class IdentityPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<User> _inner = new();
    public string HashPassword(string password) => _inner.HashPassword(null!, password);
    public bool VerifyPassword(string passwordHash, string password) =>
        _inner.VerifyHashedPassword(null!, passwordHash, password) != PasswordVerificationResult.Failed;
}
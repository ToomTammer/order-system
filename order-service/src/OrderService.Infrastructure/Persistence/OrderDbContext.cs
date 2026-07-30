using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrderService.Domain.Entity;

namespace OrderService.Infrastructure.Persistence;
public class OrderDbContext(DbContextOptions<OrderDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ProcessedInboxMessage> ProcessedInboxMessages => Set<ProcessedInboxMessage>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(b =>
        {
            b.ToTable("users");
            b.HasKey(u => u.Id);
            b.Property(u => u.Id).HasColumnName("id");
            b.Property(u => u.Username).HasColumnName("username").IsRequired();
            b.Property(u => u.PasswordHash).HasColumnName("password_hash").IsRequired();
            b.Property(u => u.FailedLoginAttempts).HasColumnName("failed_login_attempts");
            b.Property(u => u.LockedUntil).HasColumnName("locked_until");
            b.Property(u => u.CreatedAt).HasColumnName("created_at");
            b.HasIndex(u => u.Username).IsUnique();
        });

        modelBuilder.Entity<Order>(b =>
        {
            b.ToTable("orders");
            b.HasKey(o => o.Id);
            b.Property(o => o.Id).HasColumnName("id");
            b.Property(o => o.UserId).HasColumnName("user_id");
            b.Property(o => o.ProductId).HasColumnName("product_id").IsRequired();
            b.Property(o => o.Quantity).HasColumnName("quantity");
            b.Property(o => o.Status).HasColumnName("status").HasConversion<string>();
            b.Property(o => o.CreatedAt).HasColumnName("created_at");
            b.Property(o => o.UpdatedAt).HasColumnName("updated_at");
            b.HasIndex(o => o.UserId);
        });

        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("outbox_messages");
            b.HasKey(m => m.Id);
            b.Property(m => m.Id).HasColumnName("id");
            b.Property(m => m.AggregateId).HasColumnName("aggregate_id");
            b.Property(m => m.EventType).HasColumnName("event_type").IsRequired();
            b.Property(m => m.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
            b.Property(m => m.CorrelationId).HasColumnName("correlation_id");
            b.Property(m => m.CreatedAt).HasColumnName("created_at");
            b.Property(m => m.ProcessedAt).HasColumnName("processed_at");
            b.Property(m => m.Attempts).HasColumnName("attempts");
            b.HasIndex(m => m.ProcessedAt);
        });

        modelBuilder.Entity<ProcessedInboxMessage>(b =>
        {
            b.ToTable("processed_inbox_messages");
            b.HasKey(m => m.MessageId);
            b.Property(m => m.MessageId).HasColumnName("message_id");
            b.Property(m => m.ProcessedAt).HasColumnName("processed_at");
        });

        modelBuilder.Entity<RefreshToken>(b =>
        {
            b.ToTable("refresh_tokens");
            b.HasKey(t => t.Id);
            b.Property(t => t.Id).HasColumnName("id");
            b.Property(t => t.UserId).HasColumnName("user_id");
            b.Property(t => t.TokenHash).HasColumnName("token_hash").IsRequired();
            b.Property(t => t.ExpiresAt).HasColumnName("expires_at");
            b.Property(t => t.RevokedAt).HasColumnName("revoked_at");
            b.Property(t => t.CreatedAt).HasColumnName("created_at");
            b.HasIndex(t => t.TokenHash).IsUnique();
        });
    }
}
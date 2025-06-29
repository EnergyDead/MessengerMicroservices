using Microsoft.EntityFrameworkCore;
using NotificationService.Models;

namespace NotificationService.Data;

public class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options) { }

    public DbSet<MessageNotification> MessageNotifications { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MessageNotification>()
            .HasKey(mn => mn.Id);

        modelBuilder.Entity<MessageNotification>()
            .HasIndex(mn => new { mn.ChatId, mn.RecipientId, mn.IsRead });
    }
}
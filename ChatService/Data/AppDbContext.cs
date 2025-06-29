using ChatService.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatService.Data;

public class AppDbContext : DbContext
{
    public DbSet<Chat> Chats => Set<Chat>();
    public DbSet<UserChat> UserChats => Set<UserChat>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserChat>()
            .HasKey(uc => new { uc.ChatId, uc.UserId });
        
        modelBuilder.Entity<UserChat>()
            .HasOne(uc => uc.Chat)
            .WithMany(c => c.Participants)
            .HasForeignKey(uc => uc.ChatId);
    }
}

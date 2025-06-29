using MessageService.Models;
using Microsoft.EntityFrameworkCore;

namespace MessageService.Data;

public class AppDbContext : DbContext
{
    public DbSet<Message> Messages => Set<Message>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }
}
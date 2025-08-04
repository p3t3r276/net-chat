using Microsoft.EntityFrameworkCore;
using OurSpace.API.Models;

namespace OurSpace.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Message> Messages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Message>()
            .HasIndex(m => m.Timestamp);
    }
}
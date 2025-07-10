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
        // Configure the Message entity if needed (e.g., indexes, constraints)
        modelBuilder.Entity<Message>()
            .HasIndex(m => m.Timestamp); // Indexing for faster history retrieval
    }
}
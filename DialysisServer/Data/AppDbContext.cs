using Microsoft.EntityFrameworkCore;
using DialysisServer.Models;

namespace DialysisServer.Data;

public class AppDbContext : DbContext
{
    // Constructor for dependency injection
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // Define Data sets
    public DbSet<SensorData> SensorData { get; set; } = null!;

    // Apply all configurations from the assembly i.e. all classes that implement IEntityTypeConfiguration<T>
    // So everything in the Configurations folder
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
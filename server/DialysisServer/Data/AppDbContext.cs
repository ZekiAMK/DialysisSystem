using Microsoft.EntityFrameworkCore;
using DialysisServer.Models;

namespace DialysisServer.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<SensorData> SensorData { get; set; }
}
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DialysisServer.Models;

namespace DialysisServer.Data.Configurations;

public class SensorDataConfiguration : IEntityTypeConfiguration<SensorData>
{
    public void Configure(EntityTypeBuilder<SensorData> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Timestamp)
               .IsRequired()
               .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(e => e.Speed)
               .IsRequired();

        builder.Property(e => e.Cadence)
               .IsRequired();

        builder.HasIndex(e => e.Timestamp);
    }
}

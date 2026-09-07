using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class ReportHoursBreakdownConfiguration : IEntityTypeConfiguration<ReportHoursBreakdown>
{
    public void Configure(EntityTypeBuilder<ReportHoursBreakdown> builder)
    {
        builder.Property(h => h.TaskType).HasConversion<string>().HasMaxLength(30);
        builder.Property(h => h.Hours).HasColumnType("numeric(6,2)");

        builder.HasOne(h => h.Report)
            .WithMany(r => r.HoursBreakdown)
            .HasForeignKey(h => h.ReportId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

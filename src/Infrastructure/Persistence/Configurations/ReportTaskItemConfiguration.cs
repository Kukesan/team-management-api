using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class ReportTaskItemConfiguration : IEntityTypeConfiguration<ReportTaskItem>
{
    public void Configure(EntityTypeBuilder<ReportTaskItem> builder)
    {
        builder.Property(t => t.TaskName).IsRequired().HasMaxLength(300);
        builder.Property(t => t.Priority).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.TimePlannedHours).HasColumnType("numeric(6,2)");
        builder.Property(t => t.TimeSpentHours).HasColumnType("numeric(6,2)");
        builder.Property(t => t.Output).HasMaxLength(2000);

        builder.HasOne(t => t.Report)
            .WithMany(r => r.TaskItems)
            .HasForeignKey(t => t.ReportId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

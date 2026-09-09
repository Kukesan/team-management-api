using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class ReportNextWeekTaskConfiguration : IEntityTypeConfiguration<ReportNextWeekTask>
{
    public void Configure(EntityTypeBuilder<ReportNextWeekTask> builder)
    {
        builder.Property(t => t.Description).IsRequired().HasMaxLength(2000);

        builder.HasOne(t => t.Report)
            .WithMany(r => r.NextWeekTasks)
            .HasForeignKey(t => t.ReportId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

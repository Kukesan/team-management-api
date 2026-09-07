using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class ReportAchievementConfiguration : IEntityTypeConfiguration<ReportAchievement>
{
    public void Configure(EntityTypeBuilder<ReportAchievement> builder)
    {
        builder.Property(a => a.Description).IsRequired().HasMaxLength(2000);

        builder.HasOne(a => a.Report)
            .WithMany(r => r.Achievements)
            .HasForeignKey(a => a.ReportId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

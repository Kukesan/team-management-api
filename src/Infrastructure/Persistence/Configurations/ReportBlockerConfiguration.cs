using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class ReportBlockerConfiguration : IEntityTypeConfiguration<ReportBlocker>
{
    public void Configure(EntityTypeBuilder<ReportBlocker> builder)
    {
        builder.Property(b => b.Description).IsRequired().HasMaxLength(2000);

        builder.HasOne(b => b.Report)
            .WithMany(r => r.Blockers)
            .HasForeignKey(b => b.ReportId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

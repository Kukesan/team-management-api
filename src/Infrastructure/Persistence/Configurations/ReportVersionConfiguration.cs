using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class ReportVersionConfiguration : IEntityTypeConfiguration<ReportVersion>
{
    public void Configure(EntityTypeBuilder<ReportVersion> builder)
    {
        builder.Property(v => v.ContentSnapshot).HasColumnType("jsonb").IsRequired();

        builder.HasIndex(v => new { v.ReportId, v.VersionNumber }).IsUnique();

        builder.HasOne(v => v.Report)
            .WithMany(r => r.Versions)
            .HasForeignKey(v => v.ReportId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

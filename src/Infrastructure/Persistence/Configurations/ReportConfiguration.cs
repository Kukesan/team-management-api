using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.Property(r => r.WeekStartDate).HasColumnType("date");
        builder.Property(r => r.WeekEndDate).HasColumnType("date");
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(30);

        // One report per team member, per project, per week.
        builder.HasIndex(r => new { r.UserId, r.ProjectId, r.WeekStartDate }).IsUnique();
        builder.HasIndex(r => r.Status);

        builder.HasOne(r => r.User)
            .WithMany(u => u.Reports)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Project)
            .WithMany(p => p.Reports)
            .HasForeignKey(r => r.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

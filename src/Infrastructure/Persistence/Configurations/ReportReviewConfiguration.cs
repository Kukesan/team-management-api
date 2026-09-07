using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class ReportReviewConfiguration : IEntityTypeConfiguration<ReportReview>
{
    public void Configure(EntityTypeBuilder<ReportReview> builder)
    {
        builder.Property(r => r.Action).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Comment).HasMaxLength(2000);

        builder.HasOne(r => r.Report)
            .WithMany(rp => rp.Reviews)
            .HasForeignKey(r => r.ReportId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.ReportVersion)
            .WithMany(v => v.Reviews)
            .HasForeignKey(r => r.ReportVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.Reviewer)
            .WithMany(u => u.ReviewsGiven)
            .HasForeignKey(r => r.ReviewerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

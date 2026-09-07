using Application.Features.Reports.Dtos;
using FluentValidation;

namespace Application.Features.Reports.Validators;

public class CreateReportRequestValidator : AbstractValidator<CreateReportRequest>
{
    public CreateReportRequestValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();

        RuleFor(x => x.WeekEndDate)
            .GreaterThan(x => x.WeekStartDate)
            .WithMessage("WeekEndDate must be after WeekStartDate.");

        // Assumption: a "week" report covers exactly 7 days (Mon-Sun style range).
        RuleFor(x => x)
            .Must(x => x.WeekEndDate == x.WeekStartDate.AddDays(6))
            .WithMessage("WeekStartDate/WeekEndDate must span exactly 7 days.")
            .WithName("WeekEndDate");
    }
}

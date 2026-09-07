using Application.Features.Reports.Dtos;
using FluentValidation;

namespace Application.Features.Reports.Validators;

public class UpdateReportRequestValidator : AbstractValidator<UpdateReportRequest>
{
    public UpdateReportRequestValidator()
    {
        RuleFor(x => x.TaskItems).NotEmpty().WithMessage("At least one task item is required.");

        RuleForEach(x => x.TaskItems).SetValidator(new TaskItemRequestValidator());
        RuleForEach(x => x.Blockers).SetValidator(new BlockerRequestValidator());
        RuleForEach(x => x.Achievements).SetValidator(new AchievementRequestValidator());
        RuleForEach(x => x.HoursBreakdown).SetValidator(new HoursBreakdownRequestValidator());
    }
}

public class TaskItemRequestValidator : AbstractValidator<TaskItemRequest>
{
    public TaskItemRequestValidator()
    {
        RuleFor(x => x.TaskName).NotEmpty().MaximumLength(300);
        RuleFor(x => x.PlannedPercent).InclusiveBetween(0, 100);
        RuleFor(x => x.ActualPercent).InclusiveBetween(0, 100);
        RuleFor(x => x.TimePlannedHours).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TimeSpentHours).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Priority).IsInEnum();
        RuleFor(x => x.Status).IsInEnum();
    }
}

public class BlockerRequestValidator : AbstractValidator<BlockerRequest>
{
    public BlockerRequestValidator()
    {
        RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);
    }
}

public class AchievementRequestValidator : AbstractValidator<AchievementRequest>
{
    public AchievementRequestValidator()
    {
        RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);
    }
}

public class HoursBreakdownRequestValidator : AbstractValidator<HoursBreakdownRequest>
{
    public HoursBreakdownRequestValidator()
    {
        RuleFor(x => x.Hours).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TaskType).IsInEnum();
    }
}

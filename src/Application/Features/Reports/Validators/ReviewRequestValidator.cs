using Application.Features.Reports.Dtos;
using FluentValidation;

namespace Application.Features.Reports.Validators;

public class ReviewRequestValidator : AbstractValidator<ReviewRequest>
{
    private static readonly string[] AllowedActions = { "Approved", "RequestedChanges" };

    public ReviewRequestValidator()
    {
        RuleFor(x => x.Action)
            .NotEmpty()
            .Must(a => AllowedActions.Contains(a))
            .WithMessage($"Action must be one of: {string.Join(", ", AllowedActions)}.");

        RuleFor(x => x.Comment)
            .NotEmpty()
            .WithMessage("Comment is required when requesting changes.")
            .When(x => x.Action == "RequestedChanges");
    }
}

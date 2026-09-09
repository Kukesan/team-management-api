using Application.Features.Users.Dtos;
using Domain.Constants;
using FluentValidation;

namespace Application.Features.Users.Validators;

public class InviteUserRequestValidator : AbstractValidator<InviteUserRequest>
{
    public InviteUserRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Role)
            .NotEmpty()
            .Must(r => Roles.All.Contains(r))
            .WithMessage($"Role must be one of: {string.Join(", ", Roles.All)}.");
    }
}

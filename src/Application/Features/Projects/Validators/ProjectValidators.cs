using Application.Features.Projects.Dtos;
using FluentValidation;

namespace Application.Features.Projects.Validators;

public class CreateProjectRequestValidator : AbstractValidator<CreateProjectRequest>
{
    public CreateProjectRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

public class UpdateProjectRequestValidator : AbstractValidator<UpdateProjectRequest>
{
    public UpdateProjectRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

public class AssignUserRequestValidator : AbstractValidator<AssignUserRequest>
{
    public AssignUserRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}

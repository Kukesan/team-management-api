using Application.Features.Ai.Dtos;
using FluentValidation;

namespace Application.Features.Ai.Validators;

/// <summary>
/// Neither ChatRequestDto nor HelpRequestDto had a validator before -- request bodies for
/// the AI endpoints reached AiService (and from there, the FastAPI service and the OpenAI
/// API) completely unchecked. Message length matches FastAPI's own Field(max_length=4000)
/// (schemas/chat.py, schemas/help.py) so a request that would be rejected there is instead
/// rejected here, closer to the user, with a normal 400 instead of an opaque 503. History
/// is capped so a caller can't balloon token cost/latency by attaching an unbounded
/// conversation.
/// </summary>
public class ChatRequestDtoValidator : AbstractValidator<ChatRequestDto>
{
    private const int MaxMessageLength = 4000;
    private const int MaxHistoryMessages = 40;

    public ChatRequestDtoValidator()
    {
        RuleFor(x => x.Message).NotEmpty().MaximumLength(MaxMessageLength);
        RuleFor(x => x.History)
            .Must(h => h.Count <= MaxHistoryMessages)
            .WithMessage($"History is limited to {MaxHistoryMessages} messages.");
        RuleForEach(x => x.History).SetValidator(new ChatMessageDtoValidator());
    }
}

public class ChatMessageDtoValidator : AbstractValidator<ChatMessageDto>
{
    public ChatMessageDtoValidator()
    {
        RuleFor(x => x.Role).Must(r => r is "user" or "assistant").WithMessage("Role must be 'user' or 'assistant'.");
        RuleFor(x => x.Content).NotEmpty().MaximumLength(4000);
    }
}

public class HelpRequestDtoValidator : AbstractValidator<HelpRequestDto>
{
    private const int MaxMessageLength = 4000;
    private const int MaxHistoryMessages = 40;

    public HelpRequestDtoValidator()
    {
        RuleFor(x => x.Message).NotEmpty().MaximumLength(MaxMessageLength);
        RuleFor(x => x.History)
            .Must(h => h.Count <= MaxHistoryMessages)
            .WithMessage($"History is limited to {MaxHistoryMessages} messages.");
        RuleForEach(x => x.History).SetValidator(new HelpMessageDtoValidator());
    }
}

public class HelpMessageDtoValidator : AbstractValidator<HelpMessageDto>
{
    public HelpMessageDtoValidator()
    {
        RuleFor(x => x.Role).Must(r => r is "user" or "assistant").WithMessage("Role must be 'user' or 'assistant'.");
        RuleFor(x => x.Content).NotEmpty().MaximumLength(4000);
    }
}

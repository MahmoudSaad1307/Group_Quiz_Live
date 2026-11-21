using FluentValidation;

namespace QuizLive.Application.Auth;

public sealed record LoginRequest(string Email, string Password);

public sealed record AdminProfileDto(Guid Id, string Email, string DisplayName);

public sealed record LoginResult(bool Succeeded, string? Token, AdminProfileDto? Profile);

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(6)
            .MaximumLength(256);
    }
}

using BusinessAnalytics.Application.DTOs;
using BusinessAnalytics.Domain.Entities;
using FluentValidation;

namespace BusinessAnalytics.Application.Validators;

public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().Length(3, 50).Matches("^[a-zA-Z0-9_.-]+$")
            .WithMessage("Username may only contain letters, digits, '_', '.', '-'.");
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Department).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Role).Must(r => UserRoles.All.Contains(r))
            .WithMessage($"Role must be one of: {string.Join(", ", UserRoles.All)}.");
    }
}

public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().Length(3, 50).Matches("^[a-zA-Z0-9_.-]+$");
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Department).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Role).Must(r => UserRoles.All.Contains(r))
            .WithMessage($"Role must be one of: {string.Join(", ", UserRoles.All)}.");
    }
}

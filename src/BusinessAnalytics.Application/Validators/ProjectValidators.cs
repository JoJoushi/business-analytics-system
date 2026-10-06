using BusinessAnalytics.Application.DTOs;
using BusinessAnalytics.Domain.Entities;
using FluentValidation;

namespace BusinessAnalytics.Application.Validators;

public class CreateProjectRequestValidator : AbstractValidator<CreateProjectRequest>
{
    public CreateProjectRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(30).Matches("^[A-Z0-9-]+$")
            .WithMessage("Code must be upper-case letters, digits or '-'.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Department).NotEmpty().MaximumLength(100);
        RuleFor(x => x.OwnerId).GreaterThan(0).When(x => x.OwnerId.HasValue);
        RuleFor(x => x.Status).Must(s => ProjectStatus.All.Contains(s))
            .WithMessage($"Status must be one of: {string.Join(", ", ProjectStatus.All)}.");
        RuleFor(x => x.Budget).GreaterThanOrEqualTo(0);
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate)
            .When(x => x.StartDate.HasValue && x.EndDate.HasValue)
            .WithMessage("EndDate must be on or after StartDate.");
    }
}

public class UpdateProjectRequestValidator : AbstractValidator<UpdateProjectRequest>
{
    public UpdateProjectRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(30).Matches("^[A-Z0-9-]+$");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Department).NotEmpty().MaximumLength(100);
        RuleFor(x => x.OwnerId).GreaterThan(0).When(x => x.OwnerId.HasValue);
        RuleFor(x => x.Status).Must(s => ProjectStatus.All.Contains(s))
            .WithMessage($"Status must be one of: {string.Join(", ", ProjectStatus.All)}.");
        RuleFor(x => x.Budget).GreaterThanOrEqualTo(0);
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate)
            .When(x => x.StartDate.HasValue && x.EndDate.HasValue)
            .WithMessage("EndDate must be on or after StartDate.");
    }
}

public class CreateProjectWithTasksRequestValidator : AbstractValidator<CreateProjectWithTasksRequest>
{
    public CreateProjectWithTasksRequestValidator()
    {
        RuleFor(x => x.Project).NotNull().SetValidator(new CreateProjectRequestValidator());
        RuleFor(x => x.Tasks).NotNull().Must(t => t.Count <= 100).WithMessage("At most 100 tasks per request.");
        RuleForEach(x => x.Tasks).ChildRules(t =>
        {
            t.RuleFor(i => i.Title).NotEmpty().MaximumLength(200);
            t.RuleFor(i => i.Description).MaximumLength(2000);
            t.RuleFor(i => i.Priority).InclusiveBetween((short)1, (short)5);
            t.RuleFor(i => i.AssigneeId).GreaterThan(0).When(i => i.AssigneeId.HasValue);
        });
    }
}

using BusinessAnalytics.Application.DTOs;
using BusinessAnalytics.Domain.Entities;
using FluentValidation;

namespace BusinessAnalytics.Application.Validators;

public class CreateTaskRequestValidator : AbstractValidator<CreateTaskRequest>
{
    public CreateTaskRequestValidator()
    {
        RuleFor(x => x.ProjectId).GreaterThan(0);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.AssigneeId).GreaterThan(0).When(x => x.AssigneeId.HasValue);
        RuleFor(x => x.Status).Must(s => TaskItemStatus.All.Contains(s))
            .WithMessage($"Status must be one of: {string.Join(", ", TaskItemStatus.All)}.");
        RuleFor(x => x.Priority).InclusiveBetween((short)1, (short)5);
        RuleFor(x => x.ProgressPercent).InclusiveBetween((short)0, (short)100);
    }
}

public class UpdateTaskRequestValidator : AbstractValidator<UpdateTaskRequest>
{
    public UpdateTaskRequestValidator()
    {
        RuleFor(x => x.ProjectId).GreaterThan(0);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.AssigneeId).GreaterThan(0).When(x => x.AssigneeId.HasValue);
        RuleFor(x => x.Status).Must(s => TaskItemStatus.All.Contains(s))
            .WithMessage($"Status must be one of: {string.Join(", ", TaskItemStatus.All)}.");
        RuleFor(x => x.Priority).InclusiveBetween((short)1, (short)5);
        RuleFor(x => x.ProgressPercent).InclusiveBetween((short)0, (short)100);
    }
}

public class UpdateTaskStatusRequestValidator : AbstractValidator<UpdateTaskStatusRequest>
{
    public UpdateTaskStatusRequestValidator()
    {
        RuleFor(x => x.Status).Must(s => TaskItemStatus.All.Contains(s))
            .WithMessage($"Status must be one of: {string.Join(", ", TaskItemStatus.All)}.");
        RuleFor(x => x.ProgressPercent!.Value).InclusiveBetween((short)0, (short)100).When(x => x.ProgressPercent.HasValue);
    }
}

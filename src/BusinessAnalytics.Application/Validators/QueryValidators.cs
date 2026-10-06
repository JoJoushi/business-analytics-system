using BusinessAnalytics.Application.Common.Models;
using FluentValidation;

namespace BusinessAnalytics.Application.Validators;

public class PagedQueryValidator : AbstractValidator<PagedQuery>
{
    public PagedQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, PagedQuery.MaxPageSize);
        RuleFor(x => x.SortDir).Must(d => d is null || d.Equals("asc", StringComparison.OrdinalIgnoreCase) || d.Equals("desc", StringComparison.OrdinalIgnoreCase))
            .WithMessage("SortDir must be 'asc' or 'desc'.");
        RuleFor(x => x.Search).MaximumLength(100);
    }
}

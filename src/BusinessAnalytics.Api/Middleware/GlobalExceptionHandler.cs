using BusinessAnalytics.Application.Common.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace BusinessAnalytics.Api.Middleware;

/// <summary>Translates exceptions into RFC 7807 ProblemDetails responses.</summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var problem = exception switch
        {
            ValidationException ve => ValidationProblem(ve),
            NotFoundException => Problem(StatusCodes.Status404NotFound, "Resource not found", exception.Message),
            ConflictException => Problem(StatusCodes.Status409Conflict, "Conflict", exception.Message),
            BusinessRuleException => Problem(StatusCodes.Status422UnprocessableEntity, "Business rule violated", exception.Message),
            PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg =>
                Problem(StatusCodes.Status409Conflict, "Duplicate value", $"A record with the same value already exists ({pg.ConstraintName})."),
            PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } pg =>
                Problem(StatusCodes.Status409Conflict, "Related record conflict", $"Operation violates a relationship ({pg.ConstraintName})."),
            PostgresException { SqlState: PostgresErrorCodes.CheckViolation } pg =>
                Problem(StatusCodes.Status400BadRequest, "Invalid value", $"A value failed a database check ({pg.ConstraintName})."),
            NpgsqlException { IsTransient: true } or TimeoutException =>
                Problem(StatusCodes.Status503ServiceUnavailable, "Database unavailable", "The database is temporarily unavailable. Please retry."),
            BadHttpRequestException bad => Problem(bad.StatusCode, "Bad request", bad.Message),
            OperationCanceledException when context.RequestAborted.IsCancellationRequested =>
                Problem(499, "Client closed request", null),
            _ => Problem(StatusCodes.Status500InternalServerError, "Internal server error", "An unexpected error occurred.")
        };

        if (problem.Status >= 500)
            logger.LogError(exception, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
        else
            logger.LogWarning("Request failed with {Status}: {Message}", problem.Status, exception.Message);

        context.Response.StatusCode = problem.Status!.Value;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problem,
            Exception = exception
        });
    }

    private static ProblemDetails Problem(int status, string title, string? detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail
    };

    private static ValidationProblemDetails ValidationProblem(ValidationException ex)
    {
        var errors = ex.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());

        return new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred."
        };
    }
}

namespace BusinessAnalytics.Application.Common.Exceptions;

/// <summary>Base type for expected, client-facing errors. Mapped to ProblemDetails by the API.</summary>
public abstract class AppException(string message) : Exception(message);

public sealed class NotFoundException(string entity, object key)
    : AppException($"{entity} with id '{key}' was not found.");

public sealed class ConflictException(string message) : AppException(message);

/// <summary>A request that is well-formed but violates a business rule (HTTP 422).</summary>
public sealed class BusinessRuleException(string message) : AppException(message);

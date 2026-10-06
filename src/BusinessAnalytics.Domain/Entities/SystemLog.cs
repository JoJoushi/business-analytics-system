namespace BusinessAnalytics.Domain.Entities;

public class SystemLog
{
    public long Id { get; set; }
    public string Level { get; set; } = LogLevels.Info;
    public string Action { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public long? EntityId { get; set; }
    public long? UserId { get; set; }
    public string Message { get; set; } = string.Empty;
    /// <summary>Raw JSON stored in a jsonb column.</summary>
    public string? Metadata { get; set; }
    public DateTime CreatedAt { get; set; }

    public static SystemLog Create(string action, string entity, long? entityId, string message, string? metadata = null, string level = LogLevels.Info) => new()
    {
        Action = action,
        Entity = entity,
        EntityId = entityId,
        Message = message,
        Metadata = metadata,
        Level = level
    };
}

public static class LogLevels
{
    public const string Info = "info";
    public const string Warning = "warning";
    public const string Error = "error";
}

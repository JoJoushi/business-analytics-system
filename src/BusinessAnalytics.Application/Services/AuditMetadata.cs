using System.Text.Json;

namespace BusinessAnalytics.Application.Services;

internal static class AuditMetadata
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Of(object value) => JsonSerializer.Serialize(value, Options);
}

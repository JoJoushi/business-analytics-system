using System.Globalization;
using System.Reflection;
using System.Text;

namespace BusinessAnalytics.Application.Export;

/// <summary>Minimal RFC 4180 CSV writer for flat record types.</summary>
public static class CsvWriter
{
    public static byte[] Write<T>(IEnumerable<T> rows)
    {
        var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var sb = new StringBuilder();

        sb.AppendLine(string.Join(',', props.Select(p => Escape(ToSnakeCase(p.Name)))));
        foreach (var row in rows)
            sb.AppendLine(string.Join(',', props.Select(p => Escape(Format(p.GetValue(row))))));

        // UTF-8 with BOM so Excel renders Thai text correctly
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    private static string Format(object? value) => value switch
    {
        null => string.Empty,
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    internal static string Escape(string value)
    {
        // Neutralise spreadsheet formula injection
        if (value.Length > 0 && "=+-@".Contains(value[0]) && !decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
            value = "'" + value;

        return value.IndexOfAny([',', '"', '\n', '\r']) >= 0
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }

    private static string ToSnakeCase(string name) =>
        string.Concat(name.Select((c, i) => i > 0 && char.IsUpper(c) ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
}

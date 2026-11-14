using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VersionFlow.Extensions.Results;

/// <summary>Canonical UTC timestamp cursors with seven fractional second digits.</summary>
public static class CursorTime
{
    public const string Format = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";

    public static string FormatUtc(DateTimeOffset value) => value.UtcDateTime.ToString(Format, CultureInfo.InvariantCulture);

    public static string FormatUtc(DateTime value)
    {
        if (value.Kind == DateTimeKind.Unspecified)
            throw new ArgumentException("A timestamp cursor requires an explicit time zone.", nameof(value));
        return value.ToUniversalTime().ToString(Format, CultureInfo.InvariantCulture);
    }

    public static bool TryParse(string? value, out DateTimeOffset utc) => DateTimeOffset.TryParseExact(
        value, Format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out utc);
}

/// <summary>Opt-in converter for timestamp cursor properties, not a global date formatting policy.</summary>
public sealed class CursorTimeJsonConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String || !CursorTime.TryParse(reader.GetString(), out var value))
            throw new JsonException("Expected a canonical UTC timestamp cursor.");
        return value;
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        => writer.WriteStringValue(CursorTime.FormatUtc(value));
}

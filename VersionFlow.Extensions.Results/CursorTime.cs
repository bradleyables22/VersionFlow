using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VersionFlow.Extensions.Results;

/// <summary>Canonical UTC timestamp cursors with seven fractional second digits.</summary>
public static class CursorTime
{
    /// <summary>The invariant UTC cursor format with seven fractional-second digits and a literal Z suffix.</summary>
    public const string Format = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";

    /// <summary>Converts an offset timestamp to the canonical UTC cursor string.</summary>
    /// <param name="value">The timestamp to format.</param>
    /// <returns>The canonical UTC timestamp with seven fractional-second digits and a Z suffix.</returns>
    public static string FormatUtc(DateTimeOffset value) => value.UtcDateTime.ToString(Format, CultureInfo.InvariantCulture);

    /// <summary>Formats a local or UTC timestamp as a UTC cursor; rejects DateTimeKind.Unspecified.</summary>
    /// <param name="value">The timestamp to format.</param>
    /// <returns>The canonical UTC timestamp with seven fractional-second digits and a Z suffix.</returns>
    public static string FormatUtc(DateTime value)
    {
        if (value.Kind == DateTimeKind.Unspecified)
            throw new ArgumentException("A timestamp cursor requires an explicit time zone.", nameof(value));
        return value.ToUniversalTime().ToString(Format, CultureInfo.InvariantCulture);
    }

    /// <summary>Accepts only canonical UTC cursor strings; returns false for null, alternate offsets, or different precision.</summary>
    /// <param name="value">The canonical UTC cursor string to parse, or null.</param>
    /// <param name="utc">The parsed UTC timestamp on success; default on failure.</param>
    /// <returns>True only for the canonical UTC format; false for invalid or absent input.</returns>
    public static bool TryParse(string? value, out DateTimeOffset utc) => DateTimeOffset.TryParseExact(
        value, Format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out utc);
}

/// <summary>Opt-in converter for timestamp cursor properties, not a global date formatting policy.</summary>
public sealed class CursorTimeJsonConverter : JsonConverter<DateTimeOffset>
{
    /// <summary>Reads a canonical timestamp cursor string; throws JsonException for other token types or formats.</summary>
    /// <param name="reader">The JSON reader positioned at the timestamp cursor value.</param>
    /// <param name="typeToConvert">The target timestamp type requested by the serializer.</param>
    /// <param name="options">The serializer options supplied by System.Text.Json.</param>
    /// <returns>The parsed UTC timestamp.</returns>
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String || !CursorTime.TryParse(reader.GetString(), out var value))
            throw new JsonException("Expected a canonical UTC timestamp cursor.");
        return value;
    }

    /// <summary>Writes a timestamp as a canonical UTC cursor JSON string.</summary>
    /// <param name="writer">The JSON writer receiving the timestamp cursor string.</param>
    /// <param name="value">The timestamp to format.</param>
    /// <param name="options">The serializer options supplied by System.Text.Json.</param>
    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        => writer.WriteStringValue(CursorTime.FormatUtc(value));
}

using System.Text.Json;
using System.Text.Json.Serialization;
using FreshdeskCLI.Models;

namespace FreshdeskCLI;

[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    Converters = [typeof(DateTimeOffsetConverter)])]
[JsonSerializable(typeof(Ticket))]
[JsonSerializable(typeof(Ticket[]))]
[JsonSerializable(typeof(Conversation))]
[JsonSerializable(typeof(Conversation[]))]
[JsonSerializable(typeof(Attachment))]
[JsonSerializable(typeof(Attachment[]))]
[JsonSerializable(typeof(ConfigFile))]
[JsonSerializable(typeof(Dictionary<string, object>))]
internal partial class FreshdeskJsonContext : JsonSerializerContext
{
}

public sealed class DateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return DateTimeOffset.Parse(reader.GetString()!);
    }
    
    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("O"));
    }
}

public sealed class NullableDateTimeOffsetConverter : JsonConverter<DateTimeOffset?>
{
    public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString();
        return string.IsNullOrEmpty(str) ? null : DateTimeOffset.Parse(str);
    }
    
    public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
            writer.WriteStringValue(value.Value.ToString("O"));
        else
            writer.WriteNullValue();
    }
}
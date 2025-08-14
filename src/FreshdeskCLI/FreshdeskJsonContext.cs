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
[JsonSerializable(typeof(FreshdeskConfig))]
[JsonSerializable(typeof(Dictionary<string, FreshdeskConfig>))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(TicketWithConversations))]
[JsonSerializable(typeof(TicketWithConversations[]))]
[JsonSerializable(typeof(List<TicketWithConversations>))]
public partial class FreshdeskJsonContext : JsonSerializerContext
{
}

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    Converters = [typeof(DateTimeOffsetConverter)])]
[JsonSerializable(typeof(Ticket))]
[JsonSerializable(typeof(Ticket[]))]
[JsonSerializable(typeof(Conversation))]
[JsonSerializable(typeof(Conversation[]))]
[JsonSerializable(typeof(Attachment))]
[JsonSerializable(typeof(Attachment[]))]
[JsonSerializable(typeof(TicketWithConversations))]
[JsonSerializable(typeof(TicketWithConversations[]))]
[JsonSerializable(typeof(List<TicketWithConversations>))]
public partial class FreshdeskJsonIndentedContext : JsonSerializerContext
{
}

public sealed class DateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return DateTimeOffset.Parse(reader.GetString()!, System.Globalization.CultureInfo.InvariantCulture);
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
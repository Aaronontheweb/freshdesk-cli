using System.Text.Json.Serialization;

namespace FreshdeskCLI.Models;

public sealed class TicketWithConversations
{
    [JsonPropertyName("ticket")]
    public required Ticket Ticket { get; init; }
    
    [JsonPropertyName("conversations")]
    public required Conversation[] Conversations { get; init; }
}
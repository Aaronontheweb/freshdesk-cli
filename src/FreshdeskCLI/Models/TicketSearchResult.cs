using System.Text.Json.Serialization;

namespace FreshdeskCLI.Models;

public class TicketSearchResult
{
    [JsonPropertyName("results")]
    public Ticket[] Results { get; set; } = [];

    [JsonPropertyName("total")]
    public int Total { get; set; }
}
using System.Text.Json.Serialization;

namespace FreshdeskCLI.Models;

public sealed class ContactSearchResult
{
    [JsonPropertyName("results")]
    public Contact[] Results { get; set; } = [];

    [JsonPropertyName("total")]
    public int Total { get; set; }
}

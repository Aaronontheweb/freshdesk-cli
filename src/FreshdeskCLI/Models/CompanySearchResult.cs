using System.Text.Json.Serialization;

namespace FreshdeskCLI.Models;

public sealed class CompanySearchResult
{
    [JsonPropertyName("results")]
    public Company[] Results { get; set; } = [];

    [JsonPropertyName("total")]
    public int Total { get; set; }
}

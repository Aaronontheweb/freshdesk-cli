using System.Text.Json.Serialization;

namespace FreshdeskCLI.Models;

public sealed class Company
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string[]? Domains { get; set; }
    public string? HealthScore { get; set; }
    public string? Industry { get; set; }
    public string? Note { get; set; }
    public Dictionary<string, object>? CustomFields { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

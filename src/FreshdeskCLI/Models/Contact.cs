using System.Text.Json.Serialization;

namespace FreshdeskCLI.Models;

public sealed class Contact
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Mobile { get; set; }
    public string? JobTitle { get; set; }
    public string? Address { get; set; }
    public string? Description { get; set; }
    public long? CompanyId { get; set; }
    public bool Active { get; set; }
    public string Language { get; set; } = "en";
    public string? TimeZone { get; set; }
    public bool? ViewAllTickets { get; set; }
    public CompanyAssociation[]? OtherCompanies { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
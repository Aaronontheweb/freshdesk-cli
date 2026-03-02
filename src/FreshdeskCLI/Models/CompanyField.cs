namespace FreshdeskCLI.Models;

public sealed class CompanyField
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string FieldType { get; set; } = string.Empty;
    public bool RequiredForAgents { get; set; }
    public bool RequiredForCustomers { get; set; }
    public string[]? Choices { get; set; }
}

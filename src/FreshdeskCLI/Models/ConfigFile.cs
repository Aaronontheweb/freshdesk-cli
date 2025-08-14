namespace FreshdeskCLI.Models;

public sealed class ConfigFile
{
    public string Domain { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string? DefaultDownloadPath { get; set; }
    public int? MaxConcurrentDownloads { get; set; }
    public bool? AutoRetry { get; set; }
    public int? RetryCount { get; set; }
    public string? OutputFormat { get; set; }

    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(Domain) &&
               !string.IsNullOrWhiteSpace(ApiKey) &&
               Domain.Contains('.') &&
               ApiKey.Length > 10;
    }

    public string GetBaseUrl()
    {
        var domain = Domain.StartsWith("https://") ? Domain : $"https://{Domain}";
        return domain.TrimEnd('/');
    }
}
using System.Text.Json.Serialization;

namespace FreshdeskCLI.Models;

public sealed class ConfigFile
{
    public string? DefaultProfile { get; set; }
    public Dictionary<string, FreshdeskConfig> Profiles { get; set; } = new();
}

public sealed class FreshdeskConfig
{
    public string Domain { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string? DefaultDownloadPath { get; set; }
    public int? MaxConcurrentDownloads { get; set; }
    public bool? AutoRetry { get; set; }
    public int? RetryCount { get; set; }
    public string? OutputFormat { get; set; }
    public string? ProfileName { get; set; }

    [JsonIgnore]
    public bool IsValid => !string.IsNullOrWhiteSpace(Domain) &&
                           !string.IsNullOrWhiteSpace(ApiKey) &&
                           ApiKey.Length > 10;

    [JsonIgnore]
    public string BaseUrl
    {
        get
        {
            if (Domain.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return Domain.TrimEnd('/');
            if (Domain.Contains('.', StringComparison.Ordinal))
                return $"https://{Domain}";
            return $"https://{Domain}.freshdesk.com";
        }
    }

    [JsonIgnore]
    public string ApiV2Url => $"{BaseUrl}/api/v2";
}
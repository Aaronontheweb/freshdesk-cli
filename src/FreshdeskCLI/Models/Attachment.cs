using System.Text.Json.Serialization;

namespace FreshdeskCLI.Models;

public sealed class Attachment
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string AttachmentUrl { get; set; } = string.Empty;
    
    [JsonIgnore]
    public string? LocalPath { get; set; }
    
    [JsonIgnore]
    public bool IsDownloaded { get; set; }
    
    public string FormattedSize => Size switch
    {
        < 1024 => $"{Size} B",
        < 1024 * 1024 => $"{Size / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{Size / (1024.0 * 1024.0):F1} MB",
        _ => $"{Size / (1024.0 * 1024.0 * 1024.0):F1} GB"
    };
}
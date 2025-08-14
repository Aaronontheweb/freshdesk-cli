# Models Implementation Guide

## Overview
Data models for the Freshdesk CLI with full AOT support using System.Text.Json source generators.

## AOT Serialization Context

### FreshdeskJsonContext.cs
```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FreshdeskCLI;

[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    Converters = [typeof(DateTimeOffsetConverter)])]
[JsonSerializable(typeof(Ticket))]
[JsonSerializable(typeof(Ticket[]))]
[JsonSerializable(typeof(TicketListResponse))]
[JsonSerializable(typeof(TicketDetailResponse))]
[JsonSerializable(typeof(Conversation))]
[JsonSerializable(typeof(Conversation[]))]
[JsonSerializable(typeof(Attachment))]
[JsonSerializable(typeof(Attachment[]))]
[JsonSerializable(typeof(ConfigFile))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(RateLimitInfo))]
[JsonSerializable(typeof(SearchResponse))]
[JsonSerializable(typeof(UpdateTicketRequest))]
[JsonSerializable(typeof(ReplyRequest))]
internal partial class FreshdeskJsonContext : JsonSerializerContext
{
}
```

## Core Models

### Models/Ticket.cs
```csharp
namespace FreshdeskCLI.Models;

public sealed class Ticket
{
    public long Id { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DescriptionText { get; set; } = string.Empty;
    public int Status { get; set; }
    public int Priority { get; set; }
    public int Source { get; set; }
    public long? RequesterId { get; set; }
    public long? ResponderId { get; set; }
    public long? GroupId { get; set; }
    public string[] Tags { get; set; } = [];
    public string[] CcEmails { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DueBy { get; set; }
    public DateTimeOffset? FrDueBy { get; set; }
    public bool IsEscalated { get; set; }
    public bool Deleted { get; set; }
    public bool Spam { get; set; }
    public Attachment[] Attachments { get; set; } = [];
    
    // Computed properties for display
    public string StatusText => Status switch
    {
        2 => "Open",
        3 => "Pending",
        4 => "Resolved",
        5 => "Closed",
        6 => "Waiting on Customer",
        7 => "Waiting on Third Party",
        _ => $"Unknown ({Status})"
    };
    
    public string PriorityText => Priority switch
    {
        1 => "Low",
        2 => "Medium",
        3 => "High",
        4 => "Urgent",
        _ => $"Unknown ({Priority})"
    };
}
```

### Models/Attachment.cs
```csharp
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
    
    // For tracking downloads
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
```

### Models/Conversation.cs
```csharp
namespace FreshdeskCLI.Models;

public sealed class Conversation
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string Body { get; set; } = string.Empty;
    public string BodyText { get; set; } = string.Empty;
    public bool Incoming { get; set; }
    public bool Private { get; set; }
    public int Source { get; set; }
    public string? FromEmail { get; set; }
    public string[] ToEmails { get; set; } = [];
    public string[] CcEmails { get; set; } = [];
    public string[] BccEmails { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Attachment[] Attachments { get; set; } = [];
    
    public string SourceText => Source switch
    {
        0 => "Email",
        1 => "Portal",
        2 => "Phone",
        3 => "Forum",
        4 => "Twitter",
        5 => "Facebook",
        7 => "Chat",
        _ => $"Unknown ({Source})"
    };
}
```

### Models/Config.cs
```csharp
namespace FreshdeskCLI.Models;

public sealed class ConfigFile
{
    public string Domain { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string? DefaultDownloadPath { get; set; }
    public int? MaxConcurrentDownloads { get; set; }
    public bool? AutoRetry { get; set; }
    public int? RetryCount { get; set; }
    public string? OutputFormat { get; set; } // json, table, csv
    
    // Validation
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
```

## Request/Response Models

### Models/Requests/UpdateTicketRequest.cs
```csharp
namespace FreshdeskCLI.Models.Requests;

public sealed class UpdateTicketRequest
{
    public string? Subject { get; set; }
    public string? Description { get; set; }
    public int? Status { get; set; }
    public int? Priority { get; set; }
    public long? ResponderId { get; set; }
    public long? GroupId { get; set; }
    public string[]? Tags { get; set; }
    public string[]? CcEmails { get; set; }
}
```

### Models/Requests/ReplyRequest.cs
```csharp
namespace FreshdeskCLI.Models.Requests;

public sealed class ReplyRequest
{
    public string Body { get; set; } = string.Empty;
    public string[]? Attachments { get; set; } // File paths for upload
    public string[]? CcEmails { get; set; }
    public string[]? BccEmails { get; set; }
}
```

### Models/Responses/TicketListResponse.cs
```csharp
namespace FreshdeskCLI.Models.Responses;

public sealed class TicketListResponse
{
    public Ticket[] Tickets { get; set; } = [];
    public int Page { get; set; }
    public int PerPage { get; set; }
    public int Total { get; set; }
    
    // Pagination helpers
    public bool HasNextPage => Tickets.Length == PerPage;
    public int? NextPage => HasNextPage ? Page + 1 : null;
}
```

### Models/Responses/ErrorResponse.cs
```csharp
namespace FreshdeskCLI.Models.Responses;

public sealed class ErrorResponse
{
    public string Message { get; set; } = string.Empty;
    public string? Code { get; set; }
    public Dictionary<string, string[]>? Errors { get; set; }
    
    public string GetDetailedMessage()
    {
        if (Errors == null || Errors.Count == 0)
            return Message;
        
        var details = Errors.SelectMany(e => 
            e.Value.Select(v => $"{e.Key}: {v}"));
        
        return $"{Message}\n{string.Join("\n", details)}";
    }
}
```

## Value Objects

### Models/RateLimitInfo.cs
```csharp
namespace FreshdeskCLI.Models;

public sealed class RateLimitInfo
{
    public int Total { get; set; }
    public int Remaining { get; set; }
    public int UsedCurrentRequest { get; set; }
    public DateTimeOffset? ResetTime { get; set; }
    
    public double PercentageUsed => Total > 0 
        ? (Total - Remaining) / (double)Total * 100 
        : 0;
    
    public bool ShouldThrottle => Remaining < Total * 0.1; // < 10% remaining
    
    public TimeSpan? TimeUntilReset => ResetTime.HasValue 
        ? ResetTime.Value - DateTimeOffset.Now 
        : null;
}
```

## Enums with AOT Support

### Models/TicketStatus.cs
```csharp
namespace FreshdeskCLI.Models;

[JsonConverter(typeof(JsonStringEnumConverter<TicketStatus>))]
public enum TicketStatus
{
    Open = 2,
    Pending = 3,
    Resolved = 4,
    Closed = 5,
    WaitingOnCustomer = 6,
    WaitingOnThirdParty = 7
}
```

### Models/TicketPriority.cs
```csharp
namespace FreshdeskCLI.Models;

[JsonConverter(typeof(JsonStringEnumConverter<TicketPriority>))]
public enum TicketPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Urgent = 4
}
```

## Custom Converters for AOT

### Converters/DateTimeOffsetConverter.cs
```csharp
namespace FreshdeskCLI.Converters;

public sealed class DateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(
        ref Utf8JsonReader reader, 
        Type typeToConvert, 
        JsonSerializerOptions options)
    {
        return DateTimeOffset.Parse(reader.GetString()!);
    }
    
    public override void Write(
        Utf8JsonWriter writer, 
        DateTimeOffset value, 
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("O"));
    }
}
```

## Validation Attributes

### Models/Validation/RequiredIfAttribute.cs
```csharp
namespace FreshdeskCLI.Models.Validation;

[AttributeUsage(AttributeTargets.Property)]
public sealed class RequiredIfAttribute : Attribute
{
    public string PropertyName { get; }
    public object Value { get; }
    
    public RequiredIfAttribute(string propertyName, object value)
    {
        PropertyName = propertyName;
        Value = value;
    }
}
```

## Testing Considerations

### Model Tests Required
1. **Serialization/Deserialization**
   - All models round-trip correctly
   - Null handling
   - Missing properties
   - Extra properties ignored

2. **Validation**
   - Config validation
   - Request validation
   - Required field checking

3. **AOT Compatibility**
   - No reflection usage
   - Source generator output verified
   - Trimming warnings resolved

### Example Test
```csharp
[Test]
public void Ticket_Serialization_RoundTrip()
{
    var ticket = new Ticket 
    { 
        Id = 123,
        Subject = "Test",
        Status = 2
    };
    
    var json = JsonSerializer.Serialize(
        ticket, 
        FreshdeskJsonContext.Default.Ticket);
    
    var deserialized = JsonSerializer.Deserialize(
        json, 
        FreshdeskJsonContext.Default.Ticket);
    
    Assert.Equal(ticket.Id, deserialized.Id);
}
```

## Performance Considerations

1. **Use sealed classes** - Better JIT optimization
2. **Avoid virtual members** - AOT friendly
3. **Initialize collections** - Avoid null checks
4. **Computed properties** - No serialization overhead
5. **Value types where appropriate** - Stack allocation

## Migration Path

Future enhancements without breaking changes:
1. Add new optional properties
2. Add new models to context
3. Add new converters
4. Extend validation attributes
5. Add new computed properties
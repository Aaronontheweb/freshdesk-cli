using System.Text.Json.Serialization;

namespace FreshdeskCLI.Models;

[JsonConverter(typeof(JsonNumberEnumConverter<TicketStatus>))]
public enum TicketStatus
{
    None = 0,  // Default value for uninitialized state
    Open = 2,
    Pending = 3,
    Resolved = 4,
    Closed = 5,
    WaitingOnCustomer = 6,
    WaitingOnThirdParty = 7
}

[JsonConverter(typeof(JsonNumberEnumConverter<TicketPriority>))]
public enum TicketPriority
{
    None = 0,  // Default value for uninitialized state
    Low = 1,
    Medium = 2,
    High = 3,
    Urgent = 4
}

[JsonConverter(typeof(JsonNumberEnumConverter<TicketSource>))]
public enum TicketSource
{
    Email = 0,
    Portal = 1,
    Phone = 2,
    Forum = 3,
    Twitter = 4,
    Facebook = 5,
    Chat = 7
}
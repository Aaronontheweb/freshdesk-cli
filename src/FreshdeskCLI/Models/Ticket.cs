namespace FreshdeskCLI.Models;

public sealed class Ticket
{
    public long Id { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DescriptionText { get; set; }
    public TicketStatus Status { get; set; }
    public TicketPriority Priority { get; set; }
    public TicketSource Source { get; set; }
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
    public Conversation[]? Conversations { get; set; }
    
    public string StatusText => Status switch
    {
        TicketStatus.Open => "Open",
        TicketStatus.Pending => "Pending",
        TicketStatus.Resolved => "Resolved",
        TicketStatus.Closed => "Closed",
        TicketStatus.WaitingOnCustomer => "Waiting on Customer",
        TicketStatus.WaitingOnThirdParty => "Waiting on Third Party",
        _ => $"Unknown ({Status})"
    };
    
    public string PriorityText => Priority switch
    {
        TicketPriority.Low => "Low",
        TicketPriority.Medium => "Medium",
        TicketPriority.High => "High",
        TicketPriority.Urgent => "Urgent",
        _ => $"Unknown ({Priority})"
    };
}
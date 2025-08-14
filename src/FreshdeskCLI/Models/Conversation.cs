namespace FreshdeskCLI.Models;

public sealed class Conversation
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string Body { get; set; } = string.Empty;
    public string BodyText { get; set; } = string.Empty;
    public bool Incoming { get; set; }
    public bool Private { get; set; }
    public TicketSource Source { get; set; }
    public string? FromEmail { get; set; }
    public string[] ToEmails { get; set; } = [];
    public string[] CcEmails { get; set; } = [];
    public string[] BccEmails { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Attachment[] Attachments { get; set; } = [];

    public string SourceText => Source switch
    {
        TicketSource.Email => "Email",
        TicketSource.Portal => "Portal",
        TicketSource.Phone => "Phone",
        TicketSource.Forum => "Forum",
        TicketSource.Twitter => "Twitter",
        TicketSource.Facebook => "Facebook",
        TicketSource.Chat => "Chat",
        _ => $"Unknown ({Source})"
    };
}
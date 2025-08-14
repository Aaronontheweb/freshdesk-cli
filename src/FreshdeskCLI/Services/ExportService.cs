using System.Text;
using System.Text.Json;
using FreshdeskCLI.Models;
using FreshdeskCLI.Helpers;

namespace FreshdeskCLI.Services;

public interface IExportService
{
    Task ExportTicketsAsync(
        Ticket[] tickets, 
        string filePath, 
        string format = "json",
        bool includeConversations = false,
        IFreshdeskApiClient? apiClient = null,
        CancellationToken cancellationToken = default);
        
    Task ExportTicketAsync(
        Ticket ticket,
        Conversation[]? conversations,
        string filePath,
        string format = "json",
        CancellationToken cancellationToken = default);
}

public sealed class ExportService : IExportService
{
    public async Task ExportTicketsAsync(
        Ticket[] tickets,
        string filePath,
        string format = "json",
        bool includeConversations = false,
        IFreshdeskApiClient? apiClient = null,
        CancellationToken cancellationToken = default)
    {
        if (tickets == null || tickets.Length == 0)
        {
            throw new ArgumentException("No tickets to export", nameof(tickets));
        }

        // Ensure directory exists
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        switch (format.ToLowerInvariant())
        {
            case "json":
                await ExportTicketsToJsonAsync(tickets, filePath, includeConversations, apiClient, cancellationToken);
                break;
            case "csv":
                await ExportTicketsToCsvAsync(tickets, filePath, cancellationToken);
                break;
            case "xml":
                await ExportTicketsToXmlAsync(tickets, filePath, cancellationToken);
                break;
            default:
                throw new ArgumentException($"Unsupported export format: {format}", nameof(format));
        }
    }

    public async Task ExportTicketAsync(
        Ticket ticket,
        Conversation[]? conversations,
        string filePath,
        string format = "json",
        CancellationToken cancellationToken = default)
    {
        // Ensure directory exists
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        switch (format.ToLowerInvariant())
        {
            case "json":
                await ExportTicketToJsonAsync(ticket, conversations, filePath, cancellationToken);
                break;
            case "csv":
                await ExportTicketsToCsvAsync(new[] { ticket }, filePath, cancellationToken);
                break;
            case "xml":
                await ExportTicketsToXmlAsync(new[] { ticket }, filePath, cancellationToken);
                break;
            case "markdown":
            case "md":
                await ExportTicketToMarkdownAsync(ticket, conversations, filePath, cancellationToken);
                break;
            default:
                throw new ArgumentException($"Unsupported export format: {format}", nameof(format));
        }
    }

    private async Task ExportTicketsToJsonAsync(
        Ticket[] tickets,
        string filePath,
        bool includeConversations,
        IFreshdeskApiClient? apiClient,
        CancellationToken cancellationToken)
    {
        string json;

        if (includeConversations && apiClient != null)
        {
            var ticketsWithConversations = new List<TicketWithConversations>();
            
            using var progress = ProgressIndicatorFactory.Create(
                "Fetching ticket conversations",
                enabled: !Console.IsOutputRedirected);

            var completed = 0;
            var total = tickets.Length;

            foreach (var ticket in tickets)
            {
                progress.Report(completed, total, $"Fetching conversations for ticket #{ticket.Id}");
                
                var conversations = await apiClient.GetTicketConversationsAsync(ticket.Id, cancellationToken);
                
                ticketsWithConversations.Add(new TicketWithConversations
                {
                    Ticket = ticket,
                    Conversations = conversations
                });
                
                completed++;
                progress.Report(completed, total);
            }
            
            progress.Complete($"Fetched conversations for {total} tickets");
            
            // Use the indented context for export
            json = JsonSerializer.Serialize(ticketsWithConversations, FreshdeskJsonIndentedContext.Default.ListTicketWithConversations);
        }
        else
        {
            // Use the indented context for export
            json = JsonSerializer.Serialize(tickets, FreshdeskJsonIndentedContext.Default.TicketArray);
        }

        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }

    private async Task ExportTicketToJsonAsync(
        Ticket ticket,
        Conversation[]? conversations,
        string filePath,
        CancellationToken cancellationToken)
    {
        string json;
        
        if (conversations != null)
        {
            var ticketWithConversations = new TicketWithConversations
            {
                Ticket = ticket,
                Conversations = conversations
            };
            json = JsonSerializer.Serialize(ticketWithConversations, FreshdeskJsonIndentedContext.Default.TicketWithConversations);
        }
        else
        {
            json = JsonSerializer.Serialize(ticket, FreshdeskJsonIndentedContext.Default.Ticket);
        }

        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }

    private async Task ExportTicketsToCsvAsync(
        Ticket[] tickets,
        string filePath,
        CancellationToken cancellationToken)
    {
        var csv = new StringBuilder();
        
        // Header
        csv.AppendLine("ID,Subject,Status,Priority,Source,Email,Created,Updated,Due By,Tags,Description");

        // Data rows
        foreach (var ticket in tickets)
        {
            csv.AppendLine($"{ticket.Id}," +
                          $"{EscapeCsvField(ticket.Subject)}," +
                          $"{ticket.Status}," +
                          $"{ticket.Priority}," +
                          $"{ticket.Source}," +
                          $"{EscapeCsvField(ticket.Email ?? "")}," +
                          $"{ticket.CreatedAt:yyyy-MM-dd HH:mm:ss}," +
                          $"{ticket.UpdatedAt:yyyy-MM-dd HH:mm:ss}," +
                          $"{(ticket.DueBy?.ToString("yyyy-MM-dd HH:mm:ss") ?? "")}," +
                          $"{EscapeCsvField(string.Join(";", ticket.Tags ?? Array.Empty<string>()))}," +
                          $"{EscapeCsvField(ticket.Description ?? "")}");
        }

        await File.WriteAllTextAsync(filePath, csv.ToString(), cancellationToken);
    }

    private async Task ExportTicketsToXmlAsync(
        Ticket[] tickets,
        string filePath,
        CancellationToken cancellationToken)
    {
        var xml = new StringBuilder();
        xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        xml.AppendLine("<tickets>");

        foreach (var ticket in tickets)
        {
            xml.AppendLine("  <ticket>");
            xml.AppendLine($"    <id>{ticket.Id}</id>");
            xml.AppendLine($"    <subject>{EscapeXml(ticket.Subject)}</subject>");
            xml.AppendLine($"    <status>{ticket.Status}</status>");
            xml.AppendLine($"    <priority>{ticket.Priority}</priority>");
            xml.AppendLine($"    <source>{ticket.Source}</source>");
            xml.AppendLine($"    <email>{EscapeXml(ticket.Email ?? "")}</email>");
            xml.AppendLine($"    <created>{ticket.CreatedAt:yyyy-MM-dd HH:mm:ss}</created>");
            xml.AppendLine($"    <updated>{ticket.UpdatedAt:yyyy-MM-dd HH:mm:ss}</updated>");
            
            if (ticket.DueBy.HasValue)
            {
                xml.AppendLine($"    <due_by>{ticket.DueBy.Value:yyyy-MM-dd HH:mm:ss}</due_by>");
            }
            
            if (ticket.Tags != null && ticket.Tags.Length > 0)
            {
                xml.AppendLine("    <tags>");
                foreach (var tag in ticket.Tags)
                {
                    xml.AppendLine($"      <tag>{EscapeXml(tag)}</tag>");
                }
                xml.AppendLine("    </tags>");
            }
            
            xml.AppendLine($"    <description>{EscapeXml(ticket.Description ?? "")}</description>");
            
            if (ticket.Attachments != null && ticket.Attachments.Length > 0)
            {
                xml.AppendLine("    <attachments>");
                foreach (var attachment in ticket.Attachments)
                {
                    xml.AppendLine("      <attachment>");
                    xml.AppendLine($"        <name>{EscapeXml(attachment.Name)}</name>");
                    xml.AppendLine($"        <size>{attachment.Size}</size>");
                    xml.AppendLine($"        <type>{EscapeXml(attachment.ContentType)}</type>");
                    xml.AppendLine("      </attachment>");
                }
                xml.AppendLine("    </attachments>");
            }
            
            xml.AppendLine("  </ticket>");
        }

        xml.AppendLine("</tickets>");
        await File.WriteAllTextAsync(filePath, xml.ToString(), cancellationToken);
    }

    private async Task ExportTicketToMarkdownAsync(
        Ticket ticket,
        Conversation[]? conversations,
        string filePath,
        CancellationToken cancellationToken)
    {
        var md = new StringBuilder();
        
        // Header
        md.AppendLine($"# Ticket #{ticket.Id}: {ticket.Subject}");
        md.AppendLine();
        
        // Metadata
        md.AppendLine("## Details");
        md.AppendLine($"- **Status**: {ticket.Status}");
        md.AppendLine($"- **Priority**: {ticket.Priority}");
        md.AppendLine($"- **Source**: {ticket.Source}");
        md.AppendLine($"- **Email**: {ticket.Email ?? "N/A"}");
        md.AppendLine($"- **Created**: {ticket.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        md.AppendLine($"- **Updated**: {ticket.UpdatedAt:yyyy-MM-dd HH:mm:ss}");
        
        if (ticket.DueBy.HasValue)
        {
            md.AppendLine($"- **Due By**: {ticket.DueBy.Value:yyyy-MM-dd HH:mm:ss}");
        }
        
        if (ticket.Tags != null && ticket.Tags.Length > 0)
        {
            md.AppendLine($"- **Tags**: {string.Join(", ", ticket.Tags)}");
        }
        
        md.AppendLine();
        
        // Description
        md.AppendLine("## Description");
        md.AppendLine(ticket.Description ?? "*No description provided*");
        md.AppendLine();
        
        // Attachments
        if (ticket.Attachments != null && ticket.Attachments.Length > 0)
        {
            md.AppendLine("## Attachments");
            foreach (var attachment in ticket.Attachments)
            {
                md.AppendLine($"- {attachment.Name} ({attachment.FormattedSize})");
            }
            md.AppendLine();
        }
        
        // Conversations
        if (conversations != null && conversations.Length > 0)
        {
            md.AppendLine("## Conversation History");
            md.AppendLine();
            
            foreach (var conversation in conversations.OrderBy(c => c.CreatedAt))
            {
                var type = conversation.Private ? "Internal Note" : 
                          conversation.Incoming ? "Customer" : "Agent";
                
                md.AppendLine($"### {type} - {conversation.CreatedAt:yyyy-MM-dd HH:mm:ss}");
                md.AppendLine();
                
                // Use plain text if available, otherwise strip HTML
                var body = !string.IsNullOrEmpty(conversation.BodyText) 
                    ? conversation.BodyText 
                    : StripHtml(conversation.Body ?? "");
                    
                md.AppendLine(body);
                md.AppendLine();
                
                if (conversation.Attachments != null && conversation.Attachments.Length > 0)
                {
                    md.AppendLine("**Attachments:**");
                    foreach (var attachment in conversation.Attachments)
                    {
                        md.AppendLine($"- {attachment.Name} ({attachment.FormattedSize})");
                    }
                    md.AppendLine();
                }
                
                md.AppendLine("---");
                md.AppendLine();
            }
        }

        await File.WriteAllTextAsync(filePath, md.ToString(), cancellationToken);
    }

    private static string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field))
            return "";

        // Escape quotes by doubling them
        field = field.Replace("\"", "\"\"");

        // Wrap in quotes if contains comma, quote, or newline
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field}\"";
        }

        return field;
    }

    private static string EscapeXml(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html))
            return "";

        // Simple HTML stripping (for more complex HTML, consider using a proper HTML parser)
        var result = System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", "");
        result = System.Net.WebUtility.HtmlDecode(result);
        return result;
    }
}
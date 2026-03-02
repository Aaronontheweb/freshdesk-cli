using System.Text.Json;
using FreshdeskCLI.Models;
using System.Linq;

namespace FreshdeskCLI.Helpers;

public static class OutputFormatter
{
    public static void PrintTickets(Ticket[] tickets, string format = "table")
    {
        switch (format.ToLowerInvariant())
        {
            case "json":
                PrintTicketsJson(tickets);
                break;
            case "csv":
                PrintTicketsCsv(tickets);
                break;
            case "table":
            default:
                PrintTicketsTable(tickets);
                break;
        }
    }

    private static void PrintTicketsTable(Ticket[] tickets)
    {
        if (tickets.Length == 0)
        {
            Console.WriteLine("No tickets found.");
            return;
        }

        Console.WriteLine($"{"ID",-10} {"Subject",-30} {"Requester ID",-15} {"Status",-20} {"Priority",-10} {"Created",-20}");
        Console.WriteLine(new string('-', 110));

        foreach (var ticket in tickets)
        {
            var subject = TruncateString(ticket.Subject ?? "", 30);
            var requesterId = ticket.RequesterId?.ToString() ?? "N/A";
            Console.WriteLine($"{ticket.Id,-10} {subject,-30} {requesterId,-15} {ticket.StatusText,-20} {ticket.PriorityText,-10} {ticket.CreatedAt:yyyy-MM-dd HH:mm}");
        }

        Console.WriteLine($"\nTotal: {tickets.Length} tickets");
    }

    private static void PrintTicketsJson(Ticket[] tickets)
    {
        var json = JsonSerializer.Serialize(tickets, FreshdeskJsonContext.Default.TicketArray);
        Console.WriteLine(json);
    }

    private static void PrintTicketsCsv(Ticket[] tickets)
    {
        Console.WriteLine("ID,Subject,Status,Priority,Created,Updated,Email");

        foreach (var ticket in tickets)
        {
            var subject = EscapeCsvField(ticket.Subject ?? "");
            var email = EscapeCsvField(ticket.Email ?? "");
            Console.WriteLine($"{ticket.Id},{subject},{ticket.Status},{ticket.Priority},{ticket.CreatedAt:yyyy-MM-dd HH:mm:ss},{ticket.UpdatedAt:yyyy-MM-dd HH:mm:ss},{email}");
        }
    }

    public static void PrintTicketDetails(Ticket ticket, string format = "text")
    {
        switch (format.ToLowerInvariant())
        {
            case "json":
                var json = JsonSerializer.Serialize(ticket, FreshdeskJsonContext.Default.Ticket);
                Console.WriteLine(json);
                break;
            case "text":
            default:
                Console.WriteLine($"Ticket #{ticket.Id}");
                Console.WriteLine(new string('=', 50));
                Console.WriteLine($"Subject: {ticket.Subject}");
                Console.WriteLine($"Status: {ticket.Status}");
                Console.WriteLine($"Priority: {ticket.Priority}");
                Console.WriteLine($"Source: {ticket.Source}");
                Console.WriteLine($"Email: {ticket.Email}");
                Console.WriteLine($"Created: {ticket.CreatedAt:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"Updated: {ticket.UpdatedAt:yyyy-MM-dd HH:mm:ss}");

                if (!string.IsNullOrEmpty(ticket.Description))
                {
                    Console.WriteLine("\nDescription:");
                    Console.WriteLine(ticket.Description);
                }

                if (ticket.Attachments?.Length > 0)
                {
                    Console.WriteLine($"\nAttachments: {ticket.Attachments.Length}");
                    foreach (var attachment in ticket.Attachments)
                    {
                        Console.WriteLine($"  - {attachment.Name} ({FormatFileSize(attachment.Size)})");
                    }
                }

                // Show conversation summary if available
                if (ticket.Conversations?.Length > 0)
                {
                    int replyCount = ticket.Conversations.Count(c => !c.Private);
                    int noteCount = ticket.Conversations.Count(c => c.Private);
                    Console.WriteLine($"\nConversations: {replyCount} replies, {noteCount} notes");
                    Console.WriteLine("Use --tree or --full to view conversation details");
                }
                break;
        }
    }

    private static string TruncateString(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return value.Length <= maxLength ? value : value[..(maxLength - 3)] + "...";
    }

    private static string EscapeCsvField(string field)
    {
        if (field.Contains(',', StringComparison.Ordinal) ||
            field.Contains('"', StringComparison.Ordinal) ||
            field.Contains('\n', StringComparison.Ordinal))
        {
            return $"\"{field.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }
        return field;
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }

    public static void PrintTicketJson(Ticket ticket)
    {
        var json = JsonSerializer.Serialize(ticket, FreshdeskJsonContext.Default.Ticket);
        Console.WriteLine(json);
    }

    public static void PrintTicketTree(Ticket ticket, Conversation[]? conversations)
    {
        Console.WriteLine($"Ticket #{ticket.Id}: {ticket.Subject} [{ticket.Status}]");

        // Show summary
        int replyCount = conversations?.Count(c => !c.Private) ?? 0;
        int noteCount = conversations?.Count(c => c.Private) ?? 0;
        int attachmentCount = ticket.Attachments?.Length ?? 0;
        long totalAttachmentSize = ticket.Attachments?.Sum(a => a.Size) ?? 0;

        Console.WriteLine($"Summary: {replyCount} replies, {noteCount} notes, {attachmentCount} attachments ({FormatFileSize(totalAttachmentSize)})");
        Console.WriteLine();

        if (conversations == null || conversations.Length == 0)
        {
            Console.WriteLine("└── No conversations");
            return;
        }

        // Build tree structure
        for (int i = 0; i < conversations.Length; i++)
        {
            var conv = conversations[i];
            bool isLast = i == conversations.Length - 1;
            string prefix = isLast ? "└── " : "├── ";
            string childPrefix = isLast ? "    " : "│   ";

            string typeIndicator = conv.Private ? "[Note]" : conv.Incoming ? "[Customer]" : "[Agent]";
            Console.WriteLine($"{prefix}{conv.CreatedAt:yyyy-MM-dd HH:mm} - {typeIndicator}");

            // Show attachments for this conversation
            if (conv.Attachments?.Length > 0)
            {
                for (int j = 0; j < conv.Attachments.Length; j++)
                {
                    var att = conv.Attachments[j];
                    bool isLastAtt = j == conv.Attachments.Length - 1;
                    string attPrefix = isLastAtt ? "└── " : "├── ";
                    Console.WriteLine($"{childPrefix}{attPrefix}{att.Name} ({FormatFileSize(att.Size)})");
                }
            }
        }
    }

    public static void PrintTicketFull(Ticket ticket, Conversation[]? conversations)
    {
        // Print basic ticket details first
        PrintTicketDetails(ticket, "text");

        if (conversations == null || conversations.Length == 0)
        {
            Console.WriteLine("\nNo conversations found.");
            return;
        }

        Console.WriteLine("\n" + new string('=', 50));
        Console.WriteLine("CONVERSATION THREAD");
        Console.WriteLine(new string('=', 50));

        foreach (var conv in conversations)
        {
            Console.WriteLine();
            string typeLabel = conv.Private ? "INTERNAL NOTE" : conv.Incoming ? "CUSTOMER" : "AGENT";
            Console.WriteLine($"[{typeLabel}] {conv.CreatedAt:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine(new string('-', 30));

            // Show plain text body
            if (!string.IsNullOrEmpty(conv.BodyText))
            {
                Console.WriteLine(conv.BodyText);
            }
            else if (!string.IsNullOrEmpty(conv.Body))
            {
                // Strip HTML tags for basic display
                var plainText = System.Text.RegularExpressions.Regex.Replace(conv.Body, "<.*?>", "");
                Console.WriteLine(plainText);
            }

            // Show attachments
            if (conv.Attachments?.Length > 0)
            {
                Console.WriteLine($"\nAttachments ({conv.Attachments.Length}):");
                foreach (var att in conv.Attachments)
                {
                    Console.WriteLine($"  - {att.Name} ({FormatFileSize(att.Size)})");
                }
            }
        }
    }

    public static void PrintContacts(Contact[] contacts, string format = "table")
    {
        switch (format.ToLowerInvariant())
        {
            case "json":
                var json = JsonSerializer.Serialize(contacts, FreshdeskJsonContext.Default.ContactArray);
                Console.WriteLine(json);
                break;
            case "csv":
                Console.WriteLine("ID,Name,Email,Phone,CompanyID,Active,Created");
                foreach (var contact in contacts)
                {
                    var name = EscapeCsvField(contact.Name);
                    var email = EscapeCsvField(contact.Email);
                    var phone = EscapeCsvField(contact.Phone ?? "");
                    Console.WriteLine($"{contact.Id},{name},{email},{phone},{contact.CompanyId ?? 0},{contact.Active},{contact.CreatedAt:yyyy-MM-dd HH:mm:ss}");
                }
                break;
            case "table":
            default:
                if (contacts.Length == 0)
                {
                    Console.WriteLine("No contacts found.");
                    return;
                }
                Console.WriteLine($"{"ID",-10} {"Name",-25} {"Email",-30} {"Phone",-15} {"Company ID",-12} {"Active",-8} {"Created",-20}");
                Console.WriteLine(new string('-', 125));
                foreach (var contact in contacts)
                {
                    var name = TruncateString(contact.Name, 25);
                    var email = TruncateString(contact.Email, 30);
                    var phone = TruncateString(contact.Phone ?? "", 15);
                    var companyId = contact.CompanyId?.ToString() ?? "N/A";
                    Console.WriteLine($"{contact.Id,-10} {name,-25} {email,-30} {phone,-15} {companyId,-12} {contact.Active,-8} {contact.CreatedAt:yyyy-MM-dd HH:mm}");
                }
                Console.WriteLine($"\nTotal: {contacts.Length} contacts");
                break;
        }
    }

    public static void PrintContactDetails(Contact contact, string format = "table")
    {
        if (format.ToLowerInvariant() == "json")
        {
            var json = JsonSerializer.Serialize(contact, FreshdeskJsonContext.Default.Contact);
            Console.WriteLine(json);
            return;
        }

        Console.WriteLine($"Contact #{contact.Id}");
        Console.WriteLine(new string('=', 50));
        Console.WriteLine($"Name: {contact.Name}");
        Console.WriteLine($"Email: {contact.Email}");
        if (!string.IsNullOrEmpty(contact.Phone))
            Console.WriteLine($"Phone: {contact.Phone}");
        if (!string.IsNullOrEmpty(contact.Mobile))
            Console.WriteLine($"Mobile: {contact.Mobile}");
        if (!string.IsNullOrEmpty(contact.JobTitle))
            Console.WriteLine($"Job Title: {contact.JobTitle}");
        if (contact.CompanyId.HasValue)
            Console.WriteLine($"Company ID: {contact.CompanyId.Value}");
        Console.WriteLine($"View All Tickets: {contact.ViewAllTickets?.ToString() ?? "N/A"}");
        Console.WriteLine($"Active: {contact.Active}");
        Console.WriteLine($"Language: {contact.Language}");
        if (!string.IsNullOrEmpty(contact.TimeZone))
            Console.WriteLine($"Time Zone: {contact.TimeZone}");
        if (!string.IsNullOrEmpty(contact.Address))
            Console.WriteLine($"Address: {contact.Address}");
        if (!string.IsNullOrEmpty(contact.Description))
            Console.WriteLine($"Description: {contact.Description}");
        if (contact.OtherCompanies?.Length > 0)
        {
            Console.WriteLine($"Other Companies: {string.Join(", ", contact.OtherCompanies.Select(c => c.CompanyId))}");
        }
        Console.WriteLine($"Created: {contact.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"Updated: {contact.UpdatedAt:yyyy-MM-dd HH:mm:ss}");
    }

    public static void PrintCompanies(Company[] companies, string format = "table")
    {
        switch (format.ToLowerInvariant())
        {
            case "json":
                var json = JsonSerializer.Serialize(companies, FreshdeskJsonContext.Default.CompanyArray);
                Console.WriteLine(json);
                break;
            case "csv":
                Console.WriteLine("ID,Name,Description,Industry,Domains,Created");
                foreach (var company in companies)
                {
                    var name = EscapeCsvField(company.Name);
                    var desc = EscapeCsvField(company.Description ?? "");
                    var industry = EscapeCsvField(company.Industry ?? "");
                    var domains = EscapeCsvField(company.Domains != null ? string.Join(";", company.Domains) : "");
                    Console.WriteLine($"{company.Id},{name},{desc},{industry},{domains},{company.CreatedAt:yyyy-MM-dd HH:mm:ss}");
                }
                break;
            case "table":
            default:
                if (companies.Length == 0)
                {
                    Console.WriteLine("No companies found.");
                    return;
                }
                Console.WriteLine($"{"ID",-10} {"Name",-30} {"Description",-40} {"Industry",-20} {"Domains",-20} {"Created",-20}");
                Console.WriteLine(new string('-', 145));
                foreach (var company in companies)
                {
                    var name = TruncateString(company.Name, 30);
                    var desc = TruncateString(company.Description ?? "", 40);
                    var industry = TruncateString(company.Industry ?? "", 20);
                    var domains = TruncateString(company.Domains != null ? string.Join(", ", company.Domains) : "", 20);
                    Console.WriteLine($"{company.Id,-10} {name,-30} {desc,-40} {industry,-20} {domains,-20} {company.CreatedAt:yyyy-MM-dd HH:mm}");
                }
                Console.WriteLine($"\nTotal: {companies.Length} companies");
                break;
        }
    }

    public static void PrintCompanyDetails(Company company, string format = "table")
    {
        if (format.ToLowerInvariant() == "json")
        {
            var json = JsonSerializer.Serialize(company, FreshdeskJsonContext.Default.Company);
            Console.WriteLine(json);
            return;
        }

        Console.WriteLine($"Company #{company.Id}");
        Console.WriteLine(new string('=', 50));
        Console.WriteLine($"Name: {company.Name}");
        if (!string.IsNullOrEmpty(company.Description))
            Console.WriteLine($"Description: {company.Description}");
        if (company.Domains?.Length > 0)
            Console.WriteLine($"Domains: {string.Join(", ", company.Domains)}");
        if (!string.IsNullOrEmpty(company.Industry))
            Console.WriteLine($"Industry: {company.Industry}");
        if (!string.IsNullOrEmpty(company.HealthScore))
            Console.WriteLine($"Health Score: {company.HealthScore}");
        if (!string.IsNullOrEmpty(company.Note))
            Console.WriteLine($"Note: {company.Note}");
        if (company.CustomFields?.Count > 0)
        {
            Console.WriteLine("Custom Fields:");
            foreach (var field in company.CustomFields)
            {
                Console.WriteLine($"  {field.Key}: {field.Value}");
            }
        }
        Console.WriteLine($"Created: {company.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"Updated: {company.UpdatedAt:yyyy-MM-dd HH:mm:ss}");
    }

    public static void PrintCompanyFields(CompanyField[] fields, string format = "table")
    {
        switch (format.ToLowerInvariant())
        {
            case "json":
                var json = JsonSerializer.Serialize(fields, FreshdeskJsonContext.Default.CompanyFieldArray);
                Console.WriteLine(json);
                break;
            case "csv":
                Console.WriteLine("ID,Name,Label,Type,Req.Agents,Req.Customers,Choices");
                foreach (var field in fields)
                {
                    var name = EscapeCsvField(field.Name);
                    var label = EscapeCsvField(field.Label);
                    var fieldType = EscapeCsvField(field.FieldType);
                    var choices = EscapeCsvField(field.Choices != null ? string.Join(";", field.Choices) : "");
                    Console.WriteLine($"{field.Id},{name},{label},{fieldType},{field.RequiredForAgents},{field.RequiredForCustomers},{choices}");
                }
                break;
            case "table":
            default:
                if (fields.Length == 0)
                {
                    Console.WriteLine("No company fields found.");
                    return;
                }
                Console.WriteLine($"{"ID",-10} {"Name",-30} {"Label",-30} {"Type",-20} {"Req.Agents",-12} {"Req.Customers",-14} {"Choices"}");
                Console.WriteLine(new string('-', 130));
                foreach (var field in fields)
                {
                    var name = TruncateString(field.Name, 30);
                    var label = TruncateString(field.Label, 30);
                    var fieldType = TruncateString(field.FieldType, 20);
                    var choices = field.Choices != null ? TruncateString(string.Join(", ", field.Choices), 30) : "";
                    Console.WriteLine($"{field.Id,-10} {name,-30} {label,-30} {fieldType,-20} {field.RequiredForAgents,-12} {field.RequiredForCustomers,-14} {choices}");
                }
                Console.WriteLine($"\nTotal: {fields.Length} fields");
                break;
        }
    }
}
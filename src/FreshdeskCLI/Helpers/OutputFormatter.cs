using System.Text.Json;
using FreshdeskCLI.Models;

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

        Console.WriteLine($"{"ID",-10} {"Subject",-40} {"Status",-15} {"Priority",-10} {"Created",-20}");
        Console.WriteLine(new string('-', 95));

        foreach (var ticket in tickets)
        {
            var subject = TruncateString(ticket.Subject ?? "", 40);
            Console.WriteLine($"{ticket.Id,-10} {subject,-40} {ticket.Status,-15} {ticket.Priority,-10} {ticket.CreatedAt:yyyy-MM-dd HH:mm}");
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
}
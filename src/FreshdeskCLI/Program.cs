using System.Text.Json;
using FreshdeskCLI;
using FreshdeskCLI.Models;

// Simple test program to verify AOT compilation
Console.WriteLine("Freshdesk CLI v1.0.0");

if (args.Length > 0 && args[0] == "--version")
{
    Console.WriteLine("Version: 1.0.0");
    return 0;
}

if (args.Length > 0 && args[0] == "--test-aot")
{
    // Test JSON serialization with AOT
    var testTicket = new Ticket
    {
        Id = 1,
        Subject = "Test Ticket",
        Status = TicketStatus.Open,
        Priority = TicketPriority.Low,
        Source = TicketSource.Email,
        CreatedAt = DateTimeOffset.Now,
        UpdatedAt = DateTimeOffset.Now,
        Attachments = [
            new Attachment 
            { 
                Id = 1,
                Name = "test.pdf",
                Size = 12345,
                ContentType = "application/pdf",
                AttachmentUrl = "https://example.com/test.pdf",
                CreatedAt = DateTimeOffset.Now,
                UpdatedAt = DateTimeOffset.Now
            }
        ]
    };
    
    var json = JsonSerializer.Serialize(testTicket, FreshdeskJsonContext.Default.Ticket);
    Console.WriteLine($"AOT Serialization test passed: {json.Length} bytes");
    
    // Test deserialization
    var deserialized = JsonSerializer.Deserialize(json, FreshdeskJsonContext.Default.Ticket);
    Console.WriteLine($"Deserialization test passed: Ticket #{deserialized?.Id} - {deserialized?.Subject}");
    
    return 0;
}

Console.WriteLine("Usage: freshdesk [command] [options]");
Console.WriteLine("Run 'freshdesk --help' for more information");
return 0;

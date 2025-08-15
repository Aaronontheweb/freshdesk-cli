using System.Text;
using FreshdeskCLI.Helpers;
using FreshdeskCLI.Models;

namespace FreshdeskCLI.Tests.Helpers;

public class OutputFormatterTests : IDisposable
{
    private readonly StringWriter _output;
    private readonly TextWriter _originalOutput;

    public OutputFormatterTests()
    {
        _originalOutput = Console.Out;
        _output = new StringWriter();
        Console.SetOut(_output);
    }

    public void Dispose()
    {
        Console.SetOut(_originalOutput);
        _output.Dispose();
    }

    [Fact]
    public void PrintTickets_Table_DisplaysCorrectly()
    {
        // Arrange
        var tickets = new[]
        {
            new Ticket
            {
                Id = 1,
                Subject = "Test Ticket 1",
                Status = TicketStatus.Open,
                Priority = TicketPriority.High,
                CreatedAt = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero)
            },
            new Ticket
            {
                Id = 2,
                Subject = "This is a very long ticket subject that should be truncated in the table view",
                Status = TicketStatus.Closed,
                Priority = TicketPriority.Low,
                CreatedAt = new DateTimeOffset(2025, 1, 15, 11, 45, 0, TimeSpan.Zero)
            }
        };

        // Act
        OutputFormatter.PrintTickets(tickets, "table");
        var output = _output.ToString();

        // Assert
        Assert.Contains("Test Ticket 1", output);
        Assert.Contains("Open", output);
        Assert.Contains("High", output);
        Assert.Contains("This is a very long ticket ...", output); // Truncated to 30 chars
        Assert.Contains("Total: 2 tickets", output);
    }

    [Fact]
    public void PrintTickets_EmptyArray_ShowsNoTicketsMessage()
    {
        // Arrange
        var tickets = Array.Empty<Ticket>();

        // Act
        OutputFormatter.PrintTickets(tickets, "table");
        var output = _output.ToString();

        // Assert
        Assert.Contains("No tickets found", output);
    }

    [Fact]
    public void PrintTickets_Json_OutputsValidJson()
    {
        // Arrange
        var tickets = new[]
        {
            new Ticket
            {
                Id = 1,
                Subject = "Test",
                Status = TicketStatus.Open,
                Priority = TicketPriority.Medium
            }
        };

        // Act
        OutputFormatter.PrintTickets(tickets, "json");
        var output = _output.ToString();

        // Assert
        Assert.Contains("\"id\":1", output);
        Assert.Contains("\"subject\":\"Test\"", output);
        Assert.StartsWith("[", output.Trim());
        Assert.EndsWith("]", output.Trim());
    }

    [Fact]
    public void PrintTickets_Csv_OutputsCorrectFormat()
    {
        // Arrange
        var tickets = new[]
        {
            new Ticket
            {
                Id = 1,
                Subject = "Test, with comma",
                Status = TicketStatus.Open,
                Priority = TicketPriority.High,
                Email = "test@example.com",
                CreatedAt = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero),
                UpdatedAt = new DateTimeOffset(2025, 1, 15, 11, 0, 0, TimeSpan.Zero)
            }
        };

        // Act
        OutputFormatter.PrintTickets(tickets, "csv");
        var output = _output.ToString();

        // Assert
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length); // Header + 1 data row
        Assert.StartsWith("ID,Subject,Status,Priority,Created,Updated,Email", lines[0]);
        Assert.Contains("\"Test, with comma\"", lines[1]); // Escaped comma
        Assert.Contains("test@example.com", lines[1]);
    }

    [Fact]
    public void PrintTicketDetails_ShowsAllDetails()
    {
        // Arrange
        var ticket = new Ticket
        {
            Id = 123,
            Subject = "Test Ticket",
            Status = TicketStatus.Open,
            Priority = TicketPriority.High,
            Source = TicketSource.Email,
            Email = "customer@example.com",
            Description = "This is a test description",
            CreatedAt = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2025, 1, 15, 11, 0, 0, TimeSpan.Zero),
            Attachments = new[]
            {
                new Attachment { Name = "file.pdf", Size = 1024 * 500 } // 500 KB
            }
        };

        // Act
        OutputFormatter.PrintTicketDetails(ticket, "text");
        var output = _output.ToString();

        // Assert
        Assert.Contains("Ticket #123", output);
        Assert.Contains("Subject: Test Ticket", output);
        Assert.Contains("Status: Open", output);
        Assert.Contains("Priority: High", output);
        Assert.Contains("Source: Email", output);
        Assert.Contains("Email: customer@example.com", output);
        Assert.Contains("This is a test description", output);
        Assert.Contains("file.pdf", output);
        Assert.Contains("500 KB", output); // Formatted file size
    }

    [Fact]
    public void PrintTicketTree_ShowsConversationStructure()
    {
        // Arrange
        var ticket = new Ticket
        {
            Id = 123,
            Subject = "Test Ticket",
            Status = TicketStatus.Open,
            Attachments = new[]
            {
                new Attachment { Name = "main.pdf", Size = 1024 * 1024 } // 1 MB
            }
        };

        var conversations = new[]
        {
            new Conversation
            {
                Id = 1,
                Private = false,
                Incoming = true,
                CreatedAt = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero),
                Attachments = new[]
                {
                    new Attachment { Name = "screenshot.png", Size = 1024 * 250 }
                }
            },
            new Conversation
            {
                Id = 2,
                Private = true,
                Incoming = false,
                CreatedAt = new DateTimeOffset(2025, 1, 15, 11, 0, 0, TimeSpan.Zero)
            }
        };

        // Act
        OutputFormatter.PrintTicketTree(ticket, conversations);
        var output = _output.ToString();

        // Assert
        Assert.Contains("Ticket #123: Test Ticket [Open]", output);
        Assert.Contains("Summary: 1 replies, 1 notes, 1 attachments (1 MB)", output);
        Assert.Contains("[Customer]", output);
        Assert.Contains("[Note]", output);
        Assert.Contains("screenshot.png (250 KB)", output);
        Assert.Contains("├──", output); // Tree structure
        Assert.Contains("└──", output); // Tree structure
    }

    [Fact]
    public void PrintTicketFull_ShowsCompleteConversations()
    {
        // Arrange
        var ticket = new Ticket
        {
            Id = 123,
            Subject = "Test Ticket",
            Status = TicketStatus.Open
        };

        var conversations = new[]
        {
            new Conversation
            {
                Id = 1,
                Private = false,
                Incoming = true,
                BodyText = "This is the customer's message",
                CreatedAt = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero)
            },
            new Conversation
            {
                Id = 2,
                Private = true,
                Incoming = false,
                BodyText = "This is an internal note",
                CreatedAt = new DateTimeOffset(2025, 1, 15, 11, 0, 0, TimeSpan.Zero)
            }
        };

        // Act
        OutputFormatter.PrintTicketFull(ticket, conversations);
        var output = _output.ToString();

        // Assert
        Assert.Contains("CONVERSATION THREAD", output);
        Assert.Contains("[CUSTOMER]", output);
        Assert.Contains("[INTERNAL NOTE]", output);
        Assert.Contains("This is the customer's message", output);
        Assert.Contains("This is an internal note", output);
    }

    [Fact]
    public void PrintTicketFull_HandlesHtmlContent()
    {
        // Arrange
        var ticket = new Ticket
        {
            Id = 123,
            Subject = "Test",
            Status = TicketStatus.Open
        };

        var conversations = new[]
        {
            new Conversation
            {
                Id = 1,
                Body = "<p>This is <strong>HTML</strong> content</p>",
                BodyText = "", // No plain text version
                CreatedAt = DateTimeOffset.Now
            }
        };

        // Act
        OutputFormatter.PrintTicketFull(ticket, conversations);
        var output = _output.ToString();

        // Assert
        Assert.Contains("This is HTML content", output); // HTML tags stripped
        Assert.DoesNotContain("<p>", output);
        Assert.DoesNotContain("<strong>", output);
    }
}
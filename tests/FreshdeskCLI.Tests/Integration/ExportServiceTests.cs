using System.Text.Json;
using System.Xml.Linq;
using FreshdeskCLI.Models;
using FreshdeskCLI.Services;

namespace FreshdeskCLI.Tests.Integration;

public class ExportServiceTests : IDisposable
{
    private readonly string _testOutputPath;
    private readonly ExportService _exportService;
    private readonly MockFreshdeskServer _mockServer;
    private readonly HttpClient _httpClient;

    public ExportServiceTests()
    {
        _testOutputPath = Path.Combine(Path.GetTempPath(), $"export-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testOutputPath);

        _exportService = new ExportService();
        _mockServer = new MockFreshdeskServer();
        _httpClient = new HttpClient(_mockServer)
        {
            BaseAddress = new Uri("https://test.freshdesk.com")
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(_testOutputPath))
            Directory.Delete(_testOutputPath, true);

        _httpClient?.Dispose();
    }

    [Fact]
    public async Task ExportTicketsToJson_CreatesValidJsonFile()
    {
        // Arrange
        var tickets = CreateTestTickets();
        var outputFile = Path.Combine(_testOutputPath, "tickets.json");

        // Act
        await _exportService.ExportTicketsAsync(tickets, outputFile, "json");

        // Assert
        Assert.True(File.Exists(outputFile));

        var json = await File.ReadAllTextAsync(outputFile);
        var deserialized = JsonSerializer.Deserialize<Ticket[]>(json, FreshdeskJsonIndentedContext.Default.Options);

        Assert.NotNull(deserialized);
        Assert.Equal(3, deserialized.Length);
        Assert.Equal("Test Ticket 1", deserialized[0].Subject);
    }

    [Fact]
    public async Task ExportTicketsToCsv_CreatesValidCsvFile()
    {
        // Arrange
        var tickets = CreateTestTickets();
        var outputFile = Path.Combine(_testOutputPath, "tickets.csv");

        // Act
        await _exportService.ExportTicketsAsync(tickets, outputFile, "csv");

        // Assert
        Assert.True(File.Exists(outputFile));

        var lines = await File.ReadAllLinesAsync(outputFile);
        Assert.NotEmpty(lines);

        // Check header
        Assert.Contains("ID,Subject,Status,Priority", lines[0]);

        // Check data rows (3 tickets + 1 header)
        Assert.Equal(4, lines.Length);

        // Check first data row
        Assert.Contains("Test Ticket 1", lines[1]);
        Assert.Contains("Open", lines[1]);
    }

    [Fact]
    public async Task ExportTicketsToXml_CreatesValidXmlFile()
    {
        // Arrange
        var tickets = CreateTestTickets();
        var outputFile = Path.Combine(_testOutputPath, "tickets.xml");

        // Act
        await _exportService.ExportTicketsAsync(tickets, outputFile, "xml");

        // Assert
        Assert.True(File.Exists(outputFile));

        var xml = await File.ReadAllTextAsync(outputFile);
        var doc = XDocument.Parse(xml);

        Assert.NotNull(doc.Root);
        Assert.Equal("tickets", doc.Root.Name.LocalName);

        var ticketElements = doc.Root.Elements("ticket").ToList();
        Assert.Equal(3, ticketElements.Count);

        var firstTicket = ticketElements[0];
        Assert.Equal("1", firstTicket.Element("id")?.Value);
        Assert.Equal("Test Ticket 1", firstTicket.Element("subject")?.Value);
    }

    [Fact]
    public async Task ExportTicketToMarkdown_CreatesFormattedMarkdown()
    {
        // Arrange
        var ticket = new Ticket
        {
            Id = 123,
            Subject = "Test Issue",
            Description = "This is a test description",
            Status = TicketStatus.Open,
            Priority = TicketPriority.High,
            Email = "customer@example.com",
            CreatedAt = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2025, 1, 15, 11, 0, 0, TimeSpan.Zero),
            Tags = new[] { "bug", "urgent" },
            Attachments = new[]
            {
                new Attachment { Name = "screenshot.png", Size = 1024 * 500 }
            }
        };

        var conversations = new[]
        {
            new Conversation
            {
                Id = 1,
                Body = "Customer's initial message",
                BodyText = "Customer's initial message",
                Private = false,
                Incoming = true,
                CreatedAt = new DateTimeOffset(2025, 1, 15, 10, 35, 0, TimeSpan.Zero)
            },
            new Conversation
            {
                Id = 2,
                Body = "Agent's response",
                BodyText = "Agent's response",
                Private = false,
                Incoming = false,
                CreatedAt = new DateTimeOffset(2025, 1, 15, 10, 45, 0, TimeSpan.Zero)
            }
        };

        var outputFile = Path.Combine(_testOutputPath, "ticket.md");

        // Act
        await _exportService.ExportTicketAsync(ticket, conversations, outputFile, "markdown");

        // Assert
        Assert.True(File.Exists(outputFile));

        var markdown = await File.ReadAllTextAsync(outputFile);

        // Check structure
        Assert.Contains("# Ticket #123: Test Issue", markdown);
        Assert.Contains("## Details", markdown);
        Assert.Contains("- **Status**: Open", markdown);
        Assert.Contains("- **Priority**: High", markdown);
        Assert.Contains("- **Tags**: bug, urgent", markdown);

        // Check attachments
        Assert.Contains("## Attachments", markdown);
        Assert.Contains("screenshot.png", markdown);

        // Check conversations
        Assert.Contains("## Conversation History", markdown);
        Assert.Contains("Customer's initial message", markdown);
        Assert.Contains("Agent's response", markdown);
    }

    [Fact]
    public async Task ExportTicketsWithConversations_IncludesConversationData()
    {
        // Arrange
        var tickets = CreateTestTickets();
        var outputFile = Path.Combine(_testOutputPath, "tickets_with_conversations.json");

        var config = new FreshdeskConfig
        {
            Domain = "test",
            ApiKey = "test-api-key-12345"
        };

        var client = new FreshdeskApiClient(config, _httpClient);

        // Setup mock conversations for each ticket
        foreach (var ticket in tickets)
        {
            _mockServer.SetupConversations(ticket.Id, new[]
            {
                new Conversation
                {
                    Id = ticket.Id * 100,
                    Body = $"Conversation for ticket {ticket.Id}",
                    BodyText = $"Conversation for ticket {ticket.Id}",
                    Private = false,
                    Incoming = true,
                    CreatedAt = DateTimeOffset.Now
                }
            });
        }

        // Act
        await _exportService.ExportTicketsAsync(
            tickets,
            outputFile,
            "json",
            includeConversations: true,
            apiClient: client);

        // Assert
        Assert.True(File.Exists(outputFile));

        var json = await File.ReadAllTextAsync(outputFile);
        Assert.Contains("conversations", json);
        Assert.Contains("Conversation for ticket 1", json);
    }

    [Fact]
    public async Task ExportCsv_HandlesSpecialCharacters()
    {
        // Arrange
        var tickets = new[]
        {
            new Ticket
            {
                Id = 1,
                Subject = "Subject with, comma",
                Description = "Description with \"quotes\" and\nnewlines",
                Status = TicketStatus.Open,
                Priority = TicketPriority.Medium,
                Email = "test@example.com",
                CreatedAt = DateTimeOffset.Now,
                UpdatedAt = DateTimeOffset.Now
            }
        };

        var outputFile = Path.Combine(_testOutputPath, "special_chars.csv");

        // Act
        await _exportService.ExportTicketsAsync(tickets, outputFile, "csv");

        // Assert
        Assert.True(File.Exists(outputFile));

        var lines = await File.ReadAllLinesAsync(outputFile);

        // Check that special characters are properly escaped
        Assert.Contains("\"Subject with, comma\"", lines[1]);
        Assert.Contains("\"Description with \"\"quotes\"\" and", lines[1]);
    }

    [Fact]
    public async Task ExportXml_HandlesSpecialCharacters()
    {
        // Arrange
        var tickets = new[]
        {
            new Ticket
            {
                Id = 1,
                Subject = "Subject with <tag> & special chars",
                Description = "Description with 'quotes' and \"double quotes\"",
                Status = TicketStatus.Open,
                Priority = TicketPriority.Medium,
                CreatedAt = DateTimeOffset.Now,
                UpdatedAt = DateTimeOffset.Now
            }
        };

        var outputFile = Path.Combine(_testOutputPath, "special_chars.xml");

        // Act
        await _exportService.ExportTicketsAsync(tickets, outputFile, "xml");

        // Assert
        Assert.True(File.Exists(outputFile));

        var xml = await File.ReadAllTextAsync(outputFile);
        var doc = XDocument.Parse(xml); // This will fail if XML is invalid

        // Check that special characters are properly escaped
        Assert.Contains("&lt;tag&gt;", xml);
        Assert.Contains("&amp;", xml);
        Assert.Contains("&quot;", xml);
        Assert.Contains("&apos;", xml);
    }

    [Fact]
    public async Task Export_CreatesDirectoryIfNotExists()
    {
        // Arrange
        var tickets = CreateTestTickets();
        var nestedPath = Path.Combine(_testOutputPath, "nested", "folder", "structure");
        var outputFile = Path.Combine(nestedPath, "tickets.json");

        // Act
        await _exportService.ExportTicketsAsync(tickets, outputFile, "json");

        // Assert
        Assert.True(Directory.Exists(nestedPath));
        Assert.True(File.Exists(outputFile));
    }

    [Fact]
    public async Task Export_ThrowsException_ForInvalidFormat()
    {
        // Arrange
        var tickets = CreateTestTickets();
        var outputFile = Path.Combine(_testOutputPath, "tickets.invalid");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _exportService.ExportTicketsAsync(tickets, outputFile, "invalid_format"));
    }

    [Fact]
    public async Task Export_ThrowsException_ForEmptyTicketArray()
    {
        // Arrange
        var tickets = Array.Empty<Ticket>();
        var outputFile = Path.Combine(_testOutputPath, "tickets.json");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _exportService.ExportTicketsAsync(tickets, outputFile, "json"));
    }

    private static Ticket[] CreateTestTickets()
    {
        return new[]
        {
            new Ticket
            {
                Id = 1,
                Subject = "Test Ticket 1",
                Description = "Description 1",
                Status = TicketStatus.Open,
                Priority = TicketPriority.High,
                Email = "test1@example.com",
                CreatedAt = new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero),
                UpdatedAt = new DateTimeOffset(2025, 1, 15, 11, 0, 0, TimeSpan.Zero),
                Tags = new[] { "tag1", "tag2" }
            },
            new Ticket
            {
                Id = 2,
                Subject = "Test Ticket 2",
                Description = "Description 2",
                Status = TicketStatus.Pending,
                Priority = TicketPriority.Medium,
                Email = "test2@example.com",
                CreatedAt = new DateTimeOffset(2025, 1, 14, 10, 0, 0, TimeSpan.Zero),
                UpdatedAt = new DateTimeOffset(2025, 1, 14, 15, 0, 0, TimeSpan.Zero)
            },
            new Ticket
            {
                Id = 3,
                Subject = "Test Ticket 3",
                Description = "Description 3",
                Status = TicketStatus.Resolved,
                Priority = TicketPriority.Low,
                Email = "test3@example.com",
                CreatedAt = new DateTimeOffset(2025, 1, 13, 10, 0, 0, TimeSpan.Zero),
                UpdatedAt = new DateTimeOffset(2025, 1, 13, 17, 0, 0, TimeSpan.Zero),
                DueBy = new DateTimeOffset(2025, 1, 20, 10, 0, 0, TimeSpan.Zero)
            }
        };
    }
}
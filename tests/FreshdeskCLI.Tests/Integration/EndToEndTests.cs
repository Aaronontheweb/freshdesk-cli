using System.Diagnostics;
using System.Text;
using System.Text.Json;
using FreshdeskCLI.Models;
using FreshdeskCLI.Services;

namespace FreshdeskCLI.Tests.Integration;

public class EndToEndTests : IDisposable
{
    private readonly string _testConfigPath;
    private readonly string _testDownloadPath;
    private readonly HttpClient _httpClient;
    private readonly MockFreshdeskServer _mockServer;

    public EndToEndTests()
    {
        _testConfigPath = Path.Combine(Path.GetTempPath(), $"freshdesk-test-{Guid.NewGuid()}");
        _testDownloadPath = Path.Combine(Path.GetTempPath(), $"freshdesk-downloads-{Guid.NewGuid()}");

        // Ensure directories are created before setting environment variable
        if (!Directory.Exists(_testConfigPath))
        {
            Directory.CreateDirectory(_testConfigPath);
        }
        if (!Directory.Exists(_testDownloadPath))
        {
            Directory.CreateDirectory(_testDownloadPath);
        }

        Environment.SetEnvironmentVariable("FRESHDESK_CONFIG_PATH", _testConfigPath);

        _mockServer = new MockFreshdeskServer();
        _httpClient = new HttpClient(_mockServer)
        {
            BaseAddress = new Uri("https://test.freshdesk.com")
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(_testConfigPath))
            Directory.Delete(_testConfigPath, true);
        if (Directory.Exists(_testDownloadPath))
            Directory.Delete(_testDownloadPath, true);

        Environment.SetEnvironmentVariable("FRESHDESK_CONFIG_PATH", null);
        _httpClient?.Dispose();
    }

    [Fact]
    public async Task ConfigureCommand_SavesConfiguration()
    {
        // Arrange
        var configService = new ConfigurationService();
        var config = new FreshdeskConfig
        {
            Domain = "testdomain",
            ApiKey = "test-api-key-12345",
            DefaultDownloadPath = _testDownloadPath,
            ProfileName = "default"
        };

        // Act
        await configService.SaveConfigAsync(config);
        var loaded = await configService.LoadConfigAsync();

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal("testdomain", loaded.Domain);
        Assert.Equal("test-api-key-12345", loaded.ApiKey);
        Assert.Equal(_testDownloadPath, loaded.DefaultDownloadPath);
    }

    [Fact]
    public async Task ListTickets_ReturnsAllTickets()
    {
        // Arrange
        var config = new FreshdeskConfig
        {
            Domain = "test",
            ApiKey = "test-api-key-12345"
        };

        var client = new FreshdeskApiClient(config, _httpClient);
        _mockServer.SetupTickets(new[]
        {
            new Ticket { Id = 1, Subject = "Test 1", Status = TicketStatus.Open },
            new Ticket { Id = 2, Subject = "Test 2", Status = TicketStatus.Closed }
        });

        // Act
        var tickets = await client.GetTicketsAsync();

        // Assert
        Assert.NotNull(tickets);
        Assert.Equal(2, tickets.Length);
        Assert.Equal("Test 1", tickets[0].Subject);
        Assert.Equal("Test 2", tickets[1].Subject);
    }

    [Fact]
    public async Task GetTicket_WithConversations_ReturnsFullThread()
    {
        // Arrange
        var config = new FreshdeskConfig
        {
            Domain = "test",
            ApiKey = "test-api-key-12345"
        };

        var client = new FreshdeskApiClient(config, _httpClient);

        var ticket = new Ticket
        {
            Id = 123,
            Subject = "Test Ticket",
            Description = "Original description",
            Status = TicketStatus.Open,
            Priority = TicketPriority.High,
            CreatedAt = DateTimeOffset.Now.AddDays(-2)
        };

        var conversations = new[]
        {
            new Conversation
            {
                Id = 1,
                Body = "Customer's first message",
                BodyText = "Customer's first message",
                Private = false,
                Incoming = true,
                CreatedAt = DateTimeOffset.Now.AddDays(-2)
            },
            new Conversation
            {
                Id = 2,
                Body = "Support agent's reply",
                BodyText = "Support agent's reply",
                Private = false,
                Incoming = false,
                CreatedAt = DateTimeOffset.Now.AddDays(-1)
            },
            new Conversation
            {
                Id = 3,
                Body = "Internal note",
                BodyText = "Internal note",
                Private = true,
                Incoming = false,
                CreatedAt = DateTimeOffset.Now.AddHours(-6)
            }
        };

        _mockServer.SetupTicket(ticket);
        _mockServer.SetupConversations(123, conversations);

        // Act
        var retrievedTicket = await client.GetTicketAsync(123);
        var retrievedConversations = await client.GetTicketConversationsAsync(123);

        // Assert
        Assert.NotNull(retrievedTicket);
        Assert.Equal(123, retrievedTicket.Id);
        Assert.Equal("Test Ticket", retrievedTicket.Subject);

        Assert.NotNull(retrievedConversations);
        Assert.Equal(3, retrievedConversations.Length);
        Assert.Equal("Customer's first message", retrievedConversations[0].BodyText);
        Assert.False(retrievedConversations[0].Private);
        Assert.True(retrievedConversations[0].Incoming);
        Assert.True(retrievedConversations[2].Private);
    }

    [Fact]
    public async Task CreateTicket_WithAttachment_Succeeds()
    {
        // Arrange
        var config = new FreshdeskConfig
        {
            Domain = "test",
            ApiKey = "test-api-key-12345"
        };

        var client = new FreshdeskApiClient(config, _httpClient);

        var newTicket = new Ticket
        {
            Subject = "New ticket with attachment",
            Description = "This ticket has an attachment",
            Email = "customer@example.com",
            Status = TicketStatus.Open,
            Priority = TicketPriority.Medium
        };

        var createdTicket = new Ticket
        {
            Id = 456,
            Subject = newTicket.Subject,
            Description = newTicket.Description,
            Email = newTicket.Email,
            Status = newTicket.Status,
            Priority = newTicket.Priority,
            CreatedAt = DateTimeOffset.Now,
            Attachments = new[]
            {
                new Attachment
                {
                    Id = 789,
                    Name = "test.pdf",
                    Size = 1024 * 500,
                    ContentType = "application/pdf",
                    AttachmentUrl = "https://test.freshdesk.com/attachments/789"
                }
            }
        };

        _mockServer.SetupCreateTicket(newTicket, createdTicket);

        // Act
        var result = await client.CreateTicketAsync(newTicket);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(456, result.Id);
        Assert.Equal("New ticket with attachment", result.Subject);
        Assert.NotNull(result.Attachments);
        Assert.Single(result.Attachments);
        Assert.Equal("test.pdf", result.Attachments[0].Name);
    }

    [Fact]
    public async Task UpdateTicket_ChangesStatus()
    {
        // Arrange
        var config = new FreshdeskConfig
        {
            Domain = "test",
            ApiKey = "test-api-key-12345"
        };

        var client = new FreshdeskApiClient(config, _httpClient);

        var updateData = new Ticket
        {
            Status = TicketStatus.Resolved,
            Priority = TicketPriority.Low
        };

        var updatedTicket = new Ticket
        {
            Id = 123,
            Subject = "Original Subject",
            Status = TicketStatus.Resolved,
            Priority = TicketPriority.Low,
            UpdatedAt = DateTimeOffset.Now
        };

        _mockServer.SetupUpdateTicket(123, updateData, updatedTicket);

        // Act
        var result = await client.UpdateTicketAsync(123, updateData);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(123, result.Id);
        Assert.Equal(TicketStatus.Resolved, result.Status);
        Assert.Equal(TicketPriority.Low, result.Priority);
    }

    [Fact]
    public async Task ReplyToTicket_AddsConversation()
    {
        // Arrange
        var config = new FreshdeskConfig
        {
            Domain = "test",
            ApiKey = "test-api-key-12345"
        };

        var client = new FreshdeskApiClient(config, _httpClient);

        var replyConversation = new Conversation
        {
            Body = "This is a reply to the ticket",
            Private = false
        };

        var createdConversation = new Conversation
        {
            Id = 999,
            Body = replyConversation.Body,
            BodyText = "This is a reply to the ticket",
            Private = false,
            Incoming = false,
            CreatedAt = DateTimeOffset.Now
        };

        _mockServer.SetupReplyToTicket(123, replyConversation, createdConversation);

        // Act
        var result = await client.ReplyToTicketAsync(123, "This is a reply to the ticket", false);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(999, result.Id);
        Assert.Equal("This is a reply to the ticket", result.BodyText);
        Assert.False(result.Private);
        Assert.False(result.Incoming);
    }

    [Fact]
    public async Task AddPrivateNote_AddsInternalNote()
    {
        // Arrange
        var config = new FreshdeskConfig
        {
            Domain = "test",
            ApiKey = "test-api-key-12345"
        };

        var client = new FreshdeskApiClient(config, _httpClient);

        var noteConversation = new Conversation
        {
            Body = "This is a private internal note",
            Private = true
        };

        var createdNote = new Conversation
        {
            Id = 1000,
            Body = noteConversation.Body,
            BodyText = "This is a private internal note",
            Private = true,
            Incoming = false,
            CreatedAt = DateTimeOffset.Now
        };

        _mockServer.SetupAddNoteToTicket(123, noteConversation, createdNote);

        // Act
        var result = await client.ReplyToTicketAsync(123, "This is a private internal note", true);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1000, result.Id);
        Assert.Equal("This is a private internal note", result.BodyText);
        Assert.True(result.Private);
        Assert.False(result.Incoming);
    }

    [Fact]
    public async Task DownloadAttachment_SavesFile()
    {
        // Arrange
        var config = new FreshdeskConfig
        {
            Domain = "test",
            ApiKey = "test-api-key-12345",
            DefaultDownloadPath = _testDownloadPath
        };

        var client = new FreshdeskApiClient(config, _httpClient);

        var attachmentUrl = "https://test.freshdesk.com/attachments/789";
        var fileContent = Encoding.UTF8.GetBytes("This is test file content");

        _mockServer.SetupAttachmentDownload(attachmentUrl, fileContent);

        // Act
        var content = await client.DownloadAttachmentAsync(attachmentUrl);

        // Save to file
        var filePath = Path.Combine(_testDownloadPath, "test.txt");
        await File.WriteAllBytesAsync(filePath, content);

        // Assert
        Assert.Equal(fileContent, content);
        Assert.True(File.Exists(filePath));
        var savedContent = await File.ReadAllBytesAsync(filePath);
        Assert.Equal(fileContent, savedContent);
    }

    [Fact]
    public async Task GetAllTickets_ReturnsAllTickets()
    {
        // Arrange
        var config = new FreshdeskConfig
        {
            Domain = "test",
            ApiKey = "test-api-key-12345"
        };

        var client = new FreshdeskApiClient(config, _httpClient);

        var allTickets = new[]
        {
            new Ticket { Id = 1, Subject = "Urgent issue", Status = TicketStatus.Open, Priority = TicketPriority.Urgent },
            new Ticket { Id = 2, Subject = "Normal issue", Status = TicketStatus.Open, Priority = TicketPriority.Medium },
            new Ticket { Id = 3, Subject = "Resolved issue", Status = TicketStatus.Resolved, Priority = TicketPriority.Low }
        };

        _mockServer.SetupTickets(allTickets);

        // Act
        var tickets = await client.GetTicketsAsync();

        // Assert
        Assert.NotNull(tickets);
        Assert.Equal(3, tickets.Length);
        Assert.Equal("Urgent issue", tickets[0].Subject);
        Assert.Equal("Normal issue", tickets[1].Subject);
        Assert.Equal("Resolved issue", tickets[2].Subject);
    }

    [Fact]
    public async Task BulkOperation_ProcessesMultipleTickets()
    {
        // Arrange
        var config = new FreshdeskConfig
        {
            Domain = "test",
            ApiKey = "test-api-key-12345"
        };

        var client = new FreshdeskApiClient(config, _httpClient);

        var ticketIds = new long[] { 1, 2, 3 };
        var updateData = new Ticket { Status = TicketStatus.Closed };

        foreach (var id in ticketIds)
        {
            _mockServer.SetupUpdateTicket(id, updateData, new Ticket
            {
                Id = id,
                Status = TicketStatus.Closed,
                UpdatedAt = DateTimeOffset.Now
            });
        }

        // Act
        var results = new List<Ticket>();
        foreach (var id in ticketIds)
        {
            var updated = await client.UpdateTicketAsync(id, updateData);
            results.Add(updated);
        }

        // Assert
        Assert.Equal(3, results.Count);
        Assert.All(results, t => Assert.Equal(TicketStatus.Closed, t.Status));
    }
}
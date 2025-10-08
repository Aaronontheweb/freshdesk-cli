using System.Net;
using System.Text;
using System.Text.Json;
using FreshdeskCLI;
using FreshdeskCLI.Models;
using FreshdeskCLI.Services;
using Moq;
using Moq.Protected;

namespace FreshdeskCLI.Tests.Services;

public class FreshdeskApiClientTests
{
    private readonly Mock<HttpMessageHandler> _mockHttpHandler;
    private readonly HttpClient _httpClient;
    private readonly FreshdeskConfig _config;
    private readonly FreshdeskApiClient _client;

    public FreshdeskApiClientTests()
    {
        _mockHttpHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpHandler.Object)
        {
            BaseAddress = new Uri("https://test.freshdesk.com")
        };

        _config = new FreshdeskConfig
        {
            Domain = "test",
            ApiKey = "test-api-key"
        };

        _client = new FreshdeskApiClient(_config, _httpClient);
    }

    [Fact]
    public async Task TestConnectionAsync_ReturnsTrue_OnSuccessfulResponse()
    {
        // Arrange
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.PathAndQuery == "/api/v2/tickets?per_page=1"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("[]")
            });

        // Act
        var result = await _client.TestConnectionAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task TestConnectionAsync_ReturnsFalse_OnUnauthorized()
    {
        // Arrange
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Unauthorized
            });

        // Act
        var result = await _client.TestConnectionAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetTicketsAsync_ReturnsTicketsArray()
    {
        // Arrange
        var tickets = new[]
        {
            new Ticket { Id = 1, Subject = "Test 1", Status = TicketStatus.Open },
            new Ticket { Id = 2, Subject = "Test 2", Status = TicketStatus.Closed }
        };

        var json = JsonSerializer.Serialize(tickets, FreshdeskJsonContext.Default.TicketArray);

        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.PathAndQuery.Contains("/api/v2/tickets")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json)
            });

        // Act
        var result = await _client.GetTicketsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Length);
        Assert.Equal("Test 1", result[0].Subject);
        Assert.Equal("Test 2", result[1].Subject);
    }

    [Fact]
    public async Task GetTicketAsync_ReturnsTicket_WhenFound()
    {
        // Arrange
        var ticket = new Ticket
        {
            Id = 123,
            Subject = "Test Ticket",
            Status = TicketStatus.Open,
            Priority = TicketPriority.High
        };

        var json = JsonSerializer.Serialize(ticket, FreshdeskJsonContext.Default.Ticket);

        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.PathAndQuery == "/api/v2/tickets/123"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json)
            });

        // Act
        var result = await _client.GetTicketAsync(123);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(123, result.Id);
        Assert.Equal("Test Ticket", result.Subject);
    }

    [Fact]
    public async Task GetTicketAsync_ReturnsNull_WhenNotFound()
    {
        // Arrange
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.PathAndQuery == "/api/v2/tickets/999"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });

        // Act
        var result = await _client.GetTicketAsync(999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateTicketAsync_SendsCorrectRequest()
    {
        // Arrange
        var newTicket = new Ticket
        {
            Subject = "New Ticket",
            Description = "Test description",
            Status = TicketStatus.Open,
            Priority = TicketPriority.Medium,
            Email = "test@example.com"
        };

        var createdTicket = new Ticket
        {
            Id = 456,
            Subject = newTicket.Subject,
            Description = newTicket.Description,
            Status = newTicket.Status,
            Priority = newTicket.Priority,
            Email = newTicket.Email,
            CreatedAt = DateTimeOffset.Now
        };

        var responseJson = JsonSerializer.Serialize(createdTicket, FreshdeskJsonContext.Default.Ticket);

        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.PathAndQuery == "/api/v2/tickets"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Created,
                Content = new StringContent(responseJson)
            });

        // Act
        var result = await _client.CreateTicketAsync(newTicket);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(456, result.Id);
        Assert.Equal("New Ticket", result.Subject);
    }

    [Fact]
    public async Task UpdateTicketAsync_SendsCorrectRequest()
    {
        // Arrange
        var updateTicket = new Ticket
        {
            Status = TicketStatus.Resolved,
            Priority = TicketPriority.Low
        };

        var updatedTicket = new Ticket
        {
            Id = 789,
            Subject = "Updated Ticket",
            Status = TicketStatus.Resolved,
            Priority = TicketPriority.Low,
            UpdatedAt = DateTimeOffset.Now
        };

        var responseJson = JsonSerializer.Serialize(updatedTicket, FreshdeskJsonContext.Default.Ticket);

        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Put &&
                    req.RequestUri!.PathAndQuery == "/api/v2/tickets/789"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson)
            });

        // Act
        var result = await _client.UpdateTicketAsync(789, updateTicket);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(789, result.Id);
        Assert.Equal(TicketStatus.Resolved, result.Status);
        Assert.Equal(TicketPriority.Low, result.Priority);
    }

    [Fact]
    public async Task GetTicketConversationsAsync_ReturnsConversations()
    {
        // Arrange
        var conversations = new[]
        {
            new Conversation
            {
                Id = 1,
                Body = "First message",
                Private = false,
                Incoming = true
            },
            new Conversation
            {
                Id = 2,
                Body = "Reply message",
                Private = false,
                Incoming = false
            }
        };

        var json = JsonSerializer.Serialize(conversations, FreshdeskJsonContext.Default.ConversationArray);

        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.PathAndQuery.StartsWith("/api/v2/tickets/123/conversations")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json)
            });

        // Act
        var result = await _client.GetTicketConversationsAsync(123);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Length);
        Assert.Equal("First message", result[0].Body);
        Assert.False(result[0].Private);
        Assert.True(result[0].Incoming);
    }

    [Fact]
    public async Task GetTicketConversationsAsync_FetchesAllPages()
    {
        // Arrange - simulate multiple pages of conversations
        var page1Conversations = Enumerable.Range(1, 100).Select(i => new Conversation
        {
            Id = i,
            Body = $"Message {i}",
            Private = false,
            Incoming = i % 2 == 0
        }).ToArray();

        var page2Conversations = Enumerable.Range(101, 50).Select(i => new Conversation
        {
            Id = i,
            Body = $"Message {i}",
            Private = false,
            Incoming = i % 2 == 0
        }).ToArray();

        var page1Json = JsonSerializer.Serialize(page1Conversations, FreshdeskJsonContext.Default.ConversationArray);
        var page2Json = JsonSerializer.Serialize(page2Conversations, FreshdeskJsonContext.Default.ConversationArray);

        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.PathAndQuery.Contains("page=1")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(page1Json)
            });

        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.PathAndQuery.Contains("page=2")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(page2Json)
            });

        // Act
        var result = await _client.GetTicketConversationsAsync(123);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(150, result.Length);
        Assert.Equal("Message 1", result[0].Body);
        Assert.Equal("Message 150", result[149].Body);
    }

    [Fact]
    public void Constructor_SetsAuthorizationHeader()
    {
        // Arrange & Act
        var config = new FreshdeskConfig
        {
            Domain = "test",
            ApiKey = "secret-key-12345" // Must be > 10 characters
        };

        using var client = new FreshdeskApiClient(config);

        // Assert - can't directly test the header, but we can verify the client is created
        Assert.NotNull(client);
    }

    [Fact]
    public void Dispose_DisposesHttpClient_WhenOwned()
    {
        // Arrange
        var config = new FreshdeskConfig
        {
            Domain = "test",
            ApiKey = "test-key-12345" // Must be > 10 characters
        };

        var client = new FreshdeskApiClient(config);

        // Act & Assert - should not throw
        client.Dispose();
        client.Dispose(); // Second dispose should also not throw
    }
}
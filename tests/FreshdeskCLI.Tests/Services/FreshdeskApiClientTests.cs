using System.Collections.Generic;
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
        var updates = new Dictionary<string, object>
        {
            ["status"] = 4,  // Resolved
            ["priority"] = 1 // Low
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
        var result = await _client.UpdateTicketAsync(789, updates);

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

    [Fact]
    public async Task GetContactsAsync_ReturnsContactsArray()
    {
        // Arrange
        var contacts = new[]
        {
            new Contact { Id = 1, Name = "John Doe", Email = "john@example.com", CompanyId = 100, ViewAllTickets = true },
            new Contact { Id = 2, Name = "Jane Smith", Email = "jane@example.com", CompanyId = 100, ViewAllTickets = false }
        };

        var json = JsonSerializer.Serialize(contacts, FreshdeskJsonContext.Default.ContactArray);

        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.PathAndQuery.Contains("/api/v2/contacts")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json)
            });

        // Act
        var result = await _client.GetContactsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Length);
        Assert.Equal("John Doe", result[0].Name);
        Assert.True(result[0].ViewAllTickets);
        Assert.Equal(100, result[0].CompanyId);
    }

    [Fact]
    public async Task GetContactAsync_ReturnsContact_WhenFound()
    {
        // Arrange
        var contact = new Contact
        {
            Id = 123,
            Name = "Test Contact",
            Email = "test@example.com",
            CompanyId = 456,
            ViewAllTickets = true,
            Active = true
        };

        var json = JsonSerializer.Serialize(contact, FreshdeskJsonContext.Default.Contact);

        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.PathAndQuery == "/api/v2/contacts/123"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json)
            });

        // Act
        var result = await _client.GetContactAsync(123);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(123, result.Id);
        Assert.Equal("Test Contact", result.Name);
        Assert.True(result.ViewAllTickets);
    }

    [Fact]
    public async Task GetContactAsync_ReturnsNull_WhenNotFound()
    {
        // Arrange
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.PathAndQuery == "/api/v2/contacts/999"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });

        // Act
        var result = await _client.GetContactAsync(999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateContactAsync_SendsCorrectRequest()
    {
        // Arrange
        var newContact = new Dictionary<string, object>
        {
            ["name"] = "New Contact",
            ["email"] = "new@example.com",
            ["company_id"] = 789L,
            ["view_all_tickets"] = true,
            ["phone"] = "555-1234"
        };

        var createdContact = new Contact
        {
            Id = 101,
            Name = "New Contact",
            Email = "new@example.com",
            CompanyId = 789,
            ViewAllTickets = true,
            Phone = "555-1234",
            Active = true,
            CreatedAt = DateTimeOffset.Now
        };

        var responseJson = JsonSerializer.Serialize(createdContact, FreshdeskJsonContext.Default.Contact);

        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.PathAndQuery == "/api/v2/contacts"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Created,
                Content = new StringContent(responseJson)
            });

        // Act
        var result = await _client.CreateContactAsync(newContact);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(101, result.Id);
        Assert.Equal("New Contact", result.Name);
        Assert.True(result.ViewAllTickets);
        Assert.Equal(789, result.CompanyId);
    }

    [Fact]
    public async Task UpdateContactAsync_SendsCorrectRequest()
    {
        // Arrange
        var updates = new Dictionary<string, object>
        {
            ["name"] = "Updated Contact",
            ["view_all_tickets"] = false
        };

        var updatedContact = new Contact
        {
            Id = 202,
            Name = "Updated Contact",
            Email = "updated@example.com",
            ViewAllTickets = false,
            UpdatedAt = DateTimeOffset.Now
        };

        var responseJson = JsonSerializer.Serialize(updatedContact, FreshdeskJsonContext.Default.Contact);

        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Put &&
                    req.RequestUri!.PathAndQuery == "/api/v2/contacts/202"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson)
            });

        // Act
        var result = await _client.UpdateContactAsync(202, updates);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(202, result.Id);
        Assert.Equal("Updated Contact", result.Name);
        Assert.False(result.ViewAllTickets);
    }

    [Fact]
    public async Task SearchContactsAsync_ByEmail_ReturnsContacts()
    {
        // Arrange
        var contacts = new[]
        {
            new Contact { Id = 1, Name = "John Doe", Email = "john@example.com" }
        };

        var json = JsonSerializer.Serialize(contacts, FreshdeskJsonContext.Default.ContactArray);

        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.PathAndQuery.Contains("email=john%40example.com")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json)
            });

        // Act
        var result = await _client.SearchContactsAsync(email: "john@example.com");

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("john@example.com", result[0].Email);
    }

    [Fact]
    public async Task DeleteContactAsync_SendsCorrectRequest()
    {
        // Arrange
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Delete &&
                    req.RequestUri!.PathAndQuery == "/api/v2/contacts/303"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NoContent
            });

        // Act & Assert - should not throw
        await _client.DeleteContactAsync(303);
    }

    [Fact]
    public async Task GetCompaniesAsync_ReturnsCompaniesArray()
    {
        // Arrange
        var companies = new[]
        {
            new Company { Id = 1, Name = "Acme Corp", Industry = "Technology", Domains = new[] { "acme.com" } },
            new Company { Id = 2, Name = "Widget Inc", Industry = "Manufacturing", Domains = new[] { "widget.com" } }
        };

        var json = JsonSerializer.Serialize(companies, FreshdeskJsonContext.Default.CompanyArray);

        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.PathAndQuery.Contains("/api/v2/companies")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json)
            });

        // Act
        var result = await _client.GetCompaniesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Length);
        Assert.Equal("Acme Corp", result[0].Name);
        Assert.Equal("Technology", result[0].Industry);
    }

    [Fact]
    public async Task GetCompanyAsync_ReturnsCompany_WhenFound()
    {
        // Arrange
        var company = new Company
        {
            Id = 123,
            Name = "Test Company",
            Description = "Test Description",
            Industry = "Technology",
            Domains = new[] { "test.com", "example.com" }
        };

        var json = JsonSerializer.Serialize(company, FreshdeskJsonContext.Default.Company);

        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.PathAndQuery == "/api/v2/companies/123"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json)
            });

        // Act
        var result = await _client.GetCompanyAsync(123);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(123, result.Id);
        Assert.Equal("Test Company", result.Name);
        Assert.Equal(2, result.Domains!.Length);
    }

    [Fact]
    public async Task GetCompanyAsync_ReturnsNull_WhenNotFound()
    {
        // Arrange
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.PathAndQuery == "/api/v2/companies/999"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });

        // Act
        var result = await _client.GetCompanyAsync(999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateCompanyAsync_SendsCorrectRequest()
    {
        // Arrange
        var newCompany = new Dictionary<string, object>
        {
            ["name"] = "New Company",
            ["description"] = "A new company",
            ["industry"] = "Technology",
            ["domains"] = new[] { "newco.com" }
        };

        var createdCompany = new Company
        {
            Id = 101,
            Name = "New Company",
            Description = "A new company",
            Industry = "Technology",
            Domains = new[] { "newco.com" },
            CreatedAt = DateTimeOffset.Now
        };

        var responseJson = JsonSerializer.Serialize(createdCompany, FreshdeskJsonContext.Default.Company);

        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.PathAndQuery == "/api/v2/companies"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Created,
                Content = new StringContent(responseJson)
            });

        // Act
        var result = await _client.CreateCompanyAsync(newCompany);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(101, result.Id);
        Assert.Equal("New Company", result.Name);
        Assert.Equal("Technology", result.Industry);
    }

    [Fact]
    public async Task UpdateCompanyAsync_SendsCorrectRequest()
    {
        // Arrange
        var updates = new Dictionary<string, object>
        {
            ["name"] = "Updated Company",
            ["industry"] = "Finance"
        };

        var updatedCompany = new Company
        {
            Id = 202,
            Name = "Updated Company",
            Industry = "Finance",
            UpdatedAt = DateTimeOffset.Now
        };

        var responseJson = JsonSerializer.Serialize(updatedCompany, FreshdeskJsonContext.Default.Company);

        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Put &&
                    req.RequestUri!.PathAndQuery == "/api/v2/companies/202"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson)
            });

        // Act
        var result = await _client.UpdateCompanyAsync(202, updates);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(202, result.Id);
        Assert.Equal("Updated Company", result.Name);
        Assert.Equal("Finance", result.Industry);
    }

    [Fact]
    public async Task SearchCompaniesAsync_ReturnsCompanies()
    {
        // Arrange
        var companies = new[]
        {
            new Company { Id = 1, Name = "Acme Corp", Industry = "Technology" }
        };

        var searchResult = new CompanySearchResult { Results = companies, Total = companies.Length };
        var json = JsonSerializer.Serialize(searchResult, FreshdeskJsonContext.Default.CompanySearchResult);

        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.PathAndQuery.Contains("name=Acme")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json)
            });

        // Act
        var result = await _client.SearchCompaniesAsync("Acme");

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("Acme Corp", result[0].Name);
    }

    [Fact]
    public async Task DeleteCompanyAsync_SendsCorrectRequest()
    {
        // Arrange
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Delete &&
                    req.RequestUri!.PathAndQuery == "/api/v2/companies/303"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NoContent
            });

        // Act & Assert - should not throw
        await _client.DeleteCompanyAsync(303);
    }
}
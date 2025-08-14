# Testing Strategy - Freshdesk CLI

## Overview
Since we cannot test against live Freshdesk APIs, we'll use mock responses from Freshdesk documentation and implement comprehensive offline testing.

## Test Data Sources

### 1. Freshdesk API Documentation Samples
Location: `tests/TestData/FreshdeskResponses/`

```
tests/TestData/
├── FreshdeskResponses/
│   ├── tickets_list.json          # GET /api/v2/tickets response
│   ├── ticket_detail.json         # GET /api/v2/tickets/{id}
│   ├── ticket_with_conversations.json
│   ├── search_results.json
│   ├── rate_limit_exceeded.json
│   ├── authentication_error.json
│   └── validation_error.json
├── MockConfigs/
│   ├── valid_config.json
│   ├── invalid_config.json
│   └── partial_config.json
└── AttachmentSamples/
    ├── small_file.txt
    ├── medium_file.pdf
    └── large_file.zip
```

### 2. Sample Response Collection

#### Ticket List Response
```json
// tests/TestData/FreshdeskResponses/tickets_list.json
[
  {
    "id": 1,
    "subject": "Sample ticket",
    "description": "<div>Sample description</div>",
    "description_text": "Sample description",
    "status": 2,
    "priority": 1,
    "source": 2,
    "requester_id": 1000000001,
    "responder_id": 1000000002,
    "group_id": 1000000003,
    "tags": ["sample", "test"],
    "cc_emails": ["cc@example.com"],
    "created_at": "2024-01-15T10:30:00Z",
    "updated_at": "2024-01-15T14:45:00Z",
    "due_by": "2024-01-17T17:00:00Z",
    "fr_due_by": "2024-01-16T12:00:00Z",
    "is_escalated": false,
    "deleted": false,
    "spam": false
  }
]
```

#### Ticket with Conversations
```json
// tests/TestData/FreshdeskResponses/ticket_with_conversations.json
{
  "id": 1,
  "subject": "Sample ticket with attachments",
  "status": 2,
  "attachments": [
    {
      "id": 1001,
      "name": "screenshot.png",
      "content_type": "image/png",
      "size": 45678,
      "created_at": "2024-01-15T10:30:00Z",
      "updated_at": "2024-01-15T10:30:00Z",
      "attachment_url": "https://cdn.freshdesk.com/attachments/1001/screenshot.png"
    }
  ],
  "conversations": [
    {
      "id": 2001,
      "user_id": 1000000001,
      "body": "<div>Customer message</div>",
      "body_text": "Customer message",
      "incoming": true,
      "private": false,
      "source": 0,
      "created_at": "2024-01-15T11:00:00Z",
      "attachments": [
        {
          "id": 1002,
          "name": "document.pdf",
          "content_type": "application/pdf",
          "size": 123456,
          "attachment_url": "https://cdn.freshdesk.com/attachments/1002/document.pdf"
        }
      ]
    }
  ]
}
```

## Testing Approaches

### 1. Unit Testing with Mocked HTTP

#### HttpClient Mocking Strategy
```csharp
public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, HttpResponseMessage> _responses;
    
    public MockHttpMessageHandler()
    {
        _responses = new Dictionary<string, HttpResponseMessage>();
        LoadTestData();
    }
    
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, 
        CancellationToken cancellationToken)
    {
        var key = $"{request.Method} {request.RequestUri?.PathAndQuery}";
        
        if (_responses.TryGetValue(key, out var response))
        {
            // Clone response to avoid disposal issues
            return Task.FromResult(CloneResponse(response));
        }
        
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
    
    private void LoadTestData()
    {
        // Load JSON files and create responses
        _responses["GET /api/v2/tickets?page=1&per_page=100"] = 
            CreateJsonResponse("tickets_list.json");
        _responses["GET /api/v2/tickets/1"] = 
            CreateJsonResponse("ticket_detail.json");
        // ... more responses
    }
}
```

### 2. AOT Compatibility Testing

#### AOT Validation Tests
```csharp
[TestClass]
public class AotCompatibilityTests
{
    [TestMethod]
    public void JsonContext_IncludesAllModels()
    {
        // Verify all models are registered
        var context = FreshdeskJsonContext.Default;
        
        Assert.IsNotNull(context.Ticket);
        Assert.IsNotNull(context.TicketArray);
        Assert.IsNotNull(context.Conversation);
        Assert.IsNotNull(context.Attachment);
        Assert.IsNotNull(context.ConfigFile);
        // ... verify all types
    }
    
    [TestMethod]
    public void NoReflectionUsage()
    {
        // Use Roslyn analyzers to detect reflection
        // This would be a compile-time check
    }
    
    [TestMethod]
    public void TrimmingCompatible()
    {
        // Verify no trimming warnings
        // Run: dotnet publish -c Release /p:TrimmerSingleWarn=false
    }
}
```

### 3. Integration Testing with Fake Server

#### Local Test Server
```csharp
public class FakeFreshdeskServer : IDisposable
{
    private readonly WebApplication _app;
    private readonly string _baseUrl;
    
    public FakeFreshdeskServer()
    {
        var builder = WebApplication.CreateBuilder();
        _app = builder.Build();
        
        ConfigureEndpoints();
        _app.Start();
        _baseUrl = _app.Urls.First();
    }
    
    private void ConfigureEndpoints()
    {
        _app.MapGet("/api/v2/tickets", async (int? page, int? per_page) =>
        {
            var json = await File.ReadAllTextAsync("TestData/tickets_list.json");
            return Results.Content(json, "application/json");
        });
        
        _app.MapGet("/api/v2/tickets/{id}", async (long id) =>
        {
            if (id == 1)
            {
                var json = await File.ReadAllTextAsync("TestData/ticket_detail.json");
                return Results.Content(json, "application/json");
            }
            return Results.NotFound();
        });
        
        // Rate limiting simulation
        _app.MapGet("/api/v2/rate_limit_test", () =>
        {
            return Results.Content(
                File.ReadAllText("TestData/rate_limit_exceeded.json"),
                "application/json",
                statusCode: 429)
                .WithHeader("Retry-After", "60");
        });
    }
    
    public string BaseUrl => _baseUrl;
    
    public void Dispose() => _app?.DisposeAsync().GetAwaiter().GetResult();
}
```

### 4. CLI Command Testing

#### Command Line Testing
```csharp
[TestClass]
public class CommandLineTests
{
    [TestMethod]
    public async Task TicketsList_ParsesArgumentsCorrectly()
    {
        var command = new RootCommand();
        // ... setup command structure
        
        var result = await command.InvokeAsync("tickets list --status open --page 2");
        
        Assert.AreEqual(0, result);
        // Verify correct parameters were parsed
    }
    
    [TestMethod]
    public async Task InvalidCommand_ShowsHelp()
    {
        var output = new StringWriter();
        var command = new RootCommand();
        
        var result = await command.InvokeAsync("invalid", output);
        
        Assert.AreNotEqual(0, result);
        Assert.IsTrue(output.ToString().Contains("Usage:"));
    }
}
```

### 5. Configuration Testing

#### Config Manager Tests
```csharp
[TestClass]
public class ConfigManagerTests
{
    private string _tempConfigPath;
    
    [TestInitialize]
    public void Setup()
    {
        _tempConfigPath = Path.Combine(Path.GetTempPath(), ".freshdesk", "config.json");
    }
    
    [TestMethod]
    public void SaveConfig_SetsCorrectPermissions()
    {
        var config = new ConfigFile 
        { 
            Domain = "test.freshdesk.com",
            ApiKey = "test_key_123"
        };
        
        var manager = new ConfigManager(_tempConfigPath);
        manager.Save(config);
        
        if (!OperatingSystem.IsWindows())
        {
            var fileInfo = new UnixFileInfo(_tempConfigPath);
            Assert.AreEqual(
                UnixFileMode.UserRead | UnixFileMode.UserWrite,
                fileInfo.FileAccessPermissions);
        }
    }
    
    [TestMethod]
    public void LoadConfig_EnvironmentOverridesFile()
    {
        Environment.SetEnvironmentVariable("FRESHDESK_DOMAIN", "env.freshdesk.com");
        Environment.SetEnvironmentVariable("FRESHDESK_API_KEY", "env_key");
        
        var manager = new ConfigManager(_tempConfigPath);
        var config = manager.Load();
        
        Assert.AreEqual("env.freshdesk.com", config.Domain);
        Assert.AreEqual("env_key", config.ApiKey);
    }
}
```

## Test Data Management

### 1. Test Data Generator
```csharp
public static class TestDataGenerator
{
    public static Ticket CreateTicket(int id = 1, int status = 2)
    {
        return new Ticket
        {
            Id = id,
            Subject = $"Test Ticket {id}",
            Description = $"<p>Description for ticket {id}</p>",
            DescriptionText = $"Description for ticket {id}",
            Status = status,
            Priority = Random.Shared.Next(1, 5),
            CreatedAt = DateTimeOffset.Now.AddDays(-Random.Shared.Next(1, 30)),
            UpdatedAt = DateTimeOffset.Now
        };
    }
    
    public static Attachment CreateAttachment(string name, long size)
    {
        return new Attachment
        {
            Id = Random.Shared.NextInt64(1000, 9999),
            Name = name,
            ContentType = GetContentType(name),
            Size = size,
            AttachmentUrl = $"https://cdn.test.com/attachments/{name}",
            CreatedAt = DateTimeOffset.Now
        };
    }
}
```

### 2. Response Builder
```csharp
public class ResponseBuilder
{
    public HttpResponseMessage BuildTicketListResponse(
        int page = 1, 
        int perPage = 100, 
        int totalTickets = 250)
    {
        var tickets = Enumerable.Range(1, perPage)
            .Select(i => TestDataGenerator.CreateTicket(i + (page - 1) * perPage))
            .ToArray();
        
        var json = JsonSerializer.Serialize(
            tickets, 
            FreshdeskJsonContext.Default.TicketArray);
        
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        
        // Add rate limit headers
        response.Headers.Add("X-Ratelimit-Total", "700");
        response.Headers.Add("X-Ratelimit-Remaining", "650");
        response.Headers.Add("X-Ratelimit-Used-CurrentRequest", "1");
        
        return response;
    }
}
```

## Performance Testing

### 1. Benchmark Tests
```csharp
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class SerializationBenchmarks
{
    private string _ticketJson;
    private Ticket _ticket;
    
    [GlobalSetup]
    public void Setup()
    {
        _ticket = TestDataGenerator.CreateTicket();
        _ticketJson = JsonSerializer.Serialize(
            _ticket, 
            FreshdeskJsonContext.Default.Ticket);
    }
    
    [Benchmark]
    public Ticket Deserialize()
    {
        return JsonSerializer.Deserialize(
            _ticketJson, 
            FreshdeskJsonContext.Default.Ticket)!;
    }
    
    [Benchmark]
    public string Serialize()
    {
        return JsonSerializer.Serialize(
            _ticket, 
            FreshdeskJsonContext.Default.Ticket);
    }
}
```

### 2. Load Testing
```csharp
[TestClass]
public class LoadTests
{
    [TestMethod]
    public async Task HandlesConcurrentRequests()
    {
        var server = new FakeFreshdeskServer();
        var client = new FreshdeskClient(server.BaseUrl, "test_key");
        
        var tasks = Enumerable.Range(1, 100)
            .Select(i => client.GetTicketAsync(i % 10 + 1));
        
        var results = await Task.WhenAll(tasks);
        
        Assert.AreEqual(100, results.Length);
        Assert.IsTrue(results.All(r => r != null));
    }
}
```

## Continuous Testing

### 1. GitHub Actions Workflow
```yaml
name: Test

on: [push, pull_request]

jobs:
  test:
    runs-on: ${{ matrix.os }}
    strategy:
      matrix:
        os: [ubuntu-latest, windows-latest, macos-latest]
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '8.0.x'
    
    - name: Restore
      run: dotnet restore
    
    - name: Build
      run: dotnet build --no-restore
    
    - name: Test
      run: dotnet test --no-build --verbosity normal
    
    - name: AOT Publish Test
      run: |
        dotnet publish -c Release -r linux-x64 --self-contained
        ./bin/Release/net8.0/linux-x64/publish/freshdesk --version
```

### 2. Pre-commit Hooks
```bash
#!/bin/bash
# .git/hooks/pre-commit

# Run tests
dotnet test --no-build

# Check AOT compatibility
dotnet publish -c Release /p:TrimmerSingleWarn=false /p:WarnOnTrimmerWarnings=true

# Check formatting
dotnet format --verify-no-changes
```

## Test Coverage Goals

### Minimum Coverage Requirements
- **Overall**: 80%
- **Critical Paths**: 95%
  - Authentication
  - API calls
  - Error handling
  - Config management
- **Models**: 100%
- **Commands**: 90%
- **Services**: 85%

### Coverage Reporting
```bash
# Install coverage tool
dotnet tool install --global dotnet-coverage

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Generate report
reportgenerator -reports:coverage.xml -targetdir:coveragereport
```
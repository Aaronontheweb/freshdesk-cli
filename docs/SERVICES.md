# Services Implementation Guide

## Overview
Core services for the Freshdesk CLI, handling API communication, rate limiting, configuration, and attachments.

## Service Architecture

### Dependency Injection Setup
```csharp
// Program.cs service registration
var services = new ServiceCollection();

services.AddSingleton<IConfigManager, ConfigManager>();
services.AddSingleton<FreshdeskJsonContext>();
services.AddHttpClient<IFreshdeskClient, FreshdeskClient>()
    .AddHttpMessageHandler<RateLimitHandler>()
    .AddHttpMessageHandler<AuthenticationHandler>()
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        MaxConnectionsPerServer = 10
    });

services.AddSingleton<IAttachmentDownloader, AttachmentDownloader>();
services.AddSingleton<IRateLimitTracker, RateLimitTracker>();
```

## Core Services

### Services/FreshdeskClient.cs
```csharp
namespace FreshdeskCLI.Services;

public interface IFreshdeskClient
{
    Task<TicketListResponse> GetTicketsAsync(
        int page = 1, 
        int perPage = 100,
        TicketStatus? status = null,
        CancellationToken cancellationToken = default);
    
    Task<Ticket> GetTicketAsync(
        long ticketId, 
        bool includeConversations = false,
        CancellationToken cancellationToken = default);
    
    Task<SearchResponse> SearchTicketsAsync(
        string query, 
        int page = 1,
        CancellationToken cancellationToken = default);
    
    Task<Ticket> UpdateTicketAsync(
        long ticketId, 
        UpdateTicketRequest request,
        CancellationToken cancellationToken = default);
    
    Task<Conversation> ReplyToTicketAsync(
        long ticketId, 
        ReplyRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class FreshdeskClient : IFreshdeskClient
{
    private readonly HttpClient _httpClient;
    private readonly FreshdeskJsonContext _jsonContext;
    private readonly ILogger<FreshdeskClient> _logger;
    
    public FreshdeskClient(
        HttpClient httpClient, 
        FreshdeskJsonContext jsonContext,
        ILogger<FreshdeskClient> logger)
    {
        _httpClient = httpClient;
        _jsonContext = jsonContext;
        _logger = logger;
    }
    
    public async Task<TicketListResponse> GetTicketsAsync(
        int page = 1, 
        int perPage = 100,
        TicketStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string>
        {
            ["page"] = page.ToString(),
            ["per_page"] = Math.Min(perPage, 100).ToString()
        };
        
        if (status.HasValue)
        {
            query["filter"] = $"status:{(int)status.Value}";
        }
        
        var url = QueryHelpers.AddQueryString("/api/v2/tickets", query);
        
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var tickets = JsonSerializer.Deserialize(json, _jsonContext.TicketArray)!;
        
        return new TicketListResponse
        {
            Tickets = tickets,
            Page = page,
            PerPage = perPage,
            Total = tickets.Length // Note: API doesn't provide total count
        };
    }
    
    public async Task<Ticket> GetTicketAsync(
        long ticketId, 
        bool includeConversations = false,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/v2/tickets/{ticketId}";
        if (includeConversations)
        {
            url += "?include=conversations";
        }
        
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new TicketNotFoundException(ticketId);
        }
        
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        
        if (includeConversations)
        {
            return JsonSerializer.Deserialize(json, _jsonContext.TicketDetailResponse)!;
        }
        
        return JsonSerializer.Deserialize(json, _jsonContext.Ticket)!;
    }
    
    public async Task<SearchResponse> SearchTicketsAsync(
        string query, 
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        // Note: Search API has limitations - max 300 results, 30 per page
        var url = $"/api/v2/search/tickets?query={Uri.EscapeDataString(query)}";
        
        if (page > 1)
        {
            // Calculate offset for pagination (30 items per page)
            var offset = (page - 1) * 30;
            if (offset >= 300)
            {
                _logger.LogWarning("Search API limit reached (300 results max)");
                return new SearchResponse { Results = [], Total = 0 };
            }
        }
        
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize(json, _jsonContext.SearchResponse)!;
    }
    
    public async Task<Ticket> UpdateTicketAsync(
        long ticketId, 
        UpdateTicketRequest request,
        CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(request, _jsonContext.UpdateTicketRequest);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        using var response = await _httpClient.PutAsync(
            $"/api/v2/tickets/{ticketId}", 
            content, 
            cancellationToken);
        
        response.EnsureSuccessStatusCode();
        
        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize(responseJson, _jsonContext.Ticket)!;
    }
}
```

### Services/ConfigManager.cs
```csharp
namespace FreshdeskCLI.Services;

public interface IConfigManager
{
    ConfigFile? Load();
    void Save(ConfigFile config);
    void Delete();
    bool Exists();
    void SetCredentials(string domain, string apiKey);
    void ClearCredentials();
}

public sealed class ConfigManager : IConfigManager
{
    private readonly string _configDirectory;
    private readonly string _configPath;
    private readonly FreshdeskJsonContext _jsonContext;
    private readonly ILogger<ConfigManager> _logger;
    
    public ConfigManager(FreshdeskJsonContext jsonContext, ILogger<ConfigManager> logger)
    {
        _jsonContext = jsonContext;
        _logger = logger;
        
        _configDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".freshdesk");
        
        _configPath = Path.Combine(_configDirectory, "config.json");
    }
    
    public ConfigFile? Load()
    {
        // 1. Check environment variables (highest priority)
        var envDomain = Environment.GetEnvironmentVariable("FRESHDESK_DOMAIN");
        var envApiKey = Environment.GetEnvironmentVariable("FRESHDESK_API_KEY");
        
        if (!string.IsNullOrEmpty(envDomain) && !string.IsNullOrEmpty(envApiKey))
        {
            _logger.LogDebug("Using credentials from environment variables");
            return new ConfigFile
            {
                Domain = envDomain,
                ApiKey = envApiKey,
                DefaultDownloadPath = Environment.GetEnvironmentVariable("FRESHDESK_DOWNLOAD_PATH")
            };
        }
        
        // 2. Check config file
        if (!File.Exists(_configPath))
        {
            _logger.LogDebug("No config file found at {Path}", _configPath);
            return null;
        }
        
        try
        {
            var json = File.ReadAllText(_configPath);
            var config = JsonSerializer.Deserialize(json, _jsonContext.ConfigFile);
            
            if (config == null || !config.IsValid())
            {
                _logger.LogWarning("Invalid config file");
                return null;
            }
            
            _logger.LogDebug("Loaded config from {Path}", _configPath);
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load config file");
            return null;
        }
    }
    
    public void Save(ConfigFile config)
    {
        if (!config.IsValid())
        {
            throw new ArgumentException("Invalid configuration");
        }
        
        // Create directory if it doesn't exist
        Directory.CreateDirectory(_configDirectory);
        
        // Serialize config
        var json = JsonSerializer.Serialize(config, _jsonContext.ConfigFile);
        
        // Write with restricted permissions
        File.WriteAllText(_configPath, json);
        
        // Set file permissions (Unix only)
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(_configPath, 
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        
        _logger.LogInformation("Configuration saved to {Path}", _configPath);
    }
    
    public void SetCredentials(string domain, string apiKey)
    {
        // Normalize domain
        if (!domain.StartsWith("http"))
        {
            domain = $"https://{domain}";
        }
        
        if (!domain.Contains(".freshdesk.com"))
        {
            domain = domain.Replace(".com", ".freshdesk.com");
        }
        
        var config = Load() ?? new ConfigFile();
        config.Domain = domain;
        config.ApiKey = apiKey;
        
        Save(config);
    }
    
    public void ClearCredentials()
    {
        if (File.Exists(_configPath))
        {
            // Overwrite with empty data before deleting (security)
            File.WriteAllText(_configPath, "{}");
            File.Delete(_configPath);
            _logger.LogInformation("Credentials cleared");
        }
    }
    
    public void Delete() => ClearCredentials();
    
    public bool Exists() => File.Exists(_configPath);
}
```

### Services/RateLimitHandler.cs
```csharp
namespace FreshdeskCLI.Services;

public sealed class RateLimitHandler : DelegatingHandler
{
    private readonly IRateLimitTracker _tracker;
    private readonly ILogger<RateLimitHandler> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    
    public RateLimitHandler(IRateLimitTracker tracker, ILogger<RateLimitHandler> logger)
    {
        _tracker = tracker;
        _logger = logger;
    }
    
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, 
        CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            // Check if we should throttle
            var info = _tracker.GetCurrentInfo();
            if (info?.ShouldThrottle == true)
            {
                var delay = info.TimeUntilReset ?? TimeSpan.FromSeconds(60);
                _logger.LogWarning("Rate limit approaching, waiting {Delay}s", delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
            
            var response = await base.SendAsync(request, cancellationToken);
            
            // Update rate limit info from headers
            UpdateRateLimitInfo(response);
            
            // Handle rate limit exceeded
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var retryAfter = GetRetryAfter(response);
                _logger.LogWarning("Rate limit exceeded, retry after {Seconds}s", retryAfter);
                
                await Task.Delay(TimeSpan.FromSeconds(retryAfter), cancellationToken);
                
                // Retry the request
                request = CloneRequest(request);
                response = await base.SendAsync(request, cancellationToken);
            }
            
            return response;
        }
        finally
        {
            _semaphore.Release();
        }
    }
    
    private void UpdateRateLimitInfo(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("X-Ratelimit-Total", out var total) &&
            response.Headers.TryGetValues("X-Ratelimit-Remaining", out var remaining))
        {
            var info = new RateLimitInfo
            {
                Total = int.Parse(total.First()),
                Remaining = int.Parse(remaining.First()),
                ResetTime = DateTimeOffset.Now.AddHours(1) // Freshdesk resets hourly
            };
            
            if (response.Headers.TryGetValues("X-Ratelimit-Used-CurrentRequest", out var used))
            {
                info.UsedCurrentRequest = int.Parse(used.First());
            }
            
            _tracker.Update(info);
            
            _logger.LogDebug("Rate limit: {Remaining}/{Total}", info.Remaining, info.Total);
        }
    }
    
    private int GetRetryAfter(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter != null)
        {
            if (response.Headers.RetryAfter.Delta.HasValue)
            {
                return (int)response.Headers.RetryAfter.Delta.Value.TotalSeconds;
            }
            
            if (response.Headers.RetryAfter.Date.HasValue)
            {
                var delay = response.Headers.RetryAfter.Date.Value - DateTimeOffset.Now;
                return Math.Max(1, (int)delay.TotalSeconds);
            }
        }
        
        return 60; // Default to 60 seconds
    }
    
    private static HttpRequestMessage CloneRequest(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        
        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        
        if (request.Content != null)
        {
            // Note: This assumes content can be read multiple times
            // In production, might need to buffer content
            clone.Content = request.Content;
        }
        
        return clone;
    }
}

public interface IRateLimitTracker
{
    void Update(RateLimitInfo info);
    RateLimitInfo? GetCurrentInfo();
    void Reset();
}

public sealed class RateLimitTracker : IRateLimitTracker
{
    private RateLimitInfo? _currentInfo;
    private readonly object _lock = new();
    
    public void Update(RateLimitInfo info)
    {
        lock (_lock)
        {
            _currentInfo = info;
        }
    }
    
    public RateLimitInfo? GetCurrentInfo()
    {
        lock (_lock)
        {
            return _currentInfo;
        }
    }
    
    public void Reset()
    {
        lock (_lock)
        {
            _currentInfo = null;
        }
    }
}
```

### Services/AttachmentDownloader.cs
```csharp
namespace FreshdeskCLI.Services;

public interface IAttachmentDownloader
{
    Task<DownloadResult> DownloadAttachmentsAsync(
        Ticket ticket,
        string outputPath,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    Task<DownloadResult> DownloadAttachmentAsync(
        Attachment attachment,
        string outputPath,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

public sealed class AttachmentDownloader : IAttachmentDownloader
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AttachmentDownloader> _logger;
    private readonly int _maxConcurrent;
    
    public AttachmentDownloader(
        HttpClient httpClient, 
        ILogger<AttachmentDownloader> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _maxConcurrent = configuration.GetValue("MaxConcurrentDownloads", 3);
    }
    
    public async Task<DownloadResult> DownloadAttachmentsAsync(
        Ticket ticket,
        string outputPath,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Collect all attachments from ticket and conversations
        var attachments = new List<Attachment>();
        attachments.AddRange(ticket.Attachments);
        
        if (ticket.Conversations != null)
        {
            foreach (var conversation in ticket.Conversations)
            {
                attachments.AddRange(conversation.Attachments);
            }
        }
        
        if (attachments.Count == 0)
        {
            _logger.LogInformation("No attachments found for ticket {Id}", ticket.Id);
            return new DownloadResult { Success = true };
        }
        
        // Create output directory
        var ticketDir = Path.Combine(outputPath, $"ticket_{ticket.Id}");
        Directory.CreateDirectory(ticketDir);
        
        // Download with concurrency control
        using var semaphore = new SemaphoreSlim(_maxConcurrent, _maxConcurrent);
        var results = new List<FileDownloadResult>();
        var totalBytes = attachments.Sum(a => a.Size);
        var downloadedBytes = 0L;
        var progressLock = new object();
        
        var tasks = attachments.Select(async attachment =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var result = await DownloadSingleAttachment(
                    attachment, 
                    ticketDir,
                    (bytes) =>
                    {
                        lock (progressLock)
                        {
                            downloadedBytes += bytes;
                            progress?.Report(new DownloadProgress
                            {
                                TotalFiles = attachments.Count,
                                CompletedFiles = results.Count(r => r.Success),
                                TotalBytes = totalBytes,
                                DownloadedBytes = downloadedBytes,
                                CurrentFile = attachment.Name
                            });
                        }
                    },
                    cancellationToken);
                
                lock (progressLock)
                {
                    results.Add(result);
                }
                
                return result;
            }
            finally
            {
                semaphore.Release();
            }
        });
        
        await Task.WhenAll(tasks);
        
        return new DownloadResult
        {
            Success = results.All(r => r.Success),
            DownloadedFiles = results.Where(r => r.Success).Select(r => r.FilePath).ToArray(),
            FailedFiles = results.Where(r => !r.Success).Select(r => r.FileName).ToArray(),
            TotalBytes = results.Sum(r => r.BytesDownloaded)
        };
    }
    
    private async Task<FileDownloadResult> DownloadSingleAttachment(
        Attachment attachment,
        string outputDir,
        Action<long>? progressCallback,
        CancellationToken cancellationToken)
    {
        var fileName = SanitizeFileName(attachment.Name);
        var filePath = Path.Combine(outputDir, fileName);
        
        // Check if file already exists with same size
        if (File.Exists(filePath))
        {
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length == attachment.Size)
            {
                _logger.LogDebug("Skipping {Name}, already downloaded", attachment.Name);
                return new FileDownloadResult
                {
                    Success = true,
                    FileName = fileName,
                    FilePath = filePath,
                    BytesDownloaded = attachment.Size
                };
            }
        }
        
        try
        {
            using var response = await _httpClient.GetAsync(
                attachment.AttachmentUrl, 
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            
            response.EnsureSuccessStatusCode();
            
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var fileStream = new FileStream(
                filePath, 
                FileMode.Create, 
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920, // 80KB buffer
                useAsync: true);
            
            var buffer = new byte[8192];
            var totalRead = 0L;
            int read;
            
            while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                totalRead += read;
                progressCallback?.Invoke(read);
            }
            
            _logger.LogInformation("Downloaded {Name} ({Size})", 
                attachment.Name, 
                attachment.FormattedSize);
            
            return new FileDownloadResult
            {
                Success = true,
                FileName = fileName,
                FilePath = filePath,
                BytesDownloaded = totalRead
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download {Name}", attachment.Name);
            
            // Clean up partial file
            if (File.Exists(filePath))
            {
                try { File.Delete(filePath); } catch { }
            }
            
            return new FileDownloadResult
            {
                Success = false,
                FileName = fileName,
                Error = ex.Message
            };
        }
    }
    
    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", fileName.Split(invalid));
        
        // Limit length
        if (sanitized.Length > 255)
        {
            var extension = Path.GetExtension(sanitized);
            var name = Path.GetFileNameWithoutExtension(sanitized);
            sanitized = name.Substring(0, 255 - extension.Length) + extension;
        }
        
        return sanitized;
    }
}

public sealed class DownloadResult
{
    public bool Success { get; set; }
    public string[] DownloadedFiles { get; set; } = [];
    public string[] FailedFiles { get; set; } = [];
    public long TotalBytes { get; set; }
}

public sealed class FileDownloadResult
{
    public bool Success { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public long BytesDownloaded { get; set; }
    public string? Error { get; set; }
}

public sealed class DownloadProgress
{
    public int TotalFiles { get; set; }
    public int CompletedFiles { get; set; }
    public long TotalBytes { get; set; }
    public long DownloadedBytes { get; set; }
    public string CurrentFile { get; set; } = string.Empty;
    
    public double PercentComplete => TotalBytes > 0 
        ? (double)DownloadedBytes / TotalBytes * 100 
        : 0;
}
```

### Services/AuthenticationHandler.cs
```csharp
namespace FreshdeskCLI.Services;

public sealed class AuthenticationHandler : DelegatingHandler
{
    private readonly IConfigManager _configManager;
    private readonly ILogger<AuthenticationHandler> _logger;
    private string? _authHeader;
    
    public AuthenticationHandler(
        IConfigManager configManager, 
        ILogger<AuthenticationHandler> logger)
    {
        _configManager = configManager;
        _logger = logger;
    }
    
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, 
        CancellationToken cancellationToken)
    {
        // Load config if not cached
        if (_authHeader == null)
        {
            var config = _configManager.Load();
            if (config == null || !config.IsValid())
            {
                throw new AuthenticationException(
                    "Not authenticated. Run 'freshdesk auth login' first.");
            }
            
            // Create Basic auth header (API key as username, "X" as password)
            var authBytes = Encoding.UTF8.GetBytes($"{config.ApiKey}:X");
            _authHeader = $"Basic {Convert.ToBase64String(authBytes)}";
            
            // Set base address if not set
            if (request.RequestUri != null && !request.RequestUri.IsAbsoluteUri)
            {
                var baseUrl = config.GetBaseUrl();
                request.RequestUri = new Uri(new Uri(baseUrl), request.RequestUri);
            }
        }
        
        // Add authentication header
        request.Headers.Authorization = 
            System.Net.Http.Headers.AuthenticationHeaderValue.Parse(_authHeader);
        
        var response = await base.SendAsync(request, cancellationToken);
        
        // Handle authentication errors
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            _authHeader = null; // Clear cached header
            throw new AuthenticationException(
                "Authentication failed. Check your API key.");
        }
        
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new AuthorizationException(
                "Access denied. Check your permissions.");
        }
        
        return response;
    }
}
```

## Exception Handling

### Services/Exceptions.cs
```csharp
namespace FreshdeskCLI.Services.Exceptions;

public class FreshdeskException : Exception
{
    public FreshdeskException(string message) : base(message) { }
    public FreshdeskException(string message, Exception inner) : base(message, inner) { }
}

public class AuthenticationException : FreshdeskException
{
    public AuthenticationException(string message) : base(message) { }
}

public class AuthorizationException : FreshdeskException
{
    public AuthorizationException(string message) : base(message) { }
}

public class TicketNotFoundException : FreshdeskException
{
    public long TicketId { get; }
    
    public TicketNotFoundException(long ticketId) 
        : base($"Ticket {ticketId} not found")
    {
        TicketId = ticketId;
    }
}

public class RateLimitException : FreshdeskException
{
    public int RetryAfterSeconds { get; }
    
    public RateLimitException(int retryAfter) 
        : base($"Rate limit exceeded. Retry after {retryAfter} seconds")
    {
        RetryAfterSeconds = retryAfter;
    }
}
```

## Service Testing

### Mock Implementations
```csharp
public class MockFreshdeskClient : IFreshdeskClient
{
    private readonly Dictionary<long, Ticket> _tickets = new();
    
    public Task<Ticket> GetTicketAsync(
        long ticketId, 
        bool includeConversations = false,
        CancellationToken cancellationToken = default)
    {
        if (_tickets.TryGetValue(ticketId, out var ticket))
        {
            return Task.FromResult(ticket);
        }
        
        throw new TicketNotFoundException(ticketId);
    }
    
    // ... other methods
}
```
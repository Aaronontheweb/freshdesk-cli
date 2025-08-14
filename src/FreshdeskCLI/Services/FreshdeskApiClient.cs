using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FreshdeskCLI.Models;

namespace FreshdeskCLI.Services;

public interface IFreshdeskApiClient
{
    Task<Ticket[]> GetTicketsAsync(int page = 1, int perPage = 30, CancellationToken cancellationToken = default);
    Task<Ticket?> GetTicketAsync(long ticketId, CancellationToken cancellationToken = default);
    Task<Ticket> CreateTicketAsync(Ticket ticket, CancellationToken cancellationToken = default);
    Task<Ticket> UpdateTicketAsync(long ticketId, Ticket ticket, CancellationToken cancellationToken = default);
    Task<Conversation[]> GetTicketConversationsAsync(long ticketId, CancellationToken cancellationToken = default);
    Task<Conversation> ReplyToTicketAsync(long ticketId, string body, bool isPrivate = false, CancellationToken cancellationToken = default);
    Task<Stream> DownloadAttachmentAsync(string attachmentUrl, CancellationToken cancellationToken = default);
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
}

public sealed class FreshdeskApiClient : IFreshdeskApiClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly FreshdeskConfig _config;
    private readonly bool _ownsHttpClient;

    public FreshdeskApiClient(FreshdeskConfig config) : this(config, null)
    {
    }

    public FreshdeskApiClient(FreshdeskConfig config, HttpClient? httpClient)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (!config.IsValid)
            throw new ArgumentException("Invalid Freshdesk configuration", nameof(config));

        _config = config;

        if (httpClient != null)
        {
            _httpClient = httpClient;
            _ownsHttpClient = false;
        }
        else
        {
            var handler = new RateLimitHandler(new HttpClientHandler());
            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri(_config.ApiV2Url)
            };
            _ownsHttpClient = true;
        }

        ConfigureHttpClient();
    }

    private void ConfigureHttpClient()
    {
        _httpClient.DefaultRequestHeaders.Clear();

        // Freshdesk uses Basic Auth with API key as username and 'X' as password
        var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_config.ApiKey}:X"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);

        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("FreshdeskCLI/1.0");
    }

    public async Task<Ticket[]> GetTicketsAsync(int page = 1, int perPage = 30, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"/api/v2/tickets?page={page}&per_page={perPage}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize(json, FreshdeskJsonContext.Default.TicketArray) ?? [];
    }

    public async Task<Ticket?> GetTicketAsync(long ticketId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"/api/v2/tickets/{ticketId}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize(json, FreshdeskJsonContext.Default.Ticket);
    }

    public async Task<Ticket> CreateTicketAsync(Ticket ticket, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var json = JsonSerializer.Serialize(ticket, FreshdeskJsonContext.Default.Ticket);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("/api/v2/tickets", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize(responseJson, FreshdeskJsonContext.Default.Ticket)!;
    }

    public async Task<Ticket> UpdateTicketAsync(long ticketId, Ticket ticket, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var json = JsonSerializer.Serialize(ticket, FreshdeskJsonContext.Default.Ticket);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PutAsync($"/api/v2/tickets/{ticketId}", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize(responseJson, FreshdeskJsonContext.Default.Ticket)!;
    }

    public async Task<Conversation[]> GetTicketConversationsAsync(long ticketId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"/api/v2/tickets/{ticketId}/conversations", cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize(json, FreshdeskJsonContext.Default.ConversationArray) ?? [];
    }

    public async Task<Conversation> ReplyToTicketAsync(long ticketId, string body, bool isPrivate = false, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(body);

        // Create a dictionary for the reply since we don't have a specific model
        var reply = new Dictionary<string, object>
        {
            ["body"] = body,
            ["private"] = isPrivate
        };

        var json = JsonSerializer.Serialize(reply, FreshdeskJsonContext.Default.DictionaryStringObject);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"/api/v2/tickets/{ticketId}/reply", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize(responseJson, FreshdeskJsonContext.Default.Conversation)!;
    }

    public async Task<Stream> DownloadAttachmentAsync(string attachmentUrl, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(attachmentUrl);

        // Attachments require authentication too
        var response = await _httpClient.GetAsync(attachmentUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStreamAsync(cancellationToken);
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to get tickets with limit of 1 to test the connection
            var response = await _httpClient.GetAsync("/api/v2/tickets?per_page=1", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient?.Dispose();
        }
    }

    public async Task<byte[]> DownloadAttachmentAsync(string attachmentUrl)
    {
        try
        {
            var response = await _httpClient.GetAsync(attachmentUrl);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync();
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Failed to download attachment: {ex.Message}", ex);
        }
    }

    public async Task<Ticket> UploadAttachmentAsync(long ticketId, string filePath, string? fileName = null)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        fileName ??= Path.GetFileName(filePath);
        var fileBytes = await File.ReadAllBytesAsync(filePath);
        
        // Freshdesk expects attachments to be added when updating the ticket
        using var content = new MultipartFormDataContent();
        
        // Add the file
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("form-data")
        {
            Name = "\"attachments[]\"",
            FileName = $"\"{fileName}\""
        };
        content.Add(fileContent);

        try
        {
            // Use PUT to update ticket with attachment
            var response = await _httpClient.PutAsync($"api/v2/tickets/{ticketId}", content);
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync();
            var ticket = JsonSerializer.Deserialize(json, FreshdeskJsonContext.Default.Ticket);
            
            return ticket ?? throw new InvalidOperationException("No ticket returned from API");
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Failed to upload attachment: {ex.Message}", ex);
        }
    }

    private static string GetMimeType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            ".json" => "application/json",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".zip" => "application/zip",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".csv" => "text/csv",
            ".xml" => "application/xml",
            _ => "application/octet-stream"
        };
    }
}
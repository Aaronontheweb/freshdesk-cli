using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Linq;
using FreshdeskCLI.Models;

namespace FreshdeskCLI.Services;

public interface IFreshdeskApiClient
{
    Task<Ticket[]> GetTicketsAsync(int page = 1, int perPage = 30, TicketStatus? status = null, string? email = null, CancellationToken cancellationToken = default);
    Task<Ticket?> GetTicketAsync(long ticketId, CancellationToken cancellationToken = default);
    Task<Ticket> CreateTicketAsync(Ticket ticket, CancellationToken cancellationToken = default);
    Task<Ticket> UpdateTicketAsync(long ticketId, Ticket ticket, CancellationToken cancellationToken = default);
    Task<Conversation[]> GetTicketConversationsAsync(long ticketId, CancellationToken cancellationToken = default);
    Task<Conversation> ReplyToTicketAsync(long ticketId, string body, bool isPrivate = false, CancellationToken cancellationToken = default);
    Task<byte[]> DownloadAttachmentAsync(string attachmentUrl, CancellationToken cancellationToken = default);
    Task<byte[]> DownloadAttachmentByIdAsync(long attachmentId, CancellationToken cancellationToken = default);
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);

    Task<Ticket[]> SearchTicketsAsync(string query, int page = 1, CancellationToken cancellationToken = default);

    Task<Contact[]> GetContactsAsync(int page = 1, int limit = 30, CancellationToken cancellationToken = default);
    Task<Contact?> GetContactAsync(long id, CancellationToken cancellationToken = default);
    Task<Contact> CreateContactAsync(Dictionary<string, object> contactData, CancellationToken cancellationToken = default);
    Task<Contact> UpdateContactAsync(long id, Dictionary<string, object> updates, CancellationToken cancellationToken = default);
    Task<Contact[]> SearchContactsAsync(string? email = null, string? phone = null, CancellationToken cancellationToken = default);
    Task DeleteContactAsync(long id, CancellationToken cancellationToken = default);

    Task<Company[]> GetCompaniesAsync(int page = 1, int limit = 30, CancellationToken cancellationToken = default);
    Task<Company?> GetCompanyAsync(long id, CancellationToken cancellationToken = default);
    Task<Company> CreateCompanyAsync(Dictionary<string, object> companyData, CancellationToken cancellationToken = default);
    Task<Company> UpdateCompanyAsync(long id, Dictionary<string, object> updates, CancellationToken cancellationToken = default);
    Task<Company[]> SearchCompaniesAsync(string name, CancellationToken cancellationToken = default);
    Task DeleteCompanyAsync(long id, CancellationToken cancellationToken = default);
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

    public async Task<Ticket[]> GetTicketsAsync(int page = 1, int perPage = 30, TicketStatus? status = null, string? email = null, CancellationToken cancellationToken = default)
    {
        long? requesterId = null;

        if (!string.IsNullOrWhiteSpace(email))
        {
            var contacts = await SearchContactsByEmailAsync(email, cancellationToken);
            if (contacts.Length > 0)
            {
                requesterId = contacts[0].Id;
            }
            else
            {
                return [];
            }
        }

        // Use search API for status filtering
        if (status.HasValue)
        {
            var query = $"status:{(int)status.Value}";
            var endpoint = $"/api/v2/search/tickets?query=\"{Uri.EscapeDataString(query)}\"&page={page}";

            var response = await _httpClient.GetAsync(endpoint, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var searchResult = JsonSerializer.Deserialize(json, FreshdeskJsonContext.Default.TicketSearchResult);
            var searchTickets = searchResult?.Results ?? [];

            // Apply requester filter client-side if needed
            if (requesterId.HasValue)
            {
                searchTickets = searchTickets.Where(t => t.RequesterId == requesterId.Value).ToArray();
            }

            return searchTickets;
        }

        // Use regular tickets endpoint with requester_id parameter
        var queryParams = new List<string>
        {
            $"page={page}",
            $"per_page={perPage}"
        };

        if (requesterId.HasValue)
        {
            queryParams.Add($"requester_id={requesterId.Value}");
        }

        var ticketsResponse = await _httpClient.GetAsync($"/api/v2/tickets?{string.Join("&", queryParams)}", cancellationToken);
        ticketsResponse.EnsureSuccessStatusCode();

        var ticketsJson = await ticketsResponse.Content.ReadAsStringAsync(cancellationToken);
        var tickets = JsonSerializer.Deserialize(ticketsJson, FreshdeskJsonContext.Default.TicketArray) ?? [];

        return tickets;
    }

    private async Task<Contact[]> SearchContactsByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"/api/v2/contacts?email={Uri.EscapeDataString(email)}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return [];

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize(json, FreshdeskJsonContext.Default.ContactArray) ?? [];
    }

    public async Task<Contact?> GetContactAsync(long contactId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"/api/v2/contacts/{contactId}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize(json, FreshdeskJsonContext.Default.Contact);
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
        var allConversations = new List<Conversation>();
        var page = 1;
        const int perPage = 100;

        while (true)
        {
            var endpoint = $"/api/v2/tickets/{ticketId}/conversations?page={page}&per_page={perPage}";
            var response = await _httpClient.GetAsync(endpoint, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var conversations = JsonSerializer.Deserialize(json, FreshdeskJsonContext.Default.ConversationArray) ?? [];

            if (conversations.Length == 0)
                break;

            allConversations.AddRange(conversations);

            if (conversations.Length < perPage)
                break;

            page++;
        }

        return [.. allConversations];
    }

    public async Task<Conversation> ReplyToTicketAsync(long ticketId, string body, bool isPrivate = false, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(body);

        var endpoint = isPrivate ? $"/api/v2/tickets/{ticketId}/notes" : $"/api/v2/tickets/{ticketId}/reply";

        var requestBody = new Dictionary<string, object>
        {
            ["body"] = body
        };

        if (isPrivate)
        {
            requestBody["private"] = true;
        }

        var json = JsonSerializer.Serialize(requestBody, FreshdeskJsonContext.Default.DictionaryStringObject);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize(responseJson, FreshdeskJsonContext.Default.Conversation)!;
    }

    public async Task<byte[]> DownloadAttachmentAsync(string attachmentUrl, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(attachmentUrl);

        // Check if this is an S3 URL or attachment ID
        if (attachmentUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            // Try the S3 URL first (it might still be valid)
            try
            {
                var response = await _httpClient.GetAsync(attachmentUrl, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsByteArrayAsync(cancellationToken);
                }
            }
            catch { }

            // If S3 URL fails, extract attachment ID and use API endpoint
            var match = System.Text.RegularExpressions.Regex.Match(attachmentUrl, @"/attachments/production/(\d+)/");
            if (match.Success && long.TryParse(match.Groups[1].Value, out var attachmentId))
            {
                return await DownloadAttachmentByIdAsync(attachmentId, cancellationToken);
            }

            // Last resort: try the URL as-is
            var finalResponse = await _httpClient.GetAsync(attachmentUrl, cancellationToken);
            finalResponse.EnsureSuccessStatusCode();
            return await finalResponse.Content.ReadAsByteArrayAsync(cancellationToken);
        }
        else if (long.TryParse(attachmentUrl, out var id))
        {
            // It's an attachment ID
            return await DownloadAttachmentByIdAsync(id, cancellationToken);
        }
        else
        {
            throw new ArgumentException($"Invalid attachment URL or ID: {attachmentUrl}");
        }
    }

    public async Task<byte[]> DownloadAttachmentByIdAsync(long attachmentId, CancellationToken cancellationToken = default)
    {
        // Use the direct attachment API endpoint
        var requestUrl = $"/api/v2/attachments/{attachmentId}";
        var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
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

    public async Task<Ticket[]> SearchTicketsAsync(string query, int page = 1, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var endpoint = $"/api/v2/search/tickets?query=\"{Uri.EscapeDataString(query)}\"&page={page}";
        var response = await _httpClient.GetAsync(endpoint, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var searchResult = JsonSerializer.Deserialize(json, FreshdeskJsonContext.Default.TicketSearchResult);
        return searchResult?.Results ?? [];
    }

    public async Task<Contact[]> GetContactsAsync(int page = 1, int limit = 30, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"/api/v2/contacts?page={page}&per_page={limit}", cancellationToken);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize(json, FreshdeskJsonContext.Default.ContactArray) ?? [];
    }

    public async Task<Contact> CreateContactAsync(Dictionary<string, object> contactData, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contactData);
        var json = JsonSerializer.Serialize(contactData, FreshdeskJsonContext.Default.DictionaryStringObject);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("/api/v2/contacts", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"Failed to create contact. Status: {response.StatusCode}, Error: {errorBody}");
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize(responseJson, FreshdeskJsonContext.Default.Contact)!;
    }

    public async Task<Contact> UpdateContactAsync(long id, Dictionary<string, object> updates, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(updates);
        var json = JsonSerializer.Serialize(updates, FreshdeskJsonContext.Default.DictionaryStringObject);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PutAsync($"/api/v2/contacts/{id}", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"Failed to update contact. Status: {response.StatusCode}, Error: {errorBody}");
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize(responseJson, FreshdeskJsonContext.Default.Contact)!;
    }

    public async Task<Contact[]> SearchContactsAsync(string? email = null, string? phone = null, CancellationToken cancellationToken = default)
    {
        var queryParams = new List<string>();
        if (!string.IsNullOrWhiteSpace(email))
            queryParams.Add($"email={Uri.EscapeDataString(email)}");
        if (!string.IsNullOrWhiteSpace(phone))
            queryParams.Add($"phone={Uri.EscapeDataString(phone)}");

        if (queryParams.Count == 0)
            return [];

        var response = await _httpClient.GetAsync($"/api/v2/contacts?{string.Join("&", queryParams)}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return [];

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize(json, FreshdeskJsonContext.Default.ContactArray) ?? [];
    }

    public async Task DeleteContactAsync(long id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"/api/v2/contacts/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Company[]> GetCompaniesAsync(int page = 1, int limit = 30, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"/api/v2/companies?page={page}&per_page={limit}", cancellationToken);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize(json, FreshdeskJsonContext.Default.CompanyArray) ?? [];
    }

    public async Task<Company?> GetCompanyAsync(long id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"/api/v2/companies/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize(json, FreshdeskJsonContext.Default.Company);
    }

    public async Task<Company> CreateCompanyAsync(Dictionary<string, object> companyData, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(companyData);
        var json = JsonSerializer.Serialize(companyData, FreshdeskJsonContext.Default.DictionaryStringObject);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("/api/v2/companies", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"Failed to create company. Status: {response.StatusCode}, Error: {errorBody}");
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize(responseJson, FreshdeskJsonContext.Default.Company)!;
    }

    public async Task<Company> UpdateCompanyAsync(long id, Dictionary<string, object> updates, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(updates);
        var json = JsonSerializer.Serialize(updates, FreshdeskJsonContext.Default.DictionaryStringObject);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PutAsync($"/api/v2/companies/{id}", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"Failed to update company. Status: {response.StatusCode}, Error: {errorBody}");
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize(responseJson, FreshdeskJsonContext.Default.Company)!;
    }

    public async Task<Company[]> SearchCompaniesAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var response = await _httpClient.GetAsync($"/api/v2/companies/autocomplete?name={Uri.EscapeDataString(name)}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return [];

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize(json, FreshdeskJsonContext.Default.CompanyArray) ?? [];
    }

    public async Task DeleteCompanyAsync(long id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"/api/v2/companies/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
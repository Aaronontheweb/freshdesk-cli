using System.Net;
using System.Text;
using System.Text.Json;
using FreshdeskCLI;
using FreshdeskCLI.Models;

namespace FreshdeskCLI.Tests.Integration;

public class MockFreshdeskServer : HttpMessageHandler
{
    private readonly Dictionary<string, object> _responses = new();
    private readonly Dictionary<string, HttpStatusCode> _statusCodes = new();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.PathAndQuery ?? "";
        var method = request.Method.ToString();
        var key = $"{method}:{path}";

        // Check for query parameters
        if (path.Contains('?'))
        {
            var basePath = path.Split('?')[0];
            var query = path.Split('?')[1];

            // Try with full path first, then without query
            if (!_responses.ContainsKey(key))
            {
                key = $"{method}:{basePath}";
            }
        }

        if (_responses.TryGetValue(key, out var responseData))
        {
            var statusCode = _statusCodes.TryGetValue(key, out var code) ? code : HttpStatusCode.OK;

            string json;
            if (responseData is byte[] bytes)
            {
                // For binary data like attachments
                return Task.FromResult(new HttpResponseMessage(statusCode)
                {
                    Content = new ByteArrayContent(bytes)
                });
            }
            else if (responseData is Ticket ticket)
            {
                json = JsonSerializer.Serialize(ticket, FreshdeskJsonContext.Default.Ticket);
            }
            else if (responseData is Ticket[] tickets)
            {
                json = JsonSerializer.Serialize(tickets, FreshdeskJsonContext.Default.TicketArray);
            }
            else if (responseData is Conversation conversation)
            {
                json = JsonSerializer.Serialize(conversation, FreshdeskJsonContext.Default.Conversation);
            }
            else if (responseData is Conversation[] conversations)
            {
                json = JsonSerializer.Serialize(conversations, FreshdeskJsonContext.Default.ConversationArray);
            }
            else
            {
                json = responseData.ToString() ?? "{}";
            }

            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }

        // Default 404 response
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    public void SetupTickets(Ticket[] tickets)
    {
        _responses["GET:/api/v2/tickets"] = tickets;
        _statusCodes["GET:/api/v2/tickets"] = HttpStatusCode.OK;
    }

    public void SetupTicketsWithQuery(string query, Ticket[] tickets)
    {
        _responses[$"GET:/api/v2/tickets?{query}"] = tickets;
        _statusCodes[$"GET:/api/v2/tickets?{query}"] = HttpStatusCode.OK;
    }

    public void SetupTicket(Ticket ticket)
    {
        _responses[$"GET:/api/v2/tickets/{ticket.Id}"] = ticket;
        _statusCodes[$"GET:/api/v2/tickets/{ticket.Id}"] = HttpStatusCode.OK;
    }

    public void SetupConversations(long ticketId, Conversation[] conversations)
    {
        _responses[$"GET:/api/v2/tickets/{ticketId}/conversations"] = conversations;
        _statusCodes[$"GET:/api/v2/tickets/{ticketId}/conversations"] = HttpStatusCode.OK;
    }

    public void SetupCreateTicket(Ticket newTicket, Ticket createdTicket)
    {
        _responses["POST:/api/v2/tickets"] = createdTicket;
        _statusCodes["POST:/api/v2/tickets"] = HttpStatusCode.Created;
    }

    public void SetupUpdateTicket(long ticketId, Ticket updateData, Ticket updatedTicket)
    {
        _responses[$"PUT:/api/v2/tickets/{ticketId}"] = updatedTicket;
        _statusCodes[$"PUT:/api/v2/tickets/{ticketId}"] = HttpStatusCode.OK;
    }

    public void SetupReplyToTicket(long ticketId, Conversation reply, Conversation createdReply)
    {
        _responses[$"POST:/api/v2/tickets/{ticketId}/reply"] = createdReply;
        _statusCodes[$"POST:/api/v2/tickets/{ticketId}/reply"] = HttpStatusCode.Created;
    }

    public void SetupAttachmentDownload(string url, byte[] content)
    {
        var uri = new Uri(url);
        var path = uri.PathAndQuery;
        _responses[$"GET:{path}"] = content;
        _statusCodes[$"GET:{path}"] = HttpStatusCode.OK;
    }

    public void SetupError(string path, HttpStatusCode statusCode, string? message = null)
    {
        var key = $"GET:{path}";
        _responses[key] = message ?? "Error";
        _statusCodes[key] = statusCode;
    }

    public void Reset()
    {
        _responses.Clear();
        _statusCodes.Clear();
    }
}
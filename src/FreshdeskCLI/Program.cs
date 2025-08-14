using System.Text.Json;
using FreshdeskCLI;
using FreshdeskCLI.Models;
using FreshdeskCLI.Helpers;

// For now, use a simpler approach without System.CommandLine's complex features
// System.CommandLine is AOT-compatible but the beta API is still evolving

// Check for read-only mode flag
bool isReadOnly = false;
var argsList = args.ToList();
if (argsList.Contains("--read-only") || argsList.Contains("-ro"))
{
    isReadOnly = true;
    // Remove the flag from args for further processing
    argsList.RemoveAll(a => a == "--read-only" || a == "-ro");
    args = argsList.ToArray();
}

if (args.Length == 0)
{
    ShowHelp();
    return 0;
}

// Handle special flags
if (args[0] == "--version" || args[0] == "-v")
{
    Console.WriteLine("Freshdesk CLI v1.0.0");
    Console.WriteLine("Built with .NET 9 AOT compilation");
    return 0;
}

if (args[0] == "--test-aot")
{
    TestAotCompatibility();
    return 0;
}

if (args[0] == "--help" || args[0] == "-h")
{
    ShowHelp();
    return 0;
}

// Route to appropriate command handler
try
{
    if (isReadOnly)
    {
        Console.WriteLine("🔒 Running in READ-ONLY mode. All write operations are disabled.");
    }
    return await RouteCommand(args, isReadOnly);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}

static void ShowHelp()
{
    Console.WriteLine("Freshdesk CLI - Command-line interface for Freshdesk API");
    Console.WriteLine();
    Console.WriteLine("Usage: freshdesk <command> [options]");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  config set        Set configuration values");
    Console.WriteLine("  config get        Get current configuration");
    Console.WriteLine("  config test       Test connection to Freshdesk API");
    Console.WriteLine("  ticket list       List tickets");
    Console.WriteLine("  ticket get        Get ticket details");
    Console.WriteLine("  ticket create     Create a new ticket");
    Console.WriteLine("  ticket update     Update ticket status/priority");
    Console.WriteLine("  ticket search     Search tickets");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --version, -v      Show version information");
    Console.WriteLine("  --help, -h         Show this help message");
    Console.WriteLine("  --read-only, -ro   Run in read-only mode (no writes/updates)");
}

static async Task<int> RouteCommand(string[] args, bool isReadOnly = false)
{
    if (args.Length < 1)
    {
        ShowHelp();
        return 1;
    }

    return args[0].ToLowerInvariant() switch
    {
        "config" => await HandleConfigCommand(args[1..], isReadOnly),
        "ticket" => await HandleTicketCommand(args[1..], isReadOnly),
        _ => ShowUnknownCommand(args[0])
    };
}

static int ShowUnknownCommand(string command)
{
    Console.Error.WriteLine($"Unknown command: {command}");
    Console.Error.WriteLine("Run 'freshdesk --help' for usage information.");
    return 1;
}

static int ShowReadOnlyError(string operation)
{
    Console.Error.WriteLine($"❌ Operation '{operation}' is not allowed in read-only mode.");
    Console.Error.WriteLine("Remove --read-only flag to perform write operations.");
    return 1;
}

static async Task<int> HandleConfigCommand(string[] args, bool isReadOnly = false)
{
    if (args.Length < 1)
    {
        Console.WriteLine("Usage: freshdesk config <subcommand>");
        Console.WriteLine("  set     Set configuration values");
        Console.WriteLine("  get     Get current configuration");
        Console.WriteLine("  test    Test connection to Freshdesk API");
        return 1;
    }

    var configService = new FreshdeskCLI.Services.ConfigurationService();

    return args[0].ToLowerInvariant() switch
    {
        "set" => isReadOnly ? ShowReadOnlyError("config set") : await HandleConfigSet(args[1..], configService),
        "get" => await HandleConfigGet(configService),
        "test" => await HandleConfigTest(configService),
        _ => ShowUnknownCommand($"config {args[0]}")
    };
}

static async Task<int> HandleConfigSet(string[] args, FreshdeskCLI.Services.ConfigurationService configService)
{
    string? domain = null;
    string? apiKey = null;

    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--domain":
            case "-d":
                if (i + 1 < args.Length)
                    domain = args[++i];
                break;
            case "--api-key":
            case "-k":
                if (i + 1 < args.Length)
                    apiKey = args[++i];
                break;
        }
    }

    if (string.IsNullOrEmpty(domain) && string.IsNullOrEmpty(apiKey))
    {
        Console.WriteLine("Usage: freshdesk config set [options]");
        Console.WriteLine("Options:");
        Console.WriteLine("  --domain, -d <domain>    Freshdesk domain");
        Console.WriteLine("  --api-key, -k <key>      Freshdesk API key");
        return 1;
    }

    var config = await configService.LoadConfigAsync() ?? new FreshdeskCLI.Models.FreshdeskConfig();

    if (!string.IsNullOrEmpty(domain))
        config.Domain = domain;

    if (!string.IsNullOrEmpty(apiKey))
        config.ApiKey = apiKey;

    await configService.SaveConfigAsync(config);
    Console.WriteLine("Configuration saved successfully");
    return 0;
}

static async Task<int> HandleConfigGet(FreshdeskCLI.Services.ConfigurationService configService)
{
    var config = await configService.LoadConfigAsync();

    if (config == null)
    {
        Console.WriteLine("No configuration found. Use 'freshdesk config set' to configure.");
        return 1;
    }

    Console.WriteLine($"Domain: {config.Domain}");
    Console.WriteLine($"API Key: {(string.IsNullOrEmpty(config.ApiKey) ? "(not set)" : "****" + config.ApiKey[^Math.Min(4, config.ApiKey.Length)..])}");
    Console.WriteLine($"Base URL: {config.BaseUrl}");
    return 0;
}

static async Task<int> HandleConfigTest(FreshdeskCLI.Services.ConfigurationService configService)
{
    var config = await configService.LoadConfigAsync();

    if (config == null || !config.IsValid)
    {
        Console.WriteLine("Invalid or missing configuration. Use 'freshdesk config set' to configure.");
        return 1;
    }

    Console.WriteLine($"Testing connection to {config.BaseUrl}...");

    using var client = new FreshdeskCLI.Services.FreshdeskApiClient(config);
    var success = await client.TestConnectionAsync();

    if (success)
    {
        Console.WriteLine("✓ Connection successful!");
        return 0;
    }
    else
    {
        Console.WriteLine("✗ Connection failed. Please check your configuration.");
        return 1;
    }
}

static async Task<int> HandleTicketCommand(string[] args, bool isReadOnly = false)
{
    if (args.Length < 1)
    {
        Console.WriteLine("Usage: freshdesk ticket <subcommand>");
        Console.WriteLine("  list      List tickets");
        Console.WriteLine("  get       Get ticket details");
        Console.WriteLine("  create    Create a new ticket");
        Console.WriteLine("  update    Update ticket status/priority");
        Console.WriteLine("  search    Search tickets");
        return 1;
    }

    var configService = new FreshdeskCLI.Services.ConfigurationService();
    var config = await configService.LoadConfigAsync();

    if (config == null || !config.IsValid)
    {
        Console.WriteLine("Invalid or missing configuration. Use 'freshdesk config set' to configure.");
        return 1;
    }

    using var client = new FreshdeskCLI.Services.FreshdeskApiClient(config);

    return args[0].ToLowerInvariant() switch
    {
        "list" => await HandleTicketList(args[1..], client),
        "get" => await HandleTicketGet(args[1..], client),
        "create" => isReadOnly ? ShowReadOnlyError("ticket create") : await HandleTicketCreate(args[1..], client),
        "update" => isReadOnly ? ShowReadOnlyError("ticket update") : await HandleTicketUpdate(args[1..], client),
        "search" => await HandleTicketSearch(args[1..], client),
        _ => ShowUnknownCommand($"ticket {args[0]}")
    };
}

static async Task<int> HandleTicketList(string[] args, FreshdeskCLI.Services.FreshdeskApiClient client)
{
    int page = 1;
    int limit = 30;
    string format = "table";

    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--page":
            case "-p":
                if (i + 1 < args.Length && int.TryParse(args[++i], out var p))
                    page = p;
                break;
            case "--limit":
            case "-l":
                if (i + 1 < args.Length && int.TryParse(args[++i], out var l))
                    limit = l;
                break;
            case "--format":
            case "-f":
                if (i + 1 < args.Length)
                    format = args[++i];
                break;
        }
    }

    var tickets = await client.GetTicketsAsync(page, limit);
    OutputFormatter.PrintTickets(tickets, format);

    if (format.Equals("table", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine($"Page {page}, showing {tickets.Length} tickets");
    }

    return 0;
}

static async Task<int> HandleTicketGet(string[] args, FreshdeskCLI.Services.FreshdeskApiClient client)
{
    if (args.Length < 1 || !long.TryParse(args[0], out var ticketId))
    {
        Console.WriteLine("Usage: freshdesk ticket get <ticket-id> [--format json|text]");
        return 1;
    }

    string format = "text";
    for (int i = 1; i < args.Length; i++)
    {
        if ((args[i] == "--format" || args[i] == "-f") && i + 1 < args.Length)
        {
            format = args[++i];
        }
    }

    var ticket = await client.GetTicketAsync(ticketId);

    if (ticket == null)
    {
        Console.WriteLine($"Ticket {ticketId} not found.");
        return 1;
    }

    OutputFormatter.PrintTicketDetails(ticket, format);
    return 0;
}

static async Task<int> HandleTicketCreate(string[] args, FreshdeskCLI.Services.FreshdeskApiClient client)
{
    string? subject = null;
    string? description = null;
    string? email = null;
    var priority = TicketPriority.Low;
    var status = TicketStatus.Open;

    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--subject":
            case "-s":
                if (i + 1 < args.Length)
                    subject = args[++i];
                break;
            case "--description":
            case "-d":
                if (i + 1 < args.Length)
                    description = args[++i];
                break;
            case "--email":
            case "-e":
                if (i + 1 < args.Length)
                    email = args[++i];
                break;
            case "--priority":
            case "-p":
                if (i + 1 < args.Length && Enum.TryParse<TicketPriority>(args[++i], true, out var p))
                    priority = p;
                break;
        }
    }

    if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(email))
    {
        Console.WriteLine("Usage: freshdesk ticket create [options]");
        Console.WriteLine("Required options:");
        Console.WriteLine("  --subject, -s <subject>      Ticket subject");
        Console.WriteLine("  --email, -e <email>          Requester email");
        Console.WriteLine("Optional options:");
        Console.WriteLine("  --description, -d <desc>     Ticket description");
        Console.WriteLine("  --priority, -p <priority>    Priority (Low, Medium, High, Urgent)");
        return 1;
    }

    var ticket = new Ticket
    {
        Subject = subject,
        Description = description ?? subject,
        Email = email,
        Priority = priority,
        Status = status,
        Source = TicketSource.Portal
    };

    try
    {
        var created = await client.CreateTicketAsync(ticket);
        Console.WriteLine($"Ticket created successfully!");
        Console.WriteLine($"Ticket ID: {created.Id}");
        Console.WriteLine($"Subject: {created.Subject}");
        Console.WriteLine($"Status: {created.Status}");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Failed to create ticket: {ex.Message}");
        return 1;
    }
}

static async Task<int> HandleTicketUpdate(string[] args, FreshdeskCLI.Services.FreshdeskApiClient client)
{
    if (args.Length < 1 || !long.TryParse(args[0], out var ticketId))
    {
        Console.WriteLine("Usage: freshdesk ticket update <ticket-id> [options]");
        Console.WriteLine("Options:");
        Console.WriteLine("  --status, -s <status>        New status");
        Console.WriteLine("  --priority, -p <priority>    New priority");
        return 1;
    }

    TicketStatus? status = null;
    TicketPriority? priority = null;

    for (int i = 1; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--status":
            case "-s":
                if (i + 1 < args.Length && Enum.TryParse<TicketStatus>(args[++i], true, out var s))
                    status = s;
                break;
            case "--priority":
            case "-p":
                if (i + 1 < args.Length && Enum.TryParse<TicketPriority>(args[++i], true, out var p))
                    priority = p;
                break;
        }
    }

    if (!status.HasValue && !priority.HasValue)
    {
        Console.WriteLine("No updates specified. Use --status or --priority to update the ticket.");
        return 1;
    }

    var updateTicket = new Ticket();
    if (status.HasValue)
        updateTicket.Status = status.Value;
    if (priority.HasValue)
        updateTicket.Priority = priority.Value;

    try
    {
        var updated = await client.UpdateTicketAsync(ticketId, updateTicket);
        Console.WriteLine($"Ticket #{updated.Id} updated successfully!");
        Console.WriteLine($"Status: {updated.Status}");
        Console.WriteLine($"Priority: {updated.Priority}");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Failed to update ticket: {ex.Message}");
        return 1;
    }
}

static async Task<int> HandleTicketSearch(string[] args, FreshdeskCLI.Services.FreshdeskApiClient client)
{
    if (args.Length < 1)
    {
        Console.WriteLine("Usage: freshdesk ticket search <query>");
        Console.WriteLine("Examples:");
        Console.WriteLine("  freshdesk ticket search \"status:open\"");
        Console.WriteLine("  freshdesk ticket search \"priority:high\"");
        Console.WriteLine("  freshdesk ticket search \"email:user@example.com\"");
        return 1;
    }

    var query = string.Join(" ", args);

    // For now, we'll use list with filtering on the client side
    // In a real implementation, we'd use Freshdesk's search API
    Console.WriteLine($"Searching for: {query}");

    var tickets = await client.GetTicketsAsync(1, 100);

    // Simple client-side filtering
    var filtered = tickets.Where(t =>
        t.Subject?.Contains(query, StringComparison.OrdinalIgnoreCase) == true ||
        t.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) == true ||
        t.Email?.Contains(query, StringComparison.OrdinalIgnoreCase) == true
    ).ToArray();

    if (filtered.Length == 0)
    {
        Console.WriteLine("No tickets found matching your search.");
        return 0;
    }

    Console.WriteLine($"{"ID",-10} {"Subject",-40} {"Status",-15} {"Priority",-10}");
    Console.WriteLine(new string('-', 75));

    foreach (var ticket in filtered)
    {
        var subject = ticket.Subject ?? "";
        if (subject.Length > 40)
            subject = subject[..37] + "...";

        Console.WriteLine($"{ticket.Id,-10} {subject,-40} {ticket.Status,-15} {ticket.Priority,-10}");
    }

    Console.WriteLine($"\nFound {filtered.Length} matching tickets");
    return 0;
}

static void TestAotCompatibility()
{
    try
    {
        // Test JSON serialization with AOT
        var testTicket = new Ticket
        {
            Id = 1,
            Subject = "Test Ticket",
            Status = TicketStatus.Open,
            Priority = TicketPriority.Low,
            Source = TicketSource.Email,
            CreatedAt = DateTimeOffset.Now,
            UpdatedAt = DateTimeOffset.Now,
            Attachments = [
                new Attachment
                {
                    Id = 1,
                    Name = "test.pdf",
                    Size = 12345,
                    ContentType = "application/pdf",
                    AttachmentUrl = "https://example.com/test.pdf",
                    CreatedAt = DateTimeOffset.Now,
                    UpdatedAt = DateTimeOffset.Now
                }
            ]
        };

        var json = JsonSerializer.Serialize(testTicket, FreshdeskJsonContext.Default.Ticket);
        Console.WriteLine($"AOT Serialization test passed: {json.Length} bytes");

        // Test deserialization
        var deserialized = JsonSerializer.Deserialize(json, FreshdeskJsonContext.Default.Ticket);
        Console.WriteLine($"Deserialization test passed: Ticket #{deserialized?.Id} - {deserialized?.Subject}");

        // Test config serialization
        var testConfig = new FreshdeskConfig
        {
            Domain = "test",
            ApiKey = "test-key",
            ProfileName = "test"
        };

        var configJson = JsonSerializer.Serialize(testConfig, FreshdeskJsonContext.Default.FreshdeskConfig);
        Console.WriteLine($"Config serialization test passed: {configJson.Length} bytes");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"AOT test failed: {ex.Message}");
        Environment.Exit(1);
    }
}
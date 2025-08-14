using System.Text.Json;
using FreshdeskCLI;
using FreshdeskCLI.Models;

// For now, use a simpler approach without System.CommandLine's complex features
// System.CommandLine is AOT-compatible but the beta API is still evolving

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
    return await RouteCommand(args);
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
    Console.WriteLine("  config set    Set configuration values");
    Console.WriteLine("  config get    Get current configuration");
    Console.WriteLine("  config test   Test connection to Freshdesk API");
    Console.WriteLine("  ticket list   List tickets");
    Console.WriteLine("  ticket get    Get ticket details");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --version, -v    Show version information");
    Console.WriteLine("  --help, -h       Show this help message");
}

static async Task<int> RouteCommand(string[] args)
{
    if (args.Length < 1)
    {
        ShowHelp();
        return 1;
    }

    return args[0].ToLowerInvariant() switch
    {
        "config" => await HandleConfigCommand(args[1..]),
        "ticket" => await HandleTicketCommand(args[1..]),
        _ => ShowUnknownCommand(args[0])
    };
}

static int ShowUnknownCommand(string command)
{
    Console.Error.WriteLine($"Unknown command: {command}");
    Console.Error.WriteLine("Run 'freshdesk --help' for usage information.");
    return 1;
}

static async Task<int> HandleConfigCommand(string[] args)
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
        "set" => await HandleConfigSet(args[1..], configService),
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

static async Task<int> HandleTicketCommand(string[] args)
{
    if (args.Length < 1)
    {
        Console.WriteLine("Usage: freshdesk ticket <subcommand>");
        Console.WriteLine("  list    List tickets");
        Console.WriteLine("  get     Get ticket details");
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
        _ => ShowUnknownCommand($"ticket {args[0]}")
    };
}

static async Task<int> HandleTicketList(string[] args, FreshdeskCLI.Services.FreshdeskApiClient client)
{
    int page = 1;
    int limit = 30;

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
        }
    }

    var tickets = await client.GetTicketsAsync(page, limit);

    Console.WriteLine($"{"ID",-10} {"Subject",-40} {"Status",-15} {"Priority",-10}");
    Console.WriteLine(new string('-', 75));

    foreach (var ticket in tickets)
    {
        var subject = ticket.Subject ?? "";
        if (subject.Length > 40)
            subject = subject[..37] + "...";

        Console.WriteLine($"{ticket.Id,-10} {subject,-40} {ticket.Status,-15} {ticket.Priority,-10}");
    }

    Console.WriteLine($"\nShowing {tickets.Length} tickets (page {page})");
    return 0;
}

static async Task<int> HandleTicketGet(string[] args, FreshdeskCLI.Services.FreshdeskApiClient client)
{
    if (args.Length < 1 || !long.TryParse(args[0], out var ticketId))
    {
        Console.WriteLine("Usage: freshdesk ticket get <ticket-id>");
        return 1;
    }

    var ticket = await client.GetTicketAsync(ticketId);

    if (ticket == null)
    {
        Console.WriteLine($"Ticket {ticketId} not found.");
        return 1;
    }

    Console.WriteLine($"Ticket #{ticket.Id}");
    Console.WriteLine(new string('=', 50));
    Console.WriteLine($"Subject: {ticket.Subject}");
    Console.WriteLine($"Status: {ticket.Status}");
    Console.WriteLine($"Priority: {ticket.Priority}");
    Console.WriteLine($"Created: {ticket.CreatedAt:yyyy-MM-dd HH:mm:ss}");
    Console.WriteLine($"Updated: {ticket.UpdatedAt:yyyy-MM-dd HH:mm:ss}");

    if (!string.IsNullOrEmpty(ticket.Description))
    {
        Console.WriteLine("\nDescription:");
        Console.WriteLine(ticket.Description);
    }

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
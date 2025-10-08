using System.Reflection;
using System.Text.Json;
using FreshdeskCLI;
using FreshdeskCLI.Models;
using FreshdeskCLI.Helpers;
using FreshdeskCLI.Services;
using static FreshdeskCLI.Helpers.EnumParser;
using static FreshdeskCLI.Helpers.CompletionGenerator;

// For now, use a simpler approach without System.CommandLine's complex features
// System.CommandLine is AOT-compatible but the beta API is still evolving

// Get version information from assembly
var assembly = System.Reflection.Assembly.GetExecutingAssembly();
var version = assembly.GetName().Version?.ToString() ?? "1.0.0";
var informationalVersion = assembly.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? version;

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

// Check for updates asynchronously (non-blocking)
var updateCheckTask = CheckForUpdateInBackground(version);

if (args.Length == 0)
{
    ShowHelp(informationalVersion);
    // Wait for update check to complete and display if available
    var updateInfo = await updateCheckTask;
    if (updateInfo != null)
    {
        Console.WriteLine();
        Console.WriteLine($"📦 Update available: v{updateInfo.Version}");
        Console.WriteLine($"   Run 'freshdesk update' to install the latest version");
    }
    return 0;
}

// Handle special flags
if (args[0] == "--version" || args[0] == "-v")
{
    Console.WriteLine($"Freshdesk CLI v{informationalVersion}");
    Console.WriteLine("Built with .NET 9 AOT compilation");
    Console.WriteLine();
    Console.WriteLine("Created with ❤️ by Aaron Stannard");
    Console.WriteLine("https://aaronstannard.com/");

    // Wait for update check to complete and display if available
    var updateInfo = await updateCheckTask;
    if (updateInfo != null)
    {
        Console.WriteLine();
        Console.WriteLine($"📦 Update available: v{updateInfo.Version}");
        Console.WriteLine($"   Run 'freshdesk update' to install the latest version");
    }
    return 0;
}

if (args[0] == "--about")
{
    Console.WriteLine($"Freshdesk CLI v{informationalVersion}");
    Console.WriteLine("Built with .NET 9 AOT compilation");
    Console.WriteLine();
    Console.WriteLine("Created with ❤️ by Aaron Stannard");
    Console.WriteLine("https://aaronstannard.com/");

    // Wait for update check to complete and display if available
    var updateInfo = await updateCheckTask;
    if (updateInfo != null)
    {
        Console.WriteLine();
        Console.WriteLine($"📦 Update available: v{updateInfo.Version}");
        Console.WriteLine($"   Run 'freshdesk update' to install the latest version");
    }
    return 0;
}

if (args[0] == "--test-aot")
{
    TestAotCompatibility();
    return 0;
}

if (args[0] == "--help" || args[0] == "-h")
{
    ShowHelp(informationalVersion);

    // Wait for update check to complete and display if available
    var updateInfo = await updateCheckTask;
    if (updateInfo != null)
    {
        Console.WriteLine();
        Console.WriteLine($"📦 Update available: v{updateInfo.Version}");
        Console.WriteLine($"   Run 'freshdesk update' to install the latest version");
    }
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

static void ShowHelp(string? versionInfo = null)
{
    if (!string.IsNullOrEmpty(versionInfo))
    {
        Console.WriteLine($"Freshdesk CLI v{versionInfo}");
    }
    else
    {
        Console.WriteLine("Freshdesk CLI");
    }
    Console.WriteLine("Command-line interface for Freshdesk API");
    Console.WriteLine("Created with ❤️ by Aaron Stannard (https://aaronstannard.com/)");
    Console.WriteLine();
    Console.WriteLine("Usage: freshdesk <command> [options]");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  config set        Set configuration values");
    Console.WriteLine("  config get        Get current configuration");
    Console.WriteLine("  config test       Test connection to Freshdesk API");
    Console.WriteLine("  ticket list       List tickets (with --status, --email, --unresolved filters)");
    Console.WriteLine("  ticket get        Get ticket details");
    Console.WriteLine("  ticket create     Create a new ticket");
    Console.WriteLine("  ticket update     Update ticket status/priority");
    Console.WriteLine("  ticket search     Search tickets");
    Console.WriteLine("  ticket reply      Reply to a ticket");
    Console.WriteLine("  ticket note       Add internal note to a ticket");
    Console.WriteLine("  attachment list    List attachments for a ticket");
    Console.WriteLine("  attachment download Download an attachment");
    Console.WriteLine("  attachment upload  Upload an attachment to a ticket");
    Console.WriteLine("  export tickets    Export tickets to file");
    Console.WriteLine("  export ticket     Export single ticket");
    Console.WriteLine("  update            Check for and install updates");
    Console.WriteLine("  install-completion Install shell completion scripts");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --version, -v      Show version information");
    Console.WriteLine("  --about            Show about information");
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
        "attachment" => await HandleAttachmentCommand(args[1..], isReadOnly),
        "export" => await HandleExportCommand(args[1..]),
        "update" => await HandleUpdateCommand(args[1..]),
        "install-completion" => await HandleInstallCompletionCommand(args[1..]),
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
        return CommandHelp.ShowHelpAndReturn("config");
    }

    // Check if help is requested for the config command itself (not subcommands)
    if (args.Length == 1 && CommandHelp.CheckForHelp(args))
    {
        return CommandHelp.ShowHelpAndReturn("config");
    }

    var configService = new FreshdeskCLI.Services.ConfigurationService();

    return args[0].ToLowerInvariant() switch
    {
        "set" => isReadOnly ? ShowReadOnlyError("config set") : await HandleConfigSet(args[1..], configService),
        "get" => await HandleConfigGet(args[1..], configService),
        "test" => await HandleConfigTest(args[1..], configService),
        _ => ShowUnknownCommand($"config {args[0]}")
    };
}

static async Task<int> HandleConfigSet(string[] args, FreshdeskCLI.Services.ConfigurationService configService)
{
    if (CommandHelp.CheckForHelp(args))
    {
        return CommandHelp.ShowHelpAndReturn("config", "set");
    }

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
        Console.WriteLine("Error: No configuration options specified.");
        Console.WriteLine("Run 'freshdesk config set --help' for usage information.");
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

static async Task<int> HandleConfigGet(string[] args, FreshdeskCLI.Services.ConfigurationService configService)
{
    if (CommandHelp.CheckForHelp(args))
    {
        return CommandHelp.ShowHelpAndReturn("config", "get");
    }

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

static async Task<int> HandleConfigTest(string[] args, FreshdeskCLI.Services.ConfigurationService configService)
{
    if (CommandHelp.CheckForHelp(args))
    {
        return CommandHelp.ShowHelpAndReturn("config", "test");
    }

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
        return CommandHelp.ShowHelpAndReturn("ticket");
    }

    // Check if help is requested for the ticket command itself (not subcommands)
    if (args.Length == 1 && CommandHelp.CheckForHelp(args))
    {
        return CommandHelp.ShowHelpAndReturn("ticket");
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
        "reply" => isReadOnly ? ShowReadOnlyError("ticket reply") : await HandleTicketReply(args[1..], client),
        "note" => isReadOnly ? ShowReadOnlyError("ticket note") : await HandleTicketNote(args[1..], client),
        _ => ShowUnknownCommand($"ticket {args[0]}")
    };
}

static async Task<int> HandleTicketList(string[] args, FreshdeskCLI.Services.FreshdeskApiClient client)
{
    if (CommandHelp.CheckForHelp(args))
    {
        return CommandHelp.ShowHelpAndReturn("ticket", "list");
    }

    int page = 1;
    int limit = 30;
    string format = "table";
    TicketStatus? statusFilter = null;
    string? emailFilter = null;
    bool unresolvedOnly = false;

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
            case "--status":
            case "-s":
                if (i + 1 < args.Length)
                {
                    var statusArg = args[++i];
                    if (TryParseTicketStatus(statusArg, out var s))
                    {
                        statusFilter = s;
                    }
                    else
                    {
                        Console.Error.WriteLine($"Invalid status value: '{statusArg}'");
                        Console.Error.WriteLine(GetValidStatusValues());
                        return 1;
                    }
                }
                break;
            case "--email":
            case "--customer":
            case "-e":
                if (i + 1 < args.Length)
                    emailFilter = args[++i];
                break;
            case "--unresolved":
                unresolvedOnly = true;
                break;
        }
    }

    Ticket[] tickets;

    if (unresolvedOnly)
    {
        // When --unresolved is used, search for each unresolved status separately
        // to ensure we get all tickets regardless of API pagination limits
        var allUnresolvedTickets = new List<Ticket>();
        var unresolvedStatuses = new[] { TicketStatus.Open, TicketStatus.Pending, TicketStatus.WaitingOnCustomer, TicketStatus.WaitingOnThirdParty };

        foreach (var status in unresolvedStatuses)
        {
            // Use a reasonable limit per status to avoid API limits
            var statusTickets = await client.GetTicketsAsync(1, Math.Min(limit, 100), status, emailFilter);
            allUnresolvedTickets.AddRange(statusTickets);
        }

        tickets = allUnresolvedTickets
            .GroupBy(t => t.Id) // Deduplicate in case of overlaps
            .Select(g => g.First())
            .OrderByDescending(t => t.CreatedAt) // Sort by creation date, newest first
            .Take(limit) // Apply the original limit to the combined results
            .ToArray();
    }
    else
    {
        tickets = await client.GetTicketsAsync(page, limit, statusFilter, emailFilter);
    }

    OutputFormatter.PrintTickets(tickets, format);

    if (format.Equals("table", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine($"Page {page}, showing {tickets.Length} tickets");
    }

    return 0;
}

static async Task<int> HandleTicketGet(string[] args, FreshdeskCLI.Services.FreshdeskApiClient client)
{
    if (CommandHelp.CheckForHelp(args))
    {
        return CommandHelp.ShowHelpAndReturn("ticket", "get");
    }

    if (args.Length < 1 || !long.TryParse(args[0], out var ticketId))
    {
        Console.WriteLine("Error: Missing or invalid ticket ID.");
        Console.WriteLine("Run 'freshdesk ticket get --help' for usage information.");
        return 1;
    }

    string format = "text";
    bool showTree = false;
    bool showConversations = false;
    bool showFull = false;

    for (int i = 1; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--format":
            case "-f":
                if (i + 1 < args.Length)
                    format = args[++i];
                break;
            case "--tree":
                showTree = true;
                break;
            case "--conversations":
                showConversations = true;
                break;
            case "--full":
                showFull = true;
                break;
        }
    }

    var ticket = await client.GetTicketAsync(ticketId);

    if (ticket == null)
    {
        Console.WriteLine($"Ticket {ticketId} not found.");
        return 1;
    }

    // Fetch requester email
    if (ticket.RequesterId.HasValue)
    {
        var contact = await client.GetContactAsync(ticket.RequesterId.Value);
        if (contact != null)
        {
            ticket.Email = contact.Email;
        }
    }

    // Fetch conversations if needed
    Conversation[]? conversations = null;
    if (showTree || showConversations || showFull)
    {
        conversations = await client.GetTicketConversationsAsync(ticketId);
        ticket.Conversations = conversations;
    }

    // Show appropriate view
    if (format == "json")
    {
        OutputFormatter.PrintTicketJson(ticket);
    }
    else if (showTree)
    {
        OutputFormatter.PrintTicketTree(ticket, conversations);
    }
    else if (showFull || showConversations)
    {
        OutputFormatter.PrintTicketFull(ticket, conversations);
    }
    else
    {
        OutputFormatter.PrintTicketDetails(ticket, format);
    }

    return 0;
}

static async Task<int> HandleTicketCreate(string[] args, FreshdeskCLI.Services.FreshdeskApiClient client)
{
    if (CommandHelp.CheckForHelp(args))
    {
        return CommandHelp.ShowHelpAndReturn("ticket", "create");
    }

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
                if (i + 1 < args.Length)
                {
                    var priorityArg = args[++i];
                    if (TryParseTicketPriority(priorityArg, out var p))
                    {
                        priority = p;
                    }
                    else
                    {
                        Console.Error.WriteLine($"Invalid priority value: '{priorityArg}'");
                        Console.Error.WriteLine(GetValidPriorityValues());
                        return 1;
                    }
                }
                break;
        }
    }

    if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(email))
    {
        Console.WriteLine("Error: Missing required options.");
        Console.WriteLine("Run 'freshdesk ticket create --help' for usage information.");
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
    if (CommandHelp.CheckForHelp(args))
    {
        return CommandHelp.ShowHelpAndReturn("ticket", "update");
    }

    if (args.Length < 1 || !long.TryParse(args[0], out var ticketId))
    {
        Console.WriteLine("Error: Missing or invalid ticket ID.");
        Console.WriteLine("Run 'freshdesk ticket update --help' for usage information.");
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
                if (i + 1 < args.Length)
                {
                    var statusArg = args[++i];
                    if (TryParseTicketStatus(statusArg, out var s))
                    {
                        status = s;
                    }
                    else
                    {
                        Console.Error.WriteLine($"Invalid status value: '{statusArg}'");
                        Console.Error.WriteLine(GetValidStatusValues());
                        return 1;
                    }
                }
                break;
            case "--priority":
            case "-p":
                if (i + 1 < args.Length)
                {
                    var priorityArg = args[++i];
                    if (TryParseTicketPriority(priorityArg, out var p))
                    {
                        priority = p;
                    }
                    else
                    {
                        Console.Error.WriteLine($"Invalid priority value: '{priorityArg}'");
                        Console.Error.WriteLine(GetValidPriorityValues());
                        return 1;
                    }
                }
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
    if (CommandHelp.CheckForHelp(args))
    {
        return CommandHelp.ShowHelpAndReturn("ticket", "search");
    }

    string? textQuery = null;
    TicketStatus? statusFilter = null;
    TicketPriority? priorityFilter = null;
    string? emailFilter = null;
    string format = "table";

    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--query":
            case "-q":
                if (i + 1 < args.Length)
                    textQuery = args[++i];
                break;
            case "--status":
            case "-s":
                if (i + 1 < args.Length)
                {
                    var statusArg = args[++i];
                    if (TryParseTicketStatus(statusArg, out var s))
                    {
                        statusFilter = s;
                    }
                    else
                    {
                        Console.Error.WriteLine($"Invalid status value: '{statusArg}'");
                        Console.Error.WriteLine(GetValidStatusValues());
                        return 1;
                    }
                }
                break;
            case "--priority":
            case "-p":
                if (i + 1 < args.Length)
                {
                    var priorityArg = args[++i];
                    if (TryParseTicketPriority(priorityArg, out var p))
                    {
                        priorityFilter = p;
                    }
                    else
                    {
                        Console.Error.WriteLine($"Invalid priority value: '{priorityArg}'");
                        Console.Error.WriteLine(GetValidPriorityValues());
                        return 1;
                    }
                }
                break;
            case "--email":
            case "--customer":
            case "-e":
                if (i + 1 < args.Length)
                    emailFilter = args[++i];
                break;
            case "--format":
            case "-f":
                if (i + 1 < args.Length)
                    format = args[++i];
                break;
            default:
                // If no flag, treat as text query for backward compatibility
                if (!args[i].StartsWith("-"))
                    textQuery = string.IsNullOrEmpty(textQuery) ? args[i] : $"{textQuery} {args[i]}";
                break;
        }
    }

    if (string.IsNullOrEmpty(textQuery) && !statusFilter.HasValue && !priorityFilter.HasValue && string.IsNullOrEmpty(emailFilter))
    {
        Console.WriteLine("Error: No search criteria specified.");
        Console.WriteLine("Run 'freshdesk ticket search --help' for usage information.");
        return 1;
    }

    var tickets = await client.GetTicketsAsync(1, 100, statusFilter, emailFilter);

    // Apply additional filters
    if (priorityFilter.HasValue)
    {
        tickets = tickets.Where(t => t.Priority == priorityFilter.Value).ToArray();
    }

    if (!string.IsNullOrEmpty(textQuery))
    {
        tickets = tickets.Where(t =>
            t.Subject?.Contains(textQuery, StringComparison.OrdinalIgnoreCase) == true ||
            t.Description?.Contains(textQuery, StringComparison.OrdinalIgnoreCase) == true ||
            t.Email?.Contains(textQuery, StringComparison.OrdinalIgnoreCase) == true
        ).ToArray();
    }

    if (tickets.Length == 0)
    {
        Console.WriteLine("No tickets found matching your search criteria.");
        return 0;
    }

    OutputFormatter.PrintTickets(tickets, format);

    if (format.Equals("table", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine($"\nFound {tickets.Length} matching tickets");
    }

    return 0;
}

static async Task<int> HandleTicketReply(string[] args, FreshdeskCLI.Services.FreshdeskApiClient client)
{
    if (CommandHelp.CheckForHelp(args))
    {
        return CommandHelp.ShowHelpAndReturn("ticket", "reply");
    }

    if (args.Length < 1 || !long.TryParse(args[0], out var ticketId))
    {
        Console.WriteLine("Error: Missing or invalid ticket ID.");
        Console.WriteLine("Run 'freshdesk ticket reply --help' for usage information.");
        return 1;
    }

    string? message = null;
    string? filePath = null;

    for (int i = 1; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--message":
            case "-m":
                if (i + 1 < args.Length)
                    message = args[++i];
                break;
            case "--file":
            case "-f":
                if (i + 1 < args.Length)
                    filePath = args[++i];
                break;
        }
    }

    // Get message from file if specified
    if (!string.IsNullOrEmpty(filePath))
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"File not found: {filePath}");
            return 1;
        }
        message = NormalizeLineEndings(await File.ReadAllTextAsync(filePath));
    }

    // If no message provided, prompt for it
    if (string.IsNullOrEmpty(message))
    {
        Console.WriteLine("Enter your reply (press Ctrl+D or type 'EOF' on a new line when done):");
        var lines = new List<string>();
        string? line;
        while ((line = Console.ReadLine()) != null && line != "EOF")
        {
            lines.Add(line);
        }
        message = string.Join("\n", lines);
    }

    if (string.IsNullOrEmpty(message))
    {
        Console.WriteLine("No message provided.");
        return 1;
    }

    try
    {
        Console.WriteLine($"Sending reply to ticket #{ticketId}...");
        var conversation = await client.ReplyToTicketAsync(ticketId, message, isPrivate: false);
        Console.WriteLine($"✓ Reply sent successfully!");
        Console.WriteLine($"  Conversation ID: {conversation.Id}");
        Console.WriteLine($"  Created: {conversation.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Failed to send reply: {ex.Message}");
        return 1;
    }
}

static async Task<int> HandleTicketNote(string[] args, FreshdeskCLI.Services.FreshdeskApiClient client)
{
    if (CommandHelp.CheckForHelp(args))
    {
        return CommandHelp.ShowHelpAndReturn("ticket", "note");
    }

    if (args.Length < 1 || !long.TryParse(args[0], out var ticketId))
    {
        Console.WriteLine("Error: Missing or invalid ticket ID.");
        Console.WriteLine("Run 'freshdesk ticket note --help' for usage information.");
        return 1;
    }

    string? message = null;
    string? filePath = null;

    for (int i = 1; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--message":
            case "-m":
                if (i + 1 < args.Length)
                    message = args[++i];
                break;
            case "--file":
            case "-f":
                if (i + 1 < args.Length)
                    filePath = args[++i];
                break;
        }
    }

    // Get message from file if specified
    if (!string.IsNullOrEmpty(filePath))
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"File not found: {filePath}");
            return 1;
        }
        message = NormalizeLineEndings(await File.ReadAllTextAsync(filePath));
    }

    // If no message provided, prompt for it
    if (string.IsNullOrEmpty(message))
    {
        Console.WriteLine("Enter your internal note (press Ctrl+D or type 'EOF' on a new line when done):");
        var lines = new List<string>();
        string? line;
        while ((line = Console.ReadLine()) != null && line != "EOF")
        {
            lines.Add(line);
        }
        message = string.Join("\n", lines);
    }

    if (string.IsNullOrEmpty(message))
    {
        Console.WriteLine("No message provided.");
        return 1;
    }

    try
    {
        Console.WriteLine($"Adding internal note to ticket #{ticketId}...");
        var conversation = await client.ReplyToTicketAsync(ticketId, message, isPrivate: true);
        Console.WriteLine($"✓ Internal note added successfully!");
        Console.WriteLine($"  Note ID: {conversation.Id}");
        Console.WriteLine($"  Created: {conversation.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Failed to add note: {ex.Message}");
        return 1;
    }
}

static string NormalizeLineEndings(string text)
{
    return text.Replace("\r\n", "\n").Replace("\r", "\n");
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

static async Task<int> HandleAttachmentCommand(string[] args, bool isReadOnly = false)
{
    if (args.Length < 1)
    {
        return CommandHelp.ShowHelpAndReturn("attachment");
    }

    // Check if help is requested for the attachment command itself (not subcommands)
    if (args.Length == 1 && CommandHelp.CheckForHelp(args))
    {
        return CommandHelp.ShowHelpAndReturn("attachment");
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
        "list" => await HandleAttachmentList(args[1..], client),
        "download" => await HandleAttachmentDownload(args[1..], client),
        "upload" => isReadOnly ? ShowReadOnlyError("attachment upload") : await HandleAttachmentUpload(args[1..], client),
        _ => ShowUnknownCommand($"attachment {args[0]}")
    };
}

static async Task<int> HandleAttachmentList(string[] args, FreshdeskCLI.Services.FreshdeskApiClient client)
{
    if (CommandHelp.CheckForHelp(args))
    {
        return CommandHelp.ShowHelpAndReturn("attachment", "list");
    }

    if (args.Length < 1 || !long.TryParse(args[0], out var ticketId))
    {
        Console.WriteLine("Error: Missing or invalid ticket ID.");
        Console.WriteLine("Run 'freshdesk attachment list --help' for usage information.");
        return 1;
    }

    var includeConversations = args.Contains("--include-conversations");

    var ticket = await client.GetTicketAsync(ticketId);
    if (ticket == null)
    {
        Console.WriteLine($"Ticket {ticketId} not found.");
        return 1;
    }

    var allAttachments = new List<(Attachment attachment, string source)>();

    if (ticket.Attachments != null && ticket.Attachments.Length > 0)
    {
        foreach (var att in ticket.Attachments)
            allAttachments.Add((att, "Ticket"));
    }

    if (includeConversations)
    {
        var conversations = await client.GetTicketConversationsAsync(ticketId);
        foreach (var conv in conversations)
        {
            if (conv.Attachments != null && conv.Attachments.Length > 0)
            {
                foreach (var att in conv.Attachments)
                    allAttachments.Add((att, $"Conv #{conv.Id}"));
            }
        }
    }

    if (allAttachments.Count == 0)
    {
        Console.WriteLine($"No attachments found for ticket #{ticketId}");
        if (!includeConversations)
            Console.WriteLine("Tip: Use --include-conversations to check conversation attachments");
        return 0;
    }

    Console.WriteLine($"Attachments for ticket #{ticketId}: {ticket.Subject}");
    Console.WriteLine(new string('-', 100));
    Console.WriteLine($"{"ID",-15} {"Name",-35} {"Size",-10} {"Type",-15} {"Source",-15}");
    Console.WriteLine(new string('-', 100));

    foreach (var (attachment, source) in allAttachments)
    {
        var name = attachment.Name.Length > 35 ? attachment.Name[..32] + "..." : attachment.Name;
        var type = attachment.ContentType?.Length > 15 ? attachment.ContentType[..12] + "..." : attachment.ContentType ?? "unknown";
        Console.WriteLine($"{attachment.Id,-15} {name,-35} {attachment.FormattedSize,-10} {type,-15} {source,-15}");
    }

    Console.WriteLine($"\nTotal: {allAttachments.Count} attachments");
    if (!includeConversations && allAttachments.Count > 0)
        Console.WriteLine("Tip: Use --include-conversations to include conversation attachments");
    return 0;
}

static async Task<int> HandleAttachmentDownload(string[] args, FreshdeskCLI.Services.FreshdeskApiClient client)
{
    if (CommandHelp.CheckForHelp(args))
    {
        return CommandHelp.ShowHelpAndReturn("attachment", "download");
    }

    // Check if user wants to download all attachments
    if (args.Length >= 2 && args[1].Equals("all", StringComparison.OrdinalIgnoreCase))
    {
        return await HandleBulkAttachmentDownload(args, client);
    }

    if (args.Length < 2 || !long.TryParse(args[0], out var ticketId))
    {
        Console.WriteLine("Error: Missing required arguments.");
        Console.WriteLine("Run 'freshdesk attachment download --help' for usage information.");
        return 1;
    }

    if (!long.TryParse(args[1], out var attachmentId))
    {
        Console.WriteLine("Invalid attachment ID");
        return 1;
    }

    string? outputPath = null;
    for (int i = 2; i < args.Length; i++)
    {
        if ((args[i] == "--output" || args[i] == "-o") && i + 1 < args.Length)
        {
            outputPath = args[++i];
        }
    }

    // Always fetch fresh ticket data to get valid attachment URLs
    Console.WriteLine($"Fetching fresh ticket data for #{ticketId}...");
    var ticket = await client.GetTicketAsync(ticketId);
    if (ticket == null)
    {
        Console.WriteLine($"Ticket {ticketId} not found.");
        return 1;
    }

    // First check ticket attachments
    var attachment = ticket.Attachments?.FirstOrDefault(a => a.Id == attachmentId);

    // If not found in ticket, check conversation attachments
    if (attachment == null)
    {
        Console.WriteLine("Attachment not found in ticket, checking conversations...");
        var conversations = await client.GetTicketConversationsAsync(ticketId);
        foreach (var conv in conversations)
        {
            attachment = conv.Attachments?.FirstOrDefault(a => a.Id == attachmentId);
            if (attachment != null)
            {
                Console.WriteLine($"Found attachment in conversation #{conv.Id}");
                break;
            }
        }
    }

    if (attachment == null)
    {
        Console.WriteLine($"Attachment {attachmentId} not found in ticket {ticketId} or its conversations");
        return 1;
    }

    outputPath ??= attachment.Name;

    Console.WriteLine($"Downloading {attachment.Name} ({attachment.FormattedSize})...");

    try
    {
        var data = await client.DownloadAttachmentAsync(attachment.AttachmentUrl);
        await File.WriteAllBytesAsync(outputPath, data);
        Console.WriteLine($"✓ Downloaded to: {outputPath}");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Failed to download attachment: {ex.Message}");
        return 1;
    }
}

static async Task<int> HandleBulkAttachmentDownload(string[] args, FreshdeskCLI.Services.FreshdeskApiClient client)
{
    if (args.Length < 1 || !long.TryParse(args[0], out var ticketId))
    {
        Console.WriteLine("Usage: freshdesk attachment download <ticket-id> all [--output <path>] [--include-conversations]");
        return 1;
    }

    var includeConversations = args.Contains("--include-conversations");

    // Always fetch fresh ticket data to get valid attachment URLs
    Console.WriteLine($"Fetching fresh ticket data for #{ticketId}...");
    var ticket = await client.GetTicketAsync(ticketId);
    if (ticket == null)
    {
        Console.WriteLine($"Ticket {ticketId} not found.");
        return 1;
    }

    var allAttachments = new List<Attachment>();

    if (ticket.Attachments != null && ticket.Attachments.Length > 0)
    {
        allAttachments.AddRange(ticket.Attachments);
    }

    if (includeConversations)
    {
        Console.WriteLine("Fetching conversation attachments...");
        var conversations = await client.GetTicketConversationsAsync(ticketId);
        foreach (var conv in conversations)
        {
            if (conv.Attachments != null && conv.Attachments.Length > 0)
            {
                allAttachments.AddRange(conv.Attachments);
            }
        }
    }

    if (allAttachments.Count == 0)
    {
        Console.WriteLine($"No attachments found for ticket #{ticketId}");
        if (!includeConversations)
            Console.WriteLine("Tip: Use --include-conversations to include conversation attachments");
        return 0;
    }

    // Parse output path
    var outputPath = Environment.CurrentDirectory;
    var outputIndex = Array.IndexOf(args, "--output");
    if (outputIndex >= 0 && outputIndex < args.Length - 1)
    {
        outputPath = args[outputIndex + 1];
    }

    // Create folder for ticket attachments
    var ticketFolder = Path.Combine(outputPath, $"ticket_{ticketId}_attachments");
    Directory.CreateDirectory(ticketFolder);

    Console.WriteLine($"Downloading {allAttachments.Count} attachments to {ticketFolder}...\n");

    var successCount = 0;
    var errorCount = 0;
    var totalBytes = 0L;
    var errors = new List<string>();

    foreach (var attachment in allAttachments)
    {
        var fileName = Path.GetInvalidFileNameChars().Aggregate(attachment.Name, (current, c) => current.Replace(c, '_'));
        var filePath = Path.Combine(ticketFolder, fileName);

        // Handle duplicate filenames
        if (File.Exists(filePath))
        {
            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            var ext = Path.GetExtension(fileName);
            var counter = 1;
            do
            {
                fileName = $"{nameWithoutExt}_{counter}{ext}";
                filePath = Path.Combine(ticketFolder, fileName);
                counter++;
            } while (File.Exists(filePath));
        }

        Console.Write($"Downloading {attachment.Name} ({attachment.FormattedSize})... ");

        try
        {
            var data = await client.DownloadAttachmentAsync(attachment.AttachmentUrl);
            await File.WriteAllBytesAsync(filePath, data);
            successCount++;
            totalBytes += data.Length;
            Console.WriteLine("✓");
        }
        catch (Exception ex)
        {
            errorCount++;
            errors.Add($"{attachment.Name}: {ex.Message}");
            Console.WriteLine($"✗ ({ex.Message})");
        }
    }

    Console.WriteLine($"\nDownload Summary:");
    Console.WriteLine($"  Successfully downloaded: {successCount}/{allAttachments.Count} files");
    Console.WriteLine($"  Total size: {FormatFileSize(totalBytes)}");
    Console.WriteLine($"  Location: {ticketFolder}");

    if (errorCount > 0)
    {
        Console.WriteLine($"\nErrors occurred for {errorCount} files:");
        foreach (var error in errors)
        {
            Console.WriteLine($"  - {error}");
        }
        return 1;
    }

    return 0;
}

static string FormatFileSize(long bytes)
{
    string[] sizes = { "B", "KB", "MB", "GB", "TB" };
    double len = bytes;
    int order = 0;
    while (len >= 1024 && order < sizes.Length - 1)
    {
        order++;
        len = len / 1024;
    }
    return $"{len:0.##} {sizes[order]}";
}

static async Task<int> HandleExportCommand(string[] args)
{
    if (args.Length < 1)
    {
        return CommandHelp.ShowHelpAndReturn("export");
    }

    // Check if help is requested for the export command itself (not subcommands)
    if (args.Length == 1 && CommandHelp.CheckForHelp(args))
    {
        return CommandHelp.ShowHelpAndReturn("export");
    }

    var configService = new FreshdeskCLI.Services.ConfigurationService();
    var config = await configService.LoadConfigAsync();
    if (config == null || !config.IsValid)
    {
        Console.WriteLine("No valid configuration found. Run 'freshdesk config set' first.");
        return 1;
    }

    using var client = new FreshdeskCLI.Services.FreshdeskApiClient(config);

    return args[0].ToLowerInvariant() switch
    {
        "tickets" => await HandleExportTickets(args[1..], client),
        "ticket" => await HandleExportTicket(args[1..], client),
        _ => ShowUnknownCommand($"export {args[0]}")
    };
}

static async Task<int> HandleExportTickets(string[] args, FreshdeskCLI.Services.FreshdeskApiClient client)
{
    if (CommandHelp.CheckForHelp(args))
    {
        return CommandHelp.ShowHelpAndReturn("export", "tickets");
    }

    string outputPath = "tickets_export.json";
    string format = "json";
    bool includeConversations = false;
    string? status = null;
    string? priority = null;
    string? email = null;
    int? limit = null;

    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--output" when i + 1 < args.Length:
            case "-o" when i + 1 < args.Length:
                outputPath = args[++i];
                break;
            case "--format" when i + 1 < args.Length:
            case "-f" when i + 1 < args.Length:
                format = args[++i];
                break;
            case "--include-conversations":
                includeConversations = true;
                break;
            case "--status" when i + 1 < args.Length:
                status = args[++i];
                break;
            case "--priority" when i + 1 < args.Length:
                priority = args[++i];
                break;
            case "--email" when i + 1 < args.Length:
            case "--customer" when i + 1 < args.Length:
                email = args[++i];
                break;
            case "--limit" when i + 1 < args.Length:
                limit = int.Parse(args[++i]);
                break;
        }
    }

    try
    {
        Console.WriteLine("Fetching tickets...");
        var tickets = await client.GetTicketsAsync();

        // Apply filters
        if (!string.IsNullOrEmpty(status))
        {
            if (TryParseTicketStatus(status, out var statusEnum))
                tickets = tickets.Where(t => t.Status == statusEnum).ToArray();
        }

        if (!string.IsNullOrEmpty(priority))
        {
            if (TryParseTicketPriority(priority, out var priorityEnum))
                tickets = tickets.Where(t => t.Priority == priorityEnum).ToArray();
        }

        if (!string.IsNullOrEmpty(email))
        {
            tickets = tickets.Where(t => t.Email?.Contains(email, StringComparison.OrdinalIgnoreCase) == true).ToArray();
        }

        if (limit.HasValue && limit.Value > 0)
        {
            tickets = tickets.Take(limit.Value).ToArray();
        }

        var exportService = new ExportService();
        await exportService.ExportTicketsAsync(tickets, outputPath, format, includeConversations, client);

        Console.WriteLine($"✓ Exported {tickets.Length} tickets to {outputPath}");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Export failed: {ex.Message}");
        return 1;
    }
}

static async Task<int> HandleExportTicket(string[] args, FreshdeskCLI.Services.FreshdeskApiClient client)
{
    if (CommandHelp.CheckForHelp(args))
    {
        return CommandHelp.ShowHelpAndReturn("export", "ticket");
    }

    if (args.Length < 1 || !long.TryParse(args[0], out var ticketId))
    {
        Console.WriteLine("Error: Missing or invalid ticket ID.");
        Console.WriteLine("Run 'freshdesk export ticket --help' for usage information.");
        return 1;
    }

    string outputPath = $"ticket_{ticketId}_export.json";
    string format = "json";
    bool includeConversations = false;

    for (int i = 1; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--output" when i + 1 < args.Length:
            case "-o" when i + 1 < args.Length:
                outputPath = args[++i];
                break;
            case "--format" when i + 1 < args.Length:
            case "-f" when i + 1 < args.Length:
                format = args[++i];
                break;
            case "--include-conversations":
                includeConversations = true;
                break;
        }
    }

    try
    {
        Console.WriteLine($"Fetching ticket #{ticketId}...");
        var ticket = await client.GetTicketAsync(ticketId);
        if (ticket == null)
        {
            Console.WriteLine($"Ticket {ticketId} not found.");
            return 1;
        }

        Conversation[]? conversations = null;
        if (includeConversations)
        {
            Console.WriteLine("Fetching conversations...");
            conversations = await client.GetTicketConversationsAsync(ticketId);
        }

        var exportService = new ExportService();
        await exportService.ExportTicketAsync(ticket, conversations, outputPath, format);

        Console.WriteLine($"✓ Exported ticket #{ticketId} to {outputPath}");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Export failed: {ex.Message}");
        return 1;
    }
}

static async Task<int> HandleAttachmentUpload(string[] args, FreshdeskCLI.Services.FreshdeskApiClient client)
{
    if (CommandHelp.CheckForHelp(args))
    {
        return CommandHelp.ShowHelpAndReturn("attachment", "upload");
    }

    if (args.Length < 2 || !long.TryParse(args[0], out var ticketId))
    {
        Console.WriteLine("Error: Missing required arguments.");
        Console.WriteLine("Run 'freshdesk attachment upload --help' for usage information.");
        return 1;
    }

    var filePath = args[1];
    if (!File.Exists(filePath))
    {
        Console.WriteLine($"File not found: {filePath}");
        return 1;
    }

    string? fileName = null;
    for (int i = 2; i < args.Length; i++)
    {
        if ((args[i] == "--name" || args[i] == "-n") && i + 1 < args.Length)
        {
            fileName = args[++i];
        }
    }

    var fileInfo = new FileInfo(filePath);
    Console.WriteLine($"Uploading {fileInfo.Name} ({fileInfo.Length / 1024.0:F1} KB) to ticket #{ticketId}...");

    try
    {
        var updatedTicket = await client.UploadAttachmentAsync(ticketId, filePath, fileName);
        Console.WriteLine($"✓ Uploaded successfully!");
        Console.WriteLine($"  Ticket #{updatedTicket.Id} updated with attachment");
        if (updatedTicket.Attachments?.Length > 0)
        {
            var latest = updatedTicket.Attachments[^1];
            Console.WriteLine($"  Attachment: {latest.Name} ({latest.FormattedSize})");
        }
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Failed to upload attachment: {ex.Message}");
        return 1;
    }
}

static async Task<int> HandleUpdateCommand(string[] args)
{
    if (CommandHelp.CheckForHelp(args))
    {
        return CommandHelp.ShowHelpAndReturn("update");
    }

    // Get version information from assembly
    var assembly = System.Reflection.Assembly.GetExecutingAssembly();
    var currentVersion = assembly.GetName().Version?.ToString(3) ?? "1.0.0"; // Major.Minor.Patch

    // Parse flags
    bool checkOnly = false;
    bool force = false;

    foreach (var arg in args)
    {
        if (arg == "--check" || arg == "-c")
            checkOnly = true;
        if (arg == "--force" || arg == "-f")
            force = true;
    }

    try
    {
        using var httpClient = new HttpClient();
        var updateService = new UpdateService(httpClient, currentVersion);

        Console.WriteLine("Checking for updates...");
        var update = await updateService.CheckForUpdateAsync();

        if (update == null)
        {
            Console.WriteLine($"✓ You're running the latest version (v{currentVersion})");
            return 0;
        }

        Console.WriteLine($"📦 New version available: v{update.Version}");
        Console.WriteLine($"   Current version: v{currentVersion}");

        if (!string.IsNullOrEmpty(update.ReleaseNotes))
        {
            Console.WriteLine();
            Console.WriteLine("Release notes:");
            Console.WriteLine("─────────────");

            // Truncate long release notes
            var notes = update.ReleaseNotes;
            if (notes.Length > 500)
            {
                notes = notes.Substring(0, 497) + "...";
            }
            Console.WriteLine(notes);
        }

        if (checkOnly)
        {
            Console.WriteLine();
            Console.WriteLine("Run 'freshdesk update' to install the latest version");
            return 0;
        }

        if (!force)
        {
            Console.WriteLine();
            Console.Write("Do you want to update now? [y/N]: ");
            var response = Console.ReadLine();
            if (response?.ToLowerInvariant() != "y" && response?.ToLowerInvariant() != "yes")
            {
                Console.WriteLine("Update cancelled");
                return 0;
            }
        }

        Console.WriteLine();
        var success = await updateService.PerformUpdateAsync(update);

        if (success)
        {
            // This line won't be reached as the process exits during update
            Console.WriteLine("✓ Update completed successfully!");
            return 0;
        }
        else
        {
            Console.Error.WriteLine("Update failed. Please try again or download manually from:");
            Console.Error.WriteLine($"  https://github.com/Aaronontheweb/freshdesk-cli/releases/latest");
            return 1;
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Update failed: {ex.Message}");
        return 1;
    }
}

static async Task<UpdateInfo?> CheckForUpdateInBackground(string currentVersion)
{
    try
    {
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(3); // Quick timeout for background check
        var updateService = new UpdateService(httpClient, currentVersion.Split('+')[0]); // Remove build metadata
        return await updateService.CheckForUpdateAsync();
    }
    catch
    {
        // Silently ignore errors in background update check
        return null;
    }
}

static async Task<int> HandleInstallCompletionCommand(string[] args)
{
    if (CommandHelp.CheckForHelp(args))
    {
        Console.WriteLine("Usage: freshdesk install-completion [shell]");
        Console.WriteLine();
        Console.WriteLine("Install shell completion scripts for tab-completion support.");
        Console.WriteLine();
        Console.WriteLine("Supported shells:");
        Console.WriteLine("  bash       - Bash shell (Linux/macOS)");
        Console.WriteLine("  zsh        - Z shell (Linux/macOS)");
        Console.WriteLine("  powershell - PowerShell (Windows/Linux/macOS)");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  freshdesk install-completion bash");
        Console.WriteLine("  freshdesk install-completion zsh");
        Console.WriteLine("  freshdesk install-completion powershell");
        Console.WriteLine();
        Console.WriteLine("After installation, restart your shell or source the profile:");
        Console.WriteLine("  Bash:       source ~/.bashrc");
        Console.WriteLine("  Zsh:        source ~/.zshrc");
        Console.WriteLine("  PowerShell: . $PROFILE");
        return 0;
    }

    string? shell = null;
    if (args.Length > 0)
    {
        shell = args[0].ToLowerInvariant();
    }
    else
    {
        shell = DetectCurrentShell();
        if (shell == null)
        {
            Console.WriteLine("Could not detect shell. Please specify: bash, zsh, or powershell");
            return 1;
        }
        Console.WriteLine($"Detected shell: {shell}");
    }

    try
    {
        switch (shell)
        {
            case "bash":
                return await InstallBashCompletion();
            case "zsh":
                return await InstallZshCompletion();
            case "powershell":
            case "pwsh":
                return await InstallPowerShellCompletion();
            default:
                Console.Error.WriteLine($"Unsupported shell: {shell}");
                Console.Error.WriteLine("Supported shells: bash, zsh, powershell");
                return 1;
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Failed to install completion: {ex.Message}");
        return 1;
    }
}

static string? DetectCurrentShell()
{
    var shellEnv = Environment.GetEnvironmentVariable("SHELL");
    if (!string.IsNullOrEmpty(shellEnv))
    {
        if (shellEnv.Contains("bash"))
            return "bash";
        if (shellEnv.Contains("zsh"))
            return "zsh";
    }

    var psModulePath = Environment.GetEnvironmentVariable("PSModulePath");
    if (!string.IsNullOrEmpty(psModulePath))
        return "powershell";

    return null;
}

static async Task<int> InstallBashCompletion()
{
    var completion = CompletionGenerator.GenerateBashCompletion();
    var homeDir = Environment.GetEnvironmentVariable("HOME") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    var completionsDir = Path.Combine(homeDir, ".local", "share", "bash-completion", "completions");
    Directory.CreateDirectory(completionsDir);

    var completionFile = Path.Combine(completionsDir, "freshdesk");
    await File.WriteAllTextAsync(completionFile, completion);

    var bashrcFile = Path.Combine(homeDir, ".bashrc");
    if (File.Exists(bashrcFile))
    {
        var bashrcContent = await File.ReadAllTextAsync(bashrcFile);
        var sourceCommand = $"[ -f {completionFile} ] && source {completionFile}";

        if (!bashrcContent.Contains(sourceCommand))
        {
            await File.AppendAllTextAsync(bashrcFile, $"\n# Freshdesk CLI completion\n{sourceCommand}\n");
        }
    }

    Console.WriteLine($"✓ Bash completion installed to {completionFile}");
    Console.WriteLine("  Run 'source ~/.bashrc' to enable completion in current session");
    return 0;
}

static async Task<int> InstallZshCompletion()
{
    var completion = CompletionGenerator.GenerateZshCompletion();
    var homeDir = Environment.GetEnvironmentVariable("HOME") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    var completionsDir = Path.Combine(homeDir, ".local", "share", "zsh", "site-functions");
    Directory.CreateDirectory(completionsDir);

    var completionFile = Path.Combine(completionsDir, "_freshdesk");
    await File.WriteAllTextAsync(completionFile, completion);

    var zshrcFile = Path.Combine(homeDir, ".zshrc");
    if (File.Exists(zshrcFile))
    {
        var zshrcContent = await File.ReadAllTextAsync(zshrcFile);
        var fpathCommand = $"fpath=({completionsDir} $fpath)";

        if (!zshrcContent.Contains(fpathCommand))
        {
            await File.AppendAllTextAsync(zshrcFile, $"\n# Freshdesk CLI completion\n{fpathCommand}\nautoload -Uz compinit && compinit\n");
        }
    }

    Console.WriteLine($"✓ Zsh completion installed to {completionFile}");
    Console.WriteLine("  Run 'source ~/.zshrc' to enable completion in current session");
    return 0;
}

static async Task<int> InstallPowerShellCompletion()
{
    var completion = CompletionGenerator.GeneratePowerShellCompletion();

    string? profilePath = null;
    if (OperatingSystem.IsWindows())
    {
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        profilePath = Path.Combine(documentsPath, "PowerShell", "Microsoft.PowerShell_profile.ps1");
    }
    else
    {
        var homeDir = Environment.GetEnvironmentVariable("HOME") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        profilePath = Path.Combine(homeDir, ".config", "powershell", "Microsoft.PowerShell_profile.ps1");
    }

    if (profilePath != null)
    {
        var profileDir = Path.GetDirectoryName(profilePath);
        if (profileDir != null)
            Directory.CreateDirectory(profileDir);

        var completionFile = Path.Combine(Path.GetDirectoryName(profilePath)!, "freshdesk-completion.ps1");
        await File.WriteAllTextAsync(completionFile, completion);

        if (!File.Exists(profilePath))
        {
            await File.WriteAllTextAsync(profilePath, $"# Freshdesk CLI completion\n. '{completionFile}'\n");
        }
        else
        {
            var profileContent = await File.ReadAllTextAsync(profilePath);
            var sourceCommand = $". '{completionFile}'";

            if (!profileContent.Contains("freshdesk-completion.ps1"))
            {
                await File.AppendAllTextAsync(profilePath, $"\n# Freshdesk CLI completion\n{sourceCommand}\n");
            }
        }

        Console.WriteLine($"✓ PowerShell completion installed to {completionFile}");
        Console.WriteLine($"  Run '. $PROFILE' to enable completion in current session");
    }
    else
    {
        Console.WriteLine("Could not determine PowerShell profile path");
        return 1;
    }

    return 0;
}

# Commands Implementation Guide

## Overview
CLI command structure using System.CommandLine with full AOT support and discoverable help for both humans and LLMs.

## Command Structure

```
freshdesk
├── auth
│   ├── login    # Configure credentials
│   ├── status   # Show current auth status
│   └── logout   # Remove credentials
├── tickets
│   ├── list     # List tickets with filters
│   ├── get      # Get ticket details
│   ├── search   # Search tickets
│   ├── update   # Update ticket properties
│   └── reply    # Reply to a ticket
├── attachments
│   ├── list     # List attachments for a ticket
│   └── download # Download attachments
├── config
│   ├── get      # Get configuration value
│   └── set      # Set configuration value
└── help         # Show help (with --format for LLM)
```

## Root Command Implementation

### Program.cs
```csharp
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Hosting;
using System.CommandLine.Parsing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FreshdeskCLI;

public sealed class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = CreateRootCommand();
        
        var parser = new CommandLineBuilder(rootCommand)
            .UseHost(_ => Host.CreateDefaultBuilder(args), builder =>
            {
                builder.ConfigureServices((context, services) =>
                {
                    ConfigureServices(services, context.Configuration);
                });
                
                builder.ConfigureLogging((context, logging) =>
                {
                    logging.ClearProviders();
                    
                    if (context.HostingEnvironment.IsDevelopment())
                    {
                        logging.AddConsole();
                    }
                });
            })
            .UseDefaults()
            .UseHelp(ctx =>
            {
                // Custom help provider for LLM support
                ctx.HelpBuilder.CustomizeLayout(GetHelpLayout);
            })
            .UseExceptionHandler((ex, context) =>
            {
                var console = context.Console;
                console.Error.WriteLine($"Error: {ex.Message}");
                
                if (ex is FreshdeskException)
                {
                    // Provide actionable error messages
                    console.Error.WriteLine(GetActionableErrorMessage(ex));
                }
                
                context.ExitCode = 1;
            })
            .Build();
        
        return await parser.InvokeAsync(args);
    }
    
    private static RootCommand CreateRootCommand()
    {
        var rootCommand = new RootCommand("Freshdesk CLI - Manage support tickets from the command line")
        {
            TreatUnmatchedTokensAsErrors = true
        };
        
        // Global options
        var outputOption = new Option<OutputFormat>(
            aliases: ["--output", "-o"],
            description: "Output format",
            getDefaultValue: () => OutputFormat.Table);
        
        var verboseOption = new Option<bool>(
            aliases: ["--verbose", "-v"],
            description: "Enable verbose output");
        
        var configOption = new Option<string?>(
            aliases: ["--config", "-c"],
            description: "Path to config file");
        
        rootCommand.AddGlobalOption(outputOption);
        rootCommand.AddGlobalOption(verboseOption);
        rootCommand.AddGlobalOption(configOption);
        
        // Add subcommands
        rootCommand.AddCommand(CreateAuthCommand());
        rootCommand.AddCommand(CreateTicketsCommand());
        rootCommand.AddCommand(CreateAttachmentsCommand());
        rootCommand.AddCommand(CreateConfigCommand());
        rootCommand.AddCommand(CreateHelpCommand());
        
        return rootCommand;
    }
    
    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Register JSON context
        services.AddSingleton<FreshdeskJsonContext>();
        
        // Register services
        services.AddSingleton<IConfigManager, ConfigManager>();
        services.AddSingleton<IRateLimitTracker, RateLimitTracker>();
        
        // Configure HTTP client
        services.AddHttpClient<IFreshdeskClient, FreshdeskClient>()
            .AddHttpMessageHandler<AuthenticationHandler>()
            .AddHttpMessageHandler<RateLimitHandler>();
        
        services.AddTransient<AuthenticationHandler>();
        services.AddTransient<RateLimitHandler>();
        
        services.AddHttpClient<IAttachmentDownloader, AttachmentDownloader>();
        
        // Register command handlers
        services.AddTransient<AuthCommandHandler>();
        services.AddTransient<TicketsCommandHandler>();
        services.AddTransient<AttachmentsCommandHandler>();
        services.AddTransient<ConfigCommandHandler>();
    }
}
```

## Auth Commands

### Commands/AuthCommand.cs
```csharp
namespace FreshdeskCLI.Commands;

public static class AuthCommandFactory
{
    public static Command CreateAuthCommand()
    {
        var authCommand = new Command("auth", "Manage authentication");
        
        authCommand.AddCommand(CreateLoginCommand());
        authCommand.AddCommand(CreateStatusCommand());
        authCommand.AddCommand(CreateLogoutCommand());
        
        return authCommand;
    }
    
    private static Command CreateLoginCommand()
    {
        var domainArg = new Argument<string>(
            "domain",
            "Freshdesk subdomain (e.g., 'acme' for acme.freshdesk.com)");
        
        var apiKeyOption = new Option<string?>(
            aliases: ["--api-key", "-k"],
            description: "API key (will prompt if not provided)");
        
        var command = new Command("login", "Configure Freshdesk credentials")
        {
            domainArg,
            apiKeyOption
        };
        
        command.SetHandler(async (context) =>
        {
            var handler = context.GetRequiredService<AuthCommandHandler>();
            var domain = context.ParseResult.GetValueForArgument(domainArg);
            var apiKey = context.ParseResult.GetValueForOption(apiKeyOption);
            
            await handler.LoginAsync(domain, apiKey, context.GetCancellationToken());
        });
        
        return command;
    }
    
    private static Command CreateStatusCommand()
    {
        var command = new Command("status", "Show authentication status");
        
        command.SetHandler(async (context) =>
        {
            var handler = context.GetRequiredService<AuthCommandHandler>();
            await handler.ShowStatusAsync(context.Console, context.GetCancellationToken());
        });
        
        return command;
    }
    
    private static Command CreateLogoutCommand()
    {
        var forceOption = new Option<bool>(
            aliases: ["--force", "-f"],
            description: "Skip confirmation prompt");
        
        var command = new Command("logout", "Remove stored credentials")
        {
            forceOption
        };
        
        command.SetHandler(async (context) =>
        {
            var handler = context.GetRequiredService<AuthCommandHandler>();
            var force = context.ParseResult.GetValueForOption(forceOption);
            
            await handler.LogoutAsync(force, context.Console, context.GetCancellationToken());
        });
        
        return command;
    }
}

public sealed class AuthCommandHandler
{
    private readonly IConfigManager _configManager;
    private readonly IFreshdeskClient _client;
    private readonly ILogger<AuthCommandHandler> _logger;
    
    public AuthCommandHandler(
        IConfigManager configManager,
        IFreshdeskClient client,
        ILogger<AuthCommandHandler> logger)
    {
        _configManager = configManager;
        _client = client;
        _logger = logger;
    }
    
    public async Task LoginAsync(string domain, string? apiKey, CancellationToken cancellationToken)
    {
        // Prompt for API key if not provided
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.Write("API Key: ");
            Console.ForegroundColor = ConsoleColor.Black;
            Console.BackgroundColor = ConsoleColor.Black;
            apiKey = Console.ReadLine();
            Console.ResetColor();
        }
        
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new ArgumentException("API key is required");
        }
        
        // Normalize domain
        if (!domain.Contains('.'))
        {
            domain = $"{domain}.freshdesk.com";
        }
        
        // Test credentials
        Console.WriteLine("Testing credentials...");
        
        try
        {
            _configManager.SetCredentials(domain, apiKey);
            
            // Make a test API call
            var tickets = await _client.GetTicketsAsync(1, 1, cancellationToken: cancellationToken);
            
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ Authentication successful!");
            Console.ResetColor();
            Console.WriteLine($"Credentials saved for {domain}");
        }
        catch (Exception ex)
        {
            _configManager.ClearCredentials();
            
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("✗ Authentication failed");
            Console.ResetColor();
            Console.WriteLine($"Error: {ex.Message}");
            
            throw;
        }
    }
    
    public Task ShowStatusAsync(IConsole console, CancellationToken cancellationToken)
    {
        var config = _configManager.Load();
        
        if (config == null)
        {
            console.WriteLine("Not authenticated");
            console.WriteLine("Run 'freshdesk auth login' to configure credentials");
            return Task.CompletedTask;
        }
        
        console.WriteLine($"Authenticated: {config.Domain}");
        console.WriteLine($"API Key: {MaskApiKey(config.ApiKey)}");
        
        if (!string.IsNullOrEmpty(config.DefaultDownloadPath))
        {
            console.WriteLine($"Download Path: {config.DefaultDownloadPath}");
        }
        
        return Task.CompletedTask;
    }
    
    public Task LogoutAsync(bool force, IConsole console, CancellationToken cancellationToken)
    {
        if (!_configManager.Exists())
        {
            console.WriteLine("No credentials stored");
            return Task.CompletedTask;
        }
        
        if (!force)
        {
            console.Write("Remove stored credentials? [y/N]: ");
            var response = Console.ReadLine();
            
            if (response?.ToLowerInvariant() != "y")
            {
                console.WriteLine("Cancelled");
                return Task.CompletedTask;
            }
        }
        
        _configManager.ClearCredentials();
        console.WriteLine("Credentials removed");
        
        return Task.CompletedTask;
    }
    
    private static string MaskApiKey(string apiKey)
    {
        if (apiKey.Length <= 8)
        {
            return "****";
        }
        
        return $"{apiKey[..4]}...{apiKey[^4..]}";
    }
}
```

## Tickets Commands

### Commands/TicketsCommand.cs
```csharp
namespace FreshdeskCLI.Commands;

public static class TicketsCommandFactory
{
    public static Command CreateTicketsCommand()
    {
        var command = new Command("tickets", "Manage support tickets");
        
        command.AddCommand(CreateListCommand());
        command.AddCommand(CreateGetCommand());
        command.AddCommand(CreateSearchCommand());
        command.AddCommand(CreateUpdateCommand());
        command.AddCommand(CreateReplyCommand());
        
        return command;
    }
    
    private static Command CreateListCommand()
    {
        var pageOption = new Option<int>(
            aliases: ["--page", "-p"],
            description: "Page number",
            getDefaultValue: () => 1);
        
        var perPageOption = new Option<int>(
            aliases: ["--per-page", "-n"],
            description: "Items per page (max 100)",
            getDefaultValue: () => 30);
        
        var statusOption = new Option<TicketStatus?>(
            aliases: ["--status", "-s"],
            description: "Filter by status");
        
        var command = new Command("list", "List tickets")
        {
            pageOption,
            perPageOption,
            statusOption
        };
        
        command.SetHandler(async (context) =>
        {
            var handler = context.GetRequiredService<TicketsCommandHandler>();
            var output = context.ParseResult.GetValueForOption(outputOption);
            
            await handler.ListTicketsAsync(
                context.ParseResult.GetValueForOption(pageOption),
                context.ParseResult.GetValueForOption(perPageOption),
                context.ParseResult.GetValueForOption(statusOption),
                output,
                context.Console,
                context.GetCancellationToken());
        });
        
        return command;
    }
    
    private static Command CreateGetCommand()
    {
        var idArg = new Argument<long>("id", "Ticket ID");
        
        var conversationsOption = new Option<bool>(
            aliases: ["--conversations", "-c"],
            description: "Include conversations");
        
        var command = new Command("get", "Get ticket details")
        {
            idArg,
            conversationsOption
        };
        
        command.SetHandler(async (context) =>
        {
            var handler = context.GetRequiredService<TicketsCommandHandler>();
            
            await handler.GetTicketAsync(
                context.ParseResult.GetValueForArgument(idArg),
                context.ParseResult.GetValueForOption(conversationsOption),
                context.ParseResult.GetValueForOption(outputOption),
                context.Console,
                context.GetCancellationToken());
        });
        
        return command;
    }
    
    private static Command CreateSearchCommand()
    {
        var queryArg = new Argument<string>("query", "Search query");
        
        var command = new Command("search", "Search tickets")
        {
            queryArg
        };
        
        command.SetHandler(async (context) =>
        {
            var handler = context.GetRequiredService<TicketsCommandHandler>();
            
            await handler.SearchTicketsAsync(
                context.ParseResult.GetValueForArgument(queryArg),
                context.ParseResult.GetValueForOption(outputOption),
                context.Console,
                context.GetCancellationToken());
        });
        
        return command;
    }
}

public sealed class TicketsCommandHandler
{
    private readonly IFreshdeskClient _client;
    private readonly IOutputFormatter _formatter;
    private readonly ILogger<TicketsCommandHandler> _logger;
    
    public TicketsCommandHandler(
        IFreshdeskClient client,
        IOutputFormatter formatter,
        ILogger<TicketsCommandHandler> logger)
    {
        _client = client;
        _formatter = formatter;
        _logger = logger;
    }
    
    public async Task ListTicketsAsync(
        int page,
        int perPage,
        TicketStatus? status,
        OutputFormat format,
        IConsole console,
        CancellationToken cancellationToken)
    {
        var response = await _client.GetTicketsAsync(page, perPage, status, cancellationToken);
        
        if (format == OutputFormat.Json)
        {
            var json = JsonSerializer.Serialize(
                response.Tickets, 
                FreshdeskJsonContext.Default.TicketArray);
            console.WriteLine(json);
        }
        else if (format == OutputFormat.Table)
        {
            var table = new Table()
                .AddColumn("ID")
                .AddColumn("Subject")
                .AddColumn("Status")
                .AddColumn("Priority")
                .AddColumn("Created");
            
            foreach (var ticket in response.Tickets)
            {
                table.AddRow(
                    ticket.Id.ToString(),
                    Truncate(ticket.Subject, 40),
                    ticket.StatusText,
                    ticket.PriorityText,
                    ticket.CreatedAt.ToString("yyyy-MM-dd"));
            }
            
            console.Write(table.ToString());
        }
        
        if (response.HasNextPage)
        {
            console.WriteLine($"\nPage {page} of many (use --page {page + 1} for next)");
        }
    }
    
    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        
        return value.Length <= maxLength 
            ? value 
            : value[..(maxLength - 3)] + "...";
    }
}
```

## Help System for LLMs

### Commands/HelpCommand.cs
```csharp
namespace FreshdeskCLI.Commands;

public static class HelpCommandFactory
{
    public static Command CreateHelpCommand()
    {
        var formatOption = new Option<HelpFormat>(
            aliases: ["--format", "-f"],
            description: "Output format for help",
            getDefaultValue: () => HelpFormat.Human);
        
        var commandArg = new Argument<string?>(
            "command",
            description: "Command to get help for",
            getDefaultValue: () => null);
        
        var command = new Command("help", "Show help information")
        {
            formatOption,
            commandArg
        };
        
        command.SetHandler((context) =>
        {
            var format = context.ParseResult.GetValueForOption(formatOption);
            var commandName = context.ParseResult.GetValueForArgument(commandArg);
            
            if (format == HelpFormat.LLM)
            {
                OutputLLMHelp(context.Console, commandName);
            }
            else if (format == HelpFormat.Json)
            {
                OutputJsonHelp(context.Console, commandName);
            }
            else
            {
                OutputHumanHelp(context.Console, commandName);
            }
        });
        
        return command;
    }
    
    private static void OutputLLMHelp(IConsole console, string? commandName)
    {
        var help = new LLMHelpDocument
        {
            Tool = "freshdesk",
            Version = GetVersion(),
            Description = "CLI tool for managing Freshdesk support tickets",
            Authentication = new AuthenticationInfo
            {
                Required = true,
                Method = "API Key",
                SetupCommand = "freshdesk auth login <domain> --api-key <key>",
                EnvironmentVariables = ["FRESHDESK_DOMAIN", "FRESHDESK_API_KEY"]
            },
            Commands = GetCommandSchema(),
            Examples = GetExamples(),
            ErrorCodes = GetErrorCodes()
        };
        
        var json = JsonSerializer.Serialize(help, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        
        console.WriteLine(json);
    }
    
    private static List<CommandSchema> GetCommandSchema()
    {
        return new List<CommandSchema>
        {
            new CommandSchema
            {
                Name = "freshdesk tickets list",
                Description = "List support tickets",
                Parameters = new List<ParameterSchema>
                {
                    new() { Name = "--page", Type = "int", Default = "1", Description = "Page number" },
                    new() { Name = "--per-page", Type = "int", Default = "30", Description = "Items per page" },
                    new() { Name = "--status", Type = "enum", Values = ["open", "pending", "resolved", "closed"], Description = "Filter by status" }
                },
                OutputFormat = "Table or JSON",
                Example = "freshdesk tickets list --status open --output json"
            },
            new CommandSchema
            {
                Name = "freshdesk tickets get",
                Description = "Get detailed information about a specific ticket",
                Parameters = new List<ParameterSchema>
                {
                    new() { Name = "id", Type = "long", Required = true, Description = "Ticket ID" },
                    new() { Name = "--conversations", Type = "bool", Default = "false", Description = "Include conversation history" }
                },
                OutputFormat = "Detailed ticket information in specified format",
                Example = "freshdesk tickets get 12345 --conversations"
            },
            new CommandSchema
            {
                Name = "freshdesk attachments download",
                Description = "Download all attachments from a ticket",
                Parameters = new List<ParameterSchema>
                {
                    new() { Name = "ticket-id", Type = "long", Required = true, Description = "Ticket ID" },
                    new() { Name = "--output", Type = "string", Default = "./attachments", Description = "Output directory" }
                },
                OutputFormat = "Progress indicators and download summary",
                Example = "freshdesk attachments download 12345 --output ~/downloads"
            }
        };
    }
}

public class LLMHelpDocument
{
    public string Tool { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AuthenticationInfo Authentication { get; set; } = new();
    public List<CommandSchema> Commands { get; set; } = new();
    public List<Example> Examples { get; set; } = new();
    public Dictionary<int, string> ErrorCodes { get; set; } = new();
}

public class CommandSchema
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<ParameterSchema> Parameters { get; set; } = new();
    public string OutputFormat { get; set; } = string.Empty;
    public string Example { get; set; } = string.Empty;
}

public class ParameterSchema
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool Required { get; set; }
    public string? Default { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<string>? Values { get; set; }
}
```

## Output Formatting

### Commands/OutputFormatter.cs
```csharp
namespace FreshdeskCLI.Commands;

public interface IOutputFormatter
{
    void FormatTicket(Ticket ticket, OutputFormat format, IConsole console);
    void FormatTicketList(IEnumerable<Ticket> tickets, OutputFormat format, IConsole console);
    void FormatAttachmentList(IEnumerable<Attachment> attachments, OutputFormat format, IConsole console);
    void FormatError(Exception exception, IConsole console);
}

public sealed class OutputFormatter : IOutputFormatter
{
    private readonly FreshdeskJsonContext _jsonContext;
    
    public OutputFormatter(FreshdeskJsonContext jsonContext)
    {
        _jsonContext = jsonContext;
    }
    
    public void FormatTicket(Ticket ticket, OutputFormat format, IConsole console)
    {
        switch (format)
        {
            case OutputFormat.Json:
                var json = JsonSerializer.Serialize(ticket, _jsonContext.Ticket);
                console.WriteLine(json);
                break;
                
            case OutputFormat.Yaml:
                // Simple YAML output
                console.WriteLine($"id: {ticket.Id}");
                console.WriteLine($"subject: {ticket.Subject}");
                console.WriteLine($"status: {ticket.StatusText}");
                console.WriteLine($"priority: {ticket.PriorityText}");
                console.WriteLine($"created_at: {ticket.CreatedAt:O}");
                console.WriteLine($"updated_at: {ticket.UpdatedAt:O}");
                
                if (ticket.Attachments.Length > 0)
                {
                    console.WriteLine("attachments:");
                    foreach (var attachment in ticket.Attachments)
                    {
                        console.WriteLine($"  - name: {attachment.Name}");
                        console.WriteLine($"    size: {attachment.FormattedSize}");
                    }
                }
                break;
                
            case OutputFormat.Table:
            default:
                console.WriteLine($"Ticket #{ticket.Id}");
                console.WriteLine(new string('-', 50));
                console.WriteLine($"Subject:  {ticket.Subject}");
                console.WriteLine($"Status:   {ticket.StatusText}");
                console.WriteLine($"Priority: {ticket.PriorityText}");
                console.WriteLine($"Created:  {ticket.CreatedAt:yyyy-MM-dd HH:mm}");
                console.WriteLine($"Updated:  {ticket.UpdatedAt:yyyy-MM-dd HH:mm}");
                
                if (ticket.DueBy.HasValue)
                {
                    console.WriteLine($"Due By:   {ticket.DueBy:yyyy-MM-dd HH:mm}");
                }
                
                if (ticket.Tags.Length > 0)
                {
                    console.WriteLine($"Tags:     {string.Join(", ", ticket.Tags)}");
                }
                
                if (ticket.Attachments.Length > 0)
                {
                    console.WriteLine($"\nAttachments ({ticket.Attachments.Length}):");
                    foreach (var attachment in ticket.Attachments)
                    {
                        console.WriteLine($"  • {attachment.Name} ({attachment.FormattedSize})");
                    }
                }
                break;
        }
    }
}

public enum OutputFormat
{
    Table,
    Json,
    Yaml,
    Csv
}

public enum HelpFormat
{
    Human,
    LLM,
    Json
}
```

## Progress Indicators

### Commands/ProgressReporter.cs
```csharp
namespace FreshdeskCLI.Commands;

public sealed class ConsoleProgressReporter : IProgress<DownloadProgress>
{
    private readonly IConsole _console;
    private readonly object _lock = new();
    private int _lastLineLength = 0;
    
    public ConsoleProgressReporter(IConsole console)
    {
        _console = console;
    }
    
    public void Report(DownloadProgress value)
    {
        lock (_lock)
        {
            // Clear previous line
            if (_lastLineLength > 0)
            {
                _console.Write($"\r{new string(' ', _lastLineLength)}\r");
            }
            
            var message = $"Downloading: {value.CurrentFile} " +
                         $"[{value.CompletedFiles}/{value.TotalFiles}] " +
                         $"{value.PercentComplete:F1}%";
            
            _console.Write(message);
            _lastLineLength = message.Length;
            
            if (value.CompletedFiles == value.TotalFiles)
            {
                _console.WriteLine(); // New line when complete
                _lastLineLength = 0;
            }
        }
    }
}
```
using System.Collections.Generic;

namespace FreshdeskCLI.Helpers;

public static class CommandHelp
{
    internal static readonly Dictionary<string, CommandHelpInfo> HelpRegistry = new()
    {
        ["config"] = new CommandHelpInfo
        {
            Usage = "freshdesk config <subcommand> [options]",
            Description = "Manage Freshdesk CLI configuration",
            Subcommands = new Dictionary<string, string>
            {
                ["set"] = "Set configuration values",
                ["get"] = "Get current configuration",
                ["test"] = "Test connection to Freshdesk API"
            }
        },
        ["config set"] = new CommandHelpInfo
        {
            Usage = "freshdesk config set [options]",
            Description = "Set Freshdesk configuration values",
            Options = new Dictionary<string, string>
            {
                ["--domain, -d <domain>"] = "Freshdesk domain (e.g., 'yourcompany')",
                ["--api-key, -k <key>"] = "Freshdesk API key"
            },
            Examples = new[]
            {
                "freshdesk config set --domain mycompany --api-key abc123xyz",
                "freshdesk config set -d mycompany -k abc123xyz"
            }
        },
        ["config get"] = new CommandHelpInfo
        {
            Usage = "freshdesk config get",
            Description = "Display the current Freshdesk configuration",
            Examples = new[] { "freshdesk config get" }
        },
        ["config test"] = new CommandHelpInfo
        {
            Usage = "freshdesk config test",
            Description = "Test the connection to Freshdesk API using current configuration",
            Examples = new[] { "freshdesk config test" }
        },
        ["ticket"] = new CommandHelpInfo
        {
            Usage = "freshdesk ticket <subcommand> [options]",
            Description = "Manage Freshdesk tickets",
            Subcommands = new Dictionary<string, string>
            {
                ["list"] = "List tickets",
                ["get"] = "Get ticket details",
                ["create"] = "Create a new ticket",
                ["update"] = "Update ticket status/priority",
                ["search"] = "Search tickets",
                ["reply"] = "Reply to a ticket",
                ["note"] = "Add internal note to a ticket"
            }
        },
        ["ticket list"] = new CommandHelpInfo
        {
            Usage = "freshdesk ticket list [options]",
            Description = "List tickets with optional filtering",
            Options = new Dictionary<string, string>
            {
                ["--page, -p <number>"] = "Page number (default: 1)",
                ["--limit, -l <number>"] = "Items per page (default: 30)",
                ["--status, -s <status>"] = "Filter by status (open, pending, resolved, closed)",
                ["--email, --customer, -e <email>"] = "Filter by customer email",
                ["--unresolved"] = "Show only unresolved tickets (excludes resolved and closed)",
                ["--format, -f <format>"] = "Output format (table, json, csv) (default: table)"
            },
            Examples = new[]
            {
                "freshdesk ticket list --status open",
                "freshdesk ticket list --email customer@example.com",
                "freshdesk ticket list --unresolved",
                "freshdesk ticket list --page 2 --limit 50 --format json"
            }
        },
        ["ticket get"] = new CommandHelpInfo
        {
            Usage = "freshdesk ticket get <ticket-id> [options]",
            Description = "Get detailed information about a specific ticket",
            Options = new Dictionary<string, string>
            {
                ["--format, -f <format>"] = "Output format (json, text) (default: text)",
                ["--tree"] = "Show conversation tree structure",
                ["--conversations"] = "Include full conversation bodies",
                ["--full"] = "Show everything (conversations + attachments)"
            },
            Examples = new[]
            {
                "freshdesk ticket get 123",
                "freshdesk ticket get 123 --tree",
                "freshdesk ticket get 123 --format json --full"
            }
        },
        ["ticket create"] = new CommandHelpInfo
        {
            Usage = "freshdesk ticket create [options]",
            Description = "Create a new support ticket",
            RequiredOptions = new Dictionary<string, string>
            {
                ["--subject, -s <subject>"] = "Ticket subject",
                ["--email, -e <email>"] = "Requester email"
            },
            Options = new Dictionary<string, string>
            {
                ["--description, -d <desc>"] = "Ticket description",
                ["--priority, -p <priority>"] = "Priority (Low, Medium, High, Urgent) (default: Low)"
            },
            Examples = new[]
            {
                "freshdesk ticket create --subject \"Login issue\" --email user@example.com",
                "freshdesk ticket create -s \"Urgent bug\" -e user@example.com -p High -d \"Details here\""
            }
        },
        ["ticket update"] = new CommandHelpInfo
        {
            Usage = "freshdesk ticket update <ticket-id> [options]",
            Description = "Update ticket status or priority",
            Options = new Dictionary<string, string>
            {
                ["--status, -s <status>"] = "New status (open, pending, resolved, closed)",
                ["--priority, -p <priority>"] = "New priority (Low, Medium, High, Urgent)"
            },
            Examples = new[]
            {
                "freshdesk ticket update 123 --status resolved",
                "freshdesk ticket update 123 --priority High",
                "freshdesk ticket update 123 -s pending -p Medium"
            }
        },
        ["ticket search"] = new CommandHelpInfo
        {
            Usage = "freshdesk ticket search [query] [options]",
            Description = "Search for tickets matching criteria",
            Options = new Dictionary<string, string>
            {
                ["--query, -q <text>"] = "Search in subject/description",
                ["--status, -s <status>"] = "Filter by status",
                ["--priority, -p <priority>"] = "Filter by priority",
                ["--email, -e <email>"] = "Filter by customer email",
                ["--format, -f <format>"] = "Output format (table, json, csv)"
            },
            Examples = new[]
            {
                "freshdesk ticket search \"login issue\"",
                "freshdesk ticket search --status open --priority high",
                "freshdesk ticket search --email john@example.com"
            }
        },
        ["ticket reply"] = new CommandHelpInfo
        {
            Usage = "freshdesk ticket reply <ticket-id> [options]",
            Description = "Reply to a ticket",
            Options = new Dictionary<string, string>
            {
                ["--message, -m <text>"] = "Reply message (or will prompt)",
                ["--file, -f <path>"] = "Read message from file"
            },
            Examples = new[]
            {
                "freshdesk ticket reply 123 --message \"Thanks for contacting us\"",
                "freshdesk ticket reply 123 --file reply.txt"
            }
        },
        ["ticket note"] = new CommandHelpInfo
        {
            Usage = "freshdesk ticket note <ticket-id> [options]",
            Description = "Add an internal note to a ticket",
            Options = new Dictionary<string, string>
            {
                ["--message, -m <text>"] = "Note message (or will prompt)",
                ["--file, -f <path>"] = "Read message from file"
            },
            Examples = new[]
            {
                "freshdesk ticket note 123 --message \"Customer issue resolved\"",
                "freshdesk ticket note 123 --file note.txt"
            }
        },
        ["attachment"] = new CommandHelpInfo
        {
            Usage = "freshdesk attachment <subcommand> [options]",
            Description = "Manage ticket attachments",
            Subcommands = new Dictionary<string, string>
            {
                ["list"] = "List attachments for a ticket",
                ["download"] = "Download an attachment",
                ["upload"] = "Upload an attachment to a ticket"
            }
        },
        ["attachment list"] = new CommandHelpInfo
        {
            Usage = "freshdesk attachment list <ticket-id> [options]",
            Description = "List all attachments for a ticket",
            Options = new Dictionary<string, string>
            {
                ["--include-conversations"] = "Include attachments from conversation replies"
            },
            Examples = new[]
            {
                "freshdesk attachment list 123",
                "freshdesk attachment list 123 --include-conversations"
            }
        },
        ["attachment download"] = new CommandHelpInfo
        {
            Usage = "freshdesk attachment download <ticket-id> <attachment-id|all> [options]",
            Description = "Download attachment(s) from a ticket",
            Options = new Dictionary<string, string>
            {
                ["--output, -o <path>"] = "Output file or directory path",
                ["--include-conversations"] = "Include conversation attachments (when using 'all')"
            },
            Examples = new[]
            {
                "freshdesk attachment download 123 456",
                "freshdesk attachment download 123 456 --output myfile.pdf",
                "freshdesk attachment download 123 all --include-conversations"
            }
        },
        ["attachment upload"] = new CommandHelpInfo
        {
            Usage = "freshdesk attachment upload <ticket-id> <file-path> [options]",
            Description = "Upload a file as attachment to a ticket",
            Options = new Dictionary<string, string>
            {
                ["--name, -n <filename>"] = "Override filename"
            },
            Examples = new[]
            {
                "freshdesk attachment upload 123 document.pdf",
                "freshdesk attachment upload 123 /path/to/file.jpg --name screenshot.jpg"
            }
        },
        ["export"] = new CommandHelpInfo
        {
            Usage = "freshdesk export <subcommand> [options]",
            Description = "Export tickets to various formats",
            Subcommands = new Dictionary<string, string>
            {
                ["tickets"] = "Export multiple tickets",
                ["ticket"] = "Export a single ticket with full details"
            }
        },
        ["export tickets"] = new CommandHelpInfo
        {
            Usage = "freshdesk export tickets [options]",
            Description = "Export multiple tickets to a file",
            Options = new Dictionary<string, string>
            {
                ["--output, -o <path>"] = "Output file path",
                ["--format, -f <format>"] = "Export format (json, csv, xml, markdown)",
                ["--include-conversations"] = "Include conversation history",
                ["--status <status>"] = "Filter by status",
                ["--priority <priority>"] = "Filter by priority",
                ["--email <email>"] = "Filter by customer email",
                ["--limit <number>"] = "Maximum number of tickets to export"
            },
            Examples = new[]
            {
                "freshdesk export tickets --format csv --output tickets.csv",
                "freshdesk export tickets --status open --include-conversations",
                "freshdesk export tickets --limit 100 --format json"
            }
        },
        ["export ticket"] = new CommandHelpInfo
        {
            Usage = "freshdesk export ticket <ticket-id> [options]",
            Description = "Export a single ticket with full details",
            Options = new Dictionary<string, string>
            {
                ["--output, -o <path>"] = "Output file path",
                ["--format, -f <format>"] = "Export format (json, csv, xml, markdown)",
                ["--include-conversations"] = "Include conversation history"
            },
            Examples = new[]
            {
                "freshdesk export ticket 123 --format markdown",
                "freshdesk export ticket 123 --include-conversations --output ticket.json"
            }
        },
        ["update"] = new CommandHelpInfo
        {
            Usage = "freshdesk update [options]",
            Description = "Check for and install updates",
            Options = new Dictionary<string, string>
            {
                ["--check, -c"] = "Only check for updates, don't install",
                ["--force, -f"] = "Skip confirmation prompt"
            },
            Examples = new[]
            {
                "freshdesk update",
                "freshdesk update --check",
                "freshdesk update --force"
            }
        }
    };

    public static bool CheckForHelp(string[] args)
    {
        return args.Contains("--help") || args.Contains("-h");
    }

    public static void ShowHelp(params string[] commandPath)
    {
        var key = string.Join(" ", commandPath);

        if (!HelpRegistry.TryGetValue(key, out var helpInfo))
        {
            Console.WriteLine($"No help available for '{key}'");
            return;
        }

        Console.WriteLine($"Usage: {helpInfo.Usage}");

        if (!string.IsNullOrEmpty(helpInfo.Description))
        {
            Console.WriteLine();
            Console.WriteLine(helpInfo.Description);
        }

        if (helpInfo.Subcommands?.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Subcommands:");
            foreach (var (name, desc) in helpInfo.Subcommands)
            {
                Console.WriteLine($"  {name,-15} {desc}");
            }
        }

        if (helpInfo.RequiredOptions?.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Required options:");
            foreach (var (option, desc) in helpInfo.RequiredOptions)
            {
                Console.WriteLine($"  {option,-35} {desc}");
            }
        }

        if (helpInfo.Options?.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Options:");
            foreach (var (option, desc) in helpInfo.Options)
            {
                Console.WriteLine($"  {option,-35} {desc}");
            }
        }

        if (helpInfo.Subcommands?.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"Use 'freshdesk {key} <subcommand> --help' for more information on a subcommand.");
        }

        if (helpInfo.Examples?.Length > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Examples:");
            foreach (var example in helpInfo.Examples)
            {
                Console.WriteLine($"  {example}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("  --help, -h                          Show this help message");
    }

    public static int ShowHelpAndReturn(params string[] commandPath)
    {
        ShowHelp(commandPath);
        return 0;
    }
}

public class CommandHelpInfo
{
    public string Usage { get; set; } = "";
    public string Description { get; set; } = "";
    public Dictionary<string, string>? Subcommands { get; set; }
    public Dictionary<string, string>? RequiredOptions { get; set; }
    public Dictionary<string, string>? Options { get; set; }
    public string[]? Examples { get; set; }
}
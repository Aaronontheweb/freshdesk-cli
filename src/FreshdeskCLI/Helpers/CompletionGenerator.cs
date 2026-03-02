using System.Text;
using System.Linq;
using System.Collections.Generic;

namespace FreshdeskCLI.Helpers;

public static class CompletionGenerator
{
    private static readonly string[] GlobalOptions = ["--help", "-h", "--version", "-v", "--about", "--read-only", "-ro"];
    private static readonly string[] StatusValues = ["open", "pending", "resolved", "closed"];
    private static readonly string[] PriorityValues = ["low", "medium", "high", "urgent"];
    private static readonly string[] FormatValues = ["json", "table", "csv"];

    private static string[] GetTopLevelCommands()
    {
        var topLevel = CommandHelp.HelpRegistry.Keys
            .Where(k => !k.Contains(' '))
            .ToArray();
        return topLevel.Concat(["install-completion", "update", "bulk"]).ToArray();
    }

    private static Dictionary<string, string[]> GetSubCommands()
    {
        var result = new Dictionary<string, string[]>();

        foreach (var kvp in CommandHelp.HelpRegistry)
        {
            if (!kvp.Key.Contains(' ') && kvp.Value.Subcommands != null)
            {
                result[kvp.Key] = kvp.Value.Subcommands.Keys.ToArray();
            }
        }

        result["install-completion"] = [];
        result["bulk"] = ["update", "export", "delete"];
        return result;
    }

    private static Dictionary<string, string[]> GetCommandOptions()
    {
        var result = new Dictionary<string, string[]>
        {
            ["global"] = GlobalOptions
        };

        foreach (var kvp in CommandHelp.HelpRegistry)
        {
            if (kvp.Value.Options != null)
            {
                var options = new List<string>();
                foreach (var opt in kvp.Value.Options.Keys)
                {
                    var parts = opt.Split(',').Select(p => p.Trim().Split(' ')[0]);
                    options.AddRange(parts);
                }
                if (kvp.Value.RequiredOptions != null)
                {
                    foreach (var opt in kvp.Value.RequiredOptions.Keys)
                    {
                        var parts = opt.Split(',').Select(p => p.Trim().Split(' ')[0]);
                        options.AddRange(parts);
                    }
                }
                options.Add("--help");
                options.Add("-h");

                var key = kvp.Key.Replace(" ", ".");
                result[key] = options.Distinct().ToArray();
            }
        }

        result["bulk.update"] = ["--ids", "--status", "--priority", "--assignee", "--help", "-h"];
        result["bulk.export"] = ["--ids", "--output", "--format", "--help", "-h"];
        result["bulk.delete"] = ["--ids", "--confirm", "--help", "-h"];

        return result;
    }

    public static string GenerateBashCompletion()
    {
        var topLevelCommands = GetTopLevelCommands();
        var subCommands = GetSubCommands();
        var commandOptions = GetCommandOptions();

        var sb = new StringBuilder();

        sb.AppendLine("#!/bin/bash");
        sb.AppendLine("# Bash completion script for freshdesk CLI");
        sb.AppendLine("# Generated automatically - do not edit manually");
        sb.AppendLine();
        sb.AppendLine("_freshdesk() {");
        sb.AppendLine("    local cur prev");
        sb.AppendLine("    COMPREPLY=()");
        sb.AppendLine("    cur=\"${COMP_WORDS[COMP_CWORD]}\"");
        sb.AppendLine("    prev=\"${COMP_WORDS[COMP_CWORD-1]}\"");
        sb.AppendLine();

        sb.AppendLine("    # Handle option value completion");
        sb.AppendLine("    case \"${prev}\" in");
        sb.AppendLine("        --status)");
        sb.AppendLine($"            COMPREPLY=( $(compgen -W \"{string.Join(" ", StatusValues)}\" -- \"${{cur}}\") )");
        sb.AppendLine("            return 0");
        sb.AppendLine("            ;;");
        sb.AppendLine("        --priority)");
        sb.AppendLine($"            COMPREPLY=( $(compgen -W \"{string.Join(" ", PriorityValues)}\" -- \"${{cur}}\") )");
        sb.AppendLine("            return 0");
        sb.AppendLine("            ;;");
        sb.AppendLine("        --format)");
        sb.AppendLine($"            COMPREPLY=( $(compgen -W \"{string.Join(" ", FormatValues)}\" -- \"${{cur}}\") )");
        sb.AppendLine("            return 0");
        sb.AppendLine("            ;;");
        sb.AppendLine("        --file|--output|-o)");
        sb.AppendLine("            COMPREPLY=( $(compgen -f -- \"${cur}\") )");
        sb.AppendLine("            return 0");
        sb.AppendLine("            ;;");
        sb.AppendLine("    esac");
        sb.AppendLine();

        sb.AppendLine("    # Command position detection");
        sb.AppendLine("    local cmd subcmd");
        sb.AppendLine("    if [ $COMP_CWORD -ge 1 ]; then");
        sb.AppendLine("        cmd=\"${COMP_WORDS[1]}\"");
        sb.AppendLine("    fi");
        sb.AppendLine("    if [ $COMP_CWORD -ge 2 ]; then");
        sb.AppendLine("        subcmd=\"${COMP_WORDS[2]}\"");
        sb.AppendLine("    fi");
        sb.AppendLine();

        sb.AppendLine("    # First level: freshdesk [command]");
        sb.AppendLine("    if [ $COMP_CWORD -eq 1 ]; then");
        sb.AppendLine($"        COMPREPLY=( $(compgen -W \"{string.Join(" ", topLevelCommands.Concat(commandOptions["global"]))}\" -- \"${{cur}}\") )");
        sb.AppendLine("        return 0");
        sb.AppendLine("    fi");
        sb.AppendLine();

        sb.AppendLine("    # Second level: freshdesk [command] [subcommand]");
        sb.AppendLine("    if [ $COMP_CWORD -eq 2 ]; then");
        sb.AppendLine("        case \"${cmd}\" in");
        foreach (var kvp in subCommands)
        {
            if (kvp.Value.Length > 0)
            {
                sb.AppendLine($"            {kvp.Key})");
                sb.AppendLine($"                COMPREPLY=( $(compgen -W \"{string.Join(" ", kvp.Value)}\" -- \"${{cur}}\") )");
                sb.AppendLine("                return 0");
                sb.AppendLine("                ;;");
            }
        }
        sb.AppendLine("        esac");
        sb.AppendLine("    fi");
        sb.AppendLine();

        sb.AppendLine("    # Third level and beyond: options for specific commands");
        sb.AppendLine("    if [ $COMP_CWORD -ge 3 ]; then");
        sb.AppendLine("        local cmd_key=\"${cmd}.${subcmd}\"");
        sb.AppendLine("        case \"${cmd_key}\" in");
        foreach (var kvp in commandOptions.Where(k => k.Key.Contains('.')))
        {
            sb.AppendLine($"            {kvp.Key})");
            sb.AppendLine($"                COMPREPLY=( $(compgen -W \"{string.Join(" ", kvp.Value)}\" -- \"${{cur}}\") )");
            sb.AppendLine("                return 0");
            sb.AppendLine("                ;;");
        }
        sb.AppendLine("        esac");
        sb.AppendLine("    fi");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("complete -F _freshdesk freshdesk");

        return sb.ToString();
    }

    public static string GenerateZshCompletion()
    {
        var topLevelCommands = GetTopLevelCommands();
        var subCommands = GetSubCommands();

        var sb = new StringBuilder();

        sb.AppendLine("#compdef freshdesk");
        sb.AppendLine("# Zsh completion script for freshdesk CLI");
        sb.AppendLine("# Generated automatically - do not edit manually");
        sb.AppendLine();
        sb.AppendLine("_freshdesk() {");
        sb.AppendLine("    local line state");
        sb.AppendLine();
        sb.AppendLine("    _arguments -C \\");
        sb.AppendLine("        '1: :_freshdesk_commands' \\");
        sb.AppendLine("        '*::arg:->args'");
        sb.AppendLine();
        sb.AppendLine("    case $line[1] in");
        foreach (var kvp in subCommands)
        {
            sb.AppendLine($"        {kvp.Key})");
            sb.AppendLine($"            _freshdesk_{kvp.Key}");
            sb.AppendLine("            ;;");
        }
        sb.AppendLine("    esac");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("_freshdesk_commands() {");
        sb.AppendLine("    local commands; commands=(");
        foreach (var cmd in topLevelCommands)
        {
            sb.AppendLine($"        '{cmd}:Manage {cmd}'");
        }
        sb.AppendLine("    )");
        sb.AppendLine("    _describe -t commands 'freshdesk command' commands");
        sb.AppendLine("}");
        sb.AppendLine();

        foreach (var kvp in subCommands.Where(k => k.Value.Length > 0))
        {
            sb.AppendLine($"_freshdesk_{kvp.Key}() {{");
            sb.AppendLine("    local commands; commands=(");
            foreach (var subcmd in kvp.Value)
            {
                sb.AppendLine($"        '{subcmd}:{GetSubcommandDescription(kvp.Key, subcmd)}'");
            }
            sb.AppendLine("    )");
            sb.AppendLine($"    _describe -t commands 'freshdesk {kvp.Key} command' commands");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        sb.AppendLine("_freshdesk \"$@\"");

        return sb.ToString();
    }

    public static string GeneratePowerShellCompletion()
    {
        var topLevelCommands = GetTopLevelCommands();
        var subCommands = GetSubCommands();

        var sb = new StringBuilder();

        sb.AppendLine("# PowerShell completion script for freshdesk CLI");
        sb.AppendLine("# Generated automatically - do not edit manually");
        sb.AppendLine();
        sb.AppendLine("Register-ArgumentCompleter -Native -CommandName freshdesk -ScriptBlock {");
        sb.AppendLine("    param($wordToComplete, $commandAst, $cursorPosition)");
        sb.AppendLine();
        sb.AppendLine("    $commands = @{");

        foreach (var cmd in topLevelCommands)
        {
            sb.AppendLine($"        '{cmd}' = @{{");
            if (subCommands.ContainsKey(cmd) && subCommands[cmd].Length > 0)
            {
                sb.AppendLine($"            'subcommands' = @({string.Join(", ", subCommands[cmd].Select(s => $"'{s}'"))})");
            }
            sb.AppendLine("        }");
        }
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine("    $completions = @()");
        sb.AppendLine("    $elements = $commandAst.CommandElements");
        sb.AppendLine("    $elementCount = $elements.Count");
        sb.AppendLine();
        sb.AppendLine("    if ($elementCount -eq 2) {");
        sb.AppendLine("        # Complete top-level commands");
        sb.AppendLine("        $completions = $commands.Keys | Where-Object { $_ -like \"$wordToComplete*\" }");
        sb.AppendLine("    }");
        sb.AppendLine("    elseif ($elementCount -eq 3) {");
        sb.AppendLine("        # Complete subcommands");
        sb.AppendLine("        $command = $elements[1].ToString()");
        sb.AppendLine("        if ($commands.ContainsKey($command) -and $commands[$command].ContainsKey('subcommands')) {");
        sb.AppendLine("            $completions = $commands[$command]['subcommands'] | Where-Object { $_ -like \"$wordToComplete*\" }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("    else {");
        sb.AppendLine("        # Complete options");
        sb.AppendLine("        $completions = @('--help', '--version', '--format', '--status', '--priority') | Where-Object { $_ -like \"$wordToComplete*\" }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    $completions | ForEach-Object {");
        sb.AppendLine("        [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_)");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GetSubcommandDescription(string command, string subcommand)
    {
        if (CommandHelp.HelpRegistry.TryGetValue(command, out var cmdHelp) && cmdHelp.Subcommands != null)
        {
            if (cmdHelp.Subcommands.TryGetValue(subcommand, out var desc))
            {
                return desc;
            }
        }

        return (command, subcommand) switch
        {
            ("bulk", "update") => "Update multiple tickets",
            ("bulk", "export") => "Export multiple tickets",
            ("bulk", "delete") => "Delete multiple tickets",
            _ => $"{subcommand} operation"
        };
    }
}
#### 1.1.0 August 15th 2025 ####

**Feature Release**

This release brings significant new features and improvements to the Freshdesk CLI.

**New Features:**
- **Tab Completion Support** - Added shell completion for bash, zsh, and PowerShell (#45)
  - Auto-complete commands and options
  - Easy installation with `freshdesk completion` command
  - Supports all major shells
  
- **Version Management** - Added version display and update checking (#44)
  - Check current version with `--version` flag
  - Automatic update notifications
  - Self-update capability
  
- **Comprehensive Help System** - Improved help for all CLI commands
  - Detailed command descriptions
  - Examples for common use cases
  - Better error messages

**Improvements:**
- Enhanced filter parsing and ticket list display
- Better integration test coverage
- Improved code formatting and consistency

**Installation:**
Update using the self-update command:
```bash
freshdesk update
```

Or use the one-command installer:
```bash
curl -sSL https://raw.githubusercontent.com/Aaronontheweb/freshdesk-cli/dev/install.sh | bash
```

**Platform Support:**
- Linux x64
- macOS x64 (Intel)
- macOS ARM64 (Apple Silicon)
- Windows x64

#### 1.0.4 August 15th 2025 ####

**Critical Bug Fix Release**

This release fixes a critical bug that prevented users from replying to tickets.

**Bug Fixes:**
- **Fixed Ticket Reply 400 Bad Request Error** (#39)
  - Corrected API endpoint usage: `/reply` for public replies, `/notes` for private notes
  - Removed invalid `private` field from reply requests
  - Public replies and private notes now work correctly
  - Added comprehensive test coverage for both reply types

**Installation:**
Update using the self-update command:
```bash
freshdesk update
```

Or use the one-command installer:
```bash
curl -sSL https://raw.githubusercontent.com/Aaronontheweb/freshdesk-cli/dev/install.sh | bash
```

**Platform Support:**
- Linux x64
- macOS x64 (Intel)
- macOS ARM64 (Apple Silicon)
- Windows x64

#### 1.0.2 August 15th 2025 ####

**Bug Fix and Enhancement Release**

This release includes important bug fixes and adds powerful filtering capabilities to ticket operations.

**New Features:**
- **Advanced Filtering** - Added comprehensive filtering options for ticket operations (#36)
  - Filter by status, priority, agent, group, and more
  - Support for date range filtering with `--created-since` and `--updated-since`
  - Combine multiple filters for precise ticket searches

**Bug Fixes:**
- **Fixed Attachment Downloads** - Now properly includes conversation attachments when downloading (#37)
- **Fixed Windows AOT Build** - Resolved release workflow failure for Windows builds (#34)
- **Fixed Flaky Test** - Stabilized the ConfigureCommand_SavesConfiguration test (#35)

**Installation:**
Update using the self-update command:
```bash
freshdesk update
```

Or use the one-command installer:
```bash
curl -sSL https://raw.githubusercontent.com/Aaronontheweb/freshdesk-cli/dev/install.sh | bash
```

**Platform Support:**
- Linux x64
- macOS x64 (Intel)
- macOS ARM64 (Apple Silicon)
- Windows x64 (fixed in this release)

#### 1.0.1 August 14th 2025 ####

**Bug Fix Release**

This release fixes a critical JSON deserialization bug that prevented the `--tree` flag from working properly.

**Bug Fixes:**
- **Fixed `--tree` flag** - Resolved JSON deserialization error when viewing ticket conversations
- **Improved API compatibility** - Fixed `FromEmail` field type to handle Freshdesk API inconsistencies

**Technical Details:**
- Changed `Conversation.FromEmail` from `long?` to `string?` to handle multiple API response formats
- The Freshdesk API returns `from_email` as email strings, user IDs, or null values
- This fix ensures all conversation data can be properly parsed and displayed

**Installation:**
Update using the self-update command:
```bash
freshdesk update
```

Or use the one-command installer:
```bash
curl -sSL https://raw.githubusercontent.com/Aaronontheweb/freshdesk-cli/dev/install.sh | bash
```

**Platform Support:**
- Linux x64
- macOS x64 (Intel)
- macOS ARM64 (Apple Silicon)
- Windows x64 (manual download - see Issue #25)

#### 1.0.0 August 14th 2025 ####

**First Stable Release**

The first stable release of the Freshdesk CLI tool - a powerful command-line interface for managing Freshdesk tickets and support operations.

**New Features:**
- **Uninstall Support** - Easy removal via `curl -sSL https://raw.githubusercontent.com/Aaronontheweb/freshdesk-cli/dev/install.sh | bash -s -- --uninstall`
- **Improved Installation** - One-command installation with proper error handling and version detection
- **Stable Release Channel** - No more beta flags needed for installation

**Features:**
- **Configuration Management** - Set and test Freshdesk API credentials
- **Ticket Operations** - List, view, create, update, and search tickets
- **Conversation Support** - Reply to tickets and add internal notes
- **Attachment Handling** - List, download (bulk support), and upload attachments
- **Export Functionality** - Export tickets to JSON, CSV, XML, or Markdown formats
- **Self-Update Mechanism** - Built-in update command to fetch latest releases
- **Read-Only Mode** - Safe mode for viewing without making changes
- **Cross-Platform** - Native binaries for Linux and macOS (x64 and ARM64)
- **AOT Compilation** - Fast startup and minimal memory footprint using .NET 9 AOT

**Installation:**
```bash
# Quick install
curl -sSL https://raw.githubusercontent.com/Aaronontheweb/freshdesk-cli/dev/install.sh | bash

# Uninstall
curl -sSL https://raw.githubusercontent.com/Aaronontheweb/freshdesk-cli/dev/install.sh | bash -s -- --uninstall
```

**Technical Highlights:**
- Built with .NET 9 and AOT (Ahead-of-Time) compilation
- Reflection-free JSON serialization for optimal performance
- Progress indicators for long-running operations
- Comprehensive error handling and validation

**Platform Support:**
- Linux x64
- macOS x64 (Intel)
- macOS ARM64 (Apple Silicon)
- Windows x64 (manual download from releases page)

**Getting Started:**
```bash
# Configure your Freshdesk credentials
freshdesk config set --domain your-domain --api-key your-api-key

# Test the connection
freshdesk config test

# List recent tickets
freshdesk ticket list

# Get help
freshdesk --help
```

#### 1.0.0-beta1 August 14th 2025 ####

**Initial Beta Release**

The first beta release of the Freshdesk CLI tool - a powerful command-line interface for managing Freshdesk tickets and support operations.

**Features:**
- **Configuration Management** - Set and test Freshdesk API credentials
- **Ticket Operations** - List, view, create, update, and search tickets
- **Conversation Support** - Reply to tickets and add internal notes
- **Attachment Handling** - List, download (bulk support), and upload attachments
- **Export Functionality** - Export tickets to JSON, CSV, XML, or Markdown formats
- **Self-Update Mechanism** - Built-in update command to fetch latest releases
- **Read-Only Mode** - Safe mode for viewing without making changes
- **Cross-Platform** - Native binaries for Windows, Linux, and macOS (x64 and ARM64)
- **AOT Compilation** - Fast startup and minimal memory footprint using .NET 9 AOT

**Installation:**
- Easy installation via platform-specific scripts (install.sh / install.ps1)
- Dry-run mode for testing installation without system changes
- Beta version support with --beta flag
- Automatic PATH configuration

**Technical Highlights:**
- Built with .NET 9 and AOT (Ahead-of-Time) compilation
- Reflection-free JSON serialization for optimal performance
- Progress indicators for long-running operations
- Comprehensive error handling and validation

**Known Limitations:**
- This is a beta release - please report any issues
- Some advanced Freshdesk API features not yet implemented
- Search functionality currently uses basic filtering

**Getting Started:**
```bash
# Configure your Freshdesk credentials
freshdesk config set --domain your-domain --api-key your-api-key

# Test the connection
freshdesk config test

# List recent tickets
freshdesk ticket list

# Get help
freshdesk --help
```

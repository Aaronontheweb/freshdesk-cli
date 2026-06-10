#### 1.5.1 2026-06-10 ####

**Bug Fix Release**

This release fixes paragraph spacing in markdown-formatted ticket replies and notes so text no longer collapses after Freshdesk renders HTML.

**Bug Fixes:**
- **Preserve Paragraph Spacing in Markdown Replies and Notes** ([#136](https://github.com/Aaronontheweb/freshdesk-cli/pull/136))
  - `ticket reply` and `ticket note` now keep paragraph breaks after Markdown-to-HTML conversion, preventing collapsed text in Freshdesk.
  - Markdown content is rewritten into paragraph-safe HTML blocks so single and multi-paragraph messages retain readable spacing.
  - Updated help text and tests to document and verify the improved spacing behavior.

**Technical Improvements:**
- Added spacing-safe conversion logic in markdown rendering to avoid paragraph collapse in list-heavy and standard markdown content.

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

#### 1.5.0 June 9th 2026 ####

**Feature Release**

This release adds programmatic Markdown-to-HTML conversion for ticket notes and replies, along with comprehensive XSS security testing.

**New Features:**
- **Markdown-to-HTML Conversion for Notes and Replies** (#134)
  - `FreshdeskApiClient` now converts Markdown input to HTML before sending to the Freshdesk API
  - Use `--file` flag with `ticket reply` and `ticket note` to read message content from a file (Markdown is automatically converted to HTML)
  - The `--file` flag is now required for reply/note content that should be rendered as HTML
  - Converts standard Markdown formatting (headers, bold, lists, links, etc.) to HTML

**Security Improvements:**
- **XSS Prevention** (#134)
  - Raw HTML tags injected into Markdown input are now escaped (e.g., `<script>`, `<iframe>`, `<img onerror>`, `<svg onload>`)
  - Safe Markdown formatting is still rendered correctly while dangerous raw HTML is neutralized
  - Comprehensive test coverage with OWASP-style XSS payloads

**Technical Improvements:**
- Refactored `Program.cs` and `CommandHelp.cs` for cleaner command handling
- Updated `FreshdeskApiClient` with dedicated Markdown-to-HTML conversion pipeline
- Added 81 lines of new test coverage for Markdown conversion and XSS prevention
- Updated CI dependencies: `actions/checkout` 5.0.0→6.0.3, `actions/setup-dotnet` 5.0.0→5.2.0

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
#### 1.4.2 May 18th 2026 ####

**Packaging Fix Release**

This release restores the macOS x64 (Intel) binary, which was missing from the 1.4.1 release. There are no functional changes to the CLI itself.

**Fixes:**
- **Restored the macOS x64 (Intel) build**
  - The 1.4.1 release shipped without `freshdesk-1.4.1-osx-x64.tar.gz`, so the `install.sh` one-command installer failed on Intel Macs with a download error
  - Root cause: the explicit `Microsoft.DotNet.ILCompiler` package reference prevented the .NET SDK from resolving the cross-compilation toolchain needed to build the Intel binary on GitHub's Apple Silicon (arm64) macOS runners
  - Fixed by removing the explicit `Microsoft.DotNet.ILCompiler` and `Microsoft.NET.ILLink.Tasks` package references so the SDK manages the AOT/trimming toolchain (including cross-compilation packages) automatically

- **Release workflow no longer publishes incomplete releases**
  - The `create-release` job previously ran even when a platform build failed (`if: always()`), silently shipping a partial set of binaries
  - It now requires every platform build to succeed before a release is created

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

#### 1.4.1 May 16th 2026 ####

**Bug Fix Release**

This release fixes two bugs introduced in v1.4.0 that caused HTTP 400 errors on attachment downloads and ticket updates.

**Bug Fixes:**
- **Fixed Attachment Download for Ticket-Level Attachments** (#122)
  - `freshdesk attachment download` was returning HTTP 400 when downloading attachments linked to the ticket description (listed as `Source: Ticket` in `attachment list`)
  - Root cause: the shared `HttpClient` was sending an `Authorization: Basic` header to Freshdesk's pre-signed AWS S3 download URLs, which S3 rejects when query-string auth is already present
  - Fixed by adding `FreshdeskAuthHandler`, a delegating handler that strips the `Authorization` header from any request not bound for the Freshdesk host
  - Conversation attachments (`Source: Conv #...`) were unaffected and continue to work as before

- **Fixed `ticket update` HTTP 400 on Status and Priority Changes** (#127)
  - `freshdesk ticket update <id> --status resolved` (and `--priority`) was failing with HTTP 400
  - Root cause: the command was sending a full `Ticket` object with empty/default fields (Subject="", Id=0, etc.) rather than a partial payload
  - Fixed by sending only the fields being updated, matching the pattern already used by `contact update` and `company update`

**Technical Improvements:**
- Updated .NET AOT toolchain from 9.0.13 to 9.0.16 to resolve CI build failures on current runtime versions

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

#### 1.4.0 March 2nd 2026 ####

**Feature and Bug Fix Release**

This release adds company field discovery capabilities and fixes several PowerShell and company search issues.

**New Features:**
- **Company Fields Command** (#113)
  - Added `company fields` command for discovering custom fields in your Freshdesk instance
  - List all available company fields with their types and properties
  - Essential for building automated company management workflows

**Bug Fixes:**
- **Fixed PowerShell Completion Installation** (#110, #111)
  - Removed duplicate 'update' key from PowerShell completion script
  - Now installs to both PowerShell 5.1 and PowerShell 7 profile paths
  - Improved compatibility across Windows PowerShell versions

- **Fixed Company Search JSON Deserialization** (#112)
  - Added proper CompanySearchResult wrapper model for API responses
  - Company search (`company search --name`) now works correctly
  - Handles API response format properly

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

#### 1.3.1 February 11th 2026 ####

**Bug Fix Release**

This release fixes several bugs in the v1.3.0 contact and company management feature.

**Bug Fixes:**
- **Fixed Contact Deserialization Failure** (#90, #94)
  - `ViewAllTickets` now nullable to handle null API responses
  - Displays "N/A" when the field is not set
- **Fixed `--company` Flag** (#93)
  - Added `--company` as alias for `--company-id` in contact create and update
- **Fixed `--view-all-tickets` Inconsistency** (#92)
  - Both create and update now accept flag-only, explicit true/false, and `--no-view-all-tickets`
- **Fixed Ticket Search** (#87)
  - Uses Freshdesk Search API for structured filters (status, priority)
  - Paginates through up to 1000 tickets for text queries
  - Previously only searched the first 100 tickets
- **Added Missing Help Text** (#91)
  - Added help entries for all contact and company subcommands

**Technical Improvements:**
- Updated .NET packages from 9.0.12 to 9.0.13
- Fixed AOT compilation failure with ILCompiler version mismatch

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

#### 1.3.0 February 2nd 2026 ####

**Major Feature Release**

This release adds comprehensive contact and company management commands, enabling full programmatic control over Freshdesk contacts and organizations.

**New Features:**
- **Contact Management** (#88)
  - Full CRUD operations: list, get, create, update, search, delete
  - Set `view_all_tickets` flag to allow contacts to see all company tickets
  - Search contacts by email or phone
  - Associate contacts with companies
  - Support for all contact fields (name, email, phone, mobile, job title, etc.)

- **Company Management** (#88)
  - Full CRUD operations: list, get, create, update, search, delete
  - Search companies by name
  - Manage company properties (domains, industry, health score, notes)
  - Associate contacts with companies during creation

- **Generic Custom Fields** (#88)
  - Added `--custom-field` parameter for instance-specific required fields
  - Works with both contacts and companies
  - Supports multiple custom fields per command

- **Enhanced Error Reporting** (#88)
  - Display actual Freshdesk API error messages for better troubleshooting
  - Clearer validation errors for missing required fields

**Technical Improvements:**
- Fixed .NET version mismatch in AOT builds (updated to .NET 9.0.12)
- Improved Dictionary-based write operations to exclude read-only fields
- Enhanced output formatters for contact and company data
- Full pagination support for list operations
- Maintained AOT compatibility and performance standards

**Usage Examples:**
```bash
# Create a company
freshdesk company create --name "Acme Corp" --description "A great company"

# Create a contact with view_all_tickets enabled
freshdesk contact create --name "John Doe" --email john@acme.com \
  --company-id 12345 --view-all-tickets

# Search for contacts by email
freshdesk contact search --email john@acme.com

# List all companies
freshdesk company list --format table
```

**Why This Matters:**
The `view_all_tickets` flag solves intermittent unique value errors when managing contacts via the Freshdesk web UI. By creating contacts programmatically with this flag, you can ensure contacts have proper visibility across all company tickets.

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

#### 1.2.0 October 8th 2025 ####

**Bug Fix Release**

This release fixes critical issues with conversation retrieval and file content handling.

**Bug Fixes:**
- **Fixed Line Ending Normalization** (#71)
  - Properly normalize line endings when reading reply/note content from files
  - Ensures consistent behavior across different operating systems
  - Prevents formatting issues in ticket conversations

- **Fixed Conversation Pagination** (#70)
  - Implemented proper pagination for ticket conversations
  - Now retrieves complete conversation history regardless of length
  - Prevents missing messages in tickets with many replies

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

#### 1.1.2 August 15th 2025 ####

**Feature Release**

This release adds powerful filtering capabilities to help you focus on active tickets.

**New Features:**
- **Unresolved Ticket Filtering** (#49)
  - Added `--unresolved` flag to `ticket list` command
  - Filters out resolved and closed tickets, showing only active work
  - Uses targeted API searches for each unresolved status (Open, Pending, Waiting on Customer, Waiting on Third Party)
  - Deduplicates and sorts results by creation date (newest first)
  - Compatible with existing filtering options like `--email`

**Technical Improvements:**
- Enhanced API search strategy to avoid pagination limitations
- Comprehensive test coverage for new filtering functionality
- Maintained AOT compatibility and performance standards

**Usage Example:**
```bash
# Show only unresolved tickets
freshdesk ticket list --unresolved

# Combine with other filters
freshdesk ticket list --unresolved --email user@example.com
```

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

#### 1.1.1 August 15th 2025 ####

**Bug Fix Release**

This release fixes critical issues with ticket filtering and display functionality.

**Bug Fixes:**
- **Fixed Ticket Status Filtering** (#47)
  - Now uses Freshdesk search API for proper results across all pages
  - Status filtering (e.g., `--status open`) now returns correct tickets
  
- **Fixed Email-based Filtering** (#47)
  - Implemented proper email filtering by looking up contacts first
  - Email filtering (e.g., `--email user@example.com`) now works correctly
  
- **Improved Ticket Display** (#47)
  - Ticket list now shows Requester ID instead of empty email field
  - `ticket get` command now fetches and displays actual email addresses
  - Avoids N+1 API calls for better performance

**Technical Improvements:**
- Added `Contact` and `TicketSearchResult` models for API responses
- All new models registered for AOT compatibility
- Maintained 100% test coverage with all 92 unit tests passing

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

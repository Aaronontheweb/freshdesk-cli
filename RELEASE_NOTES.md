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
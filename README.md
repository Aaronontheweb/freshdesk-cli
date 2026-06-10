# Freshdesk CLI

A fast, lightweight command-line interface for [Freshdesk](https://www.freshworks.com/freshdesk/) built with .NET 9 and AOT compilation.

## Features

- 🚀 **Fast & Lightweight** - Native AOT compilation for instant startup and small binary size (~11MB)
- 🔒 **Read-Only Mode** - Safe exploration mode that prevents accidental modifications
- 📊 **Multiple Output Formats** - Table (human-readable), JSON, CSV, XML, and Markdown formats
- 👥 **Contact & Company Management** - Full CRUD operations for contacts and organizations
- 🎫 **Ticket Management** - Create, update, search, and manage support tickets
- 📝 **Markdown Replies & Notes** - Write replies and internal notes in Markdown, automatically converted to HTML
- 📤 **Export Functionality** - Export tickets and conversations to multiple formats
- 📥 **Bulk Downloads** - Download all attachments from tickets with progress indicators
- 🔐 **Secure Credential Storage** - File-based with proper permissions and environment variable support
- 🌍 **Cross-Platform** - Works on Linux, macOS, and Windows
- ⚡ **Rate Limit Handling** - Automatic retry with exponential backoff
- 📈 **Progress Indicators** - Visual progress bars for long-running operations

## Installation

### Quick Install (Recommended)

**Linux/macOS:**
```bash
curl -sSL https://raw.githubusercontent.com/Aaronontheweb/freshdesk-cli/dev/install.sh | bash
```

**Windows (PowerShell):**
```powershell
iwr -useb https://raw.githubusercontent.com/Aaronontheweb/freshdesk-cli/dev/install.ps1 | iex
```

**Install Beta Releases:**
```bash
curl -sSL https://raw.githubusercontent.com/Aaronontheweb/freshdesk-cli/dev/install.sh | bash -s -- --beta
```

### From Source

```bash
# Clone the repository
git clone https://github.com/Aaronontheweb/freshdesk-cli.git
cd freshdesk-cli

# Build and install globally (Linux/macOS)
dotnet publish src/FreshdeskCLI -c Release -r linux-x64 /p:PublishAot=true
sudo cp ./publish/linux-x64/freshdesk /usr/local/bin/

# Or run directly
dotnet run --project src/FreshdeskCLI -- --help
```

### Pre-built Binaries

Download the latest release for your platform from the [Releases](https://github.com/Aaronontheweb/freshdesk-cli/releases) page.

## Uninstallation

**Remove Freshdesk CLI:**
```bash
curl -sSL https://raw.githubusercontent.com/Aaronontheweb/freshdesk-cli/dev/install.sh | bash -s -- --uninstall
```

This will:
- Remove the `freshdesk` binary from your installation directory
- Optionally remove the configuration directory (`~/.freshdesk`) if you choose
- Optionally remove the installation directory if it becomes empty

## Configuration

### Initial Setup

```bash
# Configure with your Freshdesk credentials
freshdesk config set --domain yourcompany --api-key your_api_key_here

# Test the connection
freshdesk config test

# View current configuration
freshdesk config get
```

### Environment Variables

You can also use environment variables (useful for CI/CD):

```bash
export FRESHDESK_API_KEY=your_api_key_here
export FRESHDESK_DOMAIN=yourcompany
```

### Configuration File Location

Configuration is stored at `~/.freshdesk/config.json` with secure file permissions (user-only on Unix systems).

## Tab Completion

The CLI supports tab completion for bash, zsh, and PowerShell. This feature dynamically generates completion scripts based on the current command structure, ensuring completions are always up-to-date.

### Installation

**Auto-detect and install for current shell:**
```bash
freshdesk install-completion
```

**Install for specific shell:**
```bash
freshdesk install-completion bash       # For Bash
freshdesk install-completion zsh        # For Zsh  
freshdesk install-completion powershell # For PowerShell
```

### Activation

After installation, activate the completion:

**Bash:**
```bash
source ~/.bashrc
```

**Zsh:**
```bash
source ~/.zshrc
```

**PowerShell:**
```powershell
. $PROFILE
```

### Features

- Complete commands and subcommands (e.g., `freshdesk ti<TAB>` → `freshdesk ticket`)
- Complete options and flags (e.g., `freshdesk ticket list --st<TAB>` → `freshdesk ticket list --status`)
- Auto-complete enum values (e.g., `--status <TAB>` shows `open`, `pending`, `resolved`, `closed`)
- File path completion for `--output` and `--file` options
- Automatically stays in sync with command structure updates

## Usage

### Read-Only Mode

Add `--read-only` or `-ro` to any command to prevent write operations:

```bash
# Safe exploration - no accidental changes
freshdesk --read-only ticket list
freshdesk --read-only ticket get 123
```

### Ticket Operations

#### List Tickets

```bash
# List tickets (default format: table)
freshdesk ticket list

# Filter by status
freshdesk ticket list --status open
freshdesk ticket list --status pending

# Filter by customer email
freshdesk ticket list --email john@example.com
freshdesk ticket list --customer john@example.com

# Combine filters
freshdesk ticket list --status open --email john@example.com

# With pagination
freshdesk ticket list --page 2 --limit 50

# Export as JSON
freshdesk ticket list --format json

# Export as CSV
freshdesk ticket list --format csv > tickets.csv
```

#### Get Ticket Details

```bash
# View ticket details
freshdesk ticket get 123

# Get as JSON
freshdesk ticket get 123 --format json
```

#### Create Ticket

```bash
# Create a new ticket
freshdesk ticket create \
  --subject "Bug in login page" \
  --email "customer@example.com" \
  --description "Cannot login with valid credentials" \
  --priority high
```

#### Update Ticket

```bash
# Update ticket status and priority
freshdesk ticket update 123 --status resolved --priority low
```

#### Search Tickets

```bash
# Search tickets by text query
freshdesk ticket search "login issue"
freshdesk ticket search --query "database error"

# Search with filters
freshdesk ticket search --status open --priority high
freshdesk ticket search --email john@example.com
freshdesk ticket search --customer john@example.com

# Combine text search with filters
freshdesk ticket search "login" --status open --priority high

# Export search results
freshdesk ticket search --status open --format json
freshdesk ticket search --email john@example.com --format csv
```

#### Reply to Tickets

Reply content is read from a file (`--file` is required), treated as Markdown, and converted to HTML before being sent to Freshdesk. Raw HTML in the file is escaped rather than passed through. Freshdesk HTML rewriting is handled with paragraph-safe output so blank lines and basic spacing are preserved.

```bash
# Reply from a Markdown file
freshdesk ticket reply 123 --file response.md

# Plain text works too
freshdesk ticket reply 123 -f response.txt
```

> **Note:** `--message` is deprecated and will be removed in a future version. Use `--file` instead.

#### Add Internal Notes

Note content is read from a file (`--file` is required) and converted from Markdown to HTML, same as replies, with paragraph spacing preserved.

```bash
# Add note from a Markdown file
freshdesk ticket note 123 --file internal-notes.md

# Plain text works too
freshdesk ticket note 123 -f internal-notes.txt
```

### Contact Operations

#### List Contacts

```bash
# List contacts (default format: table)
freshdesk contact list

# With pagination
freshdesk contact list --page 2 --limit 50

# Export as JSON or CSV
freshdesk contact list --format json
freshdesk contact list --format csv > contacts.csv
```

#### Get Contact Details

```bash
# View contact details
freshdesk contact get 12345

# Get as JSON
freshdesk contact get 12345 --format json
```

#### Create Contact

```bash
# Create a new contact
freshdesk contact create \
  --name "John Doe" \
  --email "john@example.com"

# Create contact in a company
freshdesk contact create \
  --name "Jane Smith" \
  --email "jane@example.com" \
  --company-id 67890

# Create contact who can view all company tickets
freshdesk contact create \
  --name "Support Manager" \
  --email "support@company.com" \
  --company-id 67890 \
  --view-all-tickets

# Create with additional details
freshdesk contact create \
  --name "John Doe" \
  --email "john@example.com" \
  --phone "555-1234" \
  --mobile "555-5678" \
  --job-title "Engineering Manager" \
  --company-id 67890 \
  --view-all-tickets

# Create with custom fields
freshdesk contact create \
  --name "VIP Customer" \
  --email "vip@example.com" \
  --custom-field account_tier="Premium"
```

#### Update Contact

```bash
# Enable view all tickets for a contact
freshdesk contact update 12345 --view-all-tickets true

# Update contact details
freshdesk contact update 12345 \
  --name "John Smith" \
  --job-title "Senior Engineer"

# Associate contact with a company
freshdesk contact update 12345 --company-id 67890

# Disable view all tickets
freshdesk contact update 12345 --view-all-tickets false
```

#### Search Contacts

```bash
# Search by email
freshdesk contact search --email john@example.com

# Search by phone
freshdesk contact search --phone 555-1234

# Export search results
freshdesk contact search --email john@example.com --format json
```

#### Delete Contact

```bash
# Delete a contact
freshdesk contact delete 12345
```

### Company Operations

#### List Companies

```bash
# List companies (default format: table)
freshdesk company list

# With pagination
freshdesk company list --page 2 --limit 50

# Export as JSON or CSV
freshdesk company list --format json
freshdesk company list --format csv > companies.csv
```

#### Get Company Details

```bash
# View company details
freshdesk company get 67890

# Get as JSON
freshdesk company get 67890 --format json
```

#### Create Company

```bash
# Create a basic company
freshdesk company create --name "Acme Corporation"

# Create with description and industry
freshdesk company create \
  --name "Tech Startup Inc" \
  --description "Cloud services provider" \
  --industry "Technology"

# Create with domains
freshdesk company create \
  --name "Example Corp" \
  --domains example.com,example.net

# Create with custom fields (e.g., support plan)
freshdesk company create \
  --name "Premium Customer" \
  --description "VIP account" \
  --custom-field support_plan="Enterprise Support"

# Create with multiple options
freshdesk company create \
  --name "Global Enterprises" \
  --description "Multinational corporation" \
  --industry "Finance" \
  --domains globalent.com,globalent.net \
  --health-score "Healthy" \
  --custom-field support_plan="Premium Support"
```

#### Update Company

```bash
# Update company name
freshdesk company update 67890 --name "New Company Name"

# Update description and industry
freshdesk company update 67890 \
  --description "Updated description" \
  --industry "Healthcare"

# Update domains
freshdesk company update 67890 --domains newdomain.com,example.com

# Update health score
freshdesk company update 67890 --health-score "At Risk"
```

#### Search Companies

```bash
# Search by name
freshdesk company search "Acme"

# Export search results
freshdesk company search "Tech" --format json
```

#### Delete Company

```bash
# Delete a company
freshdesk company delete 67890
```

### Export Operations

```bash
# Export multiple tickets to JSON
freshdesk export tickets --output tickets.json --format json

# Export with filters
freshdesk export tickets --status open --output open_tickets.json
freshdesk export tickets --priority high --output high_priority.json
freshdesk export tickets --email john@example.com --output johns_tickets.json

# Combine multiple filters
freshdesk export tickets --status open --priority high --output urgent.json

# Export to CSV
freshdesk export tickets --format csv --output tickets.csv

# Export to XML
freshdesk export tickets --format xml --output tickets.xml

# Export with conversations included (JSON only)
freshdesk export tickets --format json --output tickets_full.json --include-conversations

# Limit number of tickets exported
freshdesk export tickets --limit 100 --output first_100.json

# Export single ticket to Markdown
freshdesk export ticket 123 --format markdown --output ticket_123.md

# Export single ticket with conversations
freshdesk export ticket 123 --format json --output ticket_123.json --include-conversations
```

### Attachment Operations

#### List Attachments

```bash
# List all attachments for a ticket
freshdesk attachment list 123
```

#### Download Attachment

```bash
# Download an attachment
freshdesk attachment download 123 456789

# Download with custom output path
freshdesk attachment download 123 456789 --output /path/to/save.pdf

# Download all attachments from a ticket
freshdesk attachment download-all 123 --output-dir ./attachments/

# Bulk download with progress indicator
freshdesk attachment download-all 123 --output-dir ./downloads/ --show-progress
```

#### Upload Attachment

```bash
# Upload a file to a ticket
freshdesk attachment upload 123 /path/to/file.pdf

# Upload with custom filename
freshdesk attachment upload 123 /path/to/file.pdf --name "Report_2024.pdf"
```

### Output Formats

All list and get commands support multiple output formats:

- **table** (default) - Human-readable table format
- **json** - Machine-readable JSON
- **csv** - Excel-compatible CSV

Example:
```bash
freshdesk ticket list --format json | jq '.[] | {id, subject, status}'
```

## Command Reference

### Global Options

| Option | Short | Description |
|--------|-------|-------------|
| `--help` | `-h` | Show help information |
| `--version` | `-v` | Show version information |
| `--read-only` | `-ro` | Run in read-only mode (blocks all write operations) |

### Config Commands

| Command | Description |
|---------|-------------|
| `config set` | Set configuration values |
| `config get` | Display current configuration |
| `config test` | Test connection to Freshdesk API |

### Ticket Commands

| Command | Description |
|---------|-------------|
| `ticket list` | List tickets with pagination |
| `ticket get <id>` | Get ticket details |
| `ticket create` | Create a new ticket |
| `ticket update <id>` | Update ticket status/priority |
| `ticket search <query>` | Search tickets |
| `ticket reply <id> --file <path>` | Reply to a ticket (Markdown file converted to HTML) |
| `ticket note <id> --file <path>` | Add internal note to a ticket (Markdown file converted to HTML) |

### Export Commands

| Command | Description |
|---------|-------------|
| `export tickets` | Export multiple tickets to JSON/CSV/XML |
| `export ticket <id>` | Export a single ticket to JSON/CSV/XML/Markdown |

### Contact Commands

| Command | Description |
|---------|-------------|
| `contact list` | List contacts with pagination |
| `contact get <id>` | Get contact details |
| `contact create` | Create a new contact |
| `contact update <id>` | Update contact details |
| `contact search` | Search contacts by email or phone |
| `contact delete <id>` | Delete a contact |

### Company Commands

| Command | Description |
|---------|-------------|
| `company list` | List companies with pagination |
| `company get <id>` | Get company details |
| `company create` | Create a new company |
| `company update <id>` | Update company details |
| `company search <name>` | Search companies by name |
| `company delete <id>` | Delete a company |

### Attachment Commands

| Command | Description |
|---------|-------------|
| `attachment list <ticket-id>` | List attachments for a ticket |
| `attachment download <ticket-id> <attachment-id>` | Download an attachment |
| `attachment download-all <ticket-id>` | Download all attachments from a ticket |
| `attachment upload <ticket-id> <file>` | Upload file to ticket |

## Examples

### Daily Operations

```bash
# Morning ticket review
freshdesk --read-only ticket list --format table

# Export yesterday's tickets
freshdesk ticket list --format csv > daily_tickets.csv

# Quick status update
freshdesk ticket update 456 --status resolved
```

### Automation

```bash
# Check for high priority tickets
freshdesk ticket list --format json | \
  jq '.[] | select(.priority == "High") | {id, subject}'

# Count open tickets
freshdesk ticket list --format json | \
  jq '[.[] | select(.status == "Open")] | length'
```

### Safe Exploration

```bash
# Always use read-only mode when exploring
alias fd-safe='freshdesk --read-only'

fd-safe ticket list
fd-safe ticket get 789
```

## Building from Source

### Prerequisites

- .NET 9 SDK or later
- Git

### Build Commands

```bash
# Clone repository
git clone https://github.com/Aaronontheweb/freshdesk-cli.git
cd freshdesk-cli

# Run tests
dotnet test

# Build for current platform
dotnet build -c Release

# Build with AOT for production (Linux)
dotnet publish src/FreshdeskCLI -c Release -r linux-x64 /p:PublishAot=true

# Build for other platforms
dotnet publish src/FreshdeskCLI -c Release -r win-x64 /p:PublishAot=true
dotnet publish src/FreshdeskCLI -c Release -r osx-x64 /p:PublishAot=true
dotnet publish src/FreshdeskCLI -c Release -r osx-arm64 /p:PublishAot=true

# Test AOT compatibility
dotnet run --project src/FreshdeskCLI -- --test-aot
```

### Supported Platforms

- `linux-x64` - Linux 64-bit
- `linux-arm64` - Linux ARM 64-bit
- `win-x64` - Windows 64-bit
- `osx-x64` - macOS Intel
- `osx-arm64` - macOS Apple Silicon

## Architecture

- **AOT Compilation** - Native ahead-of-time compilation for fast startup and small binaries
- **Source Generators** - Zero-reflection JSON serialization for AOT compatibility
- **Reactive Rate Limiting** - Handles API rate limits gracefully with automatic retry
- **Secure Storage** - Credentials stored with proper file permissions

## Contributing

Contributions are welcome! Please follow these guidelines:

### Development Guidelines

1. Maintain AOT compatibility - no reflection
2. Keep binary size under 15MB
3. Follow existing code style
4. Add tests for new features
5. Update documentation

### Pull Request Process

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## Troubleshooting

### Common Issues

**Authentication Failed**
```bash
# Check your API key and domain
freshdesk config get
freshdesk config test
```

**Rate Limited**
The CLI automatically handles rate limits with exponential backoff. If you consistently hit limits, consider reducing request frequency.

**Permission Denied**
```bash
# Ensure proper permissions on config directory
chmod 700 ~/.freshdesk
chmod 600 ~/.freshdesk/config.json
```

**Binary Not Found**
```bash
# Ensure the binary is in your PATH
echo $PATH
# Or use full path
/usr/local/bin/freshdesk --version
```

## Performance

- **Startup Time**: < 50ms (native AOT)
- **Binary Size**: ~11MB (platform dependent)
- **Memory Usage**: < 20MB typical
- **Rate Limiting**: Automatic with exponential backoff

## Security

- API keys are never logged or displayed in full
- Configuration files use secure permissions (600 on Unix)
- Environment variables supported for CI/CD
- Read-only mode for safe exploration

## Roadmap

- [x] Attachment upload/download support
- [x] Bulk download operations
- [x] Progress indicators for long operations
- [x] Export functionality (JSON/CSV/XML/Markdown)
- [x] Contact and company management
- [x] Tab completion support
- [ ] Ticket conversation thread management
- [ ] Interactive mode
- [ ] Webhook support
- [ ] Advanced filtering and sorting
- [ ] Batch ticket operations

## License

Apache License 2.0 - see [LICENSE](LICENSE) file for details.

## Support

- **Issues**: [GitHub Issues](https://github.com/Aaronontheweb/freshdesk-cli/issues)
- **Discussions**: [GitHub Discussions](https://github.com/Aaronontheweb/freshdesk-cli/discussions)

## Acknowledgments

Built with ❤️ using:
- [.NET 9](https://dotnet.microsoft.com/)
- [System.Text.Json](https://docs.microsoft.com/en-us/dotnet/api/system.text.json)
- [Freshdesk API v2](https://developers.freshdesk.com/api/)

## Author

Created with ❤️ by Aaron Stannard - [https://aaronstannard.com/](https://aaronstannard.com/)

GitHub: [@Aaronontheweb](https://github.com/Aaronontheweb)

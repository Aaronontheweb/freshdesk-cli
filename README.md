# Freshdesk CLI

A fast, lightweight command-line interface for Freshdesk built with .NET 9 and AOT compilation.

## Features

- 🚀 **Fast & Lightweight** - Native AOT compilation for instant startup and small binary size (~3-5MB)
- 🔒 **Read-Only Mode** - Safe exploration mode that prevents accidental modifications
- 📊 **Multiple Output Formats** - Table (human-readable), JSON, and CSV formats
- 🔐 **Secure Credential Storage** - File-based with proper permissions and environment variable support
- 🌍 **Cross-Platform** - Works on Linux, macOS, and Windows
- ⚡ **Rate Limit Handling** - Automatic retry with exponential backoff

## Installation

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
# Search tickets (client-side filtering)
freshdesk ticket search "login issue"
freshdesk ticket search "user@example.com"
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
- **Binary Size**: ~3-5MB (platform dependent)
- **Memory Usage**: < 20MB typical
- **Rate Limiting**: Automatic with exponential backoff

## Security

- API keys are never logged or displayed in full
- Configuration files use secure permissions (600 on Unix)
- Environment variables supported for CI/CD
- Read-only mode for safe exploration

## Roadmap

- [ ] Attachment upload/download support
- [ ] Conversation management
- [ ] Bulk operations
- [ ] Progress indicators for long operations
- [ ] Tab completion support
- [ ] Interactive mode
- [ ] Webhook support

## License

MIT License - see [LICENSE](LICENSE) file for details.

## Support

- **Issues**: [GitHub Issues](https://github.com/Aaronontheweb/freshdesk-cli/issues)
- **Discussions**: [GitHub Discussions](https://github.com/Aaronontheweb/freshdesk-cli/discussions)

## Acknowledgments

Built with ❤️ using:
- [.NET 9](https://dotnet.microsoft.com/)
- [System.Text.Json](https://docs.microsoft.com/en-us/dotnet/api/system.text.json)
- [Freshdesk API v2](https://developers.freshdesk.com/api/)

## Author

Aaron Stannard - [@Aaronontheweb](https://github.com/Aaronontheweb)
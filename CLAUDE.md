# CLAUDE.md - Freshdesk CLI Project Context

## Project Overview
This is a command-line interface (CLI) tool for interacting with the Freshdesk API v2, built with C# and AOT (Ahead-of-Time) compilation for optimal performance and single-file deployment.

## Key Technical Decisions

### Architecture
- **Language**: C# with .NET 8 and AOT compilation
- **CLI Framework**: System.CommandLine (Microsoft-supported, AOT-compatible)
- **JSON Serialization**: System.Text.Json with source generators (no reflection)
- **HTTP Client**: Built-in HttpClient with custom handlers for auth and rate limiting
- **Target Binary Size**: < 15MB standalone executable
- **Solution Format**: Using `.slnx` format (keep this, don't change to .sln)

### Testing Strategy
**IMPORTANT**: We cannot test against live Freshdesk APIs. All testing must use:
- Mock HTTP responses from Freshdesk documentation
- Fake test server for integration tests
- Sample JSON responses stored in `tests/TestData/FreshdeskResponses/`

### Build Commands
Always use `dotnet` commands, avoid direct XML manipulation:
```bash
# Add project to solution
dotnet sln FreshDeskCli.slnx add src/FreshdeskCLI/FreshdeskCLI.csproj

# Add package
dotnet add package System.CommandLine --prerelease

# Build AOT
dotnet publish -c Release -r linux-x64
```

## Implementation Status

### Completed Documentation
- ✅ Implementation plan and phases
- ✅ Models with AOT-compatible serialization
- ✅ Services (API client, config, rate limiting, attachments)
- ✅ Commands with LLM-discoverable help
- ✅ Testing strategy with mock data
- ✅ Build and deployment guide
- ✅ Credential management

### Next Steps
1. Create the actual C# project structure
2. Implement models with source generators
3. Build core services with proper error handling
4. Implement CLI commands
5. Create comprehensive test suite with mock data
6. Set up CI/CD pipeline

## API Integration Notes

### Freshdesk API v2 Constraints
- **Rate Limiting**: 700-1000 requests/hour (plan-dependent)
- **Search Limitations**: Max 300 results, no per_page parameter
- **Attachments**: Not available in list endpoints, must fetch individual tickets
- **Authentication**: Basic Auth with API key as username, "X" as password

### Key Endpoints
```
GET  /api/v2/tickets?page={n}&per_page={100}      # List tickets
GET  /api/v2/tickets/{id}                         # Get ticket
GET  /api/v2/tickets/{id}?include=conversations   # With conversations
GET  /api/v2/search/tickets?query={filter}        # Search (limited)
PUT  /api/v2/tickets/{id}                         # Update ticket
POST /api/v2/tickets/{id}/reply                   # Add reply
```

## Credential Storage
Following industry standards (AWS CLI, GitHub CLI):
- **Primary**: `~/.freshdesk/config.json` with 600 permissions
- **Override**: Environment variables (FRESHDESK_DOMAIN, FRESHDESK_API_KEY)
- **Future**: OS keyring integration

## Command Structure
```
freshdesk
├── auth
│   ├── login    # Configure credentials
│   ├── status   # Show auth status
│   └── logout   # Remove credentials
├── tickets
│   ├── list     # List tickets
│   ├── get      # Get ticket details
│   ├── search   # Search tickets
│   ├── update   # Update ticket
│   └── reply    # Reply to ticket
├── attachments
│   ├── list     # List attachments
│   └── download # Download attachments
└── help         # Help (with --format llm for LLM discovery)
```

## Help Discovery for LLMs
The CLI provides structured help output for LLM consumption:
```bash
freshdesk help --format llm
```
Returns JSON with:
- Complete command schema
- Parameter types and defaults
- Authentication requirements
- Example usage
- Error codes

## AOT Compatibility Requirements
1. **No Reflection**: Use source generators for all serialization
2. **No Dynamic Code**: No runtime code generation
3. **Explicit Registration**: All types must be registered in FreshdeskJsonContext
4. **Trimming Safe**: No trimming warnings allowed

## Testing Guidelines

### Mock Data Location
```
tests/TestData/
├── FreshdeskResponses/
│   ├── tickets_list.json
│   ├── ticket_detail.json
│   ├── ticket_with_conversations.json
│   ├── search_results.json
│   └── error_responses.json
└── MockConfigs/
    └── test_config.json
```

### Test Execution
```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Validate AOT compatibility
dotnet publish -c Release -r linux-x64 /p:TrimmerSingleWarn=false
```

## Performance Goals
- **Startup Time**: < 100ms
- **Memory Usage**: < 50MB for typical operations
- **Binary Size**: < 15MB
- **Concurrent Downloads**: 3 attachments in parallel

## Security Notes
- Never log API keys
- Mask credentials in output
- Clear sensitive data from memory after use
- Validate all user input
- Use secure input for password prompts

## Development Workflow

### Setting Up
```bash
# Clone and checkout branch
git checkout freshdesk-cli-implementation

# Create project
dotnet new console -n FreshdeskCLI -o src/FreshdeskCLI
dotnet sln FreshDeskCli.slnx add src/FreshdeskCLI/FreshdeskCLI.csproj

# Add dependencies
cd src/FreshdeskCLI
dotnet add package System.CommandLine --prerelease
dotnet add package Microsoft.Extensions.Hosting
```

### Building
```bash
# Development build
dotnet build

# Release with AOT
dotnet publish -c Release -r linux-x64

# Run locally
dotnet run -- tickets list
```

### Before Committing
1. Run tests: `dotnet test`
2. Check AOT compatibility: `dotnet publish -c Release -r linux-x64`
3. Verify no trimming warnings
4. Update documentation if needed

## Common Patterns

### Error Handling
Always provide actionable error messages:
```csharp
throw new AuthenticationException(
    "Not authenticated. Run 'freshdesk auth login' first.");
```

### Progress Reporting
Use IProgress<T> for long operations:
```csharp
var progress = new Progress<DownloadProgress>(p => 
    Console.WriteLine($"Downloading: {p.PercentComplete:F1}%"));
```

### Cancellation
Support cancellation tokens throughout:
```csharp
public async Task<Ticket> GetTicketAsync(
    long id, 
    CancellationToken cancellationToken = default)
```

## Notes for Future Development

### Planned Enhancements
- Multi-account support with profiles
- OS keyring integration for secure storage
- Batch operations for bulk updates
- Export to CSV/Excel
- Interactive mode with REPL
- Plugin system for custom commands

### Known Limitations
- Search API limited to 300 results
- No webhook support in CLI
- Attachments require individual ticket fetches
- Rate limiting requires careful request management

## Questions or Issues?
Check the documentation in the `docs/` directory:
- `IMPLEMENTATION_PLAN.md` - Overall strategy
- `TESTING_STRATEGY.md` - How to test without live API
- `BUILD_DEPLOYMENT.md` - Build and distribution
- `CREDENTIAL_MANAGEMENT.md` - Security considerations
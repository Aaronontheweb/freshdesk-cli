# Freshdesk CLI - AI Assistant Instructions

## Project Overview
This is a command-line interface (CLI) for Freshdesk API v2, built with C# and .NET 9 using AOT (Ahead-of-Time) compilation for optimal performance and small binary size.

## Key Technical Constraints

### AOT Compilation Requirements
- **NO REFLECTION**: All JSON serialization must use source generators
- Use `System.Text.Json` with `JsonSerializerContext` 
- All models must be registered in `FreshdeskJsonContext.cs`
- Target binary size: < 15MB

### API Limitations
- **NO LIVE TESTING**: We cannot test against a real Freshdesk instance
- Use mock data from Freshdesk documentation for testing
- Rate limiting is handled reactively (429 responses) not proactively
- Basic Auth with API key as username, 'X' as password

## Architecture Decisions

### Configuration Management
- File-based storage at `~/.freshdesk/config.json`
- Multi-profile support (like AWS CLI)
- Environment variable fallback (`FRESHDESK_API_KEY`, `FRESHDESK_DOMAIN`)
- Secure file permissions (user-only on Unix)

### Command Structure
- Simple command routing without complex System.CommandLine features
- Read-only mode (`--read-only`) for safe operations
- Multiple output formats (table, JSON, CSV)

### Error Handling
- Reactive rate limiting with exponential backoff
- Graceful handling of 404s and network errors
- Clear error messages for users

## Code Style Guidelines

### General Rules
- Use `var` for obvious types
- Prefer expression-bodied members for simple properties
- Use collection expressions `[]` instead of `new()`
- NO COMMENTS unless absolutely necessary
- Use meaningful variable names instead of comments

### AOT-Specific Patterns
```csharp
// GOOD - AOT-compatible
var json = JsonSerializer.Serialize(ticket, FreshdeskJsonContext.Default.Ticket);

// BAD - Uses reflection
var json = JsonSerializer.Serialize(ticket);
```

### Async/Await
- Always use `ConfigureAwait(false)` in library code (not needed here as it's an app)
- Use `CancellationToken` throughout the chain
- Prefer `ValueTask` for hot paths (not critical for CLI)

## Testing Strategy

### Mock Data Approach
Since we cannot test against a live Freshdesk instance:
1. Use sample JSON responses from Freshdesk API documentation
2. Create mock HTTP responses for unit tests
3. Test serialization/deserialization with known good data
4. Verify AOT compatibility with `--test-aot` flag

### Test Commands
```bash
# Test AOT compatibility
dotnet run -- --test-aot

# Test in read-only mode (safe)
dotnet run -- --read-only ticket list

# Test with mock config
export FRESHDESK_API_KEY=test_key
export FRESHDESK_DOMAIN=test
dotnet run -- config test
```

## Common Tasks

### Adding a New Model
1. Create the model class in `Models/`
2. Add JsonSerializable attribute in `FreshdeskJsonContext.cs`
3. Test serialization with `--test-aot`

### Adding a New Command
1. Add handler method in `Program.cs`
2. Update help text in `ShowHelp()`
3. Add read-only check if it's a write operation
4. Update README with usage examples

### Updating Dependencies
- Check AOT compatibility before adding packages
- Prefer packages with source generators over reflection
- Test binary size after updates

## Performance Considerations

### Binary Size Optimization
- Use `TrimMode=full` for aggressive trimming
- Avoid large dependencies
- Consider lazy loading for optional features

### Runtime Performance
- CLI should start in < 100ms
- Use streaming for large data sets
- Implement pagination for list operations

## Security Considerations

### Credential Storage
- Never log or display full API keys
- Use secure file permissions on config files
- Support environment variables for CI/CD
- Clear sensitive data from memory after use

### API Safety
- Implement read-only mode for exploration
- Confirm destructive operations
- Rate limit handling to prevent API abuse
- Validate all user input

## Future Enhancements

### Planned Features
- Attachment handling (upload/download)
- Conversation management
- Bulk operations
- Progress indicators
- Tab completion support

### Potential Improvements
- Keyring/credential manager integration
- Interactive mode for complex operations
- Export to multiple formats
- Webhook support for real-time updates

## Debugging Tips

### Common Issues
1. **AOT Warnings**: Check that all types are registered in JsonContext
2. **Rate Limiting**: Implement exponential backoff
3. **Large Responses**: Use pagination and streaming
4. **Auth Failures**: Verify Basic Auth format

### Useful Commands
```bash
# Check binary size
dotnet publish -c Release -r linux-x64 /p:PublishAot=true
ls -lh ./publish/linux-x64/freshdesk

# Check for reflection usage
dotnet build /p:TrimmerSingleWarn=false

# Test specific scenarios
dotnet run -- --read-only ticket list --format json | jq '.'
```

## Contact & Support
- GitHub Issues: Report bugs and feature requests
- PR Guidelines: Include tests and update documentation
- Code Review: Focus on AOT compatibility and performance
namespace FreshdeskCLI.Services;

/// <summary>
/// HTTP delegating handler that strips the Freshdesk Basic auth header from any request
/// that is not bound for the Freshdesk host. The Freshdesk API hands back pre-signed AWS
/// S3 URLs for attachments; S3 rejects requests that mix query-string authentication with
/// an <c>Authorization</c> header, so the inherited default header must be removed before
/// the attachment download leaves for S3.
/// </summary>
public sealed class FreshdeskAuthHandler : DelegatingHandler
{
    private readonly string _freshdeskHost;

    public FreshdeskAuthHandler(string freshdeskHost, HttpMessageHandler innerHandler) : base(innerHandler)
    {
        ArgumentException.ThrowIfNullOrEmpty(freshdeskHost);
        _freshdeskHost = freshdeskHost;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.RequestUri is { } uri &&
            !string.Equals(uri.Host, _freshdeskHost, StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.Authorization = null;
        }

        return base.SendAsync(request, cancellationToken);
    }
}

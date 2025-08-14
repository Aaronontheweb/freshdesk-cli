using System.Net;

namespace FreshdeskCLI.Services;

/// <summary>
/// HTTP delegating handler that handles rate limit responses from Freshdesk API.
/// Freshdesk API returns 429 Too Many Requests when rate limit is exceeded.
/// </summary>
public sealed class RateLimitHandler : DelegatingHandler
{
    private const int MaxRetries = 3;

    public RateLimitHandler(HttpMessageHandler innerHandler) : base(innerHandler)
    {
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        int retryCount = 0;

        while (retryCount < MaxRetries)
        {
            var response = await base.SendAsync(request, cancellationToken);

            // If we get rate limited, wait and retry
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                retryCount++;

                // Check for Retry-After header from Freshdesk
                var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(60);

                Console.Error.WriteLine($"Rate limited. Waiting {retryAfter.TotalSeconds:F0} seconds before retry {retryCount}/{MaxRetries}...");
                await Task.Delay(retryAfter, cancellationToken);

                // Clone the request for retry since the original may have been disposed
                request = CloneRequest(request);
                continue;
            }

            return response;
        }

        throw new HttpRequestException($"Rate limit exceeded after {MaxRetries} retries");
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Content = request.Content,
            Version = request.Version
        };

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (request.Content != null)
        {
            foreach (var header in request.Content.Headers)
            {
                clone.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
    }
}
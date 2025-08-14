using System.Net;
using System.Threading.RateLimiting;

namespace FreshdeskCLI.Services;

/// <summary>
/// HTTP delegating handler that implements rate limiting for Freshdesk API calls.
/// Freshdesk API has a rate limit of 700 requests per minute for Performance plans.
/// </summary>
public sealed class RateLimitHandler : DelegatingHandler
{
    private readonly RateLimiter _rateLimiter;
    private readonly SemaphoreSlim _semaphore;

    public RateLimitHandler(HttpMessageHandler innerHandler) : base(innerHandler)
    {
        // Freshdesk rate limit: 700 requests per minute for Performance plans
        // We'll be conservative and use 600 to leave some headroom
        _rateLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = 600,
            ReplenishmentPeriod = TimeSpan.FromMinutes(1),
            TokensPerPeriod = 600,
            AutoReplenishment = true,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 100
        });

        _semaphore = new SemaphoreSlim(1, 1);
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        using var lease = await _rateLimiter.AcquireAsync(1, cancellationToken);

        if (lease.IsAcquired)
        {
            var response = await base.SendAsync(request, cancellationToken);

            // Check for rate limit headers from Freshdesk
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                // Extract retry-after header if available
                if (response.Headers.RetryAfter != null)
                {
                    var retryAfter = response.Headers.RetryAfter.Delta ?? TimeSpan.FromSeconds(60);
                    await Task.Delay(retryAfter, cancellationToken);

                    // Retry the request
                    return await SendAsync(request, cancellationToken);
                }
            }

            return response;
        }

        throw new InvalidOperationException("Rate limit exceeded and queue is full");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _rateLimiter?.Dispose();
            _semaphore?.Dispose();
        }
        base.Dispose(disposing);
    }
}
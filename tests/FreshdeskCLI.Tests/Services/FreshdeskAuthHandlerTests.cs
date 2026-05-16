using System.Net;
using System.Net.Http.Headers;
using FreshdeskCLI.Services;

namespace FreshdeskCLI.Tests.Services;

public class FreshdeskAuthHandlerTests
{
    private const string FreshdeskHost = "test.freshdesk.com";

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private static async Task<HttpRequestMessage> SendThroughHandlerAsync(string requestUri)
    {
        var recorder = new RecordingHandler();
        var handler = new FreshdeskAuthHandler(FreshdeskHost, recorder);
        using var invoker = new HttpMessageInvoker(handler);

        var request = new HttpRequestMessage(HttpMethod.Get, requestUri)
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Basic", "dGVzdC1hcGkta2V5Olg=") }
        };

        await invoker.SendAsync(request, CancellationToken.None);

        Assert.NotNull(recorder.LastRequest);
        return recorder.LastRequest!;
    }

    [Fact]
    public async Task StripsAuthorization_ForPreSignedS3Url()
    {
        var recorded = await SendThroughHandlerAsync(
            "https://s3.amazonaws.com/euc-freshdesk/data/helpdesk/attachments/production/123/file.csv?X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Signature=abc123");

        Assert.Null(recorded.Headers.Authorization);
    }

    [Fact]
    public async Task PreservesAuthorization_ForFreshdeskHost()
    {
        var recorded = await SendThroughHandlerAsync("https://test.freshdesk.com/api/v2/tickets/1");

        Assert.NotNull(recorded.Headers.Authorization);
        Assert.Equal("Basic", recorded.Headers.Authorization!.Scheme);
    }

    [Fact]
    public async Task PreservesAuthorization_ForFreshdeskHost_RegardlessOfCasing()
    {
        var recorded = await SendThroughHandlerAsync("https://TEST.Freshdesk.COM/api/v2/tickets/1");

        Assert.NotNull(recorded.Headers.Authorization);
    }
}

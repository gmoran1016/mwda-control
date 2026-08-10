using System.Net;
using System.Text;
using Mwda.Control.Protocol;

namespace Mwda.Control.Tests.Protocol;

public sealed class AdapterHttpTransportTests
{
    [Fact]
    public async Task TransportPreservesStatusBodyAndContentType()
    {
        using var handler = new StubHttpMessageHandler(
            _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"DeviceName":"WeightRoom-AD"}""",
                    Encoding.UTF8,
                    "text/html"),
            }));
        using var transport = new AdapterHttpTransport(handler, TimeSpan.FromSeconds(2));

        var response = await transport.GetAsync(new Uri("http://192.168.137.247/test"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.ContentType);
        Assert.Contains("WeightRoom-AD", response.Body);
    }

    [Fact]
    public async Task HttpErrorRemainsAResponse()
    {
        using var handler = new StubHttpMessageHandler(
            _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("missing", Encoding.UTF8, "text/plain"),
            }));
        using var transport = new AdapterHttpTransport(handler, TimeSpan.FromSeconds(2));

        var response = await transport.GetAsync(new Uri("http://192.168.137.247/missing"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("missing", response.Body);
    }

    [Fact]
    public async Task TimeoutIncludesGetOperationAndEndpointContext()
    {
        var endpoint = new Uri("http://192.168.137.247/slow");
        using var handler = new StubHttpMessageHandler(
            async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });
        using var transport = new AdapterHttpTransport(handler, TimeSpan.FromMilliseconds(20));

        var exception = await Assert.ThrowsAsync<AdapterProtocolException>(
            () => transport.GetAsync(endpoint));

        Assert.Contains("GET", exception.Message);
        Assert.Contains(endpoint.AbsoluteUri, exception.Message);
        Assert.Contains("timed out", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CallerCancellationIsNotReportedAsATimeout()
    {
        using var handler = new StubHttpMessageHandler(
            async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });
        using var transport = new AdapterHttpTransport(handler, TimeSpan.FromSeconds(5));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => transport.GetAsync(new Uri("http://192.168.137.247/cancelled"), cancellation.Token));
    }

    [Fact]
    public async Task ConnectionFailureIncludesGetOperationAndEndpointContext()
    {
        var endpoint = new Uri("http://192.168.137.247/unreachable");
        using var handler = new StubHttpMessageHandler(
            _ => throw new HttpRequestException("Connection refused."));
        using var transport = new AdapterHttpTransport(handler, TimeSpan.FromSeconds(2));

        var exception = await Assert.ThrowsAsync<AdapterProtocolException>(
            () => transport.GetAsync(endpoint));

        Assert.Contains("GET", exception.Message);
        Assert.Contains(endpoint.AbsoluteUri, exception.Message);
        Assert.IsType<HttpRequestException>(exception.InnerException);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _send;

        public StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send)
            : this((request, _) => send(request))
        {
        }

        public StubHttpMessageHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        {
            _send = send;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            _send(request, cancellationToken);
    }
}

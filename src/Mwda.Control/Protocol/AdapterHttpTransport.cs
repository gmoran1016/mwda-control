using System.IO;
using System.Net;
using System.Net.Http;

namespace Mwda.Control.Protocol;

public sealed record AdapterHttpResponse(
    HttpStatusCode StatusCode,
    string Body,
    string? ContentType);

public sealed class AdapterHttpTransport : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly TimeSpan _requestTimeout;

    public AdapterHttpTransport(TimeSpan requestTimeout)
        : this(CreateHandler(requestTimeout), requestTimeout)
    {
    }

    public AdapterHttpTransport(HttpMessageHandler handler, TimeSpan requestTimeout)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ValidateTimeout(requestTimeout);

        _requestTimeout = requestTimeout;
        _httpClient = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
    }

    public async Task<AdapterHttpResponse> GetAsync(
        Uri requestUri,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestUri);
        if (!requestUri.IsAbsoluteUri)
        {
            throw new ArgumentException("The adapter request URI must be absolute.", nameof(requestUri));
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_requestTimeout);
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

        try
        {
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);
            var body = await response.Content.ReadAsStringAsync(timeout.Token);

            return new AdapterHttpResponse(
                response.StatusCode,
                body,
                response.Content.Headers.ContentType?.MediaType);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AdapterProtocolException(
                $"GET {requestUri.AbsoluteUri} timed out after {_requestTimeout.TotalMilliseconds:0} ms.",
                exception);
        }
        catch (HttpRequestException exception)
        {
            throw TransportFailure(requestUri, exception);
        }
        catch (IOException exception)
        {
            throw TransportFailure(requestUri, exception);
        }
    }

    public void Dispose() => _httpClient.Dispose();

    private static SocketsHttpHandler CreateHandler(TimeSpan connectTimeout)
    {
        ValidateTimeout(connectTimeout);

        return new SocketsHttpHandler
        {
            ConnectTimeout = connectTimeout,
            UseProxy = false,
            AutomaticDecompression = DecompressionMethods.All,
        };
    }

    private static void ValidateTimeout(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero || timeout == Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                timeout,
                "The adapter request timeout must be positive and finite.");
        }
    }

    private static AdapterProtocolException TransportFailure(Uri requestUri, Exception exception) =>
        new($"GET {requestUri.AbsoluteUri} failed before a complete HTTP response was received.", exception);
}

using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Mwda.Control.Protocol;

namespace Mwda.Control.Tests.Protocol;

public sealed class AdapterClientTests
{
    private static readonly AdapterEndpoint Endpoint =
        new(new Uri("http://192.168.137.247/"));

    [Fact]
    public async Task ReadsCoreSettingsFromTheConfiguredRoutes()
    {
        var requests = new List<string>();
        using var handler = new StubHttpMessageHandler(request =>
        {
            requests.Add(request.RequestUri!.PathAndQuery);
            var body = requests.Count switch
            {
                1 => """{"DeviceName":"WeightRoom-AD"}""",
                2 => """{"IsAutoAdjust":false,"OverscanSettingValue":0}""",
                3 => """{"PBCModeStatus":"Disabled"}""",
                _ => throw new InvalidOperationException("Unexpected request."),
            };

            return JsonResponse(body);
        });
        using var client = CreateClient(handler);

        var identity = await client.GetIdentityAsync();
        var overscan = await client.GetOverscanAsync();
        var protection = await client.GetPasswordProtectionAsync();

        Assert.Equal("WeightRoom-AD", identity.DeviceName);
        Assert.False(overscan.IsAutoAdjust);
        Assert.Equal(0, overscan.Value);
        Assert.True(protection.Enabled);
        Assert.Equal(
            new[]
            {
                "/cgi-bin/msupload.sh?Action=GetDeviceName",
                "/cgi-bin/msupload.sh?Action=GetOverscanSetting",
                "/cgi-bin/msupload.sh?Action=GetPBCMode",
            },
            requests);
    }

    [Fact]
    public async Task SuccessfulDeviceNameWriteIsFollowedByExactReadBack()
    {
        var requests = new List<HttpRequestSnapshot>();
        using var handler = new StubHttpMessageHandler(async request =>
        {
            requests.Add(await HttpRequestSnapshot.CreateAsync(request));
            return requests.Count == 1
                ? JsonResponse("{}")
                : JsonResponse("""{"DeviceName":"Room+West"}""");
        });
        using var client = CreateClient(handler);

        await client.SetDeviceNameAsync("Room+West");

        Assert.Equal(2, requests.Count);
        Assert.Equal(HttpMethod.Get, requests[0].Method);
        Assert.Equal(
            "/cgi-bin/msupload.sh?Action=SetDeviceName&NewDeviceName=Room%2BWest",
            requests[0].PathAndQuery);
        Assert.Null(requests[0].ContentType);
        Assert.Null(requests[0].Body);
        Assert.Equal(
            "/cgi-bin/msupload.sh?Action=GetDeviceName",
            requests[1].PathAndQuery);
    }

    [Fact]
    public async Task DeviceNameWriteRejectsCaseDifferentReadBack()
    {
        using var handler = new StubHttpMessageHandler(request =>
            request.RequestUri!.Query.Contains("SetDeviceName", StringComparison.Ordinal)
                ? JsonResponse("{}")
                : JsonResponse("""{"DeviceName":"room+west"}"""));
        using var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<AdapterProtocolException>(
            () => client.SetDeviceNameAsync("Room+West"));

        Assert.Contains(nameof(AdapterOperation.SetDeviceName), exception.Message);
        Assert.Contains("200", exception.Message);
        Assert.Contains("redacted body prefix", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("room+west", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OverscanWriteRequiresExactTypedReadBack()
    {
        using var handler = new StubHttpMessageHandler(request =>
            request.RequestUri!.Query.Contains("SetOverscanSetting", StringComparison.Ordinal)
                ? JsonResponse("{}")
                : JsonResponse("""{"IsAutoAdjust":true,"OverscanSettingValue":25}"""));
        using var client = CreateClient(handler);

        await client.SetOverscanAsync(new OverscanSettings(true, 25));
    }

    [Fact]
    public async Task PasswordProtectionWriteRequiresExactTypedReadBack()
    {
        using var handler = new StubHttpMessageHandler(request =>
            request.RequestUri!.Query.Contains("SetPBCMode", StringComparison.Ordinal)
                ? JsonResponse("{\"ErrorCode\":0}")
                : request.RequestUri.Query.Contains("GetPBCMode", StringComparison.Ordinal)
                    ? JsonResponse("""{"PBCModeStatus":"Disabled"}""")
                    : throw new InvalidOperationException("Unexpected request."));
        using var client = CreateClient(handler);

        await client.SetPasswordProtectionAsync(enabled: true, password: null);
    }

    [Fact]
    public async Task PasswordProtectionWriteAcceptsAnEmptySuccessBodyWhenReadBackMatches()
    {
        using var handler = new StubHttpMessageHandler(request =>
            request.RequestUri!.Query.Contains("SetPBCMode", StringComparison.Ordinal)
                ? TextResponse(HttpStatusCode.OK, string.Empty)
                : request.RequestUri.Query.Contains("GetPBCMode", StringComparison.Ordinal)
                    ? JsonResponse("""{"PBCModeStatus":"Disabled"}""")
                    : throw new InvalidOperationException("Unexpected request."));
        using var client = CreateClient(handler);

        await client.SetPasswordProtectionAsync(enabled: true, password: null);
    }

    [Fact]
    public async Task SuccessfulWriteWithNonzeroAdapterErrorCodeFailsBeforeReadBack()
    {
        var requestCount = 0;
        using var handler = new StubHttpMessageHandler(_ =>
        {
            requestCount++;
            return requestCount == 1
                ? JsonResponse("{\"ErrorCode\":-8}")
                : JsonResponse("{\"PBCModeStatus\":\"Enabled\"}");
        });
        using var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<AdapterProtocolException>(
            () => client.SetPasswordProtectionAsync(enabled: false, password: null));

        Assert.Equal(1, requestCount);
        Assert.Contains("error code -8", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(true, "Disabled")]
    [InlineData(false, "Enabled")]
    public async Task PairingProtectionMapsPinOnlyStateToPbcMode(
        bool enabled,
        string expectedPbcMode)
    {
        var requests = new List<string>();
        using var handler = new StubHttpMessageHandler(request =>
        {
            requests.Add(request.RequestUri!.PathAndQuery);
            return request.RequestUri.Query.Contains("SetPBCMode", StringComparison.Ordinal)
                ? JsonResponse("{\"ErrorCode\":0}")
                : JsonResponse($"{{\"PBCModeStatus\":\"{expectedPbcMode}\"}}");
        });
        using var client = CreateClient(handler);

        await client.SetPasswordProtectionAsync(enabled, password: null);

        Assert.Equal(
            $"/cgi-bin/msupload.sh?Action=SetPBCMode&PBCModeStatus={expectedPbcMode}",
            requests[0]);
        Assert.Equal("/cgi-bin/msupload.sh?Action=GetPBCMode", requests[1]);
    }

    [Fact]
    public async Task PasswordValueIsRejectedBecauseTheRecordedOperationDoesNotTransmitIt()
    {
        using var handler = new StubHttpMessageHandler(
            new Func<HttpRequestMessage, HttpResponseMessage>(
                _ => throw new InvalidOperationException("No request should be sent.")));
        using var client = CreateClient(handler);

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.SetPasswordProtectionAsync(enabled: true, password: "not-transmitted"));
    }

    [Fact]
    public async Task NonSuccessWriteIncludesOperationStatusAndRedactedBodyPrefix()
    {
        using var handler = new StubHttpMessageHandler(_ =>
            TextResponse(HttpStatusCode.InternalServerError, "secret diagnostic value"));
        using var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<AdapterProtocolException>(
            () => client.SetDeviceNameAsync("Room+West"));

        Assert.Contains(nameof(AdapterOperation.SetDeviceName), exception.Message);
        Assert.Contains("500", exception.Message);
        Assert.Contains("redacted body prefix", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret diagnostic value", exception.Message);
    }

    [Fact]
    public async Task SuccessfulWriteWithMalformedBodyFailsBeforeMatchingReadBack()
    {
        var requestCount = 0;
        using var handler = new StubHttpMessageHandler(_ =>
        {
            requestCount++;
            return requestCount == 1
                ? TextResponse(HttpStatusCode.OK, "private malformed write payload")
                : JsonResponse("""{"DeviceName":"Room+West"}""");
        });
        using var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<AdapterProtocolException>(
            () => client.SetDeviceNameAsync("Room+West"));

        Assert.Equal(1, requestCount);
        Assert.Contains(nameof(AdapterOperation.SetDeviceName), exception.Message);
        Assert.Contains("200", exception.Message);
        Assert.Contains("redacted body prefix", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private malformed write payload", exception.Message);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("\"accepted\"")]
    public async Task SuccessfulWriteWithNonObjectJsonFailsBeforeReadBack(string writeBody)
    {
        var requestCount = 0;
        using var handler = new StubHttpMessageHandler(_ =>
        {
            requestCount++;
            return requestCount == 1
                ? JsonResponse(writeBody)
                : JsonResponse("""{"DeviceName":"Room+West"}""");
        });
        using var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<AdapterProtocolException>(
            () => client.SetDeviceNameAsync("Room+West"));

        Assert.Equal(1, requestCount);
        Assert.Contains(nameof(AdapterOperation.SetDeviceName), exception.Message);
        Assert.Contains("200", exception.Message);
        Assert.Contains("redacted body prefix", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MalformedReadBackUsesWriteOperationContext()
    {
        using var handler = new StubHttpMessageHandler(request =>
            request.RequestUri!.Query.Contains("SetDeviceName", StringComparison.Ordinal)
                ? JsonResponse("{}")
                : TextResponse(HttpStatusCode.OK, "private malformed payload"));
        using var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<AdapterProtocolException>(
            () => client.SetDeviceNameAsync("Room+West"));

        Assert.Contains(nameof(AdapterOperation.SetDeviceName), exception.Message);
        Assert.Contains("200", exception.Message);
        Assert.Contains("redacted body prefix", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private malformed payload", exception.Message);
        Assert.IsType<AdapterProtocolException>(exception.InnerException);
    }

    [Fact]
    public async Task WritesAreSerializedPerClient()
    {
        var firstWriteArrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstWrite = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writeCount = 0;
        var responses = new ConcurrentQueue<string>();
        responses.Enqueue("""{"DeviceName":"First"}""");
        responses.Enqueue("""{"DeviceName":"Second"}""");

        using var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            if (request.RequestUri!.Query.Contains("SetDeviceName", StringComparison.Ordinal))
            {
                var currentWrite = Interlocked.Increment(ref writeCount);
                if (currentWrite == 1)
                {
                    firstWriteArrived.SetResult();
                    await releaseFirstWrite.Task.WaitAsync(cancellationToken);
                }

                return JsonResponse("{}");
            }

            Assert.True(responses.TryDequeue(out var response));
            return JsonResponse(response);
        });
        using var client = CreateClient(handler);

        var first = client.SetDeviceNameAsync("First");
        await firstWriteArrived.Task;
        var second = client.SetDeviceNameAsync("Second");

        Assert.False(second.IsCompleted);
        Assert.Equal(1, Volatile.Read(ref writeCount));

        releaseFirstWrite.SetResult();
        await Task.WhenAll(first, second);
        Assert.Equal(2, writeCount);
    }

    private static AdapterClient CreateClient(HttpMessageHandler handler) =>
        new(Endpoint, handler, TimeSpan.FromSeconds(2));

    private static HttpResponseMessage JsonResponse(string body) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

    private static HttpResponseMessage TextResponse(HttpStatusCode status, string body) =>
        new(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/plain"),
        };

    private sealed record HttpRequestSnapshot(
        HttpMethod Method,
        string PathAndQuery,
        string? ContentType,
        string? Body)
    {
        public static async Task<HttpRequestSnapshot> CreateAsync(HttpRequestMessage request) =>
            new(
                request.Method,
                request.RequestUri!.PathAndQuery,
                request.Content?.Headers.ContentType?.MediaType,
                request.Content is null ? null : await request.Content.ReadAsStringAsync());
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _send;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> send)
            : this((request, _) => Task.FromResult(send(request)))
        {
        }

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

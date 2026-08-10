using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Mwda.Control.Protocol;

namespace Mwda.Control.Tests.Protocol;

public sealed class AdvancedAdapterClientTests
{
    private static readonly AdapterEndpoint Endpoint =
        new(new Uri("http://192.168.137.247/"));

    [Fact]
    public async Task WallpaperFixtureReturnsTypedInfoFromObservedActionAndSchema()
    {
        using var handler = new StubHttpMessageHandler(request =>
        {
            AssertRequest(request, HttpMethod.Get, "GetWallpaperId", contentType: null);
            return JsonResponse(
                """{"WallpaperID":"3","AvailableWallpaperIDs":["1","2","3","4"],"SupportsCustomWallpaper":true}""");
        });
        using var client = CreateClient(handler);

        var result = await client.GetWallpaperInfoAsync();

        Assert.Equal("3", result.CurrentWallpaperId);
        Assert.Equal(new[] { "1", "2", "3", "4" }, result.AvailableWallpaperIds);
        Assert.True(result.SupportsCustomWallpaper);
    }

    [Fact]
    public async Task WifiFixtureReturnsTypedSettingsFromObservedActionAndSchema()
    {
        using var handler = new StubHttpMessageHandler(request =>
        {
            AssertRequest(request, HttpMethod.Get, "GetWiFiSetting", contentType: null);
            return JsonResponse("""{"WiFiSsid":"GymNet","ConnectionStatus":"Connected"}""");
        });
        using var client = CreateClient(handler);

        var result = await client.GetWiFiSettingsAsync();

        Assert.Equal(new WifiSettings("GymNet", true), result);
    }

    [Fact]
    public async Task HdcpFixtureReturnsTypedSettingsFromObservedActionAndSchema()
    {
        using var handler = new StubHttpMessageHandler(request =>
        {
            AssertRequest(request, HttpMethod.Get, "GetHdcpStatus", contentType: null);
            return JsonResponse("""{"HdcpStatus":true}""");
        });
        using var client = CreateClient(handler);

        Assert.Equal(new HdcpSettings(true), await client.GetHdcpStatusAsync());
    }

    [Fact]
    public async Task LanguageFixtureReturnsTypedInfoFromObservedActionAndSchema()
    {
        using var handler = new StubHttpMessageHandler(request =>
        {
            AssertRequest(request, HttpMethod.Get, "GetLanguage", contentType: null);
            return JsonResponse(
                """{"CurrentLanguage":"en-US","LanguageCode":["en-US","fr-FR"]}""");
        });
        using var client = CreateClient(handler);

        var result = await client.GetLanguageAsync();

        Assert.Equal("en-US", result.LanguageTag);
        Assert.Equal(new[] { "en-US", "fr-FR" }, result.AvailableLanguageTags);
    }

    [Fact]
    public async Task PredefinedWallpaperWriteUsesTypedJsonAndExactReadBack()
    {
        var requestNumber = 0;
        using var handler = new StubHttpMessageHandler(async request =>
        {
            requestNumber++;
            if (requestNumber == 1)
            {
                AssertRequest(request, HttpMethod.Post, "SetPredefinedWallpaper", "application/json");
                Assert.Equal("{\"WallpaperID\":\"2\"}", await request.Content!.ReadAsStringAsync());
                return JsonResponse("{}");
            }

            AssertRequest(request, HttpMethod.Get, "GetWallpaperId", contentType: null);
            return JsonResponse(
                """{"WallpaperID":"2","AvailableWallpaperIDs":["1","2","3","4"],"SupportsCustomWallpaper":true}""");
        });
        using var client = CreateClient(handler);

        await client.SetPredefinedWallpaperAsync("2");

        Assert.Equal(2, requestNumber);
    }

    [Fact]
    public async Task CustomWallpaperUsesBoundedMultipartWithAllowListedImageType()
    {
        var imageBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        using var handler = new StubHttpMessageHandler(async request =>
        {
            AssertRequest(request, HttpMethod.Post, "UploadWallpaper", "multipart/form-data");
            var multipart = Assert.IsType<MultipartFormDataContent>(request.Content);
            var part = Assert.Single(multipart);
            Assert.Equal("image/png", part.Headers.ContentType!.MediaType);
            Assert.Equal("wallpaper", part.Headers.ContentDisposition!.Name);
            Assert.Equal("custom.png", part.Headers.ContentDisposition.FileName);
            Assert.Equal(imageBytes, await part.ReadAsByteArrayAsync());
            return JsonResponse("{}");
        });
        using var client = CreateClient(handler);

        await client.UploadCustomWallpaperAsync(
            new MemoryStream(imageBytes),
            "custom.png",
            "image/png");
    }

    [Fact]
    public async Task WifiWriteUsesTypedJsonAndExactReadBack()
    {
        var requestNumber = 0;
        using var handler = new StubHttpMessageHandler(async request =>
        {
            requestNumber++;
            if (requestNumber == 1)
            {
                AssertRequest(request, HttpMethod.Post, "SetConfigureWiFiAP", "application/json");
                Assert.Equal(
                    "{\"WiFiSsid\":\"GymNet\",\"WiFiPwd\":\"secret\"}",
                    await request.Content!.ReadAsStringAsync());
                return JsonResponse("{}");
            }

            return JsonResponse("""{"WiFiSsid":"GymNet","ConnectionStatus":"Connected"}""");
        });
        using var client = CreateClient(handler);

        await client.SetWiFiSettingsAsync(new WifiSettings("GymNet", true, "secret"));

        Assert.Equal(2, requestNumber);
    }

    [Fact]
    public async Task ForgetWifiUsesObservedActionAndDisconnectedReadBack()
    {
        var requestNumber = 0;
        using var handler = new StubHttpMessageHandler(async request =>
        {
            requestNumber++;
            if (requestNumber == 1)
            {
                AssertRequest(request, HttpMethod.Post, "ForgetWiFi", "application/json");
                Assert.Equal("{}", await request.Content!.ReadAsStringAsync());
                return JsonResponse("{}");
            }

            return JsonResponse("""{"WiFiSsid":"","ConnectionStatus":"Disconnected"}""");
        });
        using var client = CreateClient(handler);

        await client.ForgetWiFiAsync();

        Assert.Equal(2, requestNumber);
    }

    [Fact]
    public async Task HdcpWriteUsesTypedJsonAndExactReadBack()
    {
        var requestNumber = 0;
        using var handler = new StubHttpMessageHandler(async request =>
        {
            requestNumber++;
            if (requestNumber == 1)
            {
                AssertRequest(request, HttpMethod.Post, "SetHdcpStatus", "application/json");
                Assert.Equal("{\"HdcpStatus\":true}", await request.Content!.ReadAsStringAsync());
                return JsonResponse("{}");
            }

            return JsonResponse("""{"HdcpStatus":true}""");
        });
        using var client = CreateClient(handler);

        await client.SetHdcpStatusAsync(true);

        Assert.Equal(2, requestNumber);
    }

    [Fact]
    public async Task LanguageWriteUsesTypedJsonAndExactReadBack()
    {
        var requestNumber = 0;
        using var handler = new StubHttpMessageHandler(async request =>
        {
            requestNumber++;
            if (requestNumber == 1)
            {
                AssertRequest(request, HttpMethod.Post, "SetLanguage", "application/json");
                Assert.Equal("{\"LanguageCode\":\"fr-FR\"}", await request.Content!.ReadAsStringAsync());
                return JsonResponse("{}");
            }

            return JsonResponse(
                """{"CurrentLanguage":"fr-FR","LanguageCode":["en-US","fr-FR"]}""");
        });
        using var client = CreateClient(handler);

        await client.SetLanguageAsync("fr-FR");

        Assert.Equal(2, requestNumber);
    }

    [Fact]
    public async Task RestartUsesStandaloneActionAndValidatesAcknowledgement()
    {
        using var handler = new StubHttpMessageHandler(async request =>
        {
            AssertRequest(request, HttpMethod.Post, "Restart", "application/json");
            Assert.Equal("{}", await request.Content!.ReadAsStringAsync());
            return JsonResponse("{}");
        });
        using var client = CreateClient(handler);

        await client.RestartAsync();
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.NotImplemented)]
    public async Task UnsupportedHttpStatusHasClassifiedOperationFailure(HttpStatusCode status)
    {
        using var handler = new StubHttpMessageHandler(_ => TextResponse(status, "unsupported"));
        using var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<UnsupportedAdapterOperationException>(
            () => client.GetWiFiSettingsAsync());

        Assert.Equal(AdapterOperation.GetWiFiSettings, exception.Operation);
        Assert.Equal(status, exception.StatusCode);
    }

    [Fact]
    public async Task MalformedReadSchemaHasClassifiedOperationFailure()
    {
        using var handler = new StubHttpMessageHandler(_ =>
            JsonResponse("""{"HdcpStatus":"yes"}"""));
        using var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<UnsupportedAdapterOperationException>(
            () => client.GetHdcpStatusAsync());

        Assert.Equal(AdapterOperation.GetHdcpStatus, exception.Operation);
        Assert.Equal(HttpStatusCode.OK, exception.StatusCode);
    }

    [Theory]
    [InlineData("custom.gif", "image/gif")]
    [InlineData("custom.png", "image/jpeg")]
    [InlineData("..\\custom.png", "image/png")]
    public async Task CustomWallpaperRejectsNonAllowListedOrUnsafeFileInputs(
        string fileName,
        string contentType)
    {
        using var handler = new StubHttpMessageHandler(
            new Func<HttpRequestMessage, HttpResponseMessage>(
                _ => throw new InvalidOperationException("No request should be sent.")));
        using var client = CreateClient(handler);

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.UploadCustomWallpaperAsync(
                new MemoryStream(new byte[] { 1, 2, 3 }),
                fileName,
                contentType));
    }

    [Fact]
    public async Task CustomWallpaperRejectsPayloadAboveFourMebibytesBeforeSending()
    {
        using var handler = new StubHttpMessageHandler(
            new Func<HttpRequestMessage, HttpResponseMessage>(
                _ => throw new InvalidOperationException("No request should be sent.")));
        using var client = CreateClient(handler);
        var oversizedImage = new byte[4_194_305];

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.UploadCustomWallpaperAsync(
                new MemoryStream(oversizedImage),
                "custom.jpg",
                "image/jpeg"));
    }

    [Fact]
    public async Task CustomWallpaperAcceptsPayloadAtExactFourMebibyteBoundary()
    {
        var imageBytes = new byte[4_194_304];
        imageBytes[0] = 0xFF;
        imageBytes[1] = 0xD8;
        imageBytes[2] = 0xFF;
        using var handler = new StubHttpMessageHandler(request =>
        {
            var multipart = Assert.IsType<MultipartFormDataContent>(request.Content);
            Assert.Equal(4_194_304, Assert.Single(multipart).Headers.ContentLength);
            return JsonResponse("{}");
        });
        using var client = CreateClient(handler);

        await client.UploadCustomWallpaperAsync(
            new MemoryStream(imageBytes),
            "custom.jpg",
            "image/jpeg");
    }

    [Fact]
    public async Task CustomWallpaperBoundsNonSeekableStreamAtLimitPlusOneByte()
    {
        var imageBytes = new byte[4_194_305];
        imageBytes[0] = 0xFF;
        imageBytes[1] = 0xD8;
        imageBytes[2] = 0xFF;
        using var image = new NonSeekableReadStream(imageBytes);
        using var handler = new StubHttpMessageHandler(
            new Func<HttpRequestMessage, HttpResponseMessage>(
                _ => throw new InvalidOperationException("No request should be sent.")));
        using var client = CreateClient(handler);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.UploadCustomWallpaperAsync(image, "custom.jpg", "image/jpeg"));

        Assert.Equal(4_194_305, image.BytesRead);
    }

    [Fact]
    public async Task CustomWallpaperRejectsRenamedNonImageContentBeforeSending()
    {
        using var handler = new StubHttpMessageHandler(
            new Func<HttpRequestMessage, HttpResponseMessage>(
                _ => throw new InvalidOperationException("No request should be sent.")));
        using var client = CreateClient(handler);

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.UploadCustomWallpaperAsync(
                new MemoryStream(Encoding.UTF8.GetBytes("not really a PNG")),
                "custom.png",
                "image/png"));
    }

    private static AdvancedAdapterClient CreateClient(HttpMessageHandler handler) =>
        new(Endpoint, handler, TimeSpan.FromSeconds(2));

    private static void AssertRequest(
        HttpRequestMessage request,
        HttpMethod method,
        string action,
        string? contentType)
    {
        Assert.Equal(method, request.Method);
        Assert.Equal(
            $"/cgi-bin/msupload.sh?Action={Uri.EscapeDataString(action)}",
            request.RequestUri!.PathAndQuery);
        Assert.Equal(contentType, GetMediaType(request.Content?.Headers.ContentType));
    }

    private static string? GetMediaType(MediaTypeHeaderValue? contentType) => contentType?.MediaType;

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

        private StubHttpMessageHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        {
            _send = send;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => _send(request, cancellationToken);
    }

    private sealed class NonSeekableReadStream(byte[] bytes) : Stream
    {
        private readonly MemoryStream _inner = new(bytes);

        public int BytesRead { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = _inner.Read(buffer, offset, count);
            BytesRead += read;
            return read;
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            var read = _inner.Read(buffer.Span);
            BytesRead += read;
            return ValueTask.FromResult(read);
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}

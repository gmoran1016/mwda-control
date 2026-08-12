using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Mwda.Control.Protocol;

public sealed record PreparedWallpaperImages(byte[] BlackTint, byte[] Blur);

public sealed class WallpaperImagePreparer
{
    public const int OutputWidth = 1920;
    public const int OutputHeight = 1080;
    public const int MaximumSourceBytes = ProtocolRequestCatalog.MaximumWallpaperBytes;

    private static readonly IReadOnlyDictionary<string, string> WallpaperContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
        };

    public async Task<PreparedWallpaperImages> PrepareAsync(
        Stream image,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ValidateWallpaperFile(fileName, contentType);
        if (!image.CanRead)
        {
            throw new ArgumentException("The wallpaper stream must be readable.", nameof(image));
        }

        if (image.CanSeek && image.Length - image.Position > MaximumSourceBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(image),
                "The wallpaper exceeds the four-mebibyte upload limit.");
        }

        var bytes = await ReadBoundedAsync(image, cancellationToken);
        ValidateWallpaperSignature(bytes, contentType);

        BitmapSource source;
        try
        {
            source = Decode(bytes);
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            FileFormatException or
            InvalidDataException or
            IOException or
            NotSupportedException)
        {
            throw new ArgumentException(
                "The wallpaper content could not be decoded as a supported JPG or PNG image.",
                nameof(image),
                exception);
        }

        var cropped = CenterCrop(source);
        var resized = RenderToBitmap(cropped, OutputWidth, OutputHeight);
        var blackTint = RenderToBitmap(
            resized,
            OutputWidth,
            OutputHeight,
            drawingContext =>
            {
                var tint = new SolidColorBrush(Color.FromArgb(48, 0, 0, 0));
                tint.Freeze();
                drawingContext.DrawRectangle(
                    tint,
                    pen: null,
                    new Rect(0, 0, OutputWidth, OutputHeight));
            });

        var reduced = RenderToBitmap(cropped, OutputWidth / 8, OutputHeight / 8);
        var blur = RenderToBitmap(reduced, OutputWidth, OutputHeight);

        return new PreparedWallpaperImages(
            EncodePng(blackTint),
            EncodePng(blur));
    }

    private static BitmapSource Decode(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static BitmapSource CenterCrop(BitmapSource source)
    {
        var sourceWidth = source.PixelWidth;
        var sourceHeight = source.PixelHeight;
        const double targetAspectRatio = (double)OutputWidth / OutputHeight;
        var sourceAspectRatio = (double)sourceWidth / sourceHeight;

        int cropWidth;
        int cropHeight;
        if (sourceAspectRatio > targetAspectRatio)
        {
            cropHeight = sourceHeight;
            cropWidth = Math.Min(
                sourceWidth,
                Math.Max(1, (int)Math.Round(sourceHeight * targetAspectRatio)));
        }
        else
        {
            cropWidth = sourceWidth;
            cropHeight = Math.Min(
                sourceHeight,
                Math.Max(1, (int)Math.Round(sourceWidth / targetAspectRatio)));
        }

        var crop = new CroppedBitmap(
            source,
            new Int32Rect(
                (sourceWidth - cropWidth) / 2,
                (sourceHeight - cropHeight) / 2,
                cropWidth,
                cropHeight));
        crop.Freeze();
        return crop;
    }

    private static BitmapSource RenderToBitmap(
        BitmapSource source,
        int width,
        int height,
        Action<DrawingContext>? drawOverlay = null)
    {
        var visual = new DrawingVisual();
        using (var drawingContext = visual.RenderOpen())
        {
            drawingContext.DrawImage(source, new Rect(0, 0, width, height));
            drawOverlay?.Invoke(drawingContext);
        }

        var bitmap = new RenderTargetBitmap(
            width,
            height,
            96,
            96,
            PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static byte[] EncodePng(BitmapSource bitmap)
    {
        using var output = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        encoder.Save(output);
        return output.ToArray();
    }

    private static async Task<byte[]> ReadBoundedAsync(
        Stream image,
        CancellationToken cancellationToken)
    {
        using var output = new MemoryStream();
        var buffer = new byte[81_920];
        while (true)
        {
            var remaining = MaximumSourceBytes - checked((int)output.Length);
            var read = await image.ReadAsync(
                buffer.AsMemory(0, Math.Min(buffer.Length, remaining + 1)),
                cancellationToken);
            if (read == 0)
            {
                return output.ToArray();
            }

            if (read > remaining)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(image),
                    "The wallpaper exceeds the four-mebibyte upload limit.");
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    private static void ValidateWallpaperFile(string fileName, string contentType)
    {
        if (string.IsNullOrWhiteSpace(fileName) ||
            !string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal))
        {
            throw new ArgumentException("The wallpaper file name must be a safe leaf name.", nameof(fileName));
        }

        var extension = Path.GetExtension(fileName);
        if (!WallpaperContentTypes.TryGetValue(extension, out var expectedContentType))
        {
            throw new ArgumentException("The wallpaper extension is not allow-listed.", nameof(fileName));
        }

        if (!string.Equals(contentType, expectedContentType, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The wallpaper content type does not match its allow-listed extension.",
                nameof(contentType));
        }
    }

    private static void ValidateWallpaperSignature(byte[] bytes, string contentType)
    {
        var hasExpectedSignature = contentType.Equals("image/png", StringComparison.OrdinalIgnoreCase)
            ? bytes.AsSpan().StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })
            : bytes.AsSpan().StartsWith(new byte[] { 0xFF, 0xD8, 0xFF });
        if (!hasExpectedSignature)
        {
            throw new ArgumentException(
                "The wallpaper content does not match its allow-listed image type.",
                nameof(bytes));
        }
    }
}

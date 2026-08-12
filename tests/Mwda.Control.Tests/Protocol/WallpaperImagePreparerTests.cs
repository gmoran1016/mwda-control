using System.Windows.Media;
using System.Windows.Media.Imaging;
using Mwda.Control.Protocol;

namespace Mwda.Control.Tests.Protocol;

public sealed class WallpaperImagePreparerTests
{
    [Fact]
    public async Task PreparesBothAdapterImagesAtExactly1920By1080()
    {
        var preparer = new WallpaperImagePreparer();

        var result = await preparer.PrepareAsync(
            new MemoryStream(CreatePng(2, 1)),
            "source.png",
            "image/png");

        Assert.Equal((1920, 1080), ReadPngSize(result.BlackTint));
        Assert.Equal((1920, 1080), ReadPngSize(result.Blur));
    }

    [Fact]
    public async Task AcceptsJpegSourcesThroughTheSamePreparationPath()
    {
        var preparer = new WallpaperImagePreparer();

        var result = await preparer.PrepareAsync(
            new MemoryStream(CreateJpeg(2, 1)),
            "source.jpg",
            "image/jpeg");

        Assert.Equal((1920, 1080), ReadPngSize(result.BlackTint));
        Assert.Equal((1920, 1080), ReadPngSize(result.Blur));
    }

    [Fact]
    public async Task PreparationIsDeterministicForTheSameSourceBytes()
    {
        var source = CreatePng(3, 2);
        var preparer = new WallpaperImagePreparer();

        var first = await preparer.PrepareAsync(
            new MemoryStream(source),
            "source.png",
            "image/png");
        var second = await preparer.PrepareAsync(
            new MemoryStream(source),
            "source.png",
            "image/png");

        Assert.Equal(first.BlackTint, second.BlackTint);
        Assert.Equal(first.Blur, second.Blur);
    }

    [Fact]
    public async Task CenterCropsNonWidescreenSourcesWithoutChangingAdapterDimensions()
    {
        var preparer = new WallpaperImagePreparer();

        var result = await preparer.PrepareAsync(
            new MemoryStream(CreatePng(1, 3)),
            "portrait.png",
            "image/png");

        Assert.Equal((1920, 1080), ReadPngSize(result.BlackTint));
        Assert.Equal((1920, 1080), ReadPngSize(result.Blur));
    }

    private static (int Width, int Height) ReadPngSize(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return (image.PixelWidth, image.PixelHeight);
    }

    private static byte[] CreatePng(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = (y * width + x) * 4;
                pixels[offset] = (byte)(x * 71 + 12);
                pixels[offset + 1] = (byte)(y * 83 + 24);
                pixels[offset + 2] = (byte)(x * 17 + y * 29 + 36);
                pixels[offset + 3] = 255;
            }
        }

        var source = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgra32,
            palette: null,
            pixels,
            width * 4);
        source.Freeze();

        using var output = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        encoder.Save(output);
        return output.ToArray();
    }

    private static byte[] CreateJpeg(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = 32;
            pixels[index + 1] = 96;
            pixels[index + 2] = 160;
            pixels[index + 3] = 255;
        }

        var source = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgra32,
            palette: null,
            pixels,
            width * 4);
        source.Freeze();

        using var output = new MemoryStream();
        var encoder = new JpegBitmapEncoder { QualityLevel = 90 };
        encoder.Frames.Add(BitmapFrame.Create(source));
        encoder.Save(output);
        return output.ToArray();
    }
}

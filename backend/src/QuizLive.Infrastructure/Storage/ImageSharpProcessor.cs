using QuizLive.Application.Abstractions;
using SkiaSharp;

namespace QuizLive.Infrastructure.Storage;

public sealed class SkiaImageProcessor : IImageProcessor
{
    private const int MaxDimension = 1600;
    private const int MaxInputBytes = 2 * 1024 * 1024;

    public async Task<ProcessedImage> ProcessAsync(Stream input, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        await input.CopyToAsync(buffer, ct);

        if (buffer.Length == 0 || buffer.Length > MaxInputBytes)
        {
            throw new InvalidOperationException("Image must be between 1 byte and 2 MB.");
        }

        var bytes = buffer.ToArray();
        using var codec = SKCodec.Create(new MemoryStream(bytes))
            ?? throw new InvalidOperationException("Unsupported or corrupt image.");
        using var bitmap = SKBitmap.Decode(codec)
            ?? throw new InvalidOperationException("Unsupported or corrupt image.");

        SKBitmap target = bitmap;
        SKBitmap? resized = null;

        try
        {
            if (bitmap.Width > MaxDimension || bitmap.Height > MaxDimension)
            {
                var scale = Math.Min((float)MaxDimension / bitmap.Width, (float)MaxDimension / bitmap.Height);
                var newWidth = Math.Max(1, (int)Math.Round(bitmap.Width * scale));
                var newHeight = Math.Max(1, (int)Math.Round(bitmap.Height * scale));

                resized = bitmap.Resize(new SKImageInfo(newWidth, newHeight), SKSamplingOptions.Default)
                    ?? throw new InvalidOperationException("Failed to resize image.");
                target = resized;
            }

            // Re-encoding from raw pixel buffer strips all EXIF/XMP/IPTC metadata
            using var image = SKImage.FromBitmap(target);
            using var encoded = image.Encode(SKEncodedImageFormat.Webp, 85)
                ?? throw new InvalidOperationException("Failed to encode image as WebP.");

            return new ProcessedImage(encoded.ToArray(), "image/webp", "webp");
        }
        finally
        {
            resized?.Dispose();
        }
    }
}

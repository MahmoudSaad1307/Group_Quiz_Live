namespace QuizLive.Application.Abstractions;

public sealed record ProcessedImage(byte[] Data, string ContentType, string Extension);

public interface IImageProcessor
{
    Task<ProcessedImage> ProcessAsync(Stream input, CancellationToken ct = default);
}

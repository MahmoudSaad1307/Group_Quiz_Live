using Microsoft.Extensions.Options;
using QuizLive.Application.Abstractions;

namespace QuizLive.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";
    public string UploadsPath { get; init; } = "data/uploads";
}

public sealed class LocalDiskFileStorage(IOptions<StorageOptions> options) : IFileStorage
{
    private readonly string _root = Path.GetFullPath(options.Value.UploadsPath);

    public async Task<string> SaveAsync(byte[] data, string extension, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_root);
        var cleanExt = extension.TrimStart('.').ToLowerInvariant();
        var fileName = $"{Guid.NewGuid():N}.{cleanExt}";
        var fullPath = Path.Combine(_root, fileName);

        await File.WriteAllBytesAsync(fullPath, data, ct);
        return $"/uploads/{fileName}";
    }

    public Task DeleteAsync(string relativeUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(relativeUrl) || !relativeUrl.StartsWith("/uploads/", StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        var fileName = Path.GetFileName(relativeUrl);
        var fullPath = Path.Combine(_root, fileName);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }
}

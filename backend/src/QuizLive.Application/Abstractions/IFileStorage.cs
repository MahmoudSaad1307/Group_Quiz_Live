namespace QuizLive.Application.Abstractions;

public interface IFileStorage
{
    Task<string> SaveAsync(byte[] data, string extension, CancellationToken ct = default);
    Task DeleteAsync(string relativeUrl, CancellationToken ct = default);
}

namespace QuizLive.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
    long UtcNowMs { get; }
}

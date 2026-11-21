using QuizLive.Application.Game;
using QuizLive.Application.Sessions;

namespace QuizLive.Application.Abstractions;

public interface IGameNotifier
{
    Task SendLobbyUpdatedAsync(Guid sessionId, LobbyUpdatedPayload payload, CancellationToken ct = default);
    Task SendKickedAsync(string connectionId, CancellationToken ct = default);
    Task SendErrorAsync(string connectionId, ErrorPayload payload, CancellationToken ct = default);

    Task SendSessionStartedAsync(Guid sessionId, long serverNowMs, CancellationToken ct = default);
    Task SendQuestionOpenedAsync(Guid sessionId, QuestionOpenedPayload payload, CancellationToken ct = default);
    Task SendHostQuestionViewAsync(Guid sessionId, HostQuestionViewPayload payload, CancellationToken ct = default);
    Task SendAnswerProgressAsync(Guid sessionId, AnswerProgressPayload payload, CancellationToken ct = default);
    Task SendQuestionClosedAsync(Guid sessionId, QuestionClosedPayload payload, CancellationToken ct = default);
    Task SendYourResultAsync(string connectionId, YourResultPayload payload, CancellationToken ct = default);
    Task SendLeaderboardUpdatedAsync(Guid sessionId, LeaderboardUpdatedPayload payload, CancellationToken ct = default);
    Task SendYourRankAsync(string connectionId, YourRankPayload payload, CancellationToken ct = default);
    Task SendSessionPausedAsync(Guid sessionId, CancellationToken ct = default);
    Task SendSessionResumedAsync(Guid sessionId, int remainingMs, long serverNowMs, CancellationToken ct = default);
    Task SendSessionFinishedAsync(Guid sessionId, SessionFinishedPayload payload, CancellationToken ct = default);
    Task SendFinalResultsAsync(string connectionId, FinalResultsPayload payload, CancellationToken ct = default);
}

using QuizLive.Domain.Entities;
using QuizLive.Domain.Enums;

namespace QuizLive.Application.Abstractions;

public interface ISessionRepository
{
    Task AddAsync(GameSession session, CancellationToken ct = default);
    Task<GameSession?> GetByIdWithSnapshotAsync(Guid sessionId, CancellationToken ct = default);
    Task<GameSession?> GetActiveByPinAsync(string pin, CancellationToken ct = default);
    Task<bool> IsPinActiveAsync(string pin, CancellationToken ct = default);
    Task AddPlayerAsync(Player player, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<GameSession>> GetRestorableSessionsAsync(DateTimeOffset now, CancellationToken ct = default);

    Task PersistSessionStateAsync(
        Guid sessionId,
        SessionStatus status,
        int currentQuestionIndex,
        DateTimeOffset now,
        SessionStatus? pausedFromStatus = null,
        int? pausedRemainingMs = null,
        CancellationToken ct = default);

    Task PersistQuestionClosedAsync(
        Guid sessionId,
        SessionStatus status,
        int currentQuestionIndex,
        DateTimeOffset now,
        IReadOnlyList<Answer> answers,
        IReadOnlyDictionary<Guid, (int TotalScore, int Streak, long CumulativeRtMs)> playerUpdates,
        CancellationToken ct = default);
}

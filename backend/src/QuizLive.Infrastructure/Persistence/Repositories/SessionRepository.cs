using Microsoft.EntityFrameworkCore;
using QuizLive.Application.Abstractions;
using QuizLive.Domain.Entities;
using QuizLive.Domain.Enums;

namespace QuizLive.Infrastructure.Persistence.Repositories;

public sealed class SessionRepository(QuizLiveDbContext db) : ISessionRepository
{
    public async Task AddAsync(GameSession session, CancellationToken ct = default)
    {
        db.GameSessions.Add(session);
        await db.SaveChangesAsync(ct);
    }

    public Task<GameSession?> GetByIdWithSnapshotAsync(Guid sessionId, CancellationToken ct = default) =>
        db.GameSessions
            .Include(s => s.Questions)
            .ThenInclude(q => q.Options)
            .Include(s => s.Players)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

    public Task<GameSession?> GetActiveByPinAsync(string pin, CancellationToken ct = default) =>
        db.GameSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s =>
                s.Pin == pin &&
                s.Status != SessionStatus.Finished &&
                s.Status != SessionStatus.Abandoned, ct);

    public Task<bool> IsPinActiveAsync(string pin, CancellationToken ct = default) =>
        db.GameSessions.AnyAsync(s =>
            s.Pin == pin &&
            s.Status != SessionStatus.Finished &&
            s.Status != SessionStatus.Abandoned, ct);

    public async Task AddPlayerAsync(Player player, CancellationToken ct = default)
    {
        db.Players.Add(player);
        await db.SaveChangesAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        db.SaveChangesAsync(ct);

    public async Task<IReadOnlyList<GameSession>> GetRestorableSessionsAsync(DateTimeOffset now, CancellationToken ct = default) =>
        await db.GameSessions
            .Include(s => s.Questions)
            .ThenInclude(q => q.Options)
            .Include(s => s.Players)
            .Where(s => s.Status != SessionStatus.Finished && s.Status != SessionStatus.Abandoned && s.ExpiresAt > now)
            .ToListAsync(ct);

    public async Task PersistSessionStateAsync(
        Guid sessionId,
        SessionStatus status,
        int currentQuestionIndex,
        DateTimeOffset now,
        SessionStatus? pausedFromStatus = null,
        int? pausedRemainingMs = null,
        CancellationToken ct = default)
    {
        var session = await db.GameSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null) return;

        session.SetState(status, currentQuestionIndex, now, pausedFromStatus, pausedRemainingMs);
        await db.SaveChangesAsync(ct);
    }

    public async Task PersistQuestionClosedAsync(
        Guid sessionId,
        SessionStatus status,
        int currentQuestionIndex,
        DateTimeOffset now,
        IReadOnlyList<Answer> answers,
        IReadOnlyDictionary<Guid, (int TotalScore, int Streak, long CumulativeRtMs)> playerUpdates,
        CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var session = await db.GameSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        session?.SetState(status, currentQuestionIndex, now);

        if (answers.Count > 0)
        {
            db.Answers.AddRange(answers);
        }

        var playerIds = playerUpdates.Keys.ToList();
        if (playerIds.Count > 0)
        {
            var dbPlayers = await db.Players
                .Where(p => p.SessionId == sessionId && playerIds.Contains(p.Id))
                .ToListAsync(ct);

            foreach (var p in dbPlayers)
            {
                if (playerUpdates.TryGetValue(p.Id, out var upd))
                {
                    var deltaScore = upd.TotalScore - p.TotalScore;
                    var deltaRt = upd.CumulativeRtMs - p.CumulativeResponseTimeMs;
                    p.ApplyAnswerResult(deltaScore, upd.Streak, deltaRt);
                }
            }
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}

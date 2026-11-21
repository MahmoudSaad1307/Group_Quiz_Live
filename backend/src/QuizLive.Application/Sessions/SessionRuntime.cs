using QuizLive.Application.Abstractions;
using QuizLive.Domain.Entities;
using QuizLive.Domain.Enums;
using QuizLive.Domain.ValueObjects;

namespace QuizLive.Application.Sessions;

public sealed record InMemoryAnswerRecord(
    Guid PlayerId,
    Guid QuestionId,
    Guid SelectedOptionId,
    bool IsCorrect,
    int ResponseTimeMs,
    DateTimeOffset SubmittedAt);

public sealed class PlayerRuntime
{
    public PlayerRuntime(Player entity)
    {
        Id = entity.Id;
        Nickname = entity.Nickname;
        NicknameNormalized = entity.NicknameNormalized;
        TokenHash = entity.TokenHash;
        TotalScore = entity.TotalScore;
        CurrentStreak = entity.CurrentStreak;
        CumulativeResponseTimeMs = entity.CumulativeResponseTimeMs;
        JoinedAt = entity.JoinedAt;
        JoinedAtQuestionIndex = entity.JoinedAtQuestionIndex;
        IsKicked = entity.IsKicked;
        LastSeenAt = entity.LastSeenAt;
    }

    public Guid Id { get; }
    public string Nickname { get; }
    public string NicknameNormalized { get; }
    public string TokenHash { get; }
    public int TotalScore { get; set; }
    public int CurrentStreak { get; set; }
    public long CumulativeResponseTimeMs { get; set; }
    public DateTimeOffset JoinedAt { get; }
    public int JoinedAtQuestionIndex { get; }
    public bool IsKicked { get; set; }
    public string? ConnectionId { get; set; }
    public bool IsConnected => ConnectionId is not null;
    public DateTimeOffset? DisconnectedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }

    public int PreviousRank { get; set; } = 1;
    public int LastDelta { get; set; }
    public int CorrectCount { get; set; }
}

public sealed class SessionRuntime : IDisposable
{
    public SessionRuntime(GameSession session, IClock clock)
    {
        SessionId = session.Id;
        HostId = session.HostId;
        Pin = session.Pin;
        Language = session.Language;
        Settings = session.Settings;
        Status = session.Status;
        PausedFromStatus = session.PausedFromStatus;
        PausedRemainingMs = session.PausedRemainingMs;
        CurrentQuestionIndex = session.CurrentQuestionIndex;
        ExpiresAt = session.ExpiresAt;
        Questions = session.Questions.OrderBy(q => q.OrderIndex).ToList();
        Clock = clock;

        foreach (var p in session.Players)
        {
            Players[p.Id] = new PlayerRuntime(p);
        }
    }

    public SemaphoreSlim Gate { get; } = new(1, 1);
    public Guid SessionId { get; }
    public Guid HostId { get; }
    public string Pin { get; }
    public QuizLanguage Language { get; }
    public SessionSettings Settings { get; }
    public SessionStatus Status { get; set; }
    public SessionStatus? PausedFromStatus { get; set; }
    public int? PausedRemainingMs { get; set; }
    public int CurrentQuestionIndex { get; set; }
    public DateTimeOffset ExpiresAt { get; }
    public IReadOnlyList<SessionQuestion> Questions { get; }
    public Dictionary<Guid, PlayerRuntime> Players { get; } = [];
    public Dictionary<Guid, InMemoryAnswerRecord> CurrentAnswers { get; } = [];
    public long StartedAtServerMs { get; set; }
    public long DeadlineServerMs { get; set; }
    public long LastProgressBroadcastMs { get; set; }
    public CancellationTokenSource? TimerCts { get; set; }
    public CancellationTokenSource? HostAwayCts { get; set; }
    public int HostConnectionCount { get; set; }
    public QuizLive.Application.Game.QuestionClosedPayload? LastClosedPayload { get; set; }
    public QuizLive.Application.Game.LeaderboardUpdatedPayload? LastLeaderboardPayload { get; set; }
    public IReadOnlyList<QuizLive.Application.Game.PodiumEntryDto>? LastPodium { get; set; }
    public IClock Clock { get; }

    public SessionQuestion CurrentQuestion => Questions[CurrentQuestionIndex];

    public int CountActiveEligiblePlayers(DateTimeOffset now)
    {
        return Players.Values.Count(p =>
            !p.IsKicked &&
            (p.IsConnected || (p.DisconnectedAt.HasValue && (now - p.DisconnectedAt.Value).TotalSeconds < 5)));
    }

    public void CancelTimer()
    {
        if (TimerCts is null) return;
        TimerCts.Cancel();
        TimerCts.Dispose();
        TimerCts = null;
    }

    public LobbyUpdatedPayload BuildLobbyPayload()
    {
        var active = Players.Values
            .Where(p => !p.IsKicked)
            .OrderBy(p => p.JoinedAt)
            .Select(p => new LobbyPlayerDto(p.Id, p.Nickname))
            .ToList();

        return new LobbyUpdatedPayload(active.Count, active);
    }

    public void Dispose()
    {
        CancelTimer();
        HostAwayCts?.Cancel();
        HostAwayCts?.Dispose();
        HostAwayCts = null;
        Gate.Dispose();
    }
}

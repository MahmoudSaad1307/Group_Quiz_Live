namespace QuizLive.Domain.Entities;

public sealed class Player
{
    private Player() { }

    public Player(
        Guid id,
        Guid sessionId,
        string nickname,
        string nicknameNormalized,
        string tokenHash,
        DateTimeOffset joinedAt,
        int joinedAtQuestionIndex)
    {
        Id = id;
        SessionId = sessionId;
        Nickname = nickname;
        NicknameNormalized = nicknameNormalized;
        TokenHash = tokenHash;
        JoinedAt = joinedAt.ToUniversalTime();
        LastSeenAt = JoinedAt;
        JoinedAtQuestionIndex = joinedAtQuestionIndex;
    }

    public Guid Id { get; private set; }
    public Guid SessionId { get; private set; }
    public string Nickname { get; private set; } = string.Empty;
    public string NicknameNormalized { get; private set; } = string.Empty;
    public string TokenHash { get; private set; } = string.Empty;
    public int TotalScore { get; private set; }
    public int CurrentStreak { get; private set; }
    public long CumulativeResponseTimeMs { get; private set; }
    public DateTimeOffset JoinedAt { get; private set; }
    public int JoinedAtQuestionIndex { get; private set; }
    public bool IsKicked { get; private set; }
    public DateTimeOffset LastSeenAt { get; private set; }

    public void Kick() => IsKicked = true;
    public void Touch(DateTimeOffset now) => LastSeenAt = now.ToUniversalTime();

    public void ApplyAnswerResult(int pointsAwarded, int newStreak, long responseTimeMs)
    {
        TotalScore += pointsAwarded;
        CurrentStreak = newStreak;
        CumulativeResponseTimeMs += responseTimeMs;
    }
}

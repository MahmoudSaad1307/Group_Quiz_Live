namespace QuizLive.Domain.Entities;

public sealed class Answer
{
    private Answer() { }

    public Answer(
        Guid id,
        Guid sessionId,
        Guid playerId,
        Guid questionId,
        Guid? selectedOptionId,
        bool isCorrect,
        int responseTimeMs,
        int pointsAwarded,
        int streakAtAnswer,
        DateTimeOffset submittedAt)
    {
        Id = id;
        SessionId = sessionId;
        PlayerId = playerId;
        QuestionId = questionId;
        SelectedOptionId = selectedOptionId;
        IsCorrect = isCorrect;
        ResponseTimeMs = responseTimeMs;
        PointsAwarded = pointsAwarded;
        StreakAtAnswer = streakAtAnswer;
        SubmittedAt = submittedAt.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public Guid SessionId { get; private set; }
    public Guid PlayerId { get; private set; }
    public Guid QuestionId { get; private set; }
    public Guid? SelectedOptionId { get; private set; }
    public bool IsCorrect { get; private set; }
    public int ResponseTimeMs { get; private set; }
    public int PointsAwarded { get; private set; }
    public int StreakAtAnswer { get; private set; }
    public DateTimeOffset SubmittedAt { get; private set; }
}

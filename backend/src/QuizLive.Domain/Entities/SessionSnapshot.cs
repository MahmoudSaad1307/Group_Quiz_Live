using QuizLive.Domain.Enums;

namespace QuizLive.Domain.Entities;

public sealed class SessionOption
{
    private SessionOption() { }

    public SessionOption(Guid id, Guid sessionQuestionId, int orderIndex, string text, bool isCorrect)
    {
        Id = id;
        SessionQuestionId = sessionQuestionId;
        OrderIndex = orderIndex;
        Text = text;
        IsCorrect = isCorrect;
    }

    public Guid Id { get; private set; }
    public Guid SessionQuestionId { get; private set; }
    public int OrderIndex { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public bool IsCorrect { get; private set; }
}

public sealed class SessionQuestion
{
    private readonly List<SessionOption> _options = [];

    private SessionQuestion() { }

    public SessionQuestion(
        Guid id,
        Guid sessionId,
        Guid sourceQuestionId,
        int orderIndex,
        QuestionType type,
        string text,
        string? imageUrl,
        string? explanation,
        int timeLimitSeconds,
        int points)
    {
        Id = id;
        SessionId = sessionId;
        SourceQuestionId = sourceQuestionId;
        OrderIndex = orderIndex;
        Type = type;
        Text = text;
        ImageUrl = imageUrl;
        Explanation = explanation;
        TimeLimitSeconds = timeLimitSeconds;
        Points = points;
    }

    public Guid Id { get; private set; }
    public Guid SessionId { get; private set; }
    public Guid SourceQuestionId { get; private set; }
    public int OrderIndex { get; private set; }
    public QuestionType Type { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public string? ImageUrl { get; private set; }
    public string? Explanation { get; private set; }
    public int TimeLimitSeconds { get; private set; }
    public int Points { get; private set; }
    public IReadOnlyCollection<SessionOption> Options => _options;

    public void AddOption(SessionOption option) => _options.Add(option);
}

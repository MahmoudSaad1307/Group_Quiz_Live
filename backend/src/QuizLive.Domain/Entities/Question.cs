using QuizLive.Domain.Enums;

namespace QuizLive.Domain.Entities;

public sealed class Question
{
    private static readonly int[] AllowedPoints = [0, 500, 1000, 2000];
    private readonly List<Option> _options = [];

    private Question() { }

    public Question(
        Guid id,
        Guid quizId,
        int orderIndex,
        QuestionType type,
        string text,
        string? imageUrl,
        string? explanation,
        int timeLimitSeconds,
        int points)
    {
        if (id == Guid.Empty) throw new ArgumentException("Question id cannot be empty.", nameof(id));
        ValidateFields(type, text, timeLimitSeconds, points);

        Id = id;
        QuizId = quizId;
        OrderIndex = orderIndex;
        Type = type;
        Text = text.Trim();
        ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();
        Explanation = string.IsNullOrWhiteSpace(explanation) ? null : explanation.Trim();
        TimeLimitSeconds = timeLimitSeconds;
        Points = points;
    }

    public Guid Id { get; private set; }
    public Guid QuizId { get; private set; }
    public int OrderIndex { get; private set; }
    public QuestionType Type { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public string? ImageUrl { get; private set; }
    public string? Explanation { get; private set; }
    public int TimeLimitSeconds { get; private set; }
    public int Points { get; private set; }
    public IReadOnlyCollection<Option> Options => _options;

    public void Update(
        QuestionType type,
        string text,
        string? imageUrl,
        string? explanation,
        int timeLimitSeconds,
        int points)
    {
        ValidateFields(type, text, timeLimitSeconds, points);

        Type = type;
        Text = text.Trim();
        ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();
        Explanation = string.IsNullOrWhiteSpace(explanation) ? null : explanation.Trim();
        TimeLimitSeconds = timeLimitSeconds;
        Points = points;
    }

    public void SetOrderIndex(int orderIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(orderIndex);
        OrderIndex = orderIndex;
    }

    public void SetOptions(IEnumerable<(string Text, bool IsCorrect)> options)
    {
        var list = options.ToList();
        ValidateOptions(Type, list);

        _options.Clear();
        for (var i = 0; i < list.Count; i++)
        {
            _options.Add(new Option(Guid.NewGuid(), Id, i, list[i].Text, list[i].IsCorrect));
        }
    }

    private static void ValidateFields(QuestionType type, string text, int timeLimitSeconds, int points)
    {
        if (type == QuestionType.MultiChoice)
        {
            throw new ArgumentException("MultiChoice is not yet supported.", nameof(type));
        }

        if (string.IsNullOrWhiteSpace(text) || text.Trim().Length > 500)
        {
            throw new ArgumentException("Question text must be between 1 and 500 characters.", nameof(text));
        }

        if (timeLimitSeconds is < 5 or > 120)
        {
            throw new ArgumentOutOfRangeException(nameof(timeLimitSeconds), "Time limit must be between 5 and 120 seconds.");
        }

        if (!AllowedPoints.Contains(points))
        {
            throw new ArgumentException("Points must be 0, 500, 1000, or 2000.", nameof(points));
        }
    }

    private static void ValidateOptions(QuestionType type, List<(string Text, bool IsCorrect)> list)
    {
        if (type == QuestionType.TrueFalse)
        {
            if (list.Count != 2)
            {
                throw new ArgumentException("TrueFalse questions must have exactly 2 options.");
            }
        }
        else if (list.Count is < 2 or > 4)
        {
            throw new ArgumentException("SingleChoice questions must have between 2 and 4 options.");
        }

        var correctCount = list.Count(o => o.IsCorrect);
        if (correctCount != 1)
        {
            throw new ArgumentException("Question must have exactly 1 correct option.");
        }
    }
}

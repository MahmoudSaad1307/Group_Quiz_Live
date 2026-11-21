using QuizLive.Domain.Enums;

namespace QuizLive.Domain.Entities;

public sealed class Quiz
{
    private readonly List<Question> _questions = [];

    private Quiz() { }

    public Quiz(
        Guid id,
        Guid ownerId,
        string title,
        string? description,
        QuizLanguage language,
        DateTimeOffset now)
    {
        if (id == Guid.Empty) throw new ArgumentException("Quiz id cannot be empty.", nameof(id));
        if (ownerId == Guid.Empty) throw new ArgumentException("Owner id cannot be empty.", nameof(ownerId));
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 120)
        {
            throw new ArgumentException("Title must be between 1 and 120 characters.", nameof(title));
        }

        Id = id;
        OwnerId = ownerId;
        Title = title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Language = language;
        IsArchived = false;
        CreatedAt = now.ToUniversalTime();
        UpdatedAt = now.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public QuizLanguage Language { get; private set; }
    public bool IsArchived { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyCollection<Question> Questions => _questions;

    public void UpdateMetadata(string title, string? description, QuizLanguage language, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 120)
        {
            throw new ArgumentException("Title must be between 1 and 120 characters.", nameof(title));
        }

        Title = title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Language = language;
        UpdatedAt = now.ToUniversalTime();
    }

    public void SetArchived(bool archived, DateTimeOffset now)
    {
        IsArchived = archived;
        UpdatedAt = now.ToUniversalTime();
    }

    public Question AddQuestion(
        QuestionType type,
        string text,
        string? imageUrl,
        string? explanation,
        int timeLimitSeconds,
        int points,
        IEnumerable<(string Text, bool IsCorrect)> options,
        DateTimeOffset now)
    {
        var nextIndex = _questions.Count == 0 ? 0 : _questions.Max(q => q.OrderIndex) + 1;
        var question = new Question(
            Guid.NewGuid(),
            Id,
            nextIndex,
            type,
            text,
            imageUrl,
            explanation,
            timeLimitSeconds,
            points);
        question.SetOptions(options);
        _questions.Add(question);
        UpdatedAt = now.ToUniversalTime();
        return question;
    }

    public void RemoveQuestion(Guid questionId, DateTimeOffset now)
    {
        var target = _questions.FirstOrDefault(q => q.Id == questionId)
            ?? throw new KeyNotFoundException("Question not found.");

        _questions.Remove(target);
        var ordered = _questions.OrderBy(q => q.OrderIndex).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].SetOrderIndex(i);
        }

        UpdatedAt = now.ToUniversalTime();
    }

    public void ReorderQuestions(IReadOnlyList<Guid> orderedIds, DateTimeOffset now)
    {
        if (orderedIds.Count != _questions.Count)
        {
            throw new ArgumentException("Ordered IDs must match all questions in the quiz.");
        }

        var map = _questions.ToDictionary(q => q.Id);
        if (orderedIds.Distinct().Count() != orderedIds.Count || orderedIds.Any(id => !map.ContainsKey(id)))
        {
            throw new ArgumentException("Ordered IDs must be an exact permutation of the quiz questions.");
        }

        for (var i = 0; i < orderedIds.Count; i++)
        {
            map[orderedIds[i]].SetOrderIndex(i);
        }

        UpdatedAt = now.ToUniversalTime();
    }

    public Quiz Duplicate(Guid newOwnerId, DateTimeOffset now)
    {
        var copyTitle = Title.Length > 110 ? Title[..110] + " (Copy)" : $"{Title} (Copy)";
        var copy = new Quiz(Guid.NewGuid(), newOwnerId, copyTitle, Description, Language, now);

        foreach (var q in _questions.OrderBy(q => q.OrderIndex))
        {
            copy.AddQuestion(
                q.Type,
                q.Text,
                q.ImageUrl,
                q.Explanation,
                q.TimeLimitSeconds,
                q.Points,
                q.Options.OrderBy(o => o.OrderIndex).Select(o => (o.Text, o.IsCorrect)),
                now);
        }

        return copy;
    }
}

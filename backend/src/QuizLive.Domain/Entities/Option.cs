using System.Diagnostics.CodeAnalysis;

namespace QuizLive.Domain.Entities;

[SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Domain entity name per specification.")]
public sealed class Option
{
    private Option() { }

    public Option(Guid id, Guid questionId, int orderIndex, string text, bool isCorrect)
    {
        if (id == Guid.Empty) throw new ArgumentException("Option id cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Option text is required.", nameof(text));
        ArgumentOutOfRangeException.ThrowIfNegative(orderIndex);

        Id = id;
        QuestionId = questionId;
        OrderIndex = orderIndex;
        Text = text.Trim();
        IsCorrect = isCorrect;
    }

    public Guid Id { get; private set; }
    public Guid QuestionId { get; private set; }
    public int OrderIndex { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public bool IsCorrect { get; private set; }

    public void Update(int orderIndex, string text, bool isCorrect)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Option text is required.", nameof(text));
        OrderIndex = orderIndex;
        Text = text.Trim();
        IsCorrect = isCorrect;
    }
}

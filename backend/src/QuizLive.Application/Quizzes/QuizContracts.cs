using QuizLive.Domain.Enums;

namespace QuizLive.Application.Quizzes;

public sealed record OptionDto(Guid Id, int OrderIndex, string Text, bool IsCorrect);

public sealed record QuestionDto(
    Guid Id,
    int OrderIndex,
    QuestionType Type,
    string Text,
    string? ImageUrl,
    string? Explanation,
    int TimeLimitSeconds,
    int Points,
    IReadOnlyList<OptionDto> Options);

public sealed record QuizSummaryDto(
    Guid Id,
    string Title,
    string? Description,
    QuizLanguage Language,
    bool IsArchived,
    int QuestionCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record QuizDetailDto(
    Guid Id,
    string Title,
    string? Description,
    QuizLanguage Language,
    bool IsArchived,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<QuestionDto> Questions);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

public sealed record CreateQuizRequest(string Title, string? Description, QuizLanguage Language = QuizLanguage.En);

public sealed record UpdateQuizRequest(string Title, string? Description, QuizLanguage Language, bool? IsArchived = null);

public sealed record UpsertOptionRequest(string Text, bool IsCorrect);

public sealed record UpsertQuestionRequest(
    QuestionType Type,
    string Text,
    string? ImageUrl,
    string? Explanation,
    int TimeLimitSeconds,
    int Points,
    IReadOnlyList<UpsertOptionRequest> Options);

public sealed record ReorderQuestionsRequest(IReadOnlyList<Guid> OrderedIds);

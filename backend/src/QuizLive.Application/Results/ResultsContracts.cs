using QuizLive.Domain.Enums;

namespace QuizLive.Application.Results;

public sealed record OptionStatDto(Guid OptionId, string Text, bool IsCorrect, int Count);

public sealed record QuestionStatDto(
    Guid QuestionId,
    int OrderIndex,
    string Text,
    double CorrectPercent,
    double AvgResponseTimeMs,
    IReadOnlyList<OptionStatDto> Options);

public sealed record StudentResultRowDto(
    Guid PlayerId,
    string Nickname,
    int Rank,
    int TotalScore,
    int CorrectCount,
    double AvgResponseTimeMs);

public sealed record SessionResultsDto(
    Guid SessionId,
    string Pin,
    QuizLanguage Language,
    SessionStatus Status,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    IReadOnlyList<StudentResultRowDto> Students,
    IReadOnlyList<QuestionStatDto> Questions);

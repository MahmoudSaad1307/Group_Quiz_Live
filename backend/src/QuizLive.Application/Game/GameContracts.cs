namespace QuizLive.Application.Game;

public sealed record SanitizedOptionDto(Guid Id, string Text);

public sealed record QuestionOpenedPayload(
    int QuestionIndex,
    int Total,
    Guid QuestionId,
    string Text,
    string? ImageUrl,
    IReadOnlyList<SanitizedOptionDto> Options,
    int DurationMs,
    long StartedAtServerMs,
    long ServerNowMs,
    bool ShowQuestionOnPhones);

public sealed record HostQuestionViewPayload(
    int QuestionIndex,
    int Total,
    Guid QuestionId,
    string Text,
    string? ImageUrl,
    IReadOnlyList<SanitizedOptionDto> Options,
    Guid CorrectOptionId,
    string? Explanation,
    int DurationMs,
    long StartedAtServerMs,
    long ServerNowMs);

public sealed record AnswerProgressPayload(int Answered, int TotalActive);

public sealed record QuestionClosedPayload(
    Guid CorrectOptionId,
    IReadOnlyDictionary<Guid, int> Distribution,
    string? Explanation);

public sealed record YourResultPayload(
    bool IsCorrect,
    int PointsAwarded,
    int TotalScore,
    int Streak,
    int Rank);

public sealed record LeaderboardEntryDto(
    string Nickname,
    int Score,
    int Delta,
    int Rank,
    int PreviousRank);

public sealed record LeaderboardUpdatedPayload(
    IReadOnlyList<LeaderboardEntryDto> Top5,
    int TotalPlayers);

public sealed record YourRankPayload(
    int Rank,
    int? GapToNext,
    string? NextNickname);

public sealed record PodiumEntryDto(int Rank, string Nickname, int Score);

public sealed record SessionFinishedPayload(IReadOnlyList<PodiumEntryDto> Podium);

public sealed record FinalResultsPayload(
    int Rank,
    int Score,
    int CorrectCount,
    int TotalQuestions);

public sealed record SubmitAnswerResult(
    bool Accepted,
    bool AlreadyAnswered,
    string? ErrorCode,
    string? ErrorMessage);

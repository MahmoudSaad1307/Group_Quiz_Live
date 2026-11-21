using QuizLive.Application.Sessions;
using QuizLive.Domain.Enums;

namespace QuizLive.Application.Game;

public sealed record HostRehydrateDto(
    SessionStatus Phase,
    int CurrentQuestionIndex,
    int TotalQuestions,
    LobbyUpdatedPayload Lobby,
    HostQuestionViewPayload? CurrentQuestion,
    AnswerProgressPayload? Progress,
    QuestionClosedPayload? Closed,
    LeaderboardUpdatedPayload? Leaderboard,
    IReadOnlyList<PodiumEntryDto>? Podium,
    int? PausedRemainingMs,
    long ServerNowMs);

public sealed record PlayerRehydrateDto(
    SessionStatus Phase,
    int CurrentQuestionIndex,
    int TotalQuestions,
    QuestionOpenedPayload? CurrentQuestion,
    bool AlreadyAnswered,
    YourResultPayload? YourResult,
    YourRankPayload? YourRank,
    FinalResultsPayload? FinalResults,
    int? PausedRemainingMs,
    long ServerNowMs);

public sealed record ScreenRehydrateDto(
    SessionStatus Phase,
    int CurrentQuestionIndex,
    int TotalQuestions,
    LobbyUpdatedPayload Lobby,
    QuestionOpenedPayload? CurrentQuestion,
    AnswerProgressPayload? Progress,
    QuestionClosedPayload? Closed,
    LeaderboardUpdatedPayload? Leaderboard,
    IReadOnlyList<PodiumEntryDto>? Podium,
    int? PausedRemainingMs,
    long ServerNowMs);

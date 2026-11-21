using QuizLive.Domain.Enums;
using QuizLive.Domain.ValueObjects;

namespace QuizLive.Application.Sessions;

public static class ErrorCodes
{
    public const string SessionNotFound = "SessionNotFound";
    public const string SessionStarted = "SessionStarted";
    public const string SessionEnded = "SessionEnded";
    public const string NicknameInvalid = "NicknameInvalid";
    public const string NicknameTaken = "NicknameTaken";
    public const string NicknameProfane = "NicknameProfane";
    public const string Kicked = "Kicked";
    public const string Forbidden = "Forbidden";
    public const string InvalidState = "InvalidState";
    public const string NoPlayers = "NoPlayers";
    public const string RateLimited = "RateLimited";
    public const string SessionTakenOver = "SessionTakenOver";
}

public sealed record ErrorPayload(string Code, string Message);

public sealed record CreateSessionRequest(Guid QuizId, SessionSettings? Settings);

public sealed record CreateSessionResponse(Guid SessionId, string Pin, string JoinUrl);

public sealed record SessionDetailDto(
    Guid Id,
    Guid QuizId,
    string Pin,
    QuizLanguage Language,
    SessionStatus Status,
    int CurrentQuestionIndex,
    int TotalQuestions,
    SessionSettings Settings,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);

public sealed record JoinCheckResponse(string QuizTitle, QuizLanguage Language, bool Joinable);

public sealed record ScreenTokenResponse(string Token, DateTimeOffset ExpiresAt);

public sealed record LobbyPlayerDto(Guid Id, string Nickname);

public sealed record LobbyUpdatedPayload(int PlayerCount, IReadOnlyList<LobbyPlayerDto> Players);

public sealed record JoinSessionResult(
    bool Succeeded,
    Guid SessionId,
    Guid PlayerId,
    string Nickname,
    string? Token,
    SessionStatus Phase,
    QuizLanguage Language,
    string? ErrorCode,
    string? ErrorMessage);

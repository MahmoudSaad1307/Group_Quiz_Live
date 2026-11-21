using QuizLive.Application.Abstractions;
using QuizLive.Application.Players;
using QuizLive.Domain.Entities;
using QuizLive.Domain.Enums;
using QuizLive.Domain.ValueObjects;

namespace QuizLive.Application.Sessions;

public sealed class SessionService(
    IQuizRepository quizzes,
    ISessionRepository sessions,
    SessionManager sessionManager,
    IPinGenerator pinGenerator,
    IPlayerTokenService tokenService,
    IScreenTokenService screenTokens,
    NicknameNormalizer nicknameNormalizer,
    IGameNotifier notifier,
    IClock clock)
{
    public async Task<CreateSessionResponse?> CreateSessionAsync(
        Guid hostId,
        CreateSessionRequest request,
        string appBaseUrl,
        CancellationToken ct = default)
    {
        var quiz = await quizzes.GetOwnedAsync(request.QuizId, hostId, ct);
        if (quiz is null || quiz.IsArchived || quiz.Questions.Count == 0)
        {
            return null;
        }

        var pin = await GenerateUniquePinAsync(ct);
        var now = clock.UtcNow;
        var session = new GameSession(
            Guid.NewGuid(),
            quiz.Id,
            hostId,
            pin,
            quiz.Language,
            request.Settings ?? new SessionSettings(),
            now,
            now.AddHours(6));

        session.SnapshotQuiz(quiz, Random.Shared);
        await sessions.AddAsync(session, ct);
        sessionManager.Register(session);

        var joinUrl = $"{appBaseUrl.TrimEnd('/')}/join?pin={pin}";
        return new CreateSessionResponse(session.Id, pin, joinUrl);
    }

    public async Task<SessionDetailDto?> GetSessionAsync(Guid sessionId, Guid hostId, CancellationToken ct = default)
    {
        var session = await sessions.GetByIdWithSnapshotAsync(sessionId, ct);
        if (session is null || session.HostId != hostId)
        {
            return null;
        }

        return new SessionDetailDto(
            session.Id,
            session.QuizId,
            session.Pin,
            session.Language,
            session.Status,
            session.CurrentQuestionIndex,
            session.Questions.Count,
            session.Settings,
            session.CreatedAt,
            session.ExpiresAt);
    }

    public async Task<ScreenTokenResponse?> IssueScreenTokenAsync(Guid sessionId, Guid hostId, CancellationToken ct = default)
    {
        var session = await sessions.GetByIdWithSnapshotAsync(sessionId, ct);
        if (session is null || session.HostId != hostId)
        {
            return null;
        }

        return new ScreenTokenResponse(screenTokens.CreateScreenToken(sessionId), clock.UtcNow.AddHours(2));
    }

    public async Task<JoinCheckResponse?> CheckPinAsync(string pin, CancellationToken ct = default)
    {
        var session = await sessions.GetActiveByPinAsync(pin, ct);
        if (session is null)
        {
            return null;
        }

        var quiz = await quizzes.GetOwnedAsync(session.QuizId, session.HostId, ct);
        var joinable = session.Status == SessionStatus.Lobby ||
            (session.Settings.AllowLateJoin && session.Status is not (SessionStatus.Finished or SessionStatus.Abandoned));

        return new JoinCheckResponse(quiz?.Title ?? "QuizLive", session.Language, joinable);
    }

    public async Task<JoinSessionResult> JoinSessionAsync(
        string pin,
        string nickname,
        string? existingToken,
        string connectionId,
        CancellationToken ct = default)
    {
        if (!sessionManager.TryGetByPin(pin, out var runtime))
        {
            return new(false, Guid.Empty, Guid.Empty, string.Empty, null, SessionStatus.Lobby, QuizLanguage.En,
                ErrorCodes.SessionNotFound, "Session not found or already ended.");
        }

        LobbyUpdatedPayload? lobbyPayload = null;
        string? previousConnectionToClose = null;
        JoinSessionResult result;

        await runtime.Gate.WaitAsync(ct);
        try
        {
            if (runtime.Status is SessionStatus.Finished or SessionStatus.Abandoned || clock.UtcNow > runtime.ExpiresAt)
            {
                return new(false, runtime.SessionId, Guid.Empty, string.Empty, null, runtime.Status, runtime.Language,
                    ErrorCodes.SessionEnded, "This session has already ended.");
            }

            // Rejoin via existing token
            if (!string.IsNullOrWhiteSpace(existingToken))
            {
                var incomingHash = tokenService.HashToken(existingToken);
                var existing = runtime.Players.Values.FirstOrDefault(p =>
                    tokenService.FixedTimeEquals(p.TokenHash, incomingHash));

                if (existing is not null)
                {
                    if (existing.IsKicked)
                    {
                        return new(false, runtime.SessionId, existing.Id, existing.Nickname, null, runtime.Status, runtime.Language,
                            ErrorCodes.Kicked, "You have been removed from this session.");
                    }

                    if (existing.ConnectionId is not null && existing.ConnectionId != connectionId)
                    {
                        previousConnectionToClose = existing.ConnectionId;
                    }

                    existing.ConnectionId = connectionId;
                    existing.DisconnectedAt = null;
                    existing.LastSeenAt = clock.UtcNow;

                    return new(true, runtime.SessionId, existing.Id, existing.Nickname, existingToken,
                        runtime.Status, runtime.Language, null, null);
                }
            }

            // New join check
            if (runtime.Status != SessionStatus.Lobby && !runtime.Settings.AllowLateJoin)
            {
                return new(false, runtime.SessionId, Guid.Empty, string.Empty, null, runtime.Status, runtime.Language,
                    ErrorCodes.SessionStarted, "This game has already started.");
            }

            var norm = nicknameNormalizer.NormalizeAndValidate(nickname);
            if (!norm.IsValid)
            {
                return new(false, runtime.SessionId, Guid.Empty, string.Empty, null, runtime.Status, runtime.Language,
                    norm.ErrorCode, "Please choose a valid nickname (2 to 20 characters).");
            }

            if (runtime.Players.Values.Any(p => p.NicknameNormalized == norm.NormalizedKey))
            {
                return new(false, runtime.SessionId, Guid.Empty, string.Empty, null, runtime.Status, runtime.Language,
                    ErrorCodes.NicknameTaken, "That nickname is already taken in this session.");
            }

            var (rawToken, tokenHash) = tokenService.GenerateToken();
            var player = new Player(
                Guid.NewGuid(),
                runtime.SessionId,
                norm.DisplayNickname,
                norm.NormalizedKey,
                tokenHash,
                clock.UtcNow,
                runtime.CurrentQuestionIndex);

            await sessions.AddPlayerAsync(player, ct);

            var playerRuntime = new PlayerRuntime(player)
            {
                ConnectionId = connectionId
            };
            runtime.Players[player.Id] = playerRuntime;
            lobbyPayload = runtime.BuildLobbyPayload();

            result = new(true, runtime.SessionId, player.Id, player.Nickname, rawToken,
                runtime.Status, runtime.Language, null, null);
        }
        finally
        {
            runtime.Gate.Release();
        }

        if (previousConnectionToClose is not null)
        {
            await notifier.SendErrorAsync(
                previousConnectionToClose,
                new ErrorPayload(ErrorCodes.SessionTakenOver, "You connected from another tab."),
                ct);
        }

        if (lobbyPayload is not null)
        {
            await notifier.SendLobbyUpdatedAsync(runtime.SessionId, lobbyPayload, ct);
        }

        return result;
    }

    public async Task<LobbyUpdatedPayload?> GetLobbySnapshotAsync(Guid sessionId, CancellationToken ct = default)
    {
        if (!sessionManager.TryGetById(sessionId, out var runtime))
        {
            return null;
        }

        await runtime.Gate.WaitAsync(ct);
        try
        {
            return runtime.BuildLobbyPayload();
        }
        finally
        {
            runtime.Gate.Release();
        }
    }

    public async Task<bool> KickPlayerAsync(Guid sessionId, Guid hostId, Guid playerId, CancellationToken ct = default)
    {
        if (!sessionManager.TryGetById(sessionId, out var runtime) || runtime.HostId != hostId)
        {
            return false;
        }

        string? kickedConnId;
        LobbyUpdatedPayload payload;

        await runtime.Gate.WaitAsync(ct);
        try
        {
            if (!runtime.Players.TryGetValue(playerId, out var player) || player.IsKicked)
            {
                return false;
            }

            player.IsKicked = true;
            kickedConnId = player.ConnectionId;
            player.ConnectionId = null;
            payload = runtime.BuildLobbyPayload();

            var dbSession = await sessions.GetByIdWithSnapshotAsync(sessionId, ct);
            var dbPlayer = dbSession?.Players.FirstOrDefault(p => p.Id == playerId);
            dbPlayer?.Kick();
            await sessions.SaveChangesAsync(ct);
        }
        finally
        {
            runtime.Gate.Release();
        }

        if (kickedConnId is not null)
        {
            await notifier.SendKickedAsync(kickedConnId, ct);
        }

        await notifier.SendLobbyUpdatedAsync(sessionId, payload, ct);
        return true;
    }

    private async Task<string> GenerateUniquePinAsync(CancellationToken ct)
    {
        for (var i = 0; i < 20; i++)
        {
            var candidate = pinGenerator.GeneratePin();
            if (!await sessions.IsPinActiveAsync(candidate, ct))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Failed to generate a unique active PIN.");
    }
}

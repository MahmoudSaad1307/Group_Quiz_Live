using Microsoft.AspNetCore.SignalR;
using QuizLive.Api.Hubs;
using QuizLive.Application.Abstractions;
using QuizLive.Application.Game;
using QuizLive.Application.Sessions;

namespace QuizLive.Api.Notifications;

public sealed class SignalRGameNotifier(
    IHubContext<HostHub> hostHub,
    IHubContext<PlayHub> playHub) : IGameNotifier
{
    public async Task SendLobbyUpdatedAsync(Guid sessionId, LobbyUpdatedPayload payload, CancellationToken ct = default)
    {
        await Task.WhenAll(
            hostHub.Clients.Group(HubGroupNames.Host(sessionId)).SendAsync("LobbyUpdated", payload, ct),
            playHub.Clients.Group(HubGroupNames.Screens(sessionId)).SendAsync("LobbyUpdated", payload, ct),
            playHub.Clients.Group(HubGroupNames.Players(sessionId))
                .SendAsync("LobbyUpdated", new { payload.PlayerCount, Players = Array.Empty<LobbyPlayerDto>() }, ct));
    }

    public Task SendKickedAsync(string connectionId, CancellationToken ct = default) =>
        playHub.Clients.Client(connectionId).SendAsync("Kicked", ct);

    public Task SendErrorAsync(string connectionId, ErrorPayload payload, CancellationToken ct = default) =>
        playHub.Clients.Client(connectionId).SendAsync("Error", payload, ct);

    public async Task SendSessionStartedAsync(Guid sessionId, long serverNowMs, CancellationToken ct = default)
    {
        var body = new { serverNow = serverNowMs };
        await Task.WhenAll(
            hostHub.Clients.Group(HubGroupNames.Host(sessionId)).SendAsync("SessionStarted", body, ct),
            playHub.Clients.Group(HubGroupNames.Screens(sessionId)).SendAsync("SessionStarted", body, ct),
            playHub.Clients.Group(HubGroupNames.Players(sessionId)).SendAsync("SessionStarted", body, ct));
    }

    public async Task SendQuestionOpenedAsync(Guid sessionId, QuestionOpenedPayload payload, CancellationToken ct = default)
    {
        await Task.WhenAll(
            playHub.Clients.Group(HubGroupNames.Screens(sessionId)).SendAsync("QuestionOpened", payload, ct),
            playHub.Clients.Group(HubGroupNames.Players(sessionId)).SendAsync("QuestionOpened", payload, ct));
    }

    public Task SendHostQuestionViewAsync(Guid sessionId, HostQuestionViewPayload payload, CancellationToken ct = default) =>
        hostHub.Clients.Group(HubGroupNames.Host(sessionId)).SendAsync("QuestionOpened", payload, ct);

    public async Task SendAnswerProgressAsync(Guid sessionId, AnswerProgressPayload payload, CancellationToken ct = default)
    {
        await Task.WhenAll(
            hostHub.Clients.Group(HubGroupNames.Host(sessionId)).SendAsync("AnswerProgress", payload, ct),
            playHub.Clients.Group(HubGroupNames.Screens(sessionId)).SendAsync("AnswerProgress", payload, ct));
    }

    public async Task SendQuestionClosedAsync(Guid sessionId, QuestionClosedPayload payload, CancellationToken ct = default)
    {
        await Task.WhenAll(
            hostHub.Clients.Group(HubGroupNames.Host(sessionId)).SendAsync("QuestionClosed", payload, ct),
            playHub.Clients.Group(HubGroupNames.Screens(sessionId)).SendAsync("QuestionClosed", payload, ct),
            playHub.Clients.Group(HubGroupNames.Players(sessionId)).SendAsync("QuestionClosed", payload, ct));
    }

    public Task SendYourResultAsync(string connectionId, YourResultPayload payload, CancellationToken ct = default) =>
        playHub.Clients.Client(connectionId).SendAsync("YourResult", payload, ct);

    public async Task SendLeaderboardUpdatedAsync(Guid sessionId, LeaderboardUpdatedPayload payload, CancellationToken ct = default)
    {
        await Task.WhenAll(
            hostHub.Clients.Group(HubGroupNames.Host(sessionId)).SendAsync("LeaderboardUpdated", payload, ct),
            playHub.Clients.Group(HubGroupNames.Screens(sessionId)).SendAsync("LeaderboardUpdated", payload, ct),
            playHub.Clients.Group(HubGroupNames.Players(sessionId)).SendAsync("LeaderboardUpdated", payload, ct));
    }

    public Task SendYourRankAsync(string connectionId, YourRankPayload payload, CancellationToken ct = default) =>
        playHub.Clients.Client(connectionId).SendAsync("YourRank", payload, ct);

    public async Task SendSessionPausedAsync(Guid sessionId, CancellationToken ct = default)
    {
        await Task.WhenAll(
            hostHub.Clients.Group(HubGroupNames.Host(sessionId)).SendAsync("SessionPaused", ct),
            playHub.Clients.Group(HubGroupNames.Screens(sessionId)).SendAsync("SessionPaused", ct),
            playHub.Clients.Group(HubGroupNames.Players(sessionId)).SendAsync("SessionPaused", ct));
    }

    public async Task SendSessionResumedAsync(Guid sessionId, int remainingMs, long serverNowMs, CancellationToken ct = default)
    {
        var body = new { remainingMs, serverNowMs };
        await Task.WhenAll(
            hostHub.Clients.Group(HubGroupNames.Host(sessionId)).SendAsync("SessionResumed", body, ct),
            playHub.Clients.Group(HubGroupNames.Screens(sessionId)).SendAsync("SessionResumed", body, ct),
            playHub.Clients.Group(HubGroupNames.Players(sessionId)).SendAsync("SessionResumed", body, ct));
    }

    public async Task SendSessionFinishedAsync(Guid sessionId, SessionFinishedPayload payload, CancellationToken ct = default)
    {
        await Task.WhenAll(
            hostHub.Clients.Group(HubGroupNames.Host(sessionId)).SendAsync("SessionFinished", payload, ct),
            playHub.Clients.Group(HubGroupNames.Screens(sessionId)).SendAsync("SessionFinished", payload, ct),
            playHub.Clients.Group(HubGroupNames.Players(sessionId)).SendAsync("SessionFinished", payload, ct));
    }

    public Task SendFinalResultsAsync(string connectionId, FinalResultsPayload payload, CancellationToken ct = default) =>
        playHub.Clients.Client(connectionId).SendAsync("FinalResults", payload, ct);
}

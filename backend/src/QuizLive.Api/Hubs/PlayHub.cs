using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using QuizLive.Application.Abstractions;
using QuizLive.Application.Game;
using QuizLive.Application.Sessions;

namespace QuizLive.Api.Hubs;

[AllowAnonymous]
public sealed class PlayHub(
    SessionService sessionService,
    GameLoopService gameLoop,
    IScreenTokenService screenTokens) : Hub
{
    private const string ScreenRoleKey = "is_screen";
    private const string ScreenSessionIdKey = "screen_session_id";
    private const string PlayerIdKey = "player_id";
    private const string PlayerSessionIdKey = "player_session_id";

    public override async Task OnConnectedAsync()
    {
        var http = Context.GetHttpContext();
        var role = http?.Request.Query["role"].ToString();
        if (string.Equals(role, "screen", StringComparison.OrdinalIgnoreCase))
        {
            var token = http?.Request.Query["access_token"].ToString();
            if (string.IsNullOrWhiteSpace(token) || !screenTokens.TryValidateScreenToken(token, out var sessionId))
            {
                Context.Abort();
                return;
            }

            Context.Items[ScreenRoleKey] = true;
            Context.Items[ScreenSessionIdKey] = sessionId;
            await Groups.AddToGroupAsync(Context.ConnectionId, HubGroupNames.Screens(sessionId));
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.TryGetValue(PlayerIdKey, out var pidObj) && pidObj is Guid playerId &&
            Context.Items.TryGetValue(PlayerSessionIdKey, out var sidObj) && sidObj is Guid sessionId)
        {
            await gameLoop.OnPlayerDisconnectedAsync(sessionId, playerId, Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public Task<ScreenRehydrateDto?> GetScreenLobby()
    {
        if (Context.Items.TryGetValue(ScreenRoleKey, out var isScreen) && isScreen is true &&
            Context.Items.TryGetValue(ScreenSessionIdKey, out var sidObj) && sidObj is Guid sessionId)
        {
            return gameLoop.RehydrateScreenAsync(sessionId, Context.ConnectionAborted);
        }

        return Task.FromResult<ScreenRehydrateDto?>(null);
    }

    public Task<PlayerRehydrateDto?> Rehydrate()
    {
        if (Context.Items.TryGetValue(PlayerIdKey, out var pidObj) && pidObj is Guid playerId &&
            Context.Items.TryGetValue(PlayerSessionIdKey, out var sidObj) && sidObj is Guid sessionId)
        {
            return gameLoop.RehydratePlayerAsync(sessionId, playerId, Context.ConnectionAborted);
        }

        return Task.FromResult<PlayerRehydrateDto?>(null);
    }

    public async Task<JoinSessionResult> JoinSession(string pin, string nickname, string? existingToken = null)
    {
        if (Context.Items.TryGetValue(ScreenRoleKey, out var isScreen) && isScreen is true)
        {
            return new(false, Guid.Empty, Guid.Empty, string.Empty, null,
                Domain.Enums.SessionStatus.Lobby, Domain.Enums.QuizLanguage.En,
                ErrorCodes.Forbidden, "Screen connections are read-only.");
        }

        var result = await sessionService.JoinSessionAsync(
            pin,
            nickname,
            existingToken,
            Context.ConnectionId,
            Context.ConnectionAborted);

        if (result.Succeeded)
        {
            Context.Items[PlayerIdKey] = result.PlayerId;
            Context.Items[PlayerSessionIdKey] = result.SessionId;
            await Groups.AddToGroupAsync(Context.ConnectionId, HubGroupNames.Players(result.SessionId));
        }

        return result;
    }

    public async Task<SubmitAnswerResult> SubmitAnswer(Guid questionId, Guid optionId)
    {
        if (Context.Items.TryGetValue(ScreenRoleKey, out var isScreen) && isScreen is true)
        {
            return new(false, false, ErrorCodes.Forbidden, "Screen connections cannot submit answers.");
        }

        if (!Context.Items.TryGetValue(PlayerIdKey, out var pidObj) || pidObj is not Guid playerId ||
            !Context.Items.TryGetValue(PlayerSessionIdKey, out var sidObj) || sidObj is not Guid sessionId)
        {
            return new(false, false, ErrorCodes.Forbidden, "Not joined to a session.");
        }

        return await gameLoop.SubmitAnswerAsync(sessionId, playerId, questionId, optionId, Context.ConnectionAborted);
    }
}

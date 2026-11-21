using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using QuizLive.Api.Auth;
using QuizLive.Application.Game;
using QuizLive.Application.Sessions;

namespace QuizLive.Api.Hubs;

[Authorize(Policy = "Admin")]
public sealed class HostHub(
    SessionService sessionService,
    GameLoopService gameLoop) : Hub
{
    private const string HostSessionIdKey = "host_session_id";

    public async Task<HostRehydrateDto?> JoinHostSession(Guid sessionId)
    {
        Context.Items[HostSessionIdKey] = sessionId;
        await Groups.AddToGroupAsync(Context.ConnectionId, HubGroupNames.Host(sessionId));
        await gameLoop.OnHostConnectedAsync(sessionId);
        return await gameLoop.RehydrateHostAsync(sessionId, Context.User!.GetAdminId(), Context.ConnectionAborted);
    }

    public Task<HostRehydrateDto?> Rehydrate(Guid sessionId) =>
        gameLoop.RehydrateHostAsync(sessionId, Context.User!.GetAdminId(), Context.ConnectionAborted);

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.TryGetValue(HostSessionIdKey, out var sidObj) && sidObj is Guid sessionId)
        {
            await gameLoop.OnHostDisconnectedAsync(sessionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public Task<bool> KickPlayer(Guid sessionId, Guid playerId) =>
        sessionService.KickPlayerAsync(sessionId, Context.User!.GetAdminId(), playerId, Context.ConnectionAborted);

    public async Task StartSession(Guid sessionId) =>
        await SendErrorIfFailed(await gameLoop.StartSessionAsync(sessionId, Context.User!.GetAdminId(), Context.ConnectionAborted));

    public async Task NextQuestion(Guid sessionId) =>
        await SendErrorIfFailed(await gameLoop.NextAsync(sessionId, Context.User!.GetAdminId(), Context.ConnectionAborted));

    public async Task SkipQuestion(Guid sessionId) =>
        await SendErrorIfFailed(await gameLoop.SkipQuestionAsync(sessionId, Context.User!.GetAdminId(), Context.ConnectionAborted));

    public async Task Pause(Guid sessionId) =>
        await SendErrorIfFailed(await gameLoop.PauseAsync(sessionId, Context.User!.GetAdminId(), Context.ConnectionAborted));

    public async Task Resume(Guid sessionId) =>
        await SendErrorIfFailed(await gameLoop.ResumeAsync(sessionId, Context.User!.GetAdminId(), Context.ConnectionAborted));

    public async Task EndSession(Guid sessionId) =>
        await SendErrorIfFailed(await gameLoop.EndSessionAsync(sessionId, Context.User!.GetAdminId(), Context.ConnectionAborted));

    private async Task SendErrorIfFailed((bool Ok, ErrorPayload? Error) res)
    {
        if (!res.Ok && res.Error is not null)
        {
            await Clients.Caller.SendAsync("Error", res.Error, Context.ConnectionAborted);
        }
    }
}

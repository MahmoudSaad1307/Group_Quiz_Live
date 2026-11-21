using Microsoft.AspNetCore.SignalR;
using QuizLive.Api.Auth;
using QuizLive.Application.Sessions;

namespace QuizLive.Api.Hubs;

public sealed class HostOwnershipFilter(SessionManager sessionManager) : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        if (invocationContext.Hub is HostHub)
        {
            var user = invocationContext.Context.User
                ?? throw new HubException(ErrorCodes.Forbidden);
            var adminId = user.GetAdminId();

            var sessionIdArg = invocationContext.HubMethodArguments.OfType<Guid>().FirstOrDefault();
            if (sessionIdArg != Guid.Empty &&
                (!sessionManager.TryGetById(sessionIdArg, out var runtime) || runtime.HostId != adminId))
            {
                throw new HubException(ErrorCodes.Forbidden);
            }
        }

        return await next(invocationContext);
    }
}

public sealed class HubMethodRateLimitFilter : IHubFilter
{
    private const int MaxCallsPerSecond = 20;

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var items = invocationContext.Context.Items;
        var now = Environment.TickCount64;

        if (items.TryGetValue("rl_window_start", out var startObj) && startObj is long windowStart &&
            items.TryGetValue("rl_count", out var countObj) && countObj is int count)
        {
            if (now - windowStart < 1000)
            {
                if (count >= MaxCallsPerSecond)
                {
                    await invocationContext.Hub.Clients.Caller.SendAsync(
                        "Error",
                        new ErrorPayload(ErrorCodes.RateLimited, "Too many requests."));
                    invocationContext.Context.Abort();
                    return null;
                }

                items["rl_count"] = count + 1;
            }
            else
            {
                items["rl_window_start"] = now;
                items["rl_count"] = 1;
            }
        }
        else
        {
            items["rl_window_start"] = now;
            items["rl_count"] = 1;
        }

        return await next(invocationContext);
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuizLive.Application.Abstractions;
using QuizLive.Application.Game;
using QuizLive.Application.Sessions;

namespace QuizLive.Infrastructure.Sessions;

public sealed partial class SessionLifecycleService(
    IServiceScopeFactory scopeFactory,
    SessionManager sessionManager,
    GameLoopService gameLoop,
    IClock clock,
    ILogger<SessionLifecycleService> logger) : BackgroundService
{
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ISessionRepository>();
        var restorable = await repo.GetRestorableSessionsAsync(clock.UtcNow, cancellationToken);
        foreach (var session in restorable)
        {
            await gameLoop.RestoreSessionAsync(session, cancellationToken);
        }

        if (restorable.Count > 0)
        {
            LogRestoredSessions(logger, restorable.Count);
        }

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            // Sweep expired sessions cleanly
            var now = clock.UtcNow;
            foreach (var sessionId in sessionManager.GetAllSessionIds())
            {
                if (sessionManager.TryGetById(sessionId, out var runtime) && runtime.ExpiresAt <= now)
                {
                    await gameLoop.EndSessionAsync(sessionId, runtime.HostId, stoppingToken);
                    sessionManager.Remove(sessionId);
                }
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            foreach (var sessionId in sessionManager.GetAllSessionIds())
            {
                if (sessionManager.TryGetById(sessionId, out var runtime))
                {
                    await runtime.Gate.WaitAsync(cancellationToken);
                    try
                    {
                        runtime.CancelTimer();
                        using var scope = scopeFactory.CreateScope();
                        var repo = scope.ServiceProvider.GetRequiredService<ISessionRepository>();
                        await repo.PersistSessionStateAsync(
                            sessionId,
                            runtime.Status,
                            runtime.CurrentQuestionIndex,
                            clock.UtcNow,
                            runtime.PausedFromStatus,
                            runtime.PausedRemainingMs,
                            cancellationToken);
                    }
                    finally
                    {
                        runtime.Gate.Release();
                    }
                }
            }
        }
        catch (ObjectDisposedException)
        {
            // Host service provider already disposed during test teardown
        }

        await base.StopAsync(cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Restored {Count} active sessions from PostgreSQL.")]
    private static partial void LogRestoredSessions(ILogger logger, int count);
}

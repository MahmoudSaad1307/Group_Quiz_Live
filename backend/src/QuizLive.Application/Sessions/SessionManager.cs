using System.Collections.Concurrent;
using QuizLive.Application.Abstractions;
using QuizLive.Domain.Entities;

namespace QuizLive.Application.Sessions;

public sealed class SessionManager(IClock clock)
{
    private readonly ConcurrentDictionary<Guid, SessionRuntime> _sessions = new();
    private readonly ConcurrentDictionary<string, Guid> _pinIndex = new(StringComparer.Ordinal);

    public SessionRuntime Register(GameSession session)
    {
        var runtime = new SessionRuntime(session, clock);
        _sessions[session.Id] = runtime;
        _pinIndex[session.Pin] = session.Id;
        return runtime;
    }

    public bool TryGetById(Guid sessionId, out SessionRuntime runtime) =>
        _sessions.TryGetValue(sessionId, out runtime!);

    public bool TryGetByPin(string pin, out SessionRuntime runtime)
    {
        if (_pinIndex.TryGetValue(pin, out var id) && _sessions.TryGetValue(id, out runtime!))
        {
            return true;
        }

        runtime = null!;
        return false;
    }

    public void Remove(Guid sessionId)
    {
        if (_sessions.TryRemove(sessionId, out var runtime))
        {
            _pinIndex.TryRemove(runtime.Pin, out _);
            runtime.Dispose();
        }
    }

    public IReadOnlyCollection<Guid> GetAllSessionIds() => [.. _sessions.Keys];
}

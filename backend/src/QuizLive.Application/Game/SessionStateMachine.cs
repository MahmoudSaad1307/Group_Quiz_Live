using QuizLive.Domain.Enums;

namespace QuizLive.Application.Game;

public enum SessionTrigger
{
    Start,
    CloseQuestion,
    ShowLeaderboard,
    NextQuestion,
    Finish,
    Pause,
    Resume,
    Abandon
}

public sealed class InvalidSessionTransitionException(SessionStatus from, SessionTrigger trigger)
    : InvalidOperationException($"Illegal session transition from {from} via {trigger}.")
{
    public SessionStatus From { get; } = from;
    public SessionTrigger Trigger { get; } = trigger;
}

public static class SessionStateMachine
{
    private static readonly Dictionary<(SessionStatus From, SessionTrigger Trigger), SessionStatus> Transitions = new()
    {
        [(SessionStatus.Lobby, SessionTrigger.Start)] = SessionStatus.QuestionOpen,
        [(SessionStatus.Lobby, SessionTrigger.Abandon)] = SessionStatus.Abandoned,

        [(SessionStatus.QuestionOpen, SessionTrigger.CloseQuestion)] = SessionStatus.QuestionClosed,
        [(SessionStatus.QuestionOpen, SessionTrigger.Pause)] = SessionStatus.Paused,
        [(SessionStatus.QuestionOpen, SessionTrigger.Abandon)] = SessionStatus.Abandoned,

        [(SessionStatus.Paused, SessionTrigger.Resume)] = SessionStatus.QuestionOpen,
        [(SessionStatus.Paused, SessionTrigger.Abandon)] = SessionStatus.Abandoned,

        [(SessionStatus.QuestionClosed, SessionTrigger.ShowLeaderboard)] = SessionStatus.Leaderboard,
        [(SessionStatus.QuestionClosed, SessionTrigger.Abandon)] = SessionStatus.Abandoned,

        [(SessionStatus.Leaderboard, SessionTrigger.NextQuestion)] = SessionStatus.QuestionOpen,
        [(SessionStatus.Leaderboard, SessionTrigger.Finish)] = SessionStatus.Finished,
        [(SessionStatus.Leaderboard, SessionTrigger.Abandon)] = SessionStatus.Abandoned,
    };

    public static bool CanTransition(SessionStatus from, SessionTrigger trigger) =>
        Transitions.ContainsKey((from, trigger));

    public static SessionStatus Transition(SessionStatus from, SessionTrigger trigger) =>
        Transitions.TryGetValue((from, trigger), out var next)
            ? next
            : throw new InvalidSessionTransitionException(from, trigger);
}

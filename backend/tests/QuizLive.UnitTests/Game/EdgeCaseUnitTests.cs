using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using QuizLive.Application.Abstractions;
using QuizLive.Application.Game;
using QuizLive.Application.Sessions;
using QuizLive.Domain.Entities;
using QuizLive.Domain.Enums;
using QuizLive.Domain.ValueObjects;

namespace QuizLive.UnitTests.Game;

public sealed class EdgeCaseUnitTests
{
    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);
        public long UtcNowMs => UtcNow.ToUnixTimeMilliseconds();
    }

    private sealed class NoOpNotifier : IGameNotifier
    {
        public int QuestionClosedCount { get; private set; }
        public Task SendLobbyUpdatedAsync(Guid s, LobbyUpdatedPayload p, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendKickedAsync(string c, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendErrorAsync(string c, ErrorPayload p, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendSessionStartedAsync(Guid s, long n, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendQuestionOpenedAsync(Guid s, QuestionOpenedPayload p, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendHostQuestionViewAsync(Guid s, HostQuestionViewPayload p, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendAnswerProgressAsync(Guid s, AnswerProgressPayload p, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendQuestionClosedAsync(Guid s, QuestionClosedPayload p, CancellationToken ct = default)
        {
            QuestionClosedCount++;
            return Task.CompletedTask;
        }
        public Task SendYourResultAsync(string c, YourResultPayload p, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendLeaderboardUpdatedAsync(Guid s, LeaderboardUpdatedPayload p, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendYourRankAsync(string c, YourRankPayload p, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendSessionPausedAsync(Guid s, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendSessionResumedAsync(Guid s, int r, long n, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendSessionFinishedAsync(Guid s, SessionFinishedPayload p, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendFinalResultsAsync(string c, FinalResultsPayload p, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeSessionRepo : ISessionRepository
    {
        public int PersistClosedCalls { get; private set; }
        public Task AddAsync(GameSession session, CancellationToken ct = default) => Task.CompletedTask;
        public Task<GameSession?> GetByIdWithSnapshotAsync(Guid sessionId, CancellationToken ct = default) => Task.FromResult<GameSession?>(null);
        public Task<GameSession?> GetActiveByPinAsync(string pin, CancellationToken ct = default) => Task.FromResult<GameSession?>(null);
        public Task<bool> IsPinActiveAsync(string pin, CancellationToken ct = default) => Task.FromResult(false);
        public Task AddPlayerAsync(Player player, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<GameSession>> GetRestorableSessionsAsync(DateTimeOffset now, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<GameSession>>([]);
        public Task PersistSessionStateAsync(Guid s, SessionStatus st, int idx, DateTimeOffset now, SessionStatus? pf = null, int? pr = null, CancellationToken ct = default) =>
            Task.CompletedTask;
        public Task PersistQuestionClosedAsync(Guid s, SessionStatus st, int idx, DateTimeOffset now, IReadOnlyList<Answer> ans, IReadOnlyDictionary<Guid, (int, int, long)> pu, CancellationToken ct = default)
        {
            PersistClosedCalls++;
            return Task.CompletedTask;
        }
    }

    private static (GameLoopService Engine, SessionManager Manager, FakeClock Clock, NoOpNotifier Notifier, FakeSessionRepo Repo) CreateHarness()
    {
        var clock = new FakeClock();
        var manager = new SessionManager(clock);
        var notifier = new NoOpNotifier();
        var repo = new FakeSessionRepo();

        var services = new ServiceCollection();
        services.AddSingleton<ISessionRepository>(repo);
        var sp = services.BuildServiceProvider();

        var engine = new GameLoopService(
            manager,
            sp.GetRequiredService<IServiceScopeFactory>(),
            notifier,
            clock,
            TimeProvider.System,
            NullLogger<GameLoopService>.Instance);

        return (engine, manager, clock, notifier, repo);
    }

    private static (GameSession Session, Quiz Quiz) BuildSampleSession(FakeClock clock)
    {
        var hostId = Guid.NewGuid();
        var quiz = new Quiz(Guid.NewGuid(), hostId, "Live", null, QuizLanguage.En, clock.UtcNow);
        quiz.AddQuestion(QuestionType.SingleChoice, "Original Question", null, null, 20, 1000,
            [("Correct", true), ("Wrong", false)], clock.UtcNow);

        var session = new GameSession(Guid.NewGuid(), quiz.Id, hostId, "123456", QuizLanguage.En,
            new SessionSettings(), clock.UtcNow, clock.UtcNow.AddHours(6));
        session.SnapshotQuiz(quiz, new Random(1));
        return (session, quiz);
    }

    [Fact]
    public async Task StartSession_WithZeroPlayers_ReturnsNoPlayersError()
    {
        var (engine, manager, clock, _, _) = CreateHarness();
        var (session, _) = BuildSampleSession(clock);
        manager.Register(session);

        var (ok, err) = await engine.StartSessionAsync(session.Id, session.HostId);
        ok.Should().BeFalse();
        err!.Code.Should().Be(ErrorCodes.NoPlayers);
    }

    [Fact]
    public async Task SubmitAnswer_IsIdempotent_And_AutoClosesWhenAllAnswered()
    {
        var (engine, manager, clock, notifier, repo) = CreateHarness();
        var (session, _) = BuildSampleSession(clock);
        var runtime = manager.Register(session);

        var p1 = new Player(Guid.NewGuid(), session.Id, "Alice", "alice", "h1", clock.UtcNow, 0);
        runtime.Players[p1.Id] = new PlayerRuntime(p1) { ConnectionId = "c1" };

        var (ok, _) = await engine.StartSessionAsync(session.Id, session.HostId);
        ok.Should().BeTrue();

        var q = runtime.CurrentQuestion;
        var opt = q.Options.First(o => o.IsCorrect);

        var r1 = await engine.SubmitAnswerAsync(session.Id, p1.Id, q.Id, opt.Id);
        r1.Accepted.Should().BeTrue();
        r1.AlreadyAnswered.Should().BeFalse();

        // Duplicate retry after auto-close returns invalid state or already answered, and only 1 close occurred
        notifier.QuestionClosedCount.Should().Be(1);
        repo.PersistClosedCalls.Should().Be(1);
    }

    [Fact]
    public async Task AllPlayersDisconnect_AutoPausesSession()
    {
        var (engine, manager, clock, _, _) = CreateHarness();
        var (session, _) = BuildSampleSession(clock);
        var runtime = manager.Register(session);

        var p1 = new Player(Guid.NewGuid(), session.Id, "Alice", "alice", "h1", clock.UtcNow, 0);
        runtime.Players[p1.Id] = new PlayerRuntime(p1) { ConnectionId = "c1" };

        await engine.StartSessionAsync(session.Id, session.HostId);
        await engine.OnPlayerDisconnectedAsync(session.Id, p1.Id, "c1");

        runtime.Status.Should().Be(SessionStatus.Paused);
    }

    [Fact]
    public void EditingQuizMidSession_DoesNotAffectSnapshot()
    {
        var clock = new FakeClock();
        var (session, quiz) = BuildSampleSession(clock);
        var sourceQ = quiz.Questions.First();
        sourceQ.Update(QuestionType.SingleChoice, "Edited Question", null, null, 30, 500);

        session.Questions.First().Text.Should().Be("Original Question");
        session.Questions.First().Points.Should().Be(1000);
    }

    [Fact]
    public async Task RestoreSession_WhenQuestionOpen_RestoresAsPaused()
    {
        var (engine, manager, clock, _, _) = CreateHarness();
        var (session, _) = BuildSampleSession(clock);
        session.SetState(SessionStatus.QuestionOpen, 0, clock.UtcNow);

        await engine.RestoreSessionAsync(session);

        manager.TryGetById(session.Id, out var restored).Should().BeTrue();
        restored.Status.Should().Be(SessionStatus.Paused);
    }
}

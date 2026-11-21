using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QuizLive.Application.Abstractions;
using QuizLive.Application.Sessions;
using QuizLive.Domain.Entities;
using QuizLive.Domain.Enums;

namespace QuizLive.Application.Game;

public sealed partial class GameLoopService(
    SessionManager sessionManager,
    IServiceScopeFactory scopeFactory,
    IGameNotifier notifier,
    IClock clock,
    TimeProvider timeProvider,
    ILogger<GameLoopService> logger)
{
    public async Task<(bool Ok, ErrorPayload? Error)> StartSessionAsync(Guid sessionId, Guid hostId, CancellationToken ct = default)
    {
        if (!sessionManager.TryGetById(sessionId, out var runtime) || runtime.HostId != hostId)
        {
            return (false, new ErrorPayload(ErrorCodes.Forbidden, "Session not found or access denied."));
        }

        QuestionOpenedPayload openedPayload;
        HostQuestionViewPayload hostPayload;
        long serverNowMs;
        int questionIndex;
        int durationMs;

        await runtime.Gate.WaitAsync(ct);
        try
        {
            if (runtime.Players.Values.All(p => p.IsKicked))
            {
                return (false, new ErrorPayload(ErrorCodes.NoPlayers, "At least 1 player must join before starting."));
            }

            if (runtime.Questions.Count == 0)
            {
                return (false, new ErrorPayload(ErrorCodes.InvalidState, "Quiz has no questions."));
            }

            runtime.Status = SessionStateMachine.Transition(runtime.Status, SessionTrigger.Start);
            runtime.CurrentQuestionIndex = 0;
            (openedPayload, hostPayload, serverNowMs, durationMs) = OpenCurrentQuestionLocked(runtime);
            questionIndex = runtime.CurrentQuestionIndex;

            await PersistStateAsync(runtime, ct);
            ScheduleQuestionClose(runtime, questionIndex, durationMs + AnswerWindow.GraceMs);
        }
        catch (InvalidSessionTransitionException ex)
        {
            LogIllegalTransition(logger, sessionId, ex.From, ex.Trigger);
            return (false, new ErrorPayload(ErrorCodes.InvalidState, ex.Message));
        }
        finally
        {
            runtime.Gate.Release();
        }

        await notifier.SendSessionStartedAsync(sessionId, serverNowMs, ct);
        await notifier.SendQuestionOpenedAsync(sessionId, openedPayload, ct);
        await notifier.SendHostQuestionViewAsync(sessionId, hostPayload, ct);
        return (true, null);
    }

    public async Task<SubmitAnswerResult> SubmitAnswerAsync(
        Guid sessionId,
        Guid playerId,
        Guid questionId,
        Guid optionId,
        CancellationToken ct = default)
    {
        if (!sessionManager.TryGetById(sessionId, out var runtime))
        {
            return new(false, false, ErrorCodes.SessionNotFound, "Session not found.");
        }

        AnswerProgressPayload? progressPayload = null;
        var shouldAutoClose = false;
        int questionIndexToClose = 0;

        await runtime.Gate.WaitAsync(ct);
        try
        {
            if (!runtime.Players.TryGetValue(playerId, out var player) || player.IsKicked)
            {
                return new(false, false, ErrorCodes.Kicked, "Player is not active in this session.");
            }

            if (runtime.Status != SessionStatus.QuestionOpen)
            {
                return new(false, false, ErrorCodes.InvalidState, "Question is not open.");
            }

            var currentQ = runtime.CurrentQuestion;
            if (currentQ.Id != questionId)
            {
                return new(false, false, ErrorCodes.InvalidState, "Question mismatch.");
            }

            if (runtime.CurrentAnswers.ContainsKey(playerId))
            {
                return new(true, true, null, null);
            }

            var selectedOption = currentQ.Options.FirstOrDefault(o => o.Id == optionId);
            if (selectedOption is null)
            {
                return new(false, false, ErrorCodes.InvalidState, "Invalid option.");
            }

            var nowMs = clock.UtcNowMs;
            var eval = AnswerWindow.Evaluate(nowMs, runtime.StartedAtServerMs, currentQ.TimeLimitSeconds * 1000);
            if (!eval.Accepted)
            {
                return new(false, false, ErrorCodes.InvalidState, "Time expired.");
            }

            runtime.CurrentAnswers[playerId] = new InMemoryAnswerRecord(
                playerId,
                questionId,
                optionId,
                selectedOption.IsCorrect,
                eval.ClampedResponseTimeMs,
                clock.UtcNow);

            var totalActive = Math.Max(1, runtime.CountActiveEligiblePlayers(clock.UtcNow));
            var answered = runtime.CurrentAnswers.Count;

            if (nowMs - runtime.LastProgressBroadcastMs >= 250 || answered >= totalActive)
            {
                runtime.LastProgressBroadcastMs = nowMs;
                progressPayload = new AnswerProgressPayload(answered, totalActive);
            }

            if (runtime.Settings.AutoCloseWhenAllAnswered && answered >= totalActive)
            {
                shouldAutoClose = true;
                questionIndexToClose = runtime.CurrentQuestionIndex;
            }
        }
        finally
        {
            runtime.Gate.Release();
        }

        if (progressPayload is not null)
        {
            await notifier.SendAnswerProgressAsync(sessionId, progressPayload, ct);
        }

        if (shouldAutoClose)
        {
            await CloseQuestionAsync(sessionId, questionIndexToClose, ct);
        }

        return new(true, false, null, null);
    }

    public async Task CloseQuestionAsync(Guid sessionId, int expectedQuestionIndex, CancellationToken ct = default)
    {
        if (!sessionManager.TryGetById(sessionId, out var runtime))
        {
            return;
        }

        QuestionClosedPayload closedPayload;
        List<(string ConnectionId, YourResultPayload Payload)> playerResults = [];

        await runtime.Gate.WaitAsync(ct);
        try
        {
            if (runtime.Status != SessionStatus.QuestionOpen || runtime.CurrentQuestionIndex != expectedQuestionIndex)
            {
                return;
            }

            runtime.CancelTimer();
            runtime.Status = SessionStateMachine.Transition(runtime.Status, SessionTrigger.CloseQuestion);

            var q = runtime.CurrentQuestion;
            var durationMs = q.TimeLimitSeconds * 1000;
            var correctOption = q.Options.First(o => o.IsCorrect);
            var distribution = q.Options.ToDictionary(o => o.Id, _ => 0);

            var answersToPersist = new List<Answer>(runtime.Players.Count);
            var playerUpdates = new Dictionary<Guid, (int TotalScore, int Streak, long CumulativeRtMs)>();
            var now = clock.UtcNow;

            var tempResults = new List<(PlayerRuntime Player, bool IsCorrect, int Points, int Streak)>();

            foreach (var player in runtime.Players.Values.Where(p => !p.IsKicked))
            {
                if (runtime.CurrentAnswers.TryGetValue(player.Id, out var rec))
                {
                    if (distribution.TryGetValue(rec.SelectedOptionId, out var curCount))
                    {
                        distribution[rec.SelectedOptionId] = curCount + 1;
                    }

                    var nextStreak = q.Points == 0
                        ? player.CurrentStreak
                        : (rec.IsCorrect ? player.CurrentStreak + 1 : 0);

                    var points = ScoringCalculator.CalculatePoints(
                        rec.IsCorrect,
                        rec.ResponseTimeMs,
                        durationMs,
                        q.Points,
                        nextStreak,
                        runtime.Settings.StreakBonus);

                    player.TotalScore += points;
                    player.CurrentStreak = nextStreak;
                    player.CumulativeResponseTimeMs += rec.ResponseTimeMs;
                    player.LastDelta = points;
                    if (rec.IsCorrect) player.CorrectCount++;

                    answersToPersist.Add(new Answer(
                        Guid.NewGuid(),
                        sessionId,
                        player.Id,
                        q.Id,
                        rec.SelectedOptionId,
                        rec.IsCorrect,
                        rec.ResponseTimeMs,
                        points,
                        nextStreak,
                        rec.SubmittedAt));

                    playerUpdates[player.Id] = (player.TotalScore, player.CurrentStreak, player.CumulativeResponseTimeMs);
                    tempResults.Add((player, rec.IsCorrect, points, nextStreak));
                }
                else
                {
                    // Non-answer row for analytics (D11)
                    if (q.Points > 0)
                    {
                        player.CurrentStreak = 0;
                    }
                    player.LastDelta = 0;

                    answersToPersist.Add(new Answer(
                        Guid.NewGuid(),
                        sessionId,
                        player.Id,
                        q.Id,
                        null,
                        false,
                        durationMs,
                        0,
                        player.CurrentStreak,
                        now));

                    playerUpdates[player.Id] = (player.TotalScore, player.CurrentStreak, player.CumulativeResponseTimeMs);
                    tempResults.Add((player, false, 0, player.CurrentStreak));
                }
            }

            var ranked = LeaderboardRanker.Rank(runtime.Players.Values
                .Where(p => !p.IsKicked)
                .Select(p => new RankablePlayer(
                    p.Id, p.Nickname, p.TotalScore, p.CumulativeResponseTimeMs, p.JoinedAt, p.PreviousRank, p.LastDelta)));
            var rankMap = ranked.ToDictionary(r => r.PlayerId, r => r.Rank);

            foreach (var (player, isCorrect, pts, streak) in tempResults)
            {
                if (player.ConnectionId is not null && rankMap.TryGetValue(player.Id, out var rank))
                {
                    playerResults.Add((player.ConnectionId, new YourResultPayload(isCorrect, pts, player.TotalScore, streak, rank)));
                }
            }

            using (var scope = scopeFactory.CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<ISessionRepository>();
                await repo.PersistQuestionClosedAsync(
                    sessionId,
                    runtime.Status,
                    runtime.CurrentQuestionIndex,
                    now,
                    answersToPersist,
                    playerUpdates,
                    ct);
            }

            closedPayload = new QuestionClosedPayload(correctOption.Id, distribution, q.Explanation);
            runtime.LastClosedPayload = closedPayload;
        }
        finally
        {
            runtime.Gate.Release();
        }

        await notifier.SendQuestionClosedAsync(sessionId, closedPayload, ct);
        foreach (var (connId, payload) in playerResults)
        {
            await notifier.SendYourResultAsync(connId, payload, ct);
        }
    }

    public async Task<(bool Ok, ErrorPayload? Error)> NextAsync(Guid sessionId, Guid hostId, CancellationToken ct = default)
    {
        if (!sessionManager.TryGetById(sessionId, out var runtime) || runtime.HostId != hostId)
        {
            return (false, new ErrorPayload(ErrorCodes.Forbidden, "Session not found or access denied."));
        }

        LeaderboardUpdatedPayload? leaderboardPayload = null;
        List<(string ConnectionId, YourRankPayload Payload)> rankPayloads = [];
        QuestionOpenedPayload? openedPayload = null;
        HostQuestionViewPayload? hostPayload = null;
        SessionFinishedPayload? finishedPayload = null;
        List<(string ConnectionId, FinalResultsPayload Payload)> finalPayloads = [];

        await runtime.Gate.WaitAsync(ct);
        try
        {
            runtime.CancelTimer();

            if (runtime.Status == SessionStatus.QuestionClosed)
            {
                runtime.Status = SessionStateMachine.Transition(runtime.Status, SessionTrigger.ShowLeaderboard);
                var ranked = LeaderboardRanker.Rank(runtime.Players.Values
                    .Where(p => !p.IsKicked)
                    .Select(p => new RankablePlayer(
                        p.Id, p.Nickname, p.TotalScore, p.CumulativeResponseTimeMs, p.JoinedAt, p.PreviousRank, p.LastDelta)));

                foreach (var r in ranked)
                {
                    if (runtime.Players.TryGetValue(r.PlayerId, out var pr))
                    {
                        pr.PreviousRank = r.Rank;
                        if (pr.ConnectionId is not null)
                        {
                            rankPayloads.Add((pr.ConnectionId, new YourRankPayload(r.Rank, r.GapToNext, r.NextNickname)));
                        }
                    }
                }

                var top5 = ranked.Take(5)
                    .Select(r => new LeaderboardEntryDto(r.Nickname, r.TotalScore, r.Delta, r.Rank, r.PreviousRank))
                    .ToList();
                leaderboardPayload = new LeaderboardUpdatedPayload(top5, ranked.Count);
                runtime.LastLeaderboardPayload = leaderboardPayload;

                await PersistStateAsync(runtime, ct);

                if (runtime.Settings.AutoAdvance)
                {
                    ScheduleAutoAdvance(runtime, runtime.Settings.AutoAdvanceDelaySeconds * 1000);
                }
            }
            else if (runtime.Status == SessionStatus.Leaderboard)
            {
                if (runtime.CurrentQuestionIndex + 1 < runtime.Questions.Count)
                {
                    runtime.Status = SessionStateMachine.Transition(runtime.Status, SessionTrigger.NextQuestion);
                    runtime.CurrentQuestionIndex++;
                    var (op, hp, _, durationMs) = OpenCurrentQuestionLocked(runtime);
                    openedPayload = op;
                    hostPayload = hp;

                    await PersistStateAsync(runtime, ct);
                    ScheduleQuestionClose(runtime, runtime.CurrentQuestionIndex, durationMs + AnswerWindow.GraceMs);
                }
                else
                {
                    runtime.Status = SessionStateMachine.Transition(runtime.Status, SessionTrigger.Finish);
                    var ranked = LeaderboardRanker.Rank(runtime.Players.Values
                        .Where(p => !p.IsKicked)
                        .Select(p => new RankablePlayer(
                            p.Id, p.Nickname, p.TotalScore, p.CumulativeResponseTimeMs, p.JoinedAt, p.PreviousRank, p.LastDelta)));

                    var podium = ranked.Take(3)
                        .Select(r => new PodiumEntryDto(r.Rank, r.Nickname, r.TotalScore))
                        .ToList();
                    finishedPayload = new SessionFinishedPayload(podium);
                    runtime.LastPodium = podium;

                    foreach (var r in ranked)
                    {
                        if (runtime.Players.TryGetValue(r.PlayerId, out var pr) && pr.ConnectionId is not null)
                        {
                            finalPayloads.Add((pr.ConnectionId,
                                new FinalResultsPayload(r.Rank, r.TotalScore, pr.CorrectCount, runtime.Questions.Count)));
                        }
                    }

                    await PersistStateAsync(runtime, ct);
                }
            }
            else
            {
                throw new InvalidSessionTransitionException(runtime.Status, SessionTrigger.NextQuestion);
            }
        }
        catch (InvalidSessionTransitionException ex)
        {
            LogIllegalTransition(logger, sessionId, ex.From, ex.Trigger);
            return (false, new ErrorPayload(ErrorCodes.InvalidState, ex.Message));
        }
        finally
        {
            runtime.Gate.Release();
        }

        if (leaderboardPayload is not null)
        {
            await notifier.SendLeaderboardUpdatedAsync(sessionId, leaderboardPayload, ct);
            foreach (var (connId, rp) in rankPayloads)
            {
                await notifier.SendYourRankAsync(connId, rp, ct);
            }
        }

        if (openedPayload is not null && hostPayload is not null)
        {
            await notifier.SendQuestionOpenedAsync(sessionId, openedPayload, ct);
            await notifier.SendHostQuestionViewAsync(sessionId, hostPayload, ct);
        }

        if (finishedPayload is not null)
        {
            await notifier.SendSessionFinishedAsync(sessionId, finishedPayload, ct);
            foreach (var (connId, fp) in finalPayloads)
            {
                await notifier.SendFinalResultsAsync(connId, fp, ct);
            }
        }

        return (true, null);
    }

    public async Task<(bool Ok, ErrorPayload? Error)> SkipQuestionAsync(Guid sessionId, Guid hostId, CancellationToken ct = default)
    {
        if (!sessionManager.TryGetById(sessionId, out var runtime) || runtime.HostId != hostId)
        {
            return (false, new ErrorPayload(ErrorCodes.Forbidden, "Session not found or access denied."));
        }

        int qIndex;
        await runtime.Gate.WaitAsync(ct);
        try
        {
            if (runtime.Status != SessionStatus.QuestionOpen)
            {
                return (false, new ErrorPayload(ErrorCodes.InvalidState, "No open question to skip."));
            }

            qIndex = runtime.CurrentQuestionIndex;
        }
        finally
        {
            runtime.Gate.Release();
        }

        await CloseQuestionAsync(sessionId, qIndex, ct);
        return (true, null);
    }

    public async Task<(bool Ok, ErrorPayload? Error)> PauseAsync(Guid sessionId, Guid hostId, CancellationToken ct = default)
    {
        if (!sessionManager.TryGetById(sessionId, out var runtime) || runtime.HostId != hostId)
        {
            return (false, new ErrorPayload(ErrorCodes.Forbidden, "Session not found or access denied."));
        }

        await runtime.Gate.WaitAsync(ct);
        try
        {
            runtime.Status = SessionStateMachine.Transition(runtime.Status, SessionTrigger.Pause);
            runtime.CancelTimer();
            var remaining = (int)Math.Max(0, runtime.DeadlineServerMs - clock.UtcNowMs);
            runtime.PausedFromStatus = SessionStatus.QuestionOpen;
            runtime.PausedRemainingMs = remaining;
            await PersistStateAsync(runtime, ct);
        }
        catch (InvalidSessionTransitionException ex)
        {
            return (false, new ErrorPayload(ErrorCodes.InvalidState, ex.Message));
        }
        finally
        {
            runtime.Gate.Release();
        }

        await notifier.SendSessionPausedAsync(sessionId, ct);
        return (true, null);
    }

    public async Task<(bool Ok, ErrorPayload? Error)> ResumeAsync(Guid sessionId, Guid hostId, CancellationToken ct = default)
    {
        if (!sessionManager.TryGetById(sessionId, out var runtime) || runtime.HostId != hostId)
        {
            return (false, new ErrorPayload(ErrorCodes.Forbidden, "Session not found or access denied."));
        }

        int remainingMs;
        long nowMs;

        await runtime.Gate.WaitAsync(ct);
        try
        {
            runtime.Status = SessionStateMachine.Transition(runtime.Status, SessionTrigger.Resume);
            remainingMs = runtime.PausedRemainingMs ?? 1000;
            nowMs = clock.UtcNowMs;
            var durationMs = runtime.CurrentQuestion.TimeLimitSeconds * 1000;

            runtime.DeadlineServerMs = nowMs + remainingMs;
            runtime.StartedAtServerMs = runtime.DeadlineServerMs - durationMs;
            runtime.PausedFromStatus = null;
            runtime.PausedRemainingMs = null;

            await PersistStateAsync(runtime, ct);
            ScheduleQuestionClose(runtime, runtime.CurrentQuestionIndex, remainingMs + AnswerWindow.GraceMs);
        }
        catch (InvalidSessionTransitionException ex)
        {
            return (false, new ErrorPayload(ErrorCodes.InvalidState, ex.Message));
        }
        finally
        {
            runtime.Gate.Release();
        }

        await notifier.SendSessionResumedAsync(sessionId, remainingMs, nowMs, ct);
        return (true, null);
    }

    public async Task<(bool Ok, ErrorPayload? Error)> EndSessionAsync(Guid sessionId, Guid hostId, CancellationToken ct = default)
    {
        if (!sessionManager.TryGetById(sessionId, out var runtime) || runtime.HostId != hostId)
        {
            return (false, new ErrorPayload(ErrorCodes.Forbidden, "Session not found or access denied."));
        }

        await runtime.Gate.WaitAsync(ct);
        try
        {
            runtime.CancelTimer();
            runtime.Status = SessionStateMachine.Transition(runtime.Status, SessionTrigger.Abandon);
            await PersistStateAsync(runtime, ct);
        }
        catch (InvalidSessionTransitionException ex)
        {
            return (false, new ErrorPayload(ErrorCodes.InvalidState, ex.Message));
        }
        finally
        {
            runtime.Gate.Release();
        }

        await notifier.SendSessionFinishedAsync(sessionId, new SessionFinishedPayload([]), ct);
        return (true, null);
    }

    public async Task<HostRehydrateDto?> RehydrateHostAsync(Guid sessionId, Guid hostId, CancellationToken ct = default)
    {
        if (!sessionManager.TryGetById(sessionId, out var runtime) || runtime.HostId != hostId)
        {
            return null;
        }

        await runtime.Gate.WaitAsync(ct);
        try
        {
            var nowMs = clock.UtcNowMs;
            HostQuestionViewPayload? hq = null;
            if (runtime.Status is SessionStatus.QuestionOpen or SessionStatus.Paused or SessionStatus.QuestionClosed &&
                runtime.CurrentQuestionIndex < runtime.Questions.Count)
            {
                var q = runtime.CurrentQuestion;
                var opts = q.Options.OrderBy(o => o.OrderIndex).Select(o => new SanitizedOptionDto(o.Id, o.Text)).ToList();
                var correctOpt = q.Options.First(o => o.IsCorrect);
                hq = new HostQuestionViewPayload(
                    runtime.CurrentQuestionIndex, runtime.Questions.Count, q.Id, q.Text, q.ImageUrl,
                    opts, correctOpt.Id, q.Explanation, q.TimeLimitSeconds * 1000, runtime.StartedAtServerMs, nowMs);
            }

            var totalActive = Math.Max(1, runtime.CountActiveEligiblePlayers(clock.UtcNow));
            var progress = runtime.Status == SessionStatus.QuestionOpen
                ? new AnswerProgressPayload(runtime.CurrentAnswers.Count, totalActive)
                : null;

            return new HostRehydrateDto(
                runtime.Status,
                runtime.CurrentQuestionIndex,
                runtime.Questions.Count,
                runtime.BuildLobbyPayload(),
                hq,
                progress,
                runtime.LastClosedPayload,
                runtime.LastLeaderboardPayload,
                runtime.LastPodium,
                runtime.PausedRemainingMs,
                nowMs);
        }
        finally
        {
            runtime.Gate.Release();
        }
    }

    public async Task<PlayerRehydrateDto?> RehydratePlayerAsync(Guid sessionId, Guid playerId, CancellationToken ct = default)
    {
        if (!sessionManager.TryGetById(sessionId, out var runtime))
        {
            return null;
        }

        await runtime.Gate.WaitAsync(ct);
        try
        {
            if (!runtime.Players.TryGetValue(playerId, out var player) || player.IsKicked)
            {
                return null;
            }

            var nowMs = clock.UtcNowMs;
            QuestionOpenedPayload? qPayload = null;
            if (runtime.Status is SessionStatus.QuestionOpen or SessionStatus.Paused &&
                runtime.CurrentQuestionIndex < runtime.Questions.Count)
            {
                var q = runtime.CurrentQuestion;
                var opts = q.Options.OrderBy(o => o.OrderIndex).Select(o => new SanitizedOptionDto(o.Id, o.Text)).ToList();
                qPayload = new QuestionOpenedPayload(
                    runtime.CurrentQuestionIndex, runtime.Questions.Count, q.Id, q.Text, q.ImageUrl,
                    opts, q.TimeLimitSeconds * 1000, runtime.StartedAtServerMs, nowMs, runtime.Settings.ShowQuestionOnPhones);
            }

            var ranked = LeaderboardRanker.Rank(runtime.Players.Values
                .Where(p => !p.IsKicked)
                .Select(p => new RankablePlayer(p.Id, p.Nickname, p.TotalScore, p.CumulativeResponseTimeMs, p.JoinedAt, p.PreviousRank, p.LastDelta)));
            var myRanked = ranked.FirstOrDefault(r => r.PlayerId == playerId);

            YourResultPayload? yourResult = runtime.Status == SessionStatus.QuestionClosed && myRanked is not null
                ? new YourResultPayload(
                    runtime.CurrentAnswers.TryGetValue(playerId, out var rec) && rec.IsCorrect,
                    player.LastDelta,
                    player.TotalScore,
                    player.CurrentStreak,
                    myRanked.Rank)
                : null;

            YourRankPayload? yourRank = runtime.Status == SessionStatus.Leaderboard && myRanked is not null
                ? new YourRankPayload(myRanked.Rank, myRanked.GapToNext, myRanked.NextNickname)
                : null;

            FinalResultsPayload? finalResults = runtime.Status == SessionStatus.Finished && myRanked is not null
                ? new FinalResultsPayload(myRanked.Rank, player.TotalScore, player.CorrectCount, runtime.Questions.Count)
                : null;

            return new PlayerRehydrateDto(
                runtime.Status,
                runtime.CurrentQuestionIndex,
                runtime.Questions.Count,
                qPayload,
                runtime.CurrentAnswers.ContainsKey(playerId),
                yourResult,
                yourRank,
                finalResults,
                runtime.PausedRemainingMs,
                nowMs);
        }
        finally
        {
            runtime.Gate.Release();
        }
    }

    public async Task<ScreenRehydrateDto?> RehydrateScreenAsync(Guid sessionId, CancellationToken ct = default)
    {
        if (!sessionManager.TryGetById(sessionId, out var runtime))
        {
            return null;
        }

        await runtime.Gate.WaitAsync(ct);
        try
        {
            var nowMs = clock.UtcNowMs;
            QuestionOpenedPayload? qPayload = null;
            if (runtime.Status is SessionStatus.QuestionOpen or SessionStatus.Paused or SessionStatus.QuestionClosed &&
                runtime.CurrentQuestionIndex < runtime.Questions.Count)
            {
                var q = runtime.CurrentQuestion;
                var opts = q.Options.OrderBy(o => o.OrderIndex).Select(o => new SanitizedOptionDto(o.Id, o.Text)).ToList();
                qPayload = new QuestionOpenedPayload(
                    runtime.CurrentQuestionIndex, runtime.Questions.Count, q.Id, q.Text, q.ImageUrl,
                    opts, q.TimeLimitSeconds * 1000, runtime.StartedAtServerMs, nowMs, runtime.Settings.ShowQuestionOnPhones);
            }

            var totalActive = Math.Max(1, runtime.CountActiveEligiblePlayers(clock.UtcNow));
            var progress = runtime.Status == SessionStatus.QuestionOpen
                ? new AnswerProgressPayload(runtime.CurrentAnswers.Count, totalActive)
                : null;

            return new ScreenRehydrateDto(
                runtime.Status,
                runtime.CurrentQuestionIndex,
                runtime.Questions.Count,
                runtime.BuildLobbyPayload(),
                qPayload,
                progress,
                runtime.LastClosedPayload,
                runtime.LastLeaderboardPayload,
                runtime.LastPodium,
                runtime.PausedRemainingMs,
                nowMs);
        }
        finally
        {
            runtime.Gate.Release();
        }
    }

    public async Task OnPlayerDisconnectedAsync(Guid sessionId, Guid playerId, string connectionId)
    {
        if (!sessionManager.TryGetById(sessionId, out var runtime))
        {
            return;
        }

        var shouldPauseAllGone = false;
        var scheduleGraceCheck = false;
        var qIndex = 0;

        await runtime.Gate.WaitAsync();
        try
        {
            if (!runtime.Players.TryGetValue(playerId, out var player) || player.ConnectionId != connectionId)
            {
                return;
            }

            player.ConnectionId = null;
            player.DisconnectedAt = clock.UtcNow;

            if (runtime.Status == SessionStatus.QuestionOpen)
            {
                var connectedCount = runtime.Players.Values.Count(p => !p.IsKicked && p.IsConnected);
                if (connectedCount == 0)
                {
                    shouldPauseAllGone = true;
                }
                else if (runtime.Settings.AutoCloseWhenAllAnswered)
                {
                    scheduleGraceCheck = true;
                    qIndex = runtime.CurrentQuestionIndex;
                }
            }
        }
        finally
        {
            runtime.Gate.Release();
        }

        if (shouldPauseAllGone)
        {
            await PauseAsync(sessionId, runtime.HostId);
            return;
        }

        if (scheduleGraceCheck)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5.2), timeProvider);
                if (!sessionManager.TryGetById(sessionId, out var rt)) return;

                var shouldClose = false;
                await rt.Gate.WaitAsync();
                try
                {
                    if (rt.Status == SessionStatus.QuestionOpen && rt.CurrentQuestionIndex == qIndex)
                    {
                        var eligible = rt.CountActiveEligiblePlayers(clock.UtcNow);
                        if (eligible > 0 && rt.CurrentAnswers.Count >= eligible)
                        {
                            shouldClose = true;
                        }
                    }
                }
                finally
                {
                    rt.Gate.Release();
                }

                if (shouldClose)
                {
                    await CloseQuestionAsync(sessionId, qIndex);
                }
            });
        }
    }

    public async Task OnHostConnectedAsync(Guid sessionId)
    {
        if (!sessionManager.TryGetById(sessionId, out var runtime)) return;
        await runtime.Gate.WaitAsync();
        try
        {
            runtime.HostConnectionCount++;
            runtime.HostAwayCts?.Cancel();
            runtime.HostAwayCts?.Dispose();
            runtime.HostAwayCts = null;
        }
        finally
        {
            runtime.Gate.Release();
        }
    }

    public async Task OnHostDisconnectedAsync(Guid sessionId)
    {
        if (!sessionManager.TryGetById(sessionId, out var runtime)) return;
        await runtime.Gate.WaitAsync();
        try
        {
            runtime.HostConnectionCount = Math.Max(0, runtime.HostConnectionCount - 1);
            if (runtime.HostConnectionCount == 0 && runtime.Status == SessionStatus.QuestionOpen)
            {
                runtime.HostAwayCts?.Cancel();
                runtime.HostAwayCts?.Dispose();
                var cts = new CancellationTokenSource();
                runtime.HostAwayCts = cts;
                var hostId = runtime.HostId;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(2), timeProvider, cts.Token);
                        await PauseAsync(sessionId, hostId);
                    }
                    catch (OperationCanceledException)
                    {
                        // Host returned
                    }
                }, CancellationToken.None);
            }
        }
        finally
        {
            runtime.Gate.Release();
        }
    }

    public async Task RestoreSessionAsync(GameSession session, CancellationToken ct = default)
    {
        var runtime = sessionManager.Register(session);
        if (runtime.Status == SessionStatus.QuestionOpen)
        {
            await runtime.Gate.WaitAsync(ct);
            try
            {
                runtime.Status = SessionStatus.Paused;
                runtime.PausedFromStatus = SessionStatus.QuestionOpen;
                runtime.PausedRemainingMs ??= Math.Max(1000, runtime.CurrentQuestion.TimeLimitSeconds * 1000 / 2);
                await PersistStateAsync(runtime, ct);
            }
            finally
            {
                runtime.Gate.Release();
            }
        }
    }

    private (QuestionOpenedPayload Opened, HostQuestionViewPayload Host, long ServerNowMs, int DurationMs)
        OpenCurrentQuestionLocked(SessionRuntime runtime)
    {
        runtime.CurrentAnswers.Clear();
        var q = runtime.CurrentQuestion;
        var durationMs = q.TimeLimitSeconds * 1000;
        var nowMs = clock.UtcNowMs;

        runtime.StartedAtServerMs = nowMs;
        runtime.DeadlineServerMs = nowMs + durationMs;
        runtime.LastProgressBroadcastMs = 0;

        var options = q.Options.OrderBy(o => o.OrderIndex)
            .Select(o => new SanitizedOptionDto(o.Id, o.Text))
            .ToList();
        var correctOpt = q.Options.First(o => o.IsCorrect);

        var opened = new QuestionOpenedPayload(
            runtime.CurrentQuestionIndex,
            runtime.Questions.Count,
            q.Id,
            q.Text,
            q.ImageUrl,
            options,
            durationMs,
            nowMs,
            nowMs,
            runtime.Settings.ShowQuestionOnPhones);

        var host = new HostQuestionViewPayload(
            runtime.CurrentQuestionIndex,
            runtime.Questions.Count,
            q.Id,
            q.Text,
            q.ImageUrl,
            options,
            correctOpt.Id,
            q.Explanation,
            durationMs,
            nowMs,
            nowMs);

        return (opened, host, nowMs, durationMs);
    }

    private void ScheduleQuestionClose(SessionRuntime runtime, int questionIndex, int delayMs)
    {
        runtime.CancelTimer();
        var cts = new CancellationTokenSource();
        runtime.TimerCts = cts;
        var sessionId = runtime.SessionId;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(0, delayMs)), timeProvider, cts.Token);
                await CloseQuestionAsync(sessionId, questionIndex, CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                // Expected when skipped, paused, or closed early
            }
        }, CancellationToken.None);
    }

    private void ScheduleAutoAdvance(SessionRuntime runtime, int delayMs)
    {
        runtime.CancelTimer();
        var cts = new CancellationTokenSource();
        runtime.TimerCts = cts;
        var sessionId = runtime.SessionId;
        var hostId = runtime.HostId;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(0, delayMs)), timeProvider, cts.Token);
                await NextAsync(sessionId, hostId, CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                // Expected when host manually advances or ends
            }
        }, CancellationToken.None);
    }

    private async Task PersistStateAsync(SessionRuntime runtime, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ISessionRepository>();
        await repo.PersistSessionStateAsync(
            runtime.SessionId,
            runtime.Status,
            runtime.CurrentQuestionIndex,
            clock.UtcNow,
            runtime.PausedFromStatus,
            runtime.PausedRemainingMs,
            ct);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Illegal transition on session {SessionId}: {From} -> {Trigger}")]
    private static partial void LogIllegalTransition(ILogger logger, Guid sessionId, SessionStatus from, SessionTrigger trigger);
}

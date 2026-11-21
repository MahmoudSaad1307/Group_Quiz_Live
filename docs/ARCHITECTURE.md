# QuizLive Architecture

## System Overview
QuizLive is a real-time, Kahoot-style live quiz platform built with:
- **Backend:** ASP.NET Core (.NET 10 LTS), SignalR (`/hubs/host`, `/hubs/play`), EF Core 10, PostgreSQL 17.
- **Frontend:** Next.js 16 (App Router, TypeScript), Tailwind CSS, Zustand, Framer Motion, `next-intl` (`en` / `ar` with RTL).
- **Single Origin:** Next.js rewrites `/api/*`, `/hubs/*`, `/uploads/*`, `/health/*` to `API_INTERNAL_URL` in dev; a reverse proxy routes by path in production.

## Layered Backend Structure
- `QuizLive.Domain`: Entities (`Admin`, `Quiz`, `Question`, `Option`, `GameSession`, `SessionQuestion`, `SessionOption`, `Player`, `Answer`), enums, value objects, domain invariants.
- `QuizLive.Application`: Pure game logic (`SessionStateMachine`, `ScoringCalculator`, `AnswerWindow`, `LeaderboardRanker`, `NicknameNormalizer`), `SessionManager`, `SessionRuntime`, `GameLoopService`, `QuizService`, `SessionService`, `AuthService`, FluentValidation validators.
- `QuizLive.Infrastructure`: `QuizLiveDbContext`, EF Core configurations & migrations, repositories, `SkiaImageProcessor` + `LocalDiskFileStorage`, `WordListProfanityFilter`, `ResultsExporter` (CSV/XLSX), `SessionLifecycleService` (startup restore, expiry sweeper, graceful shutdown).
- `QuizLive.Api`: Thin REST controllers, SignalR hubs (`HostHub`, `PlayHub`) with `HostOwnershipFilter` and `HubMethodRateLimitFilter`, `SignalRGameNotifier`, rate limiting, security headers, `/health*`, `/metrics`.

## Concurrency & Locking
- Each active session has a `SessionRuntime` stored in `SessionManager` (`ConcurrentDictionary<Guid, SessionRuntime>`).
- All state mutations, answer submissions, timer closes, and rehydrations acquire `runtime.Gate` (`SemaphoreSlim(1, 1)`).
- SignalR broadcasts happen **after** releasing `runtime.Gate` so slow sockets never block game state.
- Timers use `Task.Delay(..., timeProvider, cts.Token)` and pass `expectedQuestionIndex` so stale timers are no-ops.

## Persistence & Restore
- Quiz questions and options are snapshotted into `session_questions` / `session_options` at session creation.
- State transitions persist to `game_sessions`; question close persists all `answers` and updated `players` totals in one transaction.
- On startup, `SessionLifecycleService` restores non-final, non-expired sessions; any `QuestionOpen` session is restored as `Paused` for the host to resume.

## Scaling Notes
- One server instance handles 300+ concurrent players per session with in-process `SessionRuntime`.
- Horizontal scaling requires sticky sessions (or a future Redis SignalR backplane + distributed locking).

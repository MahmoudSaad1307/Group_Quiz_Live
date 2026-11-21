# Architecture Decisions (QuizLive)

## D1: .NET 10 LTS
- **Context:** Spec says ".NET 8 LTS or latest LTS". Current LTS as of Nov 2026 is .NET 10.
- **Decision:** Target `net10.0`, EF Core 10, Npgsql 10.

## D2: `Paused` in `SessionStatus`
- **Context:** State machine includes `Paused`, but the domain `Status` enum omitted it.
- **Decision:** Add `Paused` to `SessionStatus` and persist `PausedRemainingMs` and `PausedFromStatus` on `GameSession`.

## D3: Quiz Snapshot Tables
- **Context:** §11.11 requires editing a quiz never affects live sessions, while `Answer.QuestionId` references questions.
- **Decision:** Copy quiz questions and options into `SessionQuestion` and `SessionOption` rows when creating a `GameSession`. `Answer` FKs point to the snapshot rows.

## D4: Single-Origin Proxy for Cookies & SignalR
- **Context:** Cookie-based JWT (`SameSite=Lax`, `httpOnly`) and SignalR WebSockets across separate origins (`localhost:3000` vs `localhost:5000`) hit cross-site cookie restrictions.
- **Decision:** Next.js rewrites `/api/*`, `/hubs/*`, and `/uploads/*` to `API_INTERNAL_URL` in dev; reverse proxy handles path routing in production.

## D5: Anti-Forgery Strategy
- **Context:** Cookie-authenticated mutating admin routes need CSRF protection.
- **Decision:** Double-submit cookie (`XSRF-TOKEN` non-httpOnly + `X-XSRF-TOKEN` header) validated by ASP.NET Core Antiforgery on mutating admin endpoints, paired with `SameSite=Lax`.

## D6: Streak Bonus Semantics
- **Context:** Streak bonus is `+ min(streak, 5) * 100` after 2+ consecutive correct answers.
- **Decision:** `streak` includes the current correct answer; bonus applies when `streak >= 2`. No-score poll questions (`Points = 0`) never award a bonus and do not break the streak.

## D7: Final Question Transition
- **Context:** §6 states both "leaderboard after every question" and "after the last question's leaderboard -> Finished".
- **Decision:** Last question transitions `QuestionClosed -> Leaderboard -> Finished` (host clicks Next or `autoAdvance` fires).

## D8: Session Store & Redis Profile
- **Context:** Default in-memory `ConcurrentDictionary` vs optional Redis profile.
- **Decision:** Live state and per-session locks/timers run in-process via `InMemorySessionStore`. Optional `RedisSessionStore` mirrors snapshots behind `SessionStore:Provider=Redis`; multi-instance SignalR backplane remains backlog.

## D9: SignalR Protocol
- **Context:** JSON default vs MessagePack.
- **Decision:** Default to JSON (`System.Text.Json`). Evaluate MessagePack in Phase 6 load testing.

## D10: Tie-Breaking & Competition Ranking
- **Context:** §11.10 specifies tied players share the same rank, ordered by lower cumulative response time then join time.
- **Decision:** Competition ranking (`1, 1, 3`). Display order: `TotalScore DESC, CumulativeResponseTimeMs ASC, JoinedAt ASC`.

## D11: Unanswered Questions Analytics
- **Context:** §4 allows optionally persisting non-answers as null rows.
- **Decision:** Persist `SelectedOptionId = null` rows at `QuestionClosed` for all active non-answering players to simplify analytics and export.

## D12: Image Processing Library
- **Context:** Re-encode and strip EXIF metadata on uploaded question images (<= 2MB).
- **Decision:** Use `SkiaSharp` (+ `SkiaSharp.NativeAssets.Linux.NoDependencies` for Alpine containers) to decode, resize (max 1600px), strip metadata by re-encoding from the pixel buffer, and save as WebP.

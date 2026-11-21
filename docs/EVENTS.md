# QuizLive SignalR Event Contracts

## Hubs
- `/hubs/host` — Authenticated Admin/Host control channel (`ql_auth` cookie).
- `/hubs/play` — Student channel (anonymous + player token) and Big Screen (`?role=screen&access_token=...`, read-only).

## Client -> Server Methods
### HostHub (`/hubs/host`)
- `JoinHostSession(sessionId)` -> `HostRehydrateDto`
- `Rehydrate(sessionId)` -> `HostRehydrateDto`
- `StartSession(sessionId)`
- `NextQuestion(sessionId)`
- `SkipQuestion(sessionId)`
- `Pause(sessionId)`
- `Resume(sessionId)`
- `EndSession(sessionId)`
- `KickPlayer(sessionId, playerId)` -> `bool`

### PlayHub (`/hubs/play`)
- `JoinSession(pin, nickname, existingToken?)` -> `JoinSessionResult`
- `SubmitAnswer(questionId, optionId)` -> `SubmitAnswerResult`
- `Rehydrate()` -> `PlayerRehydrateDto`
- `GetScreenLobby()` -> `ScreenRehydrateDto` (screen connections only)

## Server -> Client Events
- `LobbyUpdated`: `{ "playerCount": 12, "players": [{ "id": "...", "nickname": "Alice" }] }`
- `SessionStarted`: `{ "serverNow": 1732200000000 }`
- `QuestionOpened` (students/screen — **no correctness fields**):
  ```json
  {
    "questionIndex": 0,
    "total": 5,
    "questionId": "...",
    "text": "Capital of France?",
    "imageUrl": null,
    "options": [{ "id": "...", "text": "Paris" }, { "id": "...", "text": "Rome" }],
    "durationMs": 20000,
    "startedAtServerMs": 1732200000000,
    "serverNowMs": 1732200000000,
    "showQuestionOnPhones": true
  }
  ```
- `AnswerProgress` (host + screen): `{ "answered": 8, "totalActive": 12 }`
- `QuestionClosed`: `{ "correctOptionId": "...", "distribution": { "opt1": 8, "opt2": 4 }, "explanation": "..." }`
- `YourResult` (per-player): `{ "isCorrect": true, "pointsAwarded": 950, "totalScore": 1950, "streak": 2, "rank": 1 }`
- `LeaderboardUpdated`: `{ "top5": [{ "nickname": "Alice", "score": 1950, "delta": 950, "rank": 1, "previousRank": 2 }], "totalPlayers": 12 }`
- `YourRank` (per-player): `{ "rank": 4, "gapToNext": 120, "nextNickname": "Bob" }`
- `SessionPaused` / `SessionResumed { remainingMs, serverNowMs }`
- `SessionFinished`: `{ "podium": [{ "rank": 1, "nickname": "Alice", "score": 4500 }] }`
- `FinalResults` (per-player): `{ "rank": 1, "score": 4500, "correctCount": 5, "totalQuestions": 5 }`
- `Kicked`
- `Error`: `{ "code": "NicknameTaken", "message": "..." }`

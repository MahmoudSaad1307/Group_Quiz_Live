# QuizLive REST API

## Auth & Health
- `GET /api/auth/csrf` — Issues `XSRF-TOKEN` cookie.
- `POST /api/auth/login` — Authenticates admin and sets `ql_auth` httpOnly JWT cookie.
- `POST /api/auth/logout` — Clears `ql_auth` cookie.
- `GET /api/auth/me` — Returns current authenticated admin profile.
- `GET /health`, `GET /health/live`, `GET /health/ready` — Liveness & PostgreSQL readiness.
- `GET /metrics` — Prometheus text metrics (`quizlive_sessions_active`, `quizlive_players_connected`).

## Quizzes (Admin)
- `GET /api/quizzes?search=&includeArchived=&page=&pageSize=`
- `POST /api/quizzes`
- `GET /api/quizzes/{id}`, `PUT /api/quizzes/{id}`, `DELETE /api/quizzes/{id}`
- `POST /api/quizzes/{id}/duplicate`
- `POST /api/quizzes/{quizId}/questions`
- `PUT /api/quizzes/{quizId}/questions/{questionId}`
- `DELETE /api/quizzes/{quizId}/questions/{questionId}`
- `PUT /api/quizzes/{quizId}/questions/reorder`
- `POST /api/uploads/image` (multipart `file` <= 2MB, re-encoded to WebP with metadata stripped)

## Sessions & Results
- `POST /api/sessions` `{ quizId, settings }` -> `{ sessionId, pin, joinUrl }`
- `GET /api/sessions/{id}`
- `POST /api/sessions/{id}/screen-token`
- `GET /api/sessions/{id}/results`
- `GET /api/sessions/{id}/export?format=xlsx|csv`
- `GET /api/join/{pin}` (Public, rate-limited)

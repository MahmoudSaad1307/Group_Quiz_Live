export type ErrorCode =
  | "SessionNotFound"
  | "SessionStarted"
  | "SessionEnded"
  | "NicknameInvalid"
  | "NicknameTaken"
  | "NicknameProfane"
  | "Kicked"
  | "Forbidden"
  | "InvalidState"
  | "NoPlayers"
  | "RateLimited"
  | "SessionTakenOver";

export interface ErrorPayload {
  code: ErrorCode;
  message: string;
}

export interface LobbyPlayer {
  id: string;
  nickname: string;
}

export interface LobbyUpdatedPayload {
  playerCount: number;
  players: LobbyPlayer[];
}

export interface JoinSessionResult {
  succeeded: boolean;
  sessionId: string;
  playerId: string;
  nickname: string;
  token?: string | null;
  phase: string;
  language: "En" | "Ar";
  errorCode?: ErrorCode | null;
  errorMessage?: string | null;
}

export interface SanitizedOptionDto {
  id: string;
  text: string;
}

export interface QuestionOpenedPayload {
  questionIndex: number;
  total: number;
  questionId: string;
  text: string;
  imageUrl?: string | null;
  options: SanitizedOptionDto[];
  durationMs: number;
  startedAtServerMs: number;
  serverNowMs: number;
  showQuestionOnPhones: boolean;
}

export interface HostQuestionViewPayload {
  questionIndex: number;
  total: number;
  questionId: string;
  text: string;
  imageUrl?: string | null;
  options: SanitizedOptionDto[];
  correctOptionId: string;
  explanation?: string | null;
  durationMs: number;
  startedAtServerMs: number;
  serverNowMs: number;
}

export interface AnswerProgressPayload {
  answered: number;
  totalActive: number;
}

export interface QuestionClosedPayload {
  correctOptionId: string;
  distribution: Record<string, number>;
  explanation?: string | null;
}

export interface YourResultPayload {
  isCorrect: boolean;
  pointsAwarded: number;
  totalScore: number;
  streak: number;
  rank: number;
}

export interface LeaderboardEntryDto {
  nickname: string;
  score: number;
  delta: number;
  rank: number;
  previousRank: number;
}

export interface LeaderboardUpdatedPayload {
  top5: LeaderboardEntryDto[];
  totalPlayers: number;
}

export interface YourRankPayload {
  rank: number;
  gapToNext?: number | null;
  nextNickname?: string | null;
}

export interface PodiumEntryDto {
  rank: number;
  nickname: string;
  score: number;
}

export interface SessionFinishedPayload {
  podium: PodiumEntryDto[];
}

export interface FinalResultsPayload {
  rank: number;
  score: number;
  correctCount: number;
  totalQuestions: number;
}

export interface SubmitAnswerResult {
  accepted: boolean;
  alreadyAnswered: boolean;
  errorCode?: ErrorCode | null;
  errorMessage?: string | null;
}

export interface HostRehydrateDto {
  phase: string;
  currentQuestionIndex: number;
  totalQuestions: number;
  lobby: LobbyUpdatedPayload;
  currentQuestion?: HostQuestionViewPayload | null;
  progress?: AnswerProgressPayload | null;
  closed?: QuestionClosedPayload | null;
  leaderboard?: LeaderboardUpdatedPayload | null;
  podium?: PodiumEntryDto[] | null;
  pausedRemainingMs?: number | null;
  serverNowMs: number;
}

export interface PlayerRehydrateDto {
  phase: string;
  currentQuestionIndex: number;
  totalQuestions: number;
  currentQuestion?: QuestionOpenedPayload | null;
  alreadyAnswered: boolean;
  yourResult?: YourResultPayload | null;
  yourRank?: YourRankPayload | null;
  finalResults?: FinalResultsPayload | null;
  pausedRemainingMs?: number | null;
  serverNowMs: number;
}

export interface ScreenRehydrateDto {
  phase: string;
  currentQuestionIndex: number;
  totalQuestions: number;
  lobby: LobbyUpdatedPayload;
  currentQuestion?: QuestionOpenedPayload | null;
  progress?: AnswerProgressPayload | null;
  closed?: QuestionClosedPayload | null;
  leaderboard?: LeaderboardUpdatedPayload | null;
  podium?: PodiumEntryDto[] | null;
  pausedRemainingMs?: number | null;
  serverNowMs: number;
}

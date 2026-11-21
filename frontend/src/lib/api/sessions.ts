import { apiFetch } from "./client";
import type { QuizLanguage } from "./quizzes";

export interface SessionSettings {
  showQuestionOnPhones: boolean;
  shuffleQuestions: boolean;
  shuffleOptions: boolean;
  streakBonus: boolean;
  allowLateJoin: boolean;
  autoCloseWhenAllAnswered: boolean;
  autoAdvance: boolean;
  autoAdvanceDelaySeconds: number;
}

export interface CreateSessionResponse {
  sessionId: string;
  pin: string;
  joinUrl: string;
}

export interface SessionDetailDto {
  id: string;
  quizId: string;
  pin: string;
  language: QuizLanguage;
  status: string;
  currentQuestionIndex: number;
  totalQuestions: number;
  settings: SessionSettings;
  createdAt: string;
  expiresAt: string;
}

export interface JoinCheckResponse {
  quizTitle: string;
  language: QuizLanguage;
  joinable: boolean;
}

export interface ScreenTokenResponse {
  token: string;
  expiresAt: string;
}

export interface OptionStatDto {
  optionId: string;
  text: string;
  isCorrect: boolean;
  count: number;
}

export interface QuestionStatDto {
  questionId: string;
  orderIndex: number;
  text: string;
  correctPercent: number;
  avgResponseTimeMs: number;
  options: OptionStatDto[];
}

export interface StudentResultRowDto {
  playerId: string;
  nickname: string;
  rank: number;
  totalScore: number;
  correctCount: number;
  avgResponseTimeMs: number;
}

export interface SessionResultsDto {
  sessionId: string;
  pin: string;
  language: QuizLanguage;
  status: string;
  startedAt?: string | null;
  finishedAt?: string | null;
  students: StudentResultRowDto[];
  questions: QuestionStatDto[];
}

export const sessionsApi = {
  create: (quizId: string, settings: SessionSettings) =>
    apiFetch<CreateSessionResponse>("/api/sessions", {
      method: "POST",
      body: JSON.stringify({ quizId, settings }),
    }),
  get: (id: string) => apiFetch<SessionDetailDto>(`/api/sessions/${id}`),
  issueScreenToken: (id: string) =>
    apiFetch<ScreenTokenResponse>(`/api/sessions/${id}/screen-token`, {
      method: "POST",
    }),
  checkPin: (pin: string) => apiFetch<JoinCheckResponse>(`/api/join/${pin}`),
  getResults: (id: string) => apiFetch<SessionResultsDto>(`/api/sessions/${id}/results`),
};

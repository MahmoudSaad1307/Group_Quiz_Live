import { apiFetch } from "./client";

export type QuizLanguage = "En" | "Ar";
export type QuestionType = "SingleChoice" | "TrueFalse";

export interface OptionDto {
  id: string;
  orderIndex: number;
  text: string;
  isCorrect: boolean;
}

export interface QuestionDto {
  id: string;
  orderIndex: number;
  type: QuestionType;
  text: string;
  imageUrl?: string | null;
  explanation?: string | null;
  timeLimitSeconds: number;
  points: number;
  options: OptionDto[];
}

export interface QuizSummaryDto {
  id: string;
  title: string;
  description?: string | null;
  language: QuizLanguage;
  isArchived: boolean;
  questionCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface QuizDetailDto {
  id: string;
  title: string;
  description?: string | null;
  language: QuizLanguage;
  isArchived: boolean;
  createdAt: string;
  updatedAt: string;
  questions: QuestionDto[];
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface UpsertOptionRequest {
  text: string;
  isCorrect: boolean;
}

export interface UpsertQuestionRequest {
  type: QuestionType;
  text: string;
  imageUrl?: string | null;
  explanation?: string | null;
  timeLimitSeconds: number;
  points: number;
  options: UpsertOptionRequest[];
}

export const quizzesApi = {
  list: (params: { search?: string; includeArchived?: boolean; page?: number } = {}) => {
    const qs = new URLSearchParams();
    if (params.search) qs.set("search", params.search);
    if (params.includeArchived) qs.set("includeArchived", "true");
    if (params.page) qs.set("page", String(params.page));
    const query = qs.toString();
    return apiFetch<PagedResult<QuizSummaryDto>>(`/api/quizzes${query ? `?${query}` : ""}`);
  },
  create: (body: { title: string; description?: string | null; language: QuizLanguage }) =>
    apiFetch<QuizDetailDto>("/api/quizzes", {
      method: "POST",
      body: JSON.stringify(body),
    }),
  get: (id: string) => apiFetch<QuizDetailDto>(`/api/quizzes/${id}`),
  update: (
    id: string,
    body: { title: string; description?: string | null; language: QuizLanguage; isArchived?: boolean },
  ) =>
    apiFetch<QuizDetailDto>(`/api/quizzes/${id}`, {
      method: "PUT",
      body: JSON.stringify(body),
    }),
  delete: (id: string) =>
    apiFetch<void>(`/api/quizzes/${id}`, {
      method: "DELETE",
    }),
  duplicate: (id: string) =>
    apiFetch<QuizDetailDto>(`/api/quizzes/${id}/duplicate`, {
      method: "POST",
    }),
  addQuestion: (quizId: string, body: UpsertQuestionRequest) =>
    apiFetch<QuestionDto>(`/api/quizzes/${quizId}/questions`, {
      method: "POST",
      body: JSON.stringify(body),
    }),
  updateQuestion: (quizId: string, questionId: string, body: UpsertQuestionRequest) =>
    apiFetch<QuestionDto>(`/api/quizzes/${quizId}/questions/${questionId}`, {
      method: "PUT",
      body: JSON.stringify(body),
    }),
  deleteQuestion: (quizId: string, questionId: string) =>
    apiFetch<void>(`/api/quizzes/${quizId}/questions/${questionId}`, {
      method: "DELETE",
    }),
  reorderQuestions: (quizId: string, orderedIds: string[]) =>
    apiFetch<QuizDetailDto>(`/api/quizzes/${quizId}/questions/reorder`, {
      method: "PUT",
      body: JSON.stringify({ orderedIds }),
    }),
  uploadImage: async (file: File) => {
    const formData = new FormData();
    formData.append("file", file);
    return apiFetch<{ url: string }>("/api/uploads/image", {
      method: "POST",
      body: formData,
    });
  },
};

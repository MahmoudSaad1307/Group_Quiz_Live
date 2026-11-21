import { create } from "zustand";
import type { QuestionDto, QuizDetailDto, QuizLanguage } from "@/lib/api/quizzes";

export type SaveStatus = "saved" | "dirty" | "saving" | "error";

interface BuilderState {
  quiz: QuizDetailDto | null;
  activeQuestionId: string | null;
  dirtyQuestionIds: Record<string, boolean>;
  metadataDirty: boolean;
  saveStatus: SaveStatus;

  setQuiz: (quiz: QuizDetailDto) => void;
  setActiveQuestionId: (id: string | null) => void;
  updateMetadataLocal: (patch: {
    title?: string;
    description?: string | null;
    language?: QuizLanguage;
  }) => void;
  updateQuestionLocal: (questionId: string, updater: (q: QuestionDto) => QuestionDto) => void;
  addQuestionLocal: (question: QuestionDto) => void;
  removeQuestionLocal: (questionId: string) => void;
  reorderQuestionsLocal: (orderedIds: string[]) => void;
  markMetadataSaved: () => void;
  markQuestionSaved: (questionId: string) => void;
  setSaveStatus: (status: SaveStatus) => void;
}

export const useBuilderStore = create<BuilderState>((set) => ({
  quiz: null,
  activeQuestionId: null,
  dirtyQuestionIds: {},
  metadataDirty: false,
  saveStatus: "saved",

  setQuiz: (quiz) =>
    set((state) => ({
      quiz,
      activeQuestionId:
        state.activeQuestionId && quiz.questions.some((q) => q.id === state.activeQuestionId)
          ? state.activeQuestionId
          : (quiz.questions[0]?.id ?? null),
      dirtyQuestionIds: {},
      metadataDirty: false,
      saveStatus: "saved",
    })),

  setActiveQuestionId: (id) => set({ activeQuestionId: id }),

  updateMetadataLocal: (patch) =>
    set((state) => {
      if (!state.quiz) return state;
      return {
        quiz: { ...state.quiz, ...patch },
        metadataDirty: true,
        saveStatus: "dirty",
      };
    }),

  updateQuestionLocal: (questionId, updater) =>
    set((state) => {
      if (!state.quiz) return state;
      return {
        quiz: {
          ...state.quiz,
          questions: state.quiz.questions.map((q) => (q.id === questionId ? updater(q) : q)),
        },
        dirtyQuestionIds: { ...state.dirtyQuestionIds, [questionId]: true },
        saveStatus: "dirty",
      };
    }),

  addQuestionLocal: (question) =>
    set((state) => {
      if (!state.quiz) return state;
      return {
        quiz: {
          ...state.quiz,
          questions: [...state.quiz.questions, question],
        },
        activeQuestionId: question.id,
      };
    }),

  removeQuestionLocal: (questionId) =>
    set((state) => {
      if (!state.quiz) return state;
      const remaining = state.quiz.questions
        .filter((q) => q.id !== questionId)
        .map((q, idx) => ({ ...q, orderIndex: idx }));
      const nextDirty = { ...state.dirtyQuestionIds };
      delete nextDirty[questionId];
      return {
        quiz: { ...state.quiz, questions: remaining },
        dirtyQuestionIds: nextDirty,
        activeQuestionId:
          state.activeQuestionId === questionId
            ? (remaining[0]?.id ?? null)
            : state.activeQuestionId,
      };
    }),

  reorderQuestionsLocal: (orderedIds) =>
    set((state) => {
      if (!state.quiz) return state;
      const map = new Map(state.quiz.questions.map((q) => [q.id, q]));
      const reordered = orderedIds
        .map((id, idx) => {
          const item = map.get(id);
          return item ? { ...item, orderIndex: idx } : null;
        })
        .filter((q): q is QuestionDto => q !== null);
      return {
        quiz: { ...state.quiz, questions: reordered },
      };
    }),

  markMetadataSaved: () =>
    set((state) => {
      const hasDirtyQuestions = Object.values(state.dirtyQuestionIds).some(Boolean);
      return {
        metadataDirty: false,
        saveStatus: hasDirtyQuestions ? state.saveStatus : "saved",
      };
    }),

  markQuestionSaved: (questionId) =>
    set((state) => {
      const nextDirty = { ...state.dirtyQuestionIds };
      delete nextDirty[questionId];
      const hasDirty = state.metadataDirty || Object.values(nextDirty).some(Boolean);
      return {
        dirtyQuestionIds: nextDirty,
        saveStatus: hasDirty ? state.saveStatus : "saved",
      };
    }),

  setSaveStatus: (saveStatus) => set({ saveStatus }),
}));

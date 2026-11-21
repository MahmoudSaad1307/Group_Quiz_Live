import { describe, it, expect, beforeEach } from "vitest";
import { questionSchema, quizMetadataSchema } from "@/lib/validation/quizSchema";
import { useBuilderStore } from "@/stores/builderStore";
import type { QuizDetailDto } from "@/lib/api/quizzes";

describe("quizSchema", () => {
  it("validates a valid SingleChoice question", () => {
    const res = questionSchema.safeParse({
      type: "SingleChoice",
      text: "What is 2+2?",
      timeLimitSeconds: 20,
      points: 1000,
      options: [
        { text: "3", isCorrect: false },
        { text: "4", isCorrect: true },
      ],
    });
    expect(res.success).toBe(true);
  });

  it("rejects TrueFalse with 3 options", () => {
    const res = questionSchema.safeParse({
      type: "TrueFalse",
      text: "Sky is blue",
      timeLimitSeconds: 20,
      points: 1000,
      options: [
        { text: "True", isCorrect: true },
        { text: "False", isCorrect: false },
        { text: "Maybe", isCorrect: false },
      ],
    });
    expect(res.success).toBe(false);
  });

  it("rejects question with 0 or 2 correct options", () => {
    const res = questionSchema.safeParse({
      type: "SingleChoice",
      text: "Pick one",
      timeLimitSeconds: 20,
      points: 1000,
      options: [
        { text: "A", isCorrect: true },
        { text: "B", isCorrect: true },
      ],
    });
    expect(res.success).toBe(false);
  });

  it("validates quiz metadata", () => {
    expect(
      quizMetadataSchema.safeParse({ title: "Math", language: "Ar" }).success,
    ).toBe(true);
    expect(
      quizMetadataSchema.safeParse({ title: "", language: "En" }).success,
    ).toBe(false);
  });
});

describe("builderStore", () => {
  beforeEach(() => {
    useBuilderStore.setState({
      quiz: null,
      activeQuestionId: null,
      dirtyQuestionIds: {},
      metadataDirty: false,
      saveStatus: "saved",
    });
  });

  it("tracks dirty state and clears it when saved", () => {
    const sample: QuizDetailDto = {
      id: "quiz-1",
      title: "Sample",
      description: null,
      language: "En",
      isArchived: false,
      createdAt: "2026-01-01T00:00:00Z",
      updatedAt: "2026-01-01T00:00:00Z",
      questions: [
        {
          id: "q-1",
          orderIndex: 0,
          type: "TrueFalse",
          text: "Q1",
          timeLimitSeconds: 20,
          points: 1000,
          options: [
            { id: "o-1", orderIndex: 0, text: "True", isCorrect: true },
            { id: "o-2", orderIndex: 1, text: "False", isCorrect: false },
          ],
        },
      ],
    };

    const store = useBuilderStore.getState();
    store.setQuiz(sample);
    expect(useBuilderStore.getState().activeQuestionId).toBe("q-1");

    store.updateQuestionLocal("q-1", (q) => ({ ...q, text: "Updated Q1" }));
    expect(useBuilderStore.getState().saveStatus).toBe("dirty");
    expect(useBuilderStore.getState().dirtyQuestionIds["q-1"]).toBe(true);

    store.markQuestionSaved("q-1");
    expect(useBuilderStore.getState().saveStatus).toBe("saved");
  });
});

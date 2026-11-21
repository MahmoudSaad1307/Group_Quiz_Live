"use client";

import { useEffect, useState, use } from "react";
import Link from "next/link";
import { useTranslations } from "next-intl";
import {
  DndContext,
  closestCenter,
  KeyboardSensor,
  PointerSensor,
  useSensor,
  useSensors,
  type DragEndEvent,
} from "@dnd-kit/core";
import {
  arrayMove,
  SortableContext,
  sortableKeyboardCoordinates,
  useSortable,
  verticalListSortingStrategy,
} from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import {
  ArrowLeft,
  Check,
  Eye,
  GripVertical,
  ImagePlus,
  Plus,
  Trash2,
} from "lucide-react";
import {
  quizzesApi,
  type QuestionDto,
  type QuestionType,
  type QuizLanguage,
} from "@/lib/api/quizzes";
import { allowedPoints, questionSchema, quizMetadataSchema } from "@/lib/validation/quizSchema";
import { useBuilderStore } from "@/stores/builderStore";

function SortableQuestionItem({
  question,
  index,
  isActive,
  isInvalid,
  onSelect,
}: {
  question: QuestionDto;
  index: number;
  isActive: boolean;
  isInvalid: boolean;
  onSelect: () => void;
}) {
  const { attributes, listeners, setNodeRef, transform, transition } = useSortable({
    id: question.id,
  });

  const style = {
    transform: CSS.Transform.toString(transform),
    transition,
  };

  return (
    <div
      ref={setNodeRef}
      style={style}
      className={`flex items-center gap-2 rounded-xl border p-3 cursor-pointer transition ${
        isActive
          ? "border-indigo-500 bg-indigo-50 dark:bg-indigo-950/40"
          : "border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 hover:border-slate-300"
      }`}
      onClick={onSelect}
    >
      <button
        type="button"
        {...attributes}
        {...listeners}
        onClick={(e) => e.stopPropagation()}
        className="cursor-grab text-slate-400 hover:text-slate-600 p-1"
        aria-label={`Reorder question ${index + 1}`}
      >
        <GripVertical className="h-4 w-4" />
      </button>
      <div className="min-w-0 flex-1">
        <div className="flex items-center justify-between gap-2">
          <span className="text-xs font-semibold text-slate-500">#{index + 1}</span>
          {isInvalid && (
            <span className="rounded bg-red-100 dark:bg-red-900/40 px-1.5 py-0.5 text-[10px] font-semibold text-red-700 dark:text-red-300">
              !
            </span>
          )}
        </div>
        <p className="truncate text-sm font-medium">{question.text || "..."}</p>
      </div>
    </div>
  );
}

export default function QuizBuilderPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id: quizId } = use(params);
  const t = useTranslations("builder");
  const tCommon = useTranslations("common");
  const tQuizzes = useTranslations("quizzes");

  const {
    quiz,
    activeQuestionId,
    dirtyQuestionIds,
    metadataDirty,
    saveStatus,
    setQuiz,
    setActiveQuestionId,
    updateMetadataLocal,
    updateQuestionLocal,
    addQuestionLocal,
    removeQuestionLocal,
    reorderQuestionsLocal,
    markMetadataSaved,
    markQuestionSaved,
    setSaveStatus,
  } = useBuilderStore();

  const [loading, setLoading] = useState(true);
  const [showPreview, setShowPreview] = useState(false);
  const [uploadingImage, setUploadingImage] = useState(false);

  const sensors = useSensors(
    useSensor(PointerSensor),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  );

  useEffect(() => {
    let active = true;
    quizzesApi.get(quizId).then((data) => {
      if (active) {
        setQuiz(data);
        setLoading(false);
      }
    });
    return () => {
      active = false;
    };
  }, [quizId, setQuiz]);

  // Warn before leaving with unsaved changes
  useEffect(() => {
    const handler = (e: BeforeUnloadEvent) => {
      if (saveStatus === "dirty" || saveStatus === "saving") {
        e.preventDefault();
      }
    };
    window.addEventListener("beforeunload", handler);
    return () => window.removeEventListener("beforeunload", handler);
  }, [saveStatus]);

  // Debounced autosave (800ms)
  useEffect(() => {
    if (!quiz) return;
    const dirtyIds = Object.keys(dirtyQuestionIds).filter((k) => dirtyQuestionIds[k]);
    if (!metadataDirty && dirtyIds.length === 0) return;

    const timer = setTimeout(async () => {
      let hadError = false;
      setSaveStatus("saving");

      if (metadataDirty) {
        const metaParsed = quizMetadataSchema.safeParse({
          title: quiz.title,
          description: quiz.description,
          language: quiz.language,
        });
        if (metaParsed.success) {
          try {
            await quizzesApi.update(quiz.id, metaParsed.data);
            markMetadataSaved();
          } catch {
            hadError = true;
          }
        }
      }

      for (const qId of dirtyIds) {
        const q = quiz.questions.find((item) => item.id === qId);
        if (!q) continue;
        const parsed = questionSchema.safeParse({
          type: q.type,
          text: q.text,
          imageUrl: q.imageUrl,
          explanation: q.explanation,
          timeLimitSeconds: q.timeLimitSeconds,
          points: q.points,
          options: q.options.map((o) => ({ text: o.text, isCorrect: o.isCorrect })),
        });
        if (parsed.success) {
          try {
            await quizzesApi.updateQuestion(quiz.id, q.id, parsed.data);
            markQuestionSaved(q.id);
          } catch {
            hadError = true;
          }
        }
      }

      if (hadError) {
        setSaveStatus("error");
      }
    }, 800);

    return () => clearTimeout(timer);
  }, [
    quiz,
    metadataDirty,
    dirtyQuestionIds,
    markMetadataSaved,
    markQuestionSaved,
    setSaveStatus,
  ]);

  if (loading || !quiz) {
    return <p className="text-sm text-slate-500">{tCommon("loading")}</p>;
  }

  const activeQuestion =
    quiz.questions.find((q) => q.id === activeQuestionId) ?? null;

  const activeValidation = activeQuestion
    ? questionSchema.safeParse({
        type: activeQuestion.type,
        text: activeQuestion.text,
        imageUrl: activeQuestion.imageUrl,
        explanation: activeQuestion.explanation,
        timeLimitSeconds: activeQuestion.timeLimitSeconds,
        points: activeQuestion.points,
        options: activeQuestion.options.map((o) => ({
          text: o.text,
          isCorrect: o.isCorrect,
        })),
      })
    : null;

  async function handleAddQuestion() {
    if (!quiz) return;
    const created = await quizzesApi.addQuestion(quiz.id, {
      type: "SingleChoice",
      text: "New question",
      timeLimitSeconds: 20,
      points: 1000,
      options: [
        { text: "Option 1", isCorrect: true },
        { text: "Option 2", isCorrect: false },
      ],
    });
    addQuestionLocal(created);
  }

  async function handleDeleteQuestion(questionId: string) {
    if (!quiz) return;
    await quizzesApi.deleteQuestion(quiz.id, questionId);
    removeQuestionLocal(questionId);
  }

  async function handleDragEnd(event: DragEndEvent) {
    if (!quiz) return;
    const { active, over } = event;
    if (!over || active.id === over.id) return;

    const oldIndex = quiz.questions.findIndex((q) => q.id === active.id);
    const newIndex = quiz.questions.findIndex((q) => q.id === over.id);
    if (oldIndex < 0 || newIndex < 0) return;

    const reordered = arrayMove(quiz.questions, oldIndex, newIndex).map((q) => q.id);
    reorderQuestionsLocal(reordered);
    await quizzesApi.reorderQuestions(quiz.id, reordered);
  }

  async function handleImageUpload(file: File) {
    if (!activeQuestion) return;
    setUploadingImage(true);
    try {
      const { url } = await quizzesApi.uploadImage(file);
      updateQuestionLocal(activeQuestion.id, (q) => ({ ...q, imageUrl: url }));
    } finally {
      setUploadingImage(false);
    }
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-4 border-b border-slate-200 dark:border-slate-800 pb-4">
        <div className="flex items-center gap-3">
          <Link
            href="/admin/quizzes"
            className="inline-flex items-center gap-1.5 rounded-lg border border-slate-300 dark:border-slate-700 px-3 py-1.5 text-sm hover:bg-slate-100 dark:hover:bg-slate-800"
          >
            <ArrowLeft className="h-4 w-4" />
            {t("backToQuizzes")}
          </Link>
          <span
            className={`text-xs font-medium px-2.5 py-1 rounded-full ${
              saveStatus === "saved"
                ? "bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300"
                : saveStatus === "saving"
                  ? "bg-indigo-100 text-indigo-800"
                  : saveStatus === "error"
                    ? "bg-red-100 text-red-800"
                    : "bg-amber-100 text-amber-800"
            }`}
          >
            {t(saveStatus)}
          </span>
        </div>

        <div className="flex items-center gap-2">
          {activeQuestion && (
            <button
              type="button"
              onClick={() => setShowPreview(true)}
              className="inline-flex items-center gap-1.5 rounded-xl border border-slate-300 dark:border-slate-700 px-3.5 py-2 text-sm font-medium hover:bg-slate-100 dark:hover:bg-slate-800"
            >
              <Eye className="h-4 w-4" />
              {t("preview")}
            </button>
          )}
          <button
            type="button"
            onClick={() => void handleAddQuestion()}
            className="inline-flex items-center gap-1.5 rounded-xl bg-indigo-600 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-500"
          >
            <Plus className="h-4 w-4" />
            {t("addQuestion")}
          </button>
        </div>
      </div>

      {/* Quiz Metadata */}
      <div className="grid gap-4 sm:grid-cols-3 rounded-2xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 p-4">
        <div className="sm:col-span-2">
          <label className="block text-xs font-medium text-slate-500 mb-1">
            {tQuizzes("quizTitle")}
          </label>
          <input
            type="text"
            maxLength={120}
            value={quiz.title}
            onChange={(e) => updateMetadataLocal({ title: e.target.value })}
            className="w-full rounded-lg border border-slate-300 dark:border-slate-700 bg-transparent px-3 py-2 text-sm font-semibold"
          />
        </div>
        <div>
          <label className="block text-xs font-medium text-slate-500 mb-1">
            {tQuizzes("language")}
          </label>
          <select
            value={quiz.language}
            onChange={(e) =>
              updateMetadataLocal({ language: e.target.value as QuizLanguage })
            }
            className="w-full rounded-lg border border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-900 px-3 py-2 text-sm"
          >
            <option value="En">{tQuizzes("english")}</option>
            <option value="Ar">{tQuizzes("arabic")}</option>
          </select>
        </div>
      </div>

      {/* Main Builder Split */}
      <div className="grid gap-6 lg:grid-cols-12">
        <aside className="lg:col-span-4 space-y-2">
          <h2 className="text-sm font-semibold text-slate-500 uppercase tracking-wider">
            {t("questions")} ({quiz.questions.length})
          </h2>
          <DndContext
            sensors={sensors}
            collisionDetection={closestCenter}
            onDragEnd={(e) => void handleDragEnd(e)}
          >
            <SortableContext
              items={quiz.questions.map((q) => q.id)}
              strategy={verticalListSortingStrategy}
            >
              <div className="space-y-2">
                {quiz.questions.map((q, idx) => {
                  const valid = questionSchema.safeParse({
                    type: q.type,
                    text: q.text,
                    imageUrl: q.imageUrl,
                    explanation: q.explanation,
                    timeLimitSeconds: q.timeLimitSeconds,
                    points: q.points,
                    options: q.options.map((o) => ({
                      text: o.text,
                      isCorrect: o.isCorrect,
                    })),
                  }).success;

                  return (
                    <SortableQuestionItem
                      key={q.id}
                      question={q}
                      index={idx}
                      isActive={q.id === activeQuestion?.id}
                      isInvalid={!valid}
                      onSelect={() => setActiveQuestionId(q.id)}
                    />
                  );
                })}
              </div>
            </SortableContext>
          </DndContext>
        </aside>

        <section className="lg:col-span-8">
          {!activeQuestion ? (
            <div className="rounded-2xl border border-dashed border-slate-300 dark:border-slate-700 p-12 text-center text-slate-500">
              {t("noQuestions")}
            </div>
          ) : (
            <div className="rounded-2xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 p-6 space-y-5">
              <div className="flex flex-wrap items-center justify-between gap-3">
                <div className="flex items-center gap-3">
                  <select
                    value={activeQuestion.type}
                    onChange={(e) => {
                      const nextType = e.target.value as QuestionType;
                      updateQuestionLocal(activeQuestion.id, (q) => ({
                        ...q,
                        type: nextType,
                        options:
                          nextType === "TrueFalse"
                            ? [
                                { id: "tf-1", orderIndex: 0, text: "True", isCorrect: true },
                                { id: "tf-2", orderIndex: 1, text: "False", isCorrect: false },
                              ]
                            : q.options.slice(0, 4),
                      }));
                    }}
                    className="rounded-lg border border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-900 px-3 py-1.5 text-sm"
                  >
                    <option value="SingleChoice">{t("singleChoice")}</option>
                    <option value="TrueFalse">{t("trueFalse")}</option>
                  </select>

                  <select
                    value={activeQuestion.timeLimitSeconds}
                    onChange={(e) =>
                      updateQuestionLocal(activeQuestion.id, (q) => ({
                        ...q,
                        timeLimitSeconds: Number(e.target.value),
                      }))
                    }
                    className="rounded-lg border border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-900 px-3 py-1.5 text-sm"
                  >
                    {[5, 10, 15, 20, 30, 45, 60, 90, 120].map((sec) => (
                      <option key={sec} value={sec}>
                        {sec}s
                      </option>
                    ))}
                  </select>

                  <select
                    value={activeQuestion.points}
                    onChange={(e) =>
                      updateQuestionLocal(activeQuestion.id, (q) => ({
                        ...q,
                        points: Number(e.target.value),
                      }))
                    }
                    className="rounded-lg border border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-900 px-3 py-1.5 text-sm"
                  >
                    {allowedPoints.map((pts) => (
                      <option key={pts} value={pts}>
                        {pts === 0 ? t("pollZero") : `${pts} pts`}
                      </option>
                    ))}
                  </select>
                </div>

                <button
                  type="button"
                  onClick={() => void handleDeleteQuestion(activeQuestion.id)}
                  className="inline-flex items-center gap-1 rounded-lg px-3 py-1.5 text-sm text-red-600 hover:bg-red-50 dark:hover:bg-red-950/40"
                >
                  <Trash2 className="h-4 w-4" />
                  {t("deleteQuestion")}
                </button>
              </div>

              <div>
                <label className="block text-sm font-medium mb-1">{t("questionText")}</label>
                <textarea
                  rows={2}
                  maxLength={500}
                  value={activeQuestion.text}
                  onChange={(e) =>
                    updateQuestionLocal(activeQuestion.id, (q) => ({
                      ...q,
                      text: e.target.value,
                    }))
                  }
                  className="w-full rounded-xl border border-slate-300 dark:border-slate-700 bg-transparent px-3 py-2 text-base"
                />
              </div>

              {/* Image Upload */}
              <div className="flex flex-wrap items-center gap-4">
                <label className="inline-flex cursor-pointer items-center gap-2 rounded-lg border border-slate-300 dark:border-slate-700 px-3 py-2 text-sm hover:bg-slate-100 dark:hover:bg-slate-800">
                  <ImagePlus className="h-4 w-4" />
                  {uploadingImage ? t("saving") : t("uploadImage")}
                  <input
                    type="file"
                    accept="image/*"
                    className="hidden"
                    onChange={(e) => {
                      const file = e.target.files?.[0];
                      if (file) void handleImageUpload(file);
                    }}
                  />
                </label>
                {activeQuestion.imageUrl && (
                  <div className="flex items-center gap-3">
                    {/* eslint-disable-next-line @next/next/no-img-element */}
                    <img
                      src={activeQuestion.imageUrl}
                      alt=""
                      className="h-16 w-24 rounded-lg object-cover border border-slate-200"
                    />
                    <button
                      type="button"
                      onClick={() =>
                        updateQuestionLocal(activeQuestion.id, (q) => ({
                          ...q,
                          imageUrl: null,
                        }))
                      }
                      className="text-xs text-red-600 hover:underline"
                    >
                      {t("removeImage")}
                    </button>
                  </div>
                )}
              </div>

              {/* Options */}
              <div className="space-y-3">
                <div className="flex items-center justify-between">
                  <span className="text-sm font-medium">{t("options")}</span>
                  {activeQuestion.type === "SingleChoice" &&
                    activeQuestion.options.length < 4 && (
                      <button
                        type="button"
                        onClick={() =>
                          updateQuestionLocal(activeQuestion.id, (q) => ({
                            ...q,
                            options: [
                              ...q.options,
                              {
                                id: `opt-${Date.now()}`,
                                orderIndex: q.options.length,
                                text: `Option ${q.options.length + 1}`,
                                isCorrect: false,
                              },
                            ],
                          }))
                        }
                        className="text-xs font-semibold text-indigo-600 hover:underline"
                      >
                        + {t("addOption")}
                      </button>
                    )}
                </div>

                <div className="grid gap-3 sm:grid-cols-2">
                  {activeQuestion.options.map((opt, idx) => (
                    <div
                      key={opt.id}
                      className={`flex items-center gap-2 rounded-xl border p-2.5 ${
                        opt.isCorrect
                          ? "border-emerald-500 bg-emerald-50/50 dark:bg-emerald-950/30"
                          : "border-slate-300 dark:border-slate-700"
                      }`}
                    >
                      <button
                        type="button"
                        onClick={() =>
                          updateQuestionLocal(activeQuestion.id, (q) => ({
                            ...q,
                            options: q.options.map((o, i) => ({
                              ...o,
                              isCorrect: i === idx,
                            })),
                          }))
                        }
                        aria-label={t("markCorrect")}
                        className={`flex h-7 w-7 shrink-0 items-center justify-center rounded-full border transition ${
                          opt.isCorrect
                            ? "border-emerald-600 bg-emerald-600 text-white"
                            : "border-slate-300 dark:border-slate-600"
                        }`}
                      >
                        {opt.isCorrect && <Check className="h-4 w-4" />}
                      </button>
                      <input
                        type="text"
                        maxLength={120}
                        value={opt.text}
                        onChange={(e) =>
                          updateQuestionLocal(activeQuestion.id, (q) => ({
                            ...q,
                            options: q.options.map((o, i) =>
                              i === idx ? { ...o, text: e.target.value } : o,
                            ),
                          }))
                        }
                        className="min-w-0 flex-1 bg-transparent text-sm focus:outline-none"
                      />
                      {activeQuestion.type === "SingleChoice" &&
                        activeQuestion.options.length > 2 && (
                          <button
                            type="button"
                            onClick={() =>
                              updateQuestionLocal(activeQuestion.id, (q) => ({
                                ...q,
                                options: q.options
                                  .filter((_, i) => i !== idx)
                                  .map((o, i) => ({ ...o, orderIndex: i })),
                              }))
                            }
                            className="text-slate-400 hover:text-red-500"
                          >
                            <Trash2 className="h-4 w-4" />
                          </button>
                        )}
                    </div>
                  ))}
                </div>
              </div>

              <div>
                <label className="block text-sm font-medium mb-1">{t("explanation")}</label>
                <input
                  type="text"
                  maxLength={500}
                  value={activeQuestion.explanation ?? ""}
                  onChange={(e) =>
                    updateQuestionLocal(activeQuestion.id, (q) => ({
                      ...q,
                      explanation: e.target.value || null,
                    }))
                  }
                  className="w-full rounded-lg border border-slate-300 dark:border-slate-700 bg-transparent px-3 py-2 text-sm"
                />
              </div>

              {activeValidation && !activeValidation.success && (
                <div
                  role="alert"
                  className="rounded-xl bg-red-50 dark:bg-red-950/40 p-3 text-xs text-red-700 dark:text-red-300 space-y-1"
                >
                  {activeValidation.error.issues.map((issue, i) => (
                    <p key={i}>{issue.message}</p>
                  ))}
                </div>
              )}
            </div>
          )}
        </section>
      </div>

      {showPreview && activeQuestion && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4">
          <div className="w-full max-w-2xl rounded-2xl bg-white dark:bg-slate-900 p-6 space-y-4">
            <div className="flex items-center justify-between">
              <h3 className="text-lg font-bold">{t("previewScreen")}</h3>
              <button
                type="button"
                onClick={() => setShowPreview(false)}
                className="rounded-lg border border-slate-300 dark:border-slate-700 px-3 py-1 text-sm"
              >
                {t("closePreview")}
              </button>
            </div>
            <div className="rounded-xl bg-slate-950 text-white p-6 space-y-4 text-center">
              <p className="text-2xl font-bold">{activeQuestion.text}</p>
              {activeQuestion.imageUrl && (
                // eslint-disable-next-line @next/next/no-img-element
                <img
                  src={activeQuestion.imageUrl}
                  alt=""
                  className="mx-auto max-h-48 rounded-lg object-contain"
                />
              )}
              <div className="grid grid-cols-2 gap-3 pt-2">
                {activeQuestion.options.map((o, idx) => (
                  <div
                    key={o.id}
                    className={`rounded-xl p-4 font-semibold text-white ${
                      ["bg-red-600", "bg-blue-600", "bg-amber-600", "bg-emerald-600"][idx]
                    }`}
                  >
                    {o.text}
                  </div>
                ))}
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

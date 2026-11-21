"use client";

import { useEffect, useState, type FormEvent } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { Plus, Search, Copy, Archive, Edit3, Play } from "lucide-react";
import { quizzesApi, type QuizLanguage, type QuizSummaryDto } from "@/lib/api/quizzes";

export default function QuizzesPage() {
  const t = useTranslations("quizzes");
  const tCommon = useTranslations("common");
  const router = useRouter();

  const [quizzes, setQuizzes] = useState<QuizSummaryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [includeArchived, setIncludeArchived] = useState(false);

  const [showCreate, setShowCreate] = useState(false);
  const [newTitle, setNewTitle] = useState("");
  const [newDesc, setNewDesc] = useState("");
  const [newLang, setNewLang] = useState<QuizLanguage>("En");
  const [creating, setCreating] = useState(false);

  async function loadQuizzes(term: string, archived: boolean) {
    setLoading(true);
    try {
      const res = await quizzesApi.list({
        search: term || undefined,
        includeArchived: archived,
      });
      setQuizzes(res.items);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    const timer = setTimeout(() => {
      void loadQuizzes(search, includeArchived);
    }, 200);
    return () => clearTimeout(timer);
  }, [search, includeArchived]);

  async function handleCreate(e: FormEvent) {
    e.preventDefault();
    if (!newTitle.trim()) return;
    setCreating(true);
    try {
      const created = await quizzesApi.create({
        title: newTitle.trim(),
        description: newDesc.trim() || null,
        language: newLang,
      });
      router.push(`/admin/quizzes/${created.id}`);
    } finally {
      setCreating(false);
    }
  }

  async function handleDuplicate(id: string) {
    await quizzesApi.duplicate(id);
    await loadQuizzes(search, includeArchived);
  }

  async function handleDelete(id: string) {
    await quizzesApi.delete(id);
    await loadQuizzes(search, includeArchived);
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <h1 className="text-2xl font-bold">{t("title")}</h1>
        <button
          type="button"
          onClick={() => setShowCreate(true)}
          className="inline-flex items-center gap-2 rounded-xl bg-indigo-600 px-4 py-2.5 text-sm font-semibold text-white hover:bg-indigo-500 transition"
        >
          <Plus className="h-4 w-4" />
          {t("newQuiz")}
        </button>
      </div>

      <div className="flex flex-wrap items-center gap-4">
        <div className="relative flex-1 min-w-[220px]">
          <Search className="pointer-events-none absolute start-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
          <input
            type="search"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder={t("searchPlaceholder")}
            className="w-full rounded-xl border border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-900 ps-9 pe-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
          />
        </div>

        <label className="inline-flex items-center gap-2 text-sm text-slate-700 dark:text-slate-300">
          <input
            type="checkbox"
            checked={includeArchived}
            onChange={(e) => setIncludeArchived(e.target.checked)}
            className="rounded border-slate-300"
          />
          {t("showArchived")}
        </label>
      </div>

      {loading ? (
        <p className="text-sm text-slate-500">{tCommon("loading")}</p>
      ) : quizzes.length === 0 ? (
        <div className="rounded-2xl border border-dashed border-slate-300 dark:border-slate-700 p-12 text-center text-slate-500">
          {t("empty")}
        </div>
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {quizzes.map((quiz) => (
            <div
              key={quiz.id}
              className="flex flex-col justify-between rounded-2xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 p-5 shadow-sm"
            >
              <div>
                <div className="flex items-start justify-between gap-2">
                  <h2 className="font-semibold text-lg line-clamp-2">{quiz.title}</h2>
                  <span className="shrink-0 rounded-md bg-slate-100 dark:bg-slate-800 px-2 py-0.5 text-xs font-medium uppercase">
                    {quiz.language}
                  </span>
                </div>
                {quiz.description && (
                  <p className="mt-1 text-sm text-slate-600 dark:text-slate-400 line-clamp-2">
                    {quiz.description}
                  </p>
                )}
                <div className="mt-3 flex items-center gap-2 text-xs text-slate-500">
                  <span>{t("questionsCount", { count: quiz.questionCount })}</span>
                  {quiz.isArchived && (
                    <span className="rounded bg-amber-100 dark:bg-amber-900/40 px-1.5 py-0.5 text-amber-800 dark:text-amber-300">
                      {t("archivedBadge")}
                    </span>
                  )}
                </div>
              </div>

              <div className="mt-5 flex flex-wrap items-center gap-2 pt-3 border-t border-slate-100 dark:border-slate-800">
                <Link
                  href={`/admin/quizzes/${quiz.id}`}
                  className="inline-flex items-center gap-1.5 rounded-lg bg-slate-100 dark:bg-slate-800 px-3 py-1.5 text-xs font-medium hover:bg-slate-200 dark:hover:bg-slate-700"
                >
                  <Edit3 className="h-3.5 w-3.5" />
                  {t("edit")}
                </Link>
                {quiz.questionCount > 0 && !quiz.isArchived && (
                  <Link
                    href={`/admin/sessions/new?quizId=${quiz.id}`}
                    className="inline-flex items-center gap-1.5 rounded-lg bg-indigo-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-indigo-500"
                  >
                    <Play className="h-3.5 w-3.5" />
                    {t("host")}
                  </Link>
                )}
                <button
                  type="button"
                  onClick={() => void handleDuplicate(quiz.id)}
                  className="inline-flex items-center gap-1 rounded-lg px-2.5 py-1.5 text-xs text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800"
                  title={t("duplicate")}
                >
                  <Copy className="h-3.5 w-3.5" />
                </button>
                <button
                  type="button"
                  onClick={() => void handleDelete(quiz.id)}
                  className="inline-flex items-center gap-1 rounded-lg px-2.5 py-1.5 text-xs text-red-600 hover:bg-red-50 dark:hover:bg-red-950/40"
                  title={t("archive")}
                >
                  <Archive className="h-3.5 w-3.5" />
                </button>
              </div>
            </div>
          ))}
        </div>
      )}

      {showCreate && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
          <form
            onSubmit={handleCreate}
            className="w-full max-w-md rounded-2xl bg-white dark:bg-slate-900 p-6 shadow-xl space-y-4"
          >
            <h2 className="text-xl font-bold">{t("createTitle")}</h2>
            <div>
              <label className="block text-sm font-medium mb-1">{t("quizTitle")}</label>
              <input
                type="text"
                required
                maxLength={120}
                value={newTitle}
                onChange={(e) => setNewTitle(e.target.value)}
                className="w-full rounded-lg border border-slate-300 dark:border-slate-700 bg-transparent px-3 py-2 text-sm"
              />
            </div>
            <div>
              <label className="block text-sm font-medium mb-1">{t("description")}</label>
              <textarea
                maxLength={500}
                rows={2}
                value={newDesc}
                onChange={(e) => setNewDesc(e.target.value)}
                className="w-full rounded-lg border border-slate-300 dark:border-slate-700 bg-transparent px-3 py-2 text-sm"
              />
            </div>
            <div>
              <label className="block text-sm font-medium mb-1">{t("language")}</label>
              <select
                value={newLang}
                onChange={(e) => setNewLang(e.target.value as QuizLanguage)}
                className="w-full rounded-lg border border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-900 px-3 py-2 text-sm"
              >
                <option value="En">{t("english")}</option>
                <option value="Ar">{t("arabic")}</option>
              </select>
            </div>
            <div className="flex justify-end gap-2 pt-2">
              <button
                type="button"
                onClick={() => setShowCreate(false)}
                className="rounded-lg border border-slate-300 dark:border-slate-700 px-4 py-2 text-sm"
              >
                {tCommon("cancel")}
              </button>
              <button
                type="submit"
                disabled={creating}
                className="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-500 disabled:opacity-50"
              >
                {t("create")}
              </button>
            </div>
          </form>
        </div>
      )}
    </div>
  );
}

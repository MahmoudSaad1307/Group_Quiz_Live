"use client";

import { useState, type FormEvent } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { useTranslations } from "next-intl";
import { sessionsApi, type SessionSettings } from "@/lib/api/sessions";

export default function NewSessionPage() {
  const t = useTranslations("session");
  const router = useRouter();
  const searchParams = useSearchParams();
  const quizId = searchParams.get("quizId") ?? "";

  const [settings, setSettings] = useState<SessionSettings>({
    showQuestionOnPhones: false,
    shuffleQuestions: false,
    shuffleOptions: false,
    streakBonus: true,
    allowLateJoin: false,
    autoCloseWhenAllAnswered: true,
    autoAdvance: false,
    autoAdvanceDelaySeconds: 5,
  });
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (!quizId) return;
    setSubmitting(true);
    try {
      const created = await sessionsApi.create(quizId, settings);
      router.push(`/admin/sessions/${created.sessionId}/host`);
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="mx-auto max-w-xl rounded-2xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 p-6">
      <h1 className="text-2xl font-bold">{t("newTitle")}</h1>
      <form onSubmit={handleSubmit} className="mt-6 space-y-4">
        {(
          [
            ["showQuestionOnPhones", t("showQuestionOnPhones")],
            ["shuffleQuestions", t("shuffleQuestions")],
            ["shuffleOptions", t("shuffleOptions")],
            ["streakBonus", t("streakBonus")],
            ["allowLateJoin", t("allowLateJoin")],
            ["autoCloseWhenAllAnswered", t("autoClose")],
            ["autoAdvance", t("autoAdvance")],
          ] as const
        ).map(([key, label]) => (
          <label key={key} className="flex items-center justify-between gap-3 py-1">
            <span className="text-sm font-medium">{label}</span>
            <input
              type="checkbox"
              checked={settings[key]}
              onChange={(e) =>
                setSettings((prev) => ({ ...prev, [key]: e.target.checked }))
              }
              className="h-5 w-5 rounded border-slate-300 text-indigo-600"
            />
          </label>
        ))}

        {settings.autoAdvance && (
          <div>
            <label className="block text-sm font-medium mb-1">
              {t("autoAdvanceDelay")}
            </label>
            <input
              type="number"
              min={2}
              max={30}
              value={settings.autoAdvanceDelaySeconds}
              onChange={(e) =>
                setSettings((prev) => ({
                  ...prev,
                  autoAdvanceDelaySeconds: Number(e.target.value),
                }))
              }
              className="w-32 rounded-lg border border-slate-300 dark:border-slate-700 bg-transparent px-3 py-1.5 text-sm"
            />
          </div>
        )}

        <button
          type="submit"
          disabled={submitting || !quizId}
          className="w-full rounded-xl bg-indigo-600 px-5 py-3 text-sm font-semibold text-white hover:bg-indigo-500 disabled:opacity-50"
        >
          {submitting ? t("launching") : t("launch")}
        </button>
      </form>
    </div>
  );
}

"use client";

import { useEffect, useState, use } from "react";
import { useTranslations } from "next-intl";
import { Download } from "lucide-react";
import { sessionsApi, type SessionResultsDto } from "@/lib/api/sessions";

export default function SessionResultsPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id: sessionId } = use(params);
  const t = useTranslations("results");
  const [results, setResults] = useState<SessionResultsDto | null>(null);

  useEffect(() => {
    sessionsApi.getResults(sessionId).then(setResults);
  }, [sessionId]);

  if (!results) {
    return <p className="text-sm text-slate-500">Loading...</p>;
  }

  return (
    <div className="space-y-8">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold">{t("title")}</h1>
          <p className="text-sm text-slate-500">
            PIN: <bdi dir="ltr">{results.pin}</bdi>
          </p>
        </div>
        <div className="flex gap-2">
          <a
            href={`/api/sessions/${sessionId}/export?format=csv`}
            className="inline-flex items-center gap-1.5 rounded-xl border border-slate-300 dark:border-slate-700 px-4 py-2 text-sm font-semibold hover:bg-slate-100 dark:hover:bg-slate-800"
          >
            <Download className="h-4 w-4" />
            {t("exportCsv")}
          </a>
          <a
            href={`/api/sessions/${sessionId}/export?format=xlsx`}
            className="inline-flex items-center gap-1.5 rounded-xl bg-indigo-600 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-500"
          >
            <Download className="h-4 w-4" />
            {t("exportXlsx")}
          </a>
        </div>
      </div>

      <section className="space-y-3">
        <h2 className="text-lg font-bold">{t("students")}</h2>
        <div className="overflow-x-auto rounded-2xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900">
          <table className="w-full text-sm text-start">
            <thead className="border-b border-slate-200 dark:border-slate-800 text-slate-500">
              <tr>
                <th className="px-4 py-3 text-start">{t("rank")}</th>
                <th className="px-4 py-3 text-start">{t("nickname")}</th>
                <th className="px-4 py-3 text-start">{t("score")}</th>
                <th className="px-4 py-3 text-start">{t("correct")}</th>
                <th className="px-4 py-3 text-start">{t("avgTime")}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
              {results.students.map((s) => (
                <tr key={s.playerId}>
                  <td className="px-4 py-3 font-bold">#{s.rank}</td>
                  <td className="px-4 py-3 font-medium">{s.nickname}</td>
                  <td className="px-4 py-3 font-bold text-indigo-600">{s.totalScore}</td>
                  <td className="px-4 py-3">{s.correctCount}</td>
                  <td className="px-4 py-3">{s.avgResponseTimeMs}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      <section className="space-y-3">
        <h2 className="text-lg font-bold">{t("questions")}</h2>
        <div className="grid gap-4 md:grid-cols-2">
          {results.questions.map((q) => (
            <div
              key={q.questionId}
              className="rounded-2xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 p-5 space-y-3"
            >
              <div className="flex items-start justify-between gap-2">
                <h3 className="font-semibold">
                  #{q.orderIndex + 1} {q.text}
                </h3>
                <span className="shrink-0 rounded-full bg-emerald-100 dark:bg-emerald-950 px-2.5 py-0.5 text-xs font-bold text-emerald-700 dark:text-emerald-300">
                  {t("correctPct", { pct: q.correctPercent })}
                </span>
              </div>
              <div className="space-y-1.5">
                {q.options.map((o) => (
                  <div key={o.optionId} className="flex items-center justify-between text-xs">
                    <span className={o.isCorrect ? "font-bold text-emerald-600" : ""}>
                      {o.text}
                    </span>
                    <span className="font-semibold">{o.count}</span>
                  </div>
                ))}
              </div>
            </div>
          ))}
        </div>
      </section>
    </div>
  );
}

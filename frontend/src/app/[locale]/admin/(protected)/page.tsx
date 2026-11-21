import Link from "next/link";
import { useTranslations } from "next-intl";

export default function AdminDashboardPage() {
  const t = useTranslations("admin");

  return (
    <div className="rounded-2xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 p-8 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
      <div>
        <h1 className="text-2xl font-bold">{t("dashboardTitle")}</h1>
        <p className="mt-2 text-slate-600 dark:text-slate-400">
          {t("dashboardSubtitle")}
        </p>
      </div>
      <Link
        href="/admin/quizzes"
        className="inline-flex items-center justify-center rounded-xl bg-indigo-600 px-5 py-3 text-sm font-semibold text-white hover:bg-indigo-500 transition"
      >
        {t("goToQuizzes")}
      </Link>
    </div>
  );
}

"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useLocale, useTranslations } from "next-intl";
import { authApi, type AdminProfile } from "@/lib/api/client";

export function AdminHeader({ profile }: { profile: AdminProfile }) {
  const tCommon = useTranslations("common");
  const tAuth = useTranslations("auth");
  const tAdmin = useTranslations("admin");
  const router = useRouter();
  const pathname = usePathname();
  const locale = useLocale();

  async function handleLogout() {
    await authApi.logout();
    router.push("/admin/login");
    router.refresh();
  }

  function switchLocale(nextLocale: "en" | "ar") {
    const withoutLocale = pathname.replace(/^\/(en|ar)(?=\/|$)/, "") || "/";
    router.push(`/${nextLocale}${withoutLocale}`);
  }

  return (
    <header className="border-b border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900">
      <div className="mx-auto flex max-w-6xl items-center justify-between px-4 py-3 gap-4">
        <div className="flex items-center gap-6">
          <Link
            href="/admin/quizzes"
            className="text-lg font-bold text-indigo-600 dark:text-indigo-400"
          >
            {tCommon("appName")}
          </Link>
          <nav className="flex items-center gap-4 text-sm font-medium">
            <Link
              href="/admin/quizzes"
              className="text-slate-700 dark:text-slate-200 hover:text-indigo-600 transition"
            >
              {tAdmin("quizzesNav")}
            </Link>
          </nav>
        </div>

        <div className="flex items-center gap-3">
          <button
            type="button"
            onClick={() => switchLocale(locale === "en" ? "ar" : "en")}
            className="rounded-md border border-slate-300 dark:border-slate-700 px-2.5 py-1 text-xs font-semibold hover:bg-slate-100 dark:hover:bg-slate-800"
          >
            {locale === "en" ? "العربية" : "EN"}
          </button>
          <span className="hidden sm:inline text-sm text-slate-600 dark:text-slate-300">
            {tAuth("welcome", { name: profile.displayName })}
          </span>
          <button
            type="button"
            onClick={handleLogout}
            className="rounded-lg border border-slate-300 dark:border-slate-700 px-3 py-1.5 text-sm font-medium hover:bg-slate-100 dark:hover:bg-slate-800 transition"
          >
            {tCommon("signOut")}
          </button>
        </div>
      </div>
    </header>
  );
}

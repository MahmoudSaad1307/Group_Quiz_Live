import Link from "next/link";
import { useTranslations } from "next-intl";

export default function HomePage() {
  const t = useTranslations("common");

  return (
    <main className="min-h-screen flex flex-col items-center justify-center p-6 text-center gap-6">
      <h1 className="text-4xl font-bold tracking-tight">{t("appName")}</h1>
      <div className="flex gap-4">
        <Link
          href="/admin/login"
          className="rounded-lg bg-indigo-600 px-5 py-3 text-white font-semibold hover:bg-indigo-500 transition"
        >
          Host Sign In
        </Link>
      </div>
    </main>
  );
}

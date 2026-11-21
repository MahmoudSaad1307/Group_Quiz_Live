"use client";

import { useState, type FormEvent } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { useTranslations } from "next-intl";
import { createPlayConnection } from "@/lib/realtime/signalrClients";
import type { JoinSessionResult } from "@/lib/realtime/events";
import { usePlayStore } from "@/stores/sessionStores";

export default function JoinPage() {
  const t = useTranslations("join");
  const router = useRouter();
  const searchParams = useSearchParams();
  const setJoined = usePlayStore((s) => s.setJoined);

  const [pin, setPin] = useState(searchParams.get("pin") ?? "");
  const [nickname, setNickname] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);

    const conn = createPlayConnection();
    try {
      await conn.start();
      const res = await conn.invoke<JoinSessionResult>(
        "JoinSession",
        pin.trim(),
        nickname.trim(),
        null,
      );

      if (!res.succeeded) {
        const key = res.errorCode ?? "SessionNotFound";
        setError(t(`errors.${key}`));
        await conn.stop();
        return;
      }

      if (res.token) {
        localStorage.setItem(
          "ql_player_auth",
          JSON.stringify({
            sessionId: res.sessionId,
            pin: pin.trim(),
            token: res.token,
            nickname: res.nickname,
          }),
        );
      }

      setJoined({
        sessionId: res.sessionId,
        playerId: res.playerId,
        nickname: res.nickname,
        phase: res.phase,
      });

      await conn.stop();
      router.push("/play");
    } catch {
      setError(t("errors.SessionNotFound"));
      await conn.stop().catch(() => {});
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <main className="min-h-screen flex items-center justify-center bg-indigo-700 p-4">
      <div className="w-full max-w-sm rounded-3xl bg-white dark:bg-slate-900 p-6 shadow-xl">
        <h1 className="text-center text-2xl font-black">{t("title")}</h1>
        <form onSubmit={handleSubmit} className="mt-6 space-y-4">
          <div>
            <label htmlFor="pin" className="block text-sm font-medium mb-1">
              {t("pin")}
            </label>
            <input
              id="pin"
              type="text"
              inputMode="numeric"
              dir="ltr"
              required
              maxLength={6}
              value={pin}
              onChange={(e) => setPin(e.target.value.replace(/\D/g, ""))}
              className="w-full rounded-xl border border-slate-300 dark:border-slate-700 bg-transparent px-4 py-3.5 text-center text-2xl font-black tracking-widest"
            />
          </div>

          <div>
            <label htmlFor="nickname" className="block text-sm font-medium mb-1">
              {t("nickname")}
            </label>
            <input
              id="nickname"
              type="text"
              required
              minLength={2}
              maxLength={20}
              value={nickname}
              onChange={(e) => setNickname(e.target.value)}
              className="w-full rounded-xl border border-slate-300 dark:border-slate-700 bg-transparent px-4 py-3.5 text-center text-lg font-bold"
            />
          </div>

          {error && (
            <p
              role="alert"
              className="rounded-xl bg-red-50 dark:bg-red-950/50 p-3 text-center text-sm font-medium text-red-700 dark:text-red-300"
            >
              {error}
            </p>
          )}

          <button
            type="submit"
            disabled={submitting}
            className="w-full min-h-[56px] rounded-xl bg-slate-900 dark:bg-indigo-600 px-4 py-3.5 text-base font-bold text-white hover:opacity-90 disabled:opacity-50"
          >
            {submitting ? t("joining") : t("submit")}
          </button>
        </form>
      </div>
    </main>
  );
}

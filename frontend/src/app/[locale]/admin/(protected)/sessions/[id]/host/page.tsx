"use client";

import Link from "next/link";
import { useEffect, useRef, useState, use } from "react";
import { useTranslations } from "next-intl";
import { QRCodeSVG } from "qrcode.react";
import { Copy, ExternalLink, Pause, Play, SkipForward, Square, UserX, ArrowRight } from "lucide-react";
import { sessionsApi, type SessionDetailDto } from "@/lib/api/sessions";
import { createHostConnection } from "@/lib/realtime/signalrClients";
import type {
  AnswerProgressPayload,
  HostQuestionViewPayload,
  HostRehydrateDto,
  LeaderboardUpdatedPayload,
  LobbyUpdatedPayload,
  QuestionClosedPayload,
  SessionFinishedPayload,
} from "@/lib/realtime/events";
import { useHostStore } from "@/stores/sessionStores";
import type * as signalR from "@microsoft/signalr";

export default function HostControlPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id: sessionId } = use(params);
  const t = useTranslations("host");
  const [session, setSession] = useState<SessionDetailDto | null>(null);
  const [copied, setCopied] = useState(false);
  const connRef = useRef<signalR.HubConnection | null>(null);

  const {
    phase,
    playerCount,
    players,
    currentQuestion,
    progress,
    closed,
    leaderboard,
    podium,
    setConnected,
    setLobby,
    onQuestionOpened,
    onAnswerProgress,
    onQuestionClosed,
    onLeaderboard,
    onPaused,
    onResumed,
    onFinished,
    rehydrate,
    reset,
  } = useHostStore();

  useEffect(() => {
    let active = true;
    reset();

    sessionsApi.get(sessionId).then((data) => {
      if (active) setSession(data);
    });

    const conn = createHostConnection();
    connRef.current = conn;

    conn.on("LobbyUpdated", (payload: LobbyUpdatedPayload) => {
      setLobby(payload.playerCount, payload.players);
    });
    conn.on("QuestionOpened", (payload: HostQuestionViewPayload) => {
      onQuestionOpened(payload);
    });
    conn.on("AnswerProgress", (payload: AnswerProgressPayload) => {
      onAnswerProgress(payload);
    });
    conn.on("QuestionClosed", (payload: QuestionClosedPayload) => {
      onQuestionClosed(payload);
    });
    conn.on("LeaderboardUpdated", (payload: LeaderboardUpdatedPayload) => {
      onLeaderboard(payload);
    });
    conn.on("SessionPaused", () => onPaused());
    conn.on("SessionResumed", (p: { remainingMs: number; serverNowMs: number }) => {
      onResumed(p.remainingMs, p.serverNowMs);
    });
    conn.on("SessionFinished", (payload: SessionFinishedPayload) => {
      onFinished(payload.podium);
    });

    const applySnapshot = (snap: HostRehydrateDto | null) => {
      if (!snap || !active) return;
      rehydrate({
        phase: snap.phase,
        lobby: snap.lobby.players,
        playerCount: snap.lobby.playerCount,
        currentQuestion: snap.currentQuestion,
        progress: snap.progress,
        closed: snap.closed,
        leaderboard: snap.leaderboard,
        podium: snap.podium,
        serverNowMs: snap.serverNowMs,
      });
    };

    conn.onreconnected(async () => {
      setConnected(true);
      applySnapshot(await conn.invoke<HostRehydrateDto | null>("Rehydrate", sessionId));
    });

    conn.start().then(async () => {
      if (!active) return;
      setConnected(true);
      applySnapshot(await conn.invoke<HostRehydrateDto | null>("JoinHostSession", sessionId));
    });

    return () => {
      active = false;
      void conn.stop();
    };
  }, [
    sessionId,
    reset,
    setConnected,
    setLobby,
    onQuestionOpened,
    onAnswerProgress,
    onQuestionClosed,
    onLeaderboard,
    onPaused,
    onResumed,
    onFinished,
    rehydrate,
  ]);

  const joinUrl =
    typeof window !== "undefined" && session
      ? `${window.location.origin}/join?pin=${session.pin}`
      : "";

  async function handleCopy() {
    if (!joinUrl) return;
    await navigator.clipboard.writeText(joinUrl);
    setCopied(true);
    setTimeout(() => setCopied(false), 1500);
  }

  async function handleOpenBigScreen() {
    if (!session) return;
    const { token } = await sessionsApi.issueScreenToken(sessionId);
    window.open(
      `/screen/${sessionId}?t=${encodeURIComponent(token)}&pin=${encodeURIComponent(session.pin)}`,
      "_blank",
      "noopener",
    );
  }

  if (!session) {
    return <p className="text-sm text-slate-500">Loading...</p>;
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 px-5 py-3">
        <div className="flex items-center gap-4">
          <span className="text-xs font-semibold uppercase text-slate-500">
            {t("pinLabel")}:
          </span>
          <bdi dir="ltr" className="text-2xl font-black tracking-widest text-indigo-600">
            {session.pin}
          </bdi>
          <span className="rounded-full bg-slate-100 dark:bg-slate-800 px-3 py-1 text-xs font-semibold">
            {phase}
          </span>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <button
            type="button"
            onClick={() => void handleOpenBigScreen()}
            className="inline-flex items-center gap-1.5 rounded-lg border border-slate-300 dark:border-slate-700 px-3 py-1.5 text-xs font-medium"
          >
            <ExternalLink className="h-3.5 w-3.5" />
            {t("openScreen")}
          </button>
          {phase !== "Lobby" && phase !== "Finished" && (
            <button
              type="button"
              onClick={() => {
                if (window.confirm(t("confirmEnd"))) {
                  void connRef.current?.invoke("EndSession", sessionId);
                }
              }}
              className="inline-flex items-center gap-1.5 rounded-lg bg-red-600 px-3 py-1.5 text-xs font-semibold text-white"
            >
              <Square className="h-3.5 w-3.5" />
              {t("end")}
            </button>
          )}
        </div>
      </div>

      {phase === "Lobby" && (
        <div className="grid gap-6 md:grid-cols-12">
          <div className="md:col-span-5 rounded-2xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 p-6 flex flex-col items-center text-center gap-4">
            {joinUrl && (
              <div className="rounded-xl bg-white p-3 border border-slate-200">
                <QRCodeSVG value={joinUrl} size={160} />
              </div>
            )}
            <button
              type="button"
              onClick={() => void handleCopy()}
              className="inline-flex items-center gap-1.5 rounded-xl border border-slate-300 dark:border-slate-700 px-3.5 py-2 text-sm font-medium"
            >
              <Copy className="h-4 w-4" />
              {copied ? t("copied") : t("copyLink")}
            </button>
          </div>

          <div className="md:col-span-7 rounded-2xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 p-6 flex flex-col justify-between gap-4">
            <div>
              <div className="flex items-center justify-between">
                <h1 className="text-xl font-bold">{t("lobbyTitle")}</h1>
                <span className="rounded-full bg-indigo-100 dark:bg-indigo-950 px-3 py-1 text-sm font-semibold text-indigo-700 dark:text-indigo-300">
                  {t("playersJoined", { count: playerCount })}
                </span>
              </div>
              {players.length === 0 ? (
                <p className="mt-8 text-center text-sm text-slate-500">{t("needOnePlayer")}</p>
              ) : (
                <div className="mt-4 flex flex-wrap gap-2 max-h-72 overflow-y-auto">
                  {players.map((p) => (
                    <div
                      key={p.id}
                      className="inline-flex items-center gap-2 rounded-full border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 px-3 py-1.5 text-sm font-medium"
                    >
                      <span className="max-w-[160px] truncate">{p.nickname}</span>
                      <button
                        type="button"
                        onClick={() => void connRef.current?.invoke("KickPlayer", sessionId, p.id)}
                        className="text-slate-400 hover:text-red-500"
                        aria-label={`${t("kick")} ${p.nickname}`}
                      >
                        <UserX className="h-3.5 w-3.5" />
                      </button>
                    </div>
                  ))}
                </div>
              )}
            </div>

            <button
              type="button"
              disabled={playerCount === 0}
              onClick={() => void connRef.current?.invoke("StartSession", sessionId)}
              className="inline-flex w-full items-center justify-center gap-2 rounded-xl bg-emerald-600 px-5 py-3.5 text-base font-bold text-white hover:bg-emerald-500 disabled:opacity-40"
            >
              <Play className="h-5 w-5" />
              {t("startGame")}
            </button>
          </div>
        </div>
      )}

      {(phase === "QuestionOpen" || phase === "Paused" || phase === "QuestionClosed") &&
        currentQuestion && (
          <div className="rounded-2xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 p-6 space-y-5">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <span className="text-sm font-semibold text-slate-500">
                #{currentQuestion.questionIndex + 1} / {currentQuestion.total}
              </span>
              {progress && (
                <span className="rounded-full bg-indigo-100 dark:bg-indigo-950 px-3 py-1 text-sm font-bold text-indigo-700 dark:text-indigo-300">
                  {t("answeredCounter", { answered: progress.answered, total: progress.totalActive })}
                </span>
              )}
            </div>

            <h2 className="text-2xl font-bold">{currentQuestion.text}</h2>

            <div className="grid gap-3 sm:grid-cols-2">
              {currentQuestion.options.map((opt) => {
                const isCorrect = opt.id === currentQuestion.correctOptionId;
                const count = closed?.distribution[opt.id] ?? 0;
                return (
                  <div
                    key={opt.id}
                    className={`rounded-xl border p-4 flex items-center justify-between ${
                      isCorrect
                        ? "border-emerald-500 bg-emerald-50 dark:bg-emerald-950/30"
                        : "border-slate-200 dark:border-slate-800"
                    }`}
                  >
                    <span className="font-medium">{opt.text}</span>
                    {closed && <span className="font-bold text-sm">{count}</span>}
                  </div>
                );
              })}
            </div>

            <div className="flex flex-wrap gap-3 pt-2">
              {phase === "QuestionOpen" && (
                <>
                  <button
                    type="button"
                    onClick={() => void connRef.current?.invoke("Pause", sessionId)}
                    className="inline-flex items-center gap-2 rounded-xl border border-slate-300 dark:border-slate-700 px-4 py-2.5 text-sm font-semibold"
                  >
                    <Pause className="h-4 w-4" />
                    {t("pause")}
                  </button>
                  <button
                    type="button"
                    onClick={() => void connRef.current?.invoke("SkipQuestion", sessionId)}
                    className="inline-flex items-center gap-2 rounded-xl bg-amber-600 px-4 py-2.5 text-sm font-semibold text-white"
                  >
                    <SkipForward className="h-4 w-4" />
                    {t("skip")}
                  </button>
                </>
              )}
              {phase === "Paused" && (
                <button
                  type="button"
                  onClick={() => void connRef.current?.invoke("Resume", sessionId)}
                  className="inline-flex items-center gap-2 rounded-xl bg-emerald-600 px-4 py-2.5 text-sm font-semibold text-white"
                >
                  <Play className="h-4 w-4" />
                  {t("resume")}
                </button>
              )}
              {phase === "QuestionClosed" && (
                <button
                  type="button"
                  onClick={() => void connRef.current?.invoke("NextQuestion", sessionId)}
                  className="inline-flex items-center gap-2 rounded-xl bg-indigo-600 px-5 py-2.5 text-sm font-semibold text-white"
                >
                  {t("next")}
                  <ArrowRight className="h-4 w-4" />
                </button>
              )}
            </div>
          </div>
        )}

      {phase === "Leaderboard" && leaderboard && (
        <div className="rounded-2xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 p-6 space-y-4">
          <h2 className="text-xl font-bold">Top 5</h2>
          <div className="space-y-2">
            {leaderboard.top5.map((entry, idx) => (
              <div
                key={idx}
                className="flex items-center justify-between rounded-xl bg-slate-50 dark:bg-slate-800 px-4 py-3"
              >
                <span className="font-bold">
                  #{entry.rank} {entry.nickname}
                </span>
                <span className="font-black text-indigo-600">{entry.score}</span>
              </div>
            ))}
          </div>
          <button
            type="button"
            onClick={() => void connRef.current?.invoke("NextQuestion", sessionId)}
            className="inline-flex items-center gap-2 rounded-xl bg-indigo-600 px-5 py-2.5 text-sm font-semibold text-white"
          >
            {t("next")}
            <ArrowRight className="h-4 w-4" />
          </button>
        </div>
      )}

      {phase === "Finished" && podium && (
        <div className="rounded-2xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 p-6 space-y-4">
          <div className="flex items-center justify-between">
            <h2 className="text-2xl font-bold">Final Podium</h2>
            <Link
              href={`/admin/sessions/${sessionId}/results`}
              className="rounded-xl bg-indigo-600 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-500"
            >
              View Full Results &amp; Export
            </Link>
          </div>
          <div className="space-y-2">
            {podium.map((p) => (
              <div
                key={p.rank}
                className="flex items-center justify-between rounded-xl bg-amber-50 dark:bg-amber-950/30 px-4 py-3"
              >
                <span className="font-bold">
                  #{p.rank} {p.nickname}
                </span>
                <span className="font-black">{p.score}</span>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

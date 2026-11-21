"use client";

import { useEffect, useState, use } from "react";
import { useSearchParams } from "next/navigation";
import { useTranslations } from "next-intl";
import { QRCodeSVG } from "qrcode.react";
import { motion, useReducedMotion } from "framer-motion";
import { Volume2, VolumeX } from "lucide-react";
import { soundEngine } from "@/lib/sound";
import { createPlayConnection } from "@/lib/realtime/signalrClients";
import { getRemainingMs } from "@/lib/clock";
import type {
  AnswerProgressPayload,
  LeaderboardUpdatedPayload,
  LobbyUpdatedPayload,
  QuestionClosedPayload,
  QuestionOpenedPayload,
  ScreenRehydrateDto,
  SessionFinishedPayload,
} from "@/lib/realtime/events";
import { useScreenStore } from "@/stores/sessionStores";

export default function BigScreenPage({
  params,
}: {
  params: Promise<{ sessionId: string }>;
}) {
  const { sessionId } = use(params);
  const searchParams = useSearchParams();
  const t = useTranslations("screen");
  const pin = searchParams.get("pin") ?? "";

  const {
    phase,
    playerCount,
    players,
    currentQuestion,
    progress,
    closed,
    leaderboard,
    podium,
    serverOffsetMs,
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
  } = useScreenStore();

  const [remainingSec, setRemainingSec] = useState(0);
  const [soundOn, setSoundOn] = useState(false);
  const reduceMotion = useReducedMotion();

  useEffect(() => {
    const queryToken = searchParams.get("t");
    const storageKey = `ql_screen_token_${sessionId}`;
    const token = queryToken ?? sessionStorage.getItem(storageKey) ?? "";

    if (queryToken) {
      sessionStorage.setItem(storageKey, queryToken);
      window.history.replaceState({}, "", `/screen/${sessionId}`);
    }

    if (!token) return;

    const conn = createPlayConnection(token);
    conn.on("LobbyUpdated", (p: LobbyUpdatedPayload) => setLobby(p.playerCount, p.players));
    conn.on("QuestionOpened", (p: QuestionOpenedPayload) => onQuestionOpened(p));
    conn.on("AnswerProgress", (p: AnswerProgressPayload) => onAnswerProgress(p));
    conn.on("QuestionClosed", (p: QuestionClosedPayload) => {
      onQuestionClosed(p);
      soundEngine.playReveal();
    });
    conn.on("LeaderboardUpdated", (p: LeaderboardUpdatedPayload) => onLeaderboard(p));
    conn.on("SessionPaused", () => onPaused());
    conn.on("SessionResumed", (p: { remainingMs: number; serverNowMs: number }) =>
      onResumed(p.remainingMs, p.serverNowMs),
    );
    conn.on("SessionFinished", (p: SessionFinishedPayload) => {
      onFinished(p.podium);
      soundEngine.playFinish();
      if (!reduceMotion) {
        void import("canvas-confetti").then((m) => m.default({ particleCount: 120, spread: 80 }));
      }
    });

    const applySnapshot = (snapshot: ScreenRehydrateDto | null) => {
      if (!snapshot) return;
      rehydrate({
        phase: snapshot.phase,
        lobby: snapshot.lobby.players,
        playerCount: snapshot.lobby.playerCount,
        currentQuestion: snapshot.currentQuestion,
        progress: snapshot.progress,
        closed: snapshot.closed,
        leaderboard: snapshot.leaderboard,
        podium: snapshot.podium,
        serverNowMs: snapshot.serverNowMs,
      });
    };

    conn.onreconnected(async () => {
      setConnected(true);
      applySnapshot(await conn.invoke<ScreenRehydrateDto | null>("GetScreenLobby"));
    });

    conn.start().then(async () => {
      setConnected(true);
      applySnapshot(await conn.invoke<ScreenRehydrateDto | null>("GetScreenLobby"));
    });

    return () => {
      void conn.stop();
    };
  }, [
    sessionId,
    searchParams,
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
    reduceMotion,
  ]);

  useEffect(() => {
    if (phase !== "QuestionOpen" || !currentQuestion) return;
    let rafId = 0;
    const tick = () => {
      const ms = getRemainingMs(
        currentQuestion.startedAtServerMs,
        currentQuestion.durationMs,
        serverOffsetMs,
      );
      setRemainingSec(Math.ceil(ms / 1000));
      if (ms > 0) rafId = requestAnimationFrame(tick);
    };
    rafId = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(rafId);
  }, [phase, currentQuestion, serverOffsetMs]);

  const joinUrl =
    typeof window !== "undefined" ? `${window.location.origin}/join` : "";

  return (
    <main className="min-h-screen bg-slate-950 text-white p-8 flex flex-col justify-between relative">
      <button
        type="button"
        onClick={() => {
          soundEngine.unlock();
          const next = !soundOn;
          soundEngine.enabled = next;
          setSoundOn(next);
        }}
        className="fixed top-4 end-4 z-20 rounded-full bg-slate-800/80 p-2.5 text-slate-300 hover:text-white"
        aria-label="Toggle sound"
      >
        {soundOn ? <Volume2 className="h-5 w-5" /> : <VolumeX className="h-5 w-5" />}
      </button>
      {phase === "Lobby" && (
        <>
          <div className="flex flex-wrap items-center justify-between gap-6 rounded-3xl bg-slate-900 p-8 border border-slate-800">
            <div>
              <p className="text-lg text-slate-400">{t("joinAt")}</p>
              <p className="text-2xl font-bold text-indigo-400">{joinUrl}</p>
              {pin && (
                <p className="mt-3 text-6xl font-black tracking-widest">
                  <bdi dir="ltr">{pin}</bdi>
                </p>
              )}
            </div>
            {joinUrl && (
              <div className="rounded-2xl bg-white p-4">
                <QRCodeSVG value={joinUrl} size={140} />
              </div>
            )}
          </div>

          <div className="my-8 flex-1 flex flex-col items-center justify-center gap-8">
            <div className="rounded-full bg-indigo-600/20 border border-indigo-500/40 px-8 py-3 text-3xl font-bold">
              {t("players", { count: playerCount })}
            </div>
            {players.length === 0 ? (
              <p className="text-2xl text-slate-400">{t("waitingForPlayers")}</p>
            ) : (
              <div className="flex flex-wrap justify-center gap-4 max-w-5xl">
                {players.map((p) => (
                  <span key={p.id} className="rounded-2xl bg-slate-800 px-6 py-3 text-2xl font-bold">
                    {p.nickname}
                  </span>
                ))}
              </div>
            )}
          </div>
        </>
      )}

      {(phase === "QuestionOpen" || phase === "Paused" || phase === "QuestionClosed") &&
        currentQuestion && (
          <div className="flex-1 flex flex-col justify-between gap-8 max-w-6xl mx-auto w-full">
            <div className="flex items-center justify-between">
              <div className="h-20 w-20 rounded-full border-4 border-indigo-400 flex items-center justify-center text-3xl font-black">
                {phase === "Paused" ? "⏸" : remainingSec}
              </div>
              <span className="text-xl font-bold text-slate-400">
                {currentQuestion.questionIndex + 1} / {currentQuestion.total}
              </span>
              {progress && (
                <span className="rounded-full bg-slate-800 px-5 py-2 text-xl font-bold">
                  {progress.answered} / {progress.totalActive}
                </span>
              )}
            </div>

            <div className="text-center space-y-6">
              <h1 className="text-4xl sm:text-5xl font-black leading-tight">
                {currentQuestion.text}
              </h1>
              {currentQuestion.imageUrl && (
                // eslint-disable-next-line @next/next/no-img-element
                <img
                  src={currentQuestion.imageUrl}
                  alt=""
                  className="mx-auto max-h-64 rounded-2xl object-contain"
                />
              )}
            </div>

            <div className="grid grid-cols-2 gap-4">
              {currentQuestion.options.map((opt, idx) => {
                const colors = ["bg-red-600", "bg-blue-600", "bg-amber-500", "bg-emerald-600"];
                const isCorrect = closed?.correctOptionId === opt.id;
                const count = closed?.distribution[opt.id] ?? 0;
                return (
                  <div
                    key={opt.id}
                    className={`rounded-2xl p-6 text-2xl font-bold flex items-center justify-between ${
                      colors[idx % 4]
                    } ${closed && !isCorrect ? "opacity-40" : ""} ${
                      closed && isCorrect ? "ring-4 ring-white" : ""
                    }`}
                  >
                    <span>{opt.text}</span>
                    {closed && <span className="text-3xl font-black">{count}</span>}
                  </div>
                );
              })}
            </div>
          </div>
        )}

      {phase === "Leaderboard" && leaderboard && (
        <div className="flex-1 flex flex-col items-center justify-center gap-6 max-w-3xl mx-auto w-full">
          <h1 className="text-4xl font-black">{t("leaderboard")}</h1>
          <div className="w-full space-y-3">
            {leaderboard.top5.map((e) => (
              <motion.div
                key={e.nickname}
                layout={!reduceMotion}
                transition={{ type: "spring", stiffness: 300, damping: 28 }}
                className="flex items-center justify-between rounded-2xl bg-slate-900 border border-slate-800 px-6 py-4 text-2xl font-bold"
              >
                <span>
                  #{e.rank} {e.nickname}
                </span>
                <span className="text-indigo-400">{e.score}</span>
              </motion.div>
            ))}
          </div>
        </div>
      )}

      {phase === "Finished" && podium && (
        <div className="flex-1 flex flex-col items-center justify-center gap-8 max-w-3xl mx-auto w-full">
          <h1 className="text-5xl font-black text-amber-400">{t("podium")}</h1>
          <div className="w-full space-y-4">
            {podium.map((p) => (
              <div
                key={p.rank}
                className="flex items-center justify-between rounded-2xl bg-slate-900 border border-amber-500/40 px-8 py-5 text-3xl font-black"
              >
                <span>
                  #{p.rank} {p.nickname}
                </span>
                <span className="text-amber-400">{p.score}</span>
              </div>
            ))}
          </div>
        </div>
      )}
    </main>
  );
}

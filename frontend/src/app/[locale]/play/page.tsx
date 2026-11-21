"use client";

import { useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { AnswerButtons } from "@/components/AnswerButtons";
import { getRemainingMs } from "@/lib/clock";
import { createPlayConnection } from "@/lib/realtime/signalrClients";
import type {
  ErrorPayload,
  FinalResultsPayload,
  JoinSessionResult,
  LobbyUpdatedPayload,
  PlayerRehydrateDto,
  QuestionClosedPayload,
  QuestionOpenedPayload,
  SubmitAnswerResult,
  YourRankPayload,
  YourResultPayload,
} from "@/lib/realtime/events";
import { usePlayStore } from "@/stores/sessionStores";
import type * as signalR from "@microsoft/signalr";

export default function PlayPage() {
  const t = useTranslations("play");
  const router = useRouter();
  const connRef = useRef<signalR.HubConnection | null>(null);
  const [remainingPct, setRemainingPct] = useState(100);

  const {
    nickname,
    phase,
    currentQuestion,
    selectedOptionId,
    lockedIn,
    yourResult,
    yourRank,
    finalResults,
    serverOffsetMs,
    kicked,
    setJoined,
    setConnected,
    setPlayerCount,
    onQuestionOpened,
    markLockedIn,
    onQuestionClosed,
    onYourResult,
    onYourRank,
    onPaused,
    onResumed,
    onFinalResults,
    rehydrate,
    setKicked,
    setError,
  } = usePlayStore();

  useEffect(() => {
    const raw = localStorage.getItem("ql_player_auth");
    if (!raw) {
      router.replace("/join");
      return;
    }

    const saved = JSON.parse(raw) as {
      pin: string;
      token: string;
      nickname: string;
    };

    const conn = createPlayConnection();
    connRef.current = conn;

    conn.on("LobbyUpdated", (p: LobbyUpdatedPayload) => setPlayerCount(p.playerCount));
    conn.on("QuestionOpened", (p: QuestionOpenedPayload) => onQuestionOpened(p));
    conn.on("QuestionClosed", (p: QuestionClosedPayload) => onQuestionClosed(p));
    conn.on("YourResult", (p: YourResultPayload) => onYourResult(p));
    conn.on("YourRank", (p: YourRankPayload) => onYourRank(p));
    conn.on("SessionPaused", () => onPaused());
    conn.on("SessionResumed", (p: { remainingMs: number; serverNowMs: number }) =>
      onResumed(p.remainingMs, p.serverNowMs),
    );
    conn.on("FinalResults", (p: FinalResultsPayload) => onFinalResults(p));
    conn.on("Error", (err: ErrorPayload) => {
      setError(err);
      if (err.code === "SessionTakenOver") {
        void conn.stop();
      }
    });
    conn.on("Kicked", () => {
      localStorage.removeItem("ql_player_auth");
      setKicked();
    });

    const syncState = async () => {
      const snap = await conn.invoke<PlayerRehydrateDto | null>("Rehydrate");
      if (snap) {
        rehydrate({
          phase: snap.phase,
          currentQuestion: snap.currentQuestion,
          alreadyAnswered: snap.alreadyAnswered,
          yourResult: snap.yourResult,
          yourRank: snap.yourRank,
          finalResults: snap.finalResults,
          serverNowMs: snap.serverNowMs,
        });
      }
    };

    conn.onreconnected(async () => {
      setConnected(true);
      await syncState();
    });

    conn.start().then(async () => {
      setConnected(true);
      const res = await conn.invoke<JoinSessionResult>(
        "JoinSession",
        saved.pin,
        saved.nickname,
        saved.token,
      );
      if (res.succeeded) {
        setJoined({
          sessionId: res.sessionId,
          playerId: res.playerId,
          nickname: res.nickname,
          phase: res.phase,
        });
        await syncState();
      } else {
        localStorage.removeItem("ql_player_auth");
        router.replace("/join");
      }
    });

    return () => {
      void conn.stop();
    };
  }, [
    router,
    setConnected,
    setJoined,
    setKicked,
    setPlayerCount,
    onQuestionOpened,
    onQuestionClosed,
    onYourResult,
    onYourRank,
    onPaused,
    onResumed,
    onFinalResults,
    rehydrate,
    setError,
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
      setRemainingPct(Math.max(0, Math.min(100, (ms / currentQuestion.durationMs) * 100)));
      if (ms > 0) rafId = requestAnimationFrame(tick);
    };
    rafId = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(rafId);
  }, [phase, currentQuestion, serverOffsetMs]);

  async function handleSelectOption(optionId: string) {
    if (!currentQuestion || lockedIn) return;
    markLockedIn(optionId);
    await connRef.current?.invoke<SubmitAnswerResult>(
      "SubmitAnswer",
      currentQuestion.questionId,
      optionId,
    );
  }

  if (kicked) {
    return (
      <main className="min-h-screen flex items-center justify-center bg-red-700 text-white p-6 text-center">
        <p className="text-2xl font-bold">{t("kicked")}</p>
      </main>
    );
  }

  if (phase === "Lobby") {
    return (
      <main className="min-h-screen flex flex-col items-center justify-center bg-indigo-700 text-white p-6 text-center gap-4">
        <h1 className="text-4xl font-black">{t("youreIn")}</h1>
        {nickname && (
          <div className="rounded-2xl bg-white/15 px-6 py-3 text-2xl font-bold">
            {nickname}
          </div>
        )}
        <p className="text-lg text-indigo-100">{t("seeNameOnScreen")}</p>
        <p className="text-sm text-indigo-200 animate-pulse">{t("waitingToStart")}</p>
      </main>
    );
  }

  if (phase === "Paused") {
    return (
      <main className="min-h-screen flex items-center justify-center bg-slate-900 text-white p-6 text-center">
        <p className="text-2xl font-bold">{t("paused")}</p>
      </main>
    );
  }

  if (phase === "QuestionOpen" && currentQuestion) {
    if (lockedIn) {
      return (
        <main className="min-h-screen flex flex-col items-center justify-center bg-indigo-700 text-white p-6 text-center gap-4">
          <h1 className="text-3xl font-black">{t("lockedIn")}</h1>
          <p className="text-indigo-200">{t("waitingForOthers")}</p>
        </main>
      );
    }

    return (
      <main className="min-h-screen flex flex-col justify-between bg-slate-950 text-white p-4 gap-4">
        <div className="w-full h-2.5 rounded-full bg-slate-800 overflow-hidden">
          <div
            className="h-full bg-indigo-500 transition-[width] duration-75"
            style={{ width: `${remainingPct}%` }}
          />
        </div>

        {currentQuestion.showQuestionOnPhones && (
          <div className="text-center space-y-3 py-2">
            <h1 className="text-xl font-bold">{currentQuestion.text}</h1>
            {currentQuestion.imageUrl && (
              // eslint-disable-next-line @next/next/no-img-element
              <img
                src={currentQuestion.imageUrl}
                alt=""
                className="mx-auto max-h-40 rounded-xl object-contain"
              />
            )}
          </div>
        )}

        <div className="my-auto">
          <AnswerButtons
            options={currentQuestion.options}
            disabled={lockedIn}
            selectedOptionId={selectedOptionId}
            onSelect={(id) => void handleSelectOption(id)}
          />
        </div>
      </main>
    );
  }

  if (phase === "QuestionClosed" && yourResult) {
    return (
      <main
        className={`min-h-screen flex flex-col items-center justify-center text-white p-6 text-center gap-4 ${
          yourResult.isCorrect ? "bg-emerald-600" : "bg-rose-600"
        }`}
      >
        <h1 className="text-4xl font-black">
          {yourResult.isCorrect ? t("correct") : t("wrong")}
        </h1>
        {yourResult.pointsAwarded > 0 && (
          <div className="rounded-2xl bg-black/20 px-6 py-2 text-2xl font-black">
            {t("pointsEarned", { points: yourResult.pointsAwarded })}
          </div>
        )}
        {yourResult.streak >= 2 && (
          <p className="text-lg font-bold">🔥 {t("streak", { count: yourResult.streak })}</p>
        )}
        <p className="text-lg">{t("totalScore", { score: yourResult.totalScore })}</p>
        <p className="text-sm opacity-90">{t("rank", { rank: yourResult.rank })}</p>
      </main>
    );
  }

  if (phase === "Leaderboard" && yourRank) {
    return (
      <main className="min-h-screen flex flex-col items-center justify-center bg-indigo-700 text-white p-6 text-center gap-4">
        <h1 className="text-4xl font-black">{t("rank", { rank: yourRank.rank })}</h1>
        {yourRank.gapToNext != null && yourRank.nextNickname && (
          <p className="text-lg text-indigo-100">
            {t("gapToNext", { gap: yourRank.gapToNext, name: yourRank.nextNickname })}
          </p>
        )}
      </main>
    );
  }

  if (phase === "Finished" && finalResults) {
    return (
      <main className="min-h-screen flex flex-col items-center justify-center bg-slate-950 text-white p-6 text-center gap-4">
        <h1 className="text-4xl font-black text-amber-400">
          {t("rank", { rank: finalResults.rank })}
        </h1>
        <p className="text-2xl font-bold">{t("totalScore", { score: finalResults.score })}</p>
        <p className="text-slate-300">
          {t("correctCount", {
            correct: finalResults.correctCount,
            total: finalResults.totalQuestions,
          })}
        </p>
        <p className="text-sm text-slate-400">{t("finalThanks")}</p>
      </main>
    );
  }

  return (
    <main className="min-h-screen flex items-center justify-center bg-indigo-700 text-white p-6">
      <p className="text-lg font-bold">{phase}</p>
    </main>
  );
}

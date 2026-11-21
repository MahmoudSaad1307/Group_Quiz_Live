import { create } from "zustand";
import { computeServerOffset } from "@/lib/clock";
import type {
  AnswerProgressPayload,
  ErrorPayload,
  FinalResultsPayload,
  HostQuestionViewPayload,
  LeaderboardUpdatedPayload,
  LobbyPlayer,
  PodiumEntryDto,
  QuestionClosedPayload,
  QuestionOpenedPayload,
  YourRankPayload,
  YourResultPayload,
} from "@/lib/realtime/events";

interface HostState {
  connected: boolean;
  phase: string;
  playerCount: number;
  players: LobbyPlayer[];
  currentQuestion: HostQuestionViewPayload | null;
  progress: AnswerProgressPayload | null;
  closed: QuestionClosedPayload | null;
  leaderboard: LeaderboardUpdatedPayload | null;
  podium: PodiumEntryDto[] | null;
  serverOffsetMs: number;
  error: ErrorPayload | null;
  setConnected: (connected: boolean) => void;
  setLobby: (playerCount: number, players: LobbyPlayer[]) => void;
  onQuestionOpened: (payload: HostQuestionViewPayload) => void;
  onAnswerProgress: (payload: AnswerProgressPayload) => void;
  onQuestionClosed: (payload: QuestionClosedPayload) => void;
  onLeaderboard: (payload: LeaderboardUpdatedPayload) => void;
  onPaused: () => void;
  onResumed: (remainingMs: number, serverNowMs: number) => void;
  onFinished: (podium: PodiumEntryDto[]) => void;
  rehydrate: (snap: {
    phase: string;
    lobby: LobbyPlayer[];
    playerCount: number;
    currentQuestion?: HostQuestionViewPayload | null;
    progress?: AnswerProgressPayload | null;
    closed?: QuestionClosedPayload | null;
    leaderboard?: LeaderboardUpdatedPayload | null;
    podium?: PodiumEntryDto[] | null;
    serverNowMs: number;
  }) => void;
  setError: (error: ErrorPayload | null) => void;
  reset: () => void;
}

export const useHostStore = create<HostState>((set) => ({
  connected: false,
  phase: "Lobby",
  playerCount: 0,
  players: [],
  currentQuestion: null,
  progress: null,
  closed: null,
  leaderboard: null,
  podium: null,
  serverOffsetMs: 0,
  error: null,
  setConnected: (connected) => set({ connected }),
  setLobby: (playerCount, players) => set({ playerCount, players }),
  onQuestionOpened: (payload) =>
    set({
      phase: "QuestionOpen",
      currentQuestion: payload,
      progress: { answered: 0, totalActive: 0 },
      closed: null,
      leaderboard: null,
      serverOffsetMs: computeServerOffset(payload.serverNowMs),
    }),
  onAnswerProgress: (progress) => set({ progress }),
  onQuestionClosed: (closed) => set({ phase: "QuestionClosed", closed }),
  onLeaderboard: (leaderboard) => set({ phase: "Leaderboard", leaderboard }),
  onPaused: () => set({ phase: "Paused" }),
  onResumed: (remainingMs, serverNowMs) =>
    set((state) => {
      if (!state.currentQuestion) return { phase: "QuestionOpen" };
      const offset = computeServerOffset(serverNowMs);
      return {
        phase: "QuestionOpen",
        serverOffsetMs: offset,
        currentQuestion: {
          ...state.currentQuestion,
          startedAtServerMs: serverNowMs - (state.currentQuestion.durationMs - remainingMs),
          serverNowMs,
        },
      };
    }),
  onFinished: (podium) => set({ phase: "Finished", podium }),
  rehydrate: (snap) =>
    set({
      phase: snap.phase,
      players: snap.lobby,
      playerCount: snap.playerCount,
      currentQuestion: snap.currentQuestion ?? null,
      progress: snap.progress ?? null,
      closed: snap.closed ?? null,
      leaderboard: snap.leaderboard ?? null,
      podium: snap.podium ?? null,
      serverOffsetMs: computeServerOffset(snap.serverNowMs),
    }),
  setError: (error) => set({ error }),
  reset: () =>
    set({
      connected: false,
      phase: "Lobby",
      playerCount: 0,
      players: [],
      currentQuestion: null,
      progress: null,
      closed: null,
      leaderboard: null,
      podium: null,
      error: null,
    }),
}));

interface ScreenState {
  connected: boolean;
  phase: string;
  playerCount: number;
  players: LobbyPlayer[];
  currentQuestion: QuestionOpenedPayload | null;
  progress: AnswerProgressPayload | null;
  closed: QuestionClosedPayload | null;
  leaderboard: LeaderboardUpdatedPayload | null;
  podium: PodiumEntryDto[] | null;
  serverOffsetMs: number;
  setConnected: (connected: boolean) => void;
  setLobby: (playerCount: number, players: LobbyPlayer[]) => void;
  onQuestionOpened: (payload: QuestionOpenedPayload) => void;
  onAnswerProgress: (payload: AnswerProgressPayload) => void;
  onQuestionClosed: (payload: QuestionClosedPayload) => void;
  onLeaderboard: (payload: LeaderboardUpdatedPayload) => void;
  onPaused: () => void;
  onResumed: (remainingMs: number, serverNowMs: number) => void;
  onFinished: (podium: PodiumEntryDto[]) => void;
  rehydrate: (snap: {
    phase: string;
    lobby: LobbyPlayer[];
    playerCount: number;
    currentQuestion?: QuestionOpenedPayload | null;
    progress?: AnswerProgressPayload | null;
    closed?: QuestionClosedPayload | null;
    leaderboard?: LeaderboardUpdatedPayload | null;
    podium?: PodiumEntryDto[] | null;
    serverNowMs: number;
  }) => void;
}

export const useScreenStore = create<ScreenState>((set) => ({
  connected: false,
  phase: "Lobby",
  playerCount: 0,
  players: [],
  currentQuestion: null,
  progress: null,
  closed: null,
  leaderboard: null,
  podium: null,
  serverOffsetMs: 0,
  setConnected: (connected) => set({ connected }),
  setLobby: (playerCount, players) => set({ playerCount, players }),
  onQuestionOpened: (payload) =>
    set({
      phase: "QuestionOpen",
      currentQuestion: payload,
      progress: null,
      closed: null,
      leaderboard: null,
      serverOffsetMs: computeServerOffset(payload.serverNowMs),
    }),
  onAnswerProgress: (progress) => set({ progress }),
  onQuestionClosed: (closed) => set({ phase: "QuestionClosed", closed }),
  onLeaderboard: (leaderboard) => set({ phase: "Leaderboard", leaderboard }),
  onPaused: () => set({ phase: "Paused" }),
  onResumed: (remainingMs, serverNowMs) =>
    set((state) => {
      if (!state.currentQuestion) return { phase: "QuestionOpen" };
      return {
        phase: "QuestionOpen",
        serverOffsetMs: computeServerOffset(serverNowMs),
        currentQuestion: {
          ...state.currentQuestion,
          startedAtServerMs: serverNowMs - (state.currentQuestion.durationMs - remainingMs),
          serverNowMs,
        },
      };
    }),
  onFinished: (podium) => set({ phase: "Finished", podium }),
  rehydrate: (snap) =>
    set({
      phase: snap.phase,
      players: snap.lobby,
      playerCount: snap.playerCount,
      currentQuestion: snap.currentQuestion ?? null,
      progress: snap.progress ?? null,
      closed: snap.closed ?? null,
      leaderboard: snap.leaderboard ?? null,
      podium: snap.podium ?? null,
      serverOffsetMs: computeServerOffset(snap.serverNowMs),
    }),
}));

interface PlayState {
  connected: boolean;
  sessionId: string | null;
  playerId: string | null;
  nickname: string | null;
  phase: string;
  playerCount: number;
  currentQuestion: QuestionOpenedPayload | null;
  selectedOptionId: string | null;
  lockedIn: boolean;
  closed: QuestionClosedPayload | null;
  yourResult: YourResultPayload | null;
  yourRank: YourRankPayload | null;
  finalResults: FinalResultsPayload | null;
  serverOffsetMs: number;
  kicked: boolean;
  error: ErrorPayload | null;
  setJoined: (data: { sessionId: string; playerId: string; nickname: string; phase: string }) => void;
  setConnected: (connected: boolean) => void;
  setPlayerCount: (count: number) => void;
  onQuestionOpened: (payload: QuestionOpenedPayload) => void;
  markLockedIn: (optionId: string) => void;
  onQuestionClosed: (payload: QuestionClosedPayload) => void;
  onYourResult: (payload: YourResultPayload) => void;
  onYourRank: (payload: YourRankPayload) => void;
  onPaused: () => void;
  onResumed: (remainingMs: number, serverNowMs: number) => void;
  onFinalResults: (payload: FinalResultsPayload) => void;
  rehydrate: (snap: {
    phase: string;
    currentQuestion?: QuestionOpenedPayload | null;
    alreadyAnswered: boolean;
    yourResult?: YourResultPayload | null;
    yourRank?: YourRankPayload | null;
    finalResults?: FinalResultsPayload | null;
    serverNowMs: number;
  }) => void;
  setKicked: () => void;
  setError: (error: ErrorPayload | null) => void;
}

export const usePlayStore = create<PlayState>((set) => ({
  connected: false,
  sessionId: null,
  playerId: null,
  nickname: null,
  phase: "Lobby",
  playerCount: 0,
  currentQuestion: null,
  selectedOptionId: null,
  lockedIn: false,
  closed: null,
  yourResult: null,
  yourRank: null,
  finalResults: null,
  serverOffsetMs: 0,
  kicked: false,
  error: null,
  setJoined: (data) => set({ ...data, kicked: false, error: null }),
  setConnected: (connected) => set({ connected }),
  setPlayerCount: (playerCount) => set({ playerCount }),
  onQuestionOpened: (payload) =>
    set({
      phase: "QuestionOpen",
      currentQuestion: payload,
      selectedOptionId: null,
      lockedIn: false,
      closed: null,
      yourResult: null,
      serverOffsetMs: computeServerOffset(payload.serverNowMs),
    }),
  markLockedIn: (optionId) => set({ selectedOptionId: optionId, lockedIn: true }),
  onQuestionClosed: (closed) => set({ phase: "QuestionClosed", closed }),
  onYourResult: (yourResult) => set({ yourResult }),
  onYourRank: (yourRank) => set({ phase: "Leaderboard", yourRank }),
  onPaused: () => set({ phase: "Paused" }),
  onResumed: (remainingMs, serverNowMs) =>
    set((state) => {
      if (!state.currentQuestion) return { phase: "QuestionOpen" };
      return {
        phase: "QuestionOpen",
        serverOffsetMs: computeServerOffset(serverNowMs),
        currentQuestion: {
          ...state.currentQuestion,
          startedAtServerMs: serverNowMs - (state.currentQuestion.durationMs - remainingMs),
          serverNowMs,
        },
      };
    }),
  onFinalResults: (finalResults) => set({ phase: "Finished", finalResults }),
  rehydrate: (snap) =>
    set({
      phase: snap.phase,
      currentQuestion: snap.currentQuestion ?? null,
      lockedIn: snap.alreadyAnswered,
      yourResult: snap.yourResult ?? null,
      yourRank: snap.yourRank ?? null,
      finalResults: snap.finalResults ?? null,
      serverOffsetMs: computeServerOffset(snap.serverNowMs),
    }),
  setKicked: () => set({ kicked: true }),
  setError: (error) => set({ error }),
}));

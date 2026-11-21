export function computeServerOffset(serverNowMs: number, clientNowMs = Date.now()): number {
  return serverNowMs - clientNowMs;
}

export function getRemainingMs(
  startedAtServerMs: number,
  durationMs: number,
  serverOffsetMs: number,
  clientNowMs = Date.now(),
): number {
  const estimatedServerNow = clientNowMs + serverOffsetMs;
  return Math.max(0, startedAtServerMs + durationMs - estimatedServerNow);
}

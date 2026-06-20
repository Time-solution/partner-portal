/** Aligns with backend OpenIddict access token lifetime (~30 min). */
export const SESSION_IDLE_LOCK_MS = 30 * 60 * 1000;

/** Warn two minutes before auto-lock. */
export const SESSION_IDLE_WARN_MS = 28 * 60 * 1000;

export const SESSION_IDLE_CHECK_MS = 15_000;

export type IdleState = "active" | "warning" | "locked";

export function computeIdleState(
  lastActivityAt: number,
  now: number,
  lockMs: number = SESSION_IDLE_LOCK_MS,
  warnMs: number = SESSION_IDLE_WARN_MS,
): IdleState {
  const idleMs = now - lastActivityAt;
  if (idleMs >= lockMs) return "locked";
  if (idleMs >= warnMs) return "warning";
  return "active";
}

export function msUntilLock(
  lastActivityAt: number,
  now: number,
  lockMs: number = SESSION_IDLE_LOCK_MS,
): number {
  return Math.max(0, lockMs - (now - lastActivityAt));
}

export function formatIdleCountdown(ms: number): string {
  const totalSeconds = Math.ceil(ms / 1000);
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return `${minutes}:${seconds.toString().padStart(2, "0")}`;
}

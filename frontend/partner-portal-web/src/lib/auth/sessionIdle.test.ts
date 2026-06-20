import { describe, expect, it } from "vitest";
import {
  SESSION_IDLE_LOCK_MS,
  SESSION_IDLE_WARN_MS,
  computeIdleState,
  formatIdleCountdown,
  msUntilLock,
} from "./sessionIdle";

describe("sessionIdle", () => {
  const base = 1_000_000;

  it("returns active before the warning threshold", () => {
    expect(computeIdleState(base, base + SESSION_IDLE_WARN_MS - 1)).toBe("active");
  });

  it("returns warning between 28 and 30 minutes idle", () => {
    expect(computeIdleState(base, base + SESSION_IDLE_WARN_MS)).toBe("warning");
    expect(computeIdleState(base, base + SESSION_IDLE_LOCK_MS - 1)).toBe("warning");
  });

  it("returns locked at or after 30 minutes idle", () => {
    expect(computeIdleState(base, base + SESSION_IDLE_LOCK_MS)).toBe("locked");
  });

  it("computes remaining lock time", () => {
    expect(msUntilLock(base, base + 60_000)).toBe(SESSION_IDLE_LOCK_MS - 60_000);
    expect(msUntilLock(base, base + SESSION_IDLE_LOCK_MS)).toBe(0);
  });

  it("formats countdown as m:ss", () => {
    expect(formatIdleCountdown(125_000)).toBe("2:05");
    expect(formatIdleCountdown(59_000)).toBe("0:59");
  });
});

import { useCallback, useEffect, useRef, useState } from "react";
import {
  SESSION_IDLE_CHECK_MS,
  SESSION_IDLE_LOCK_MS,
  computeIdleState,
  msUntilLock,
} from "@/lib/auth/sessionIdle";

const ACTIVITY_EVENTS = ["mousedown", "mousemove", "keydown", "scroll", "touchstart", "click"] as const;

export function useSessionIdleLock(onLock: () => void) {
  const lastActivityRef = useRef(Date.now());
  const lockedRef = useRef(false);
  const onLockRef = useRef(onLock);
  const [showWarning, setShowWarning] = useState(false);
  const [remainingMs, setRemainingMs] = useState(SESSION_IDLE_LOCK_MS);

  onLockRef.current = onLock;

  const recordActivity = useCallback(() => {
    lastActivityRef.current = Date.now();
    lockedRef.current = false;
    setShowWarning(false);
    setRemainingMs(SESSION_IDLE_LOCK_MS);
  }, []);

  const evaluate = useCallback(() => {
    const now = Date.now();
    const state = computeIdleState(lastActivityRef.current, now);

    if (state === "locked") {
      if (!lockedRef.current) {
        lockedRef.current = true;
        setShowWarning(false);
        onLockRef.current();
      }
      return;
    }

    setRemainingMs(msUntilLock(lastActivityRef.current, now));
    setShowWarning(state === "warning");
  }, []);

  useEffect(() => {
    const onActivity = () => recordActivity();
    for (const event of ACTIVITY_EVENTS) {
      window.addEventListener(event, onActivity, { passive: true });
    }

    evaluate();
    const interval = window.setInterval(evaluate, SESSION_IDLE_CHECK_MS);

    return () => {
      for (const event of ACTIVITY_EVENTS) {
        window.removeEventListener(event, onActivity);
      }
      window.clearInterval(interval);
    };
  }, [evaluate, recordActivity]);

  return { showWarning, remainingMs, staySignedIn: recordActivity };
}

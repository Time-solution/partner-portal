import { useEffect, useRef } from "react";

interface SessionTimeoutOptions {
  timeoutMs: number;
  onTimeout: () => void;
  enabled?: boolean;
}

/**
 * Logs the user out after a period of inactivity. Activity (pointer, keyboard,
 * scroll, touch) resets the idle timer. Mirrors the backend's short-lived
 * access-token policy with a client-side idle guard.
 */
export function useSessionTimeout({
  timeoutMs,
  onTimeout,
  enabled = true,
}: SessionTimeoutOptions) {
  const timer = useRef<number | undefined>(undefined);
  const onTimeoutRef = useRef(onTimeout);
  onTimeoutRef.current = onTimeout;

  useEffect(() => {
    if (!enabled) return;

    const reset = () => {
      if (timer.current !== undefined) {
        window.clearTimeout(timer.current);
      }
      timer.current = window.setTimeout(() => onTimeoutRef.current(), timeoutMs);
    };

    const events: (keyof WindowEventMap)[] = [
      "mousemove",
      "keydown",
      "click",
      "scroll",
      "touchstart",
    ];
    events.forEach((event) => window.addEventListener(event, reset, { passive: true }));
    reset();

    return () => {
      events.forEach((event) => window.removeEventListener(event, reset));
      if (timer.current !== undefined) {
        window.clearTimeout(timer.current);
      }
    };
  }, [timeoutMs, enabled]);
}

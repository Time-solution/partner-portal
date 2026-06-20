/** Notifies UI when mock portal data mutates (persist / reset). Live API can no-op. */
const EVENT = "zahy-portal-data-changed";

export function notifyPortalDataChanged() {
  if (typeof window !== "undefined") {
    window.dispatchEvent(new CustomEvent(EVENT));
  }
}

export function subscribePortalDataChanged(listener: () => void): () => void {
  if (typeof window === "undefined") return () => undefined;
  window.addEventListener(EVENT, listener);
  return () => window.removeEventListener(EVENT, listener);
}

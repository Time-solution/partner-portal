import * as React from "react";
import { X } from "lucide-react";
import { cn } from "@/lib/utils";

/**
 * Hand-rolled shadcn-style side sheet (Tailwind only — no extra Radix deps). Opens from the
 * inline-END side so it mirrors correctly under RTL (dir comes from the nearest `dir` ancestor).
 * Escape and overlay-click close it; focus lands on the panel.
 */
export function Sheet({
  open,
  onClose,
  title,
  children,
  footer,
  dir,
}: {
  open: boolean;
  onClose: () => void;
  title: React.ReactNode;
  children: React.ReactNode;
  footer?: React.ReactNode;
  dir?: "rtl" | "ltr";
}) {
  const panelRef = React.useRef<HTMLDivElement>(null);

  React.useEffect(() => {
    if (!open) return;
    panelRef.current?.focus();
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [open, onClose]);

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50" role="presentation" dir={dir}>
      <div
        className="absolute inset-0 bg-black/50"
        data-testid="sheet-overlay"
        onClick={onClose}
        aria-hidden="true"
      />
      <div
        ref={panelRef}
        role="dialog"
        aria-modal="true"
        tabIndex={-1}
        data-testid="detail-sheet"
        className={cn(
          "absolute inset-y-0 end-0 flex w-full max-w-md flex-col border-s border-border bg-surface text-surface-foreground shadow-xl",
          "focus-visible:outline-none",
        )}
      >
        <div className="flex items-center justify-between gap-3 border-b border-border px-5 py-4">
          <h2 className="text-base font-semibold">{title}</h2>
          <button
            type="button"
            onClick={onClose}
            data-testid="sheet-close"
            className="rounded-md p-1 text-muted-foreground hover:bg-muted hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            <X className="h-4 w-4" aria-hidden="true" />
          </button>
        </div>
        <div className="flex-1 space-y-4 overflow-y-auto px-5 py-4">{children}</div>
        {footer ? <div className="border-t border-border px-5 py-4">{footer}</div> : null}
      </div>
    </div>
  );
}

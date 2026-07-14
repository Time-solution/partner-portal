import * as React from "react";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

/**
 * Hand-rolled shadcn-style centered confirm dialog (Tailwind only). One primary confirm action,
 * one cancel. Escape/overlay close = cancel. RTL mirrors via the `dir` prop / ancestor.
 */
export function ConfirmDialog({
  open,
  title,
  children,
  confirmLabel,
  cancelLabel,
  onConfirm,
  onCancel,
  busy,
  destructive,
  dir,
  testId = "confirm-dialog",
}: {
  open: boolean;
  title: React.ReactNode;
  children: React.ReactNode;
  confirmLabel: string;
  cancelLabel: string;
  onConfirm: () => void;
  onCancel: () => void;
  busy?: boolean;
  destructive?: boolean;
  dir?: "rtl" | "ltr";
  testId?: string;
}) {
  React.useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") onCancel();
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [open, onCancel]);

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4" dir={dir}>
      <div className="absolute inset-0 bg-black/50" onClick={onCancel} aria-hidden="true" />
      <div
        role="alertdialog"
        aria-modal="true"
        data-testid={testId}
        className="relative w-full max-w-sm rounded-lg border border-border bg-surface p-5 text-surface-foreground shadow-xl"
      >
        <h2 className="text-base font-semibold">{title}</h2>
        <div className="mt-2 space-y-2 text-sm text-muted-foreground">{children}</div>
        <div className="mt-4 flex items-center justify-end gap-2">
          <Button size="sm" variant="outline" onClick={onCancel} data-testid={`${testId}-cancel`}>
            {cancelLabel}
          </Button>
          <Button
            size="sm"
            disabled={busy}
            onClick={onConfirm}
            data-testid={`${testId}-confirm`}
            className={cn(destructive && "bg-danger text-white hover:bg-danger/90")}
          >
            {confirmLabel}
          </Button>
        </div>
      </div>
    </div>
  );
}

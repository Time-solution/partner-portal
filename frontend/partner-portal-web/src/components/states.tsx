import type { ComponentType } from "react";
import { AlertTriangle, Inbox, RefreshCcw } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";

/** Empty state — icon + one line + optional primary action (the list-screen rule). */
export function EmptyState({
  icon: Icon = Inbox,
  message,
  actionLabel,
  onAction,
}: {
  icon?: ComponentType<{ className?: string }>;
  message: string;
  actionLabel?: string;
  onAction?: () => void;
}) {
  return (
    <div
      data-testid="empty-state"
      className="flex flex-col items-center gap-3 rounded-lg border border-dashed border-border px-6 py-10 text-center"
    >
      <Icon className="h-8 w-8 text-muted-foreground" aria-hidden="true" />
      <p className="text-sm text-muted-foreground">{message}</p>
      {actionLabel && onAction ? (
        <Button size="sm" onClick={onAction}>
          {actionLabel}
        </Button>
      ) : null}
    </div>
  );
}

/** Error state — same role="alert" banner anatomy as the MerchantPreview pattern + retry. */
export function ErrorState({
  message,
  retryLabel,
  onRetry,
}: {
  message: string;
  retryLabel?: string;
  onRetry?: () => void;
}) {
  return (
    <div
      role="alert"
      data-testid="error-state"
      className="flex items-start gap-3 rounded-lg border border-rose-300 bg-rose-50 px-4 py-3 text-sm text-rose-900 dark:border-rose-800 dark:bg-rose-950/40 dark:text-rose-200"
    >
      <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
      <div className="flex-1 space-y-2">
        <p>{message}</p>
        {retryLabel && onRetry ? (
          <Button size="sm" variant="outline" onClick={onRetry}>
            <RefreshCcw className="h-3.5 w-3.5" aria-hidden="true" />
            {retryLabel}
          </Button>
        ) : null}
      </div>
    </div>
  );
}

/** Skeleton loading for card grids — one anatomy everywhere. */
export function CardGridSkeleton({ count = 3 }: { count?: number }) {
  return (
    <div data-testid="skeleton-grid" className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
      {Array.from({ length: count }, (_, i) => (
        <div key={i} className="space-y-3 rounded-lg border border-border p-5">
          <Skeleton className="h-5 w-2/3" />
          <Skeleton className="h-4 w-1/3" />
          <Skeleton className="h-16 w-full" />
          <Skeleton className="h-9 w-full" />
        </div>
      ))}
    </div>
  );
}

import { Inbox, type LucideIcon } from "lucide-react";
import { cn } from "@/lib/utils";

/**
 * Shared empty-state for list pages — a short, muted, centered message so a list is
 * never a blank screen when there is no data. Theme tokens only (muted-foreground),
 * no arbitrary colors. RTL-safe (centered, direction-agnostic).
 */
export function EmptyState({
  message,
  icon: Icon = Inbox,
  className,
}: {
  message: string;
  icon?: LucideIcon;
  className?: string;
}) {
  return (
    <div className={cn("flex flex-col items-center justify-center gap-2 py-10 text-center", className)}>
      <Icon className="h-6 w-6 text-muted-foreground/70" aria-hidden="true" />
      <p className="text-sm text-muted-foreground">{message}</p>
    </div>
  );
}

/** The same empty message rendered as a full-width table row (for use inside <tbody>). */
export function TableEmptyRow({ colSpan, message }: { colSpan: number; message: string }) {
  return (
    <tr>
      <td colSpan={colSpan} className="py-10 text-center text-sm text-muted-foreground">
        {message}
      </td>
    </tr>
  );
}

import { Badge, type BadgeVariant } from "@/components/ui/badge";

/**
 * ONE badge set across catalog items, usage packages and activations (the design-system rule):
 * Draft → muted, Published/Active → success, Ended/Archived → gray, Pending/Suspended → warning.
 * Callers pass the already-translated label; the tone comes from the raw status string.
 */
const STATUS_TONE: Record<string, BadgeVariant> = {
  Draft: "muted",
  Active: "success",
  Published: "success",
  Archived: "outline",
  Ended: "outline",
  Pending: "warning",
  Suspended: "warning",
};

export function StatusBadge({ status, label }: { status: string; label: string }) {
  return (
    <Badge variant={STATUS_TONE[status] ?? "muted"} data-testid={`status-badge-${status}`}>
      {label}
    </Badge>
  );
}

/** Offering-kind chip — informational, always outline tone. */
export function OfferingKindBadge({ label }: { label: string }) {
  return (
    <Badge variant="outline" data-testid="offering-kind-badge">
      {label}
    </Badge>
  );
}

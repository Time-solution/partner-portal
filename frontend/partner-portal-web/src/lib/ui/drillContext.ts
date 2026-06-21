import type { OrgLevel } from "@/lib/org/orgModel";

/**
 * DISPLAY-ONLY context resolution for the finance drill-down. The drill-down always
 * reads data that is ALREADY scoped to the viewer's org, so these helpers only decide
 * WHAT NAME to print in the context header — they never change scope or filtering.
 */

/**
 * The entity name to show as "whose data is on screen". Platform sees the aggregate of
 * every partner + merchant; a partner / merchant org shows its own name.
 */
export function drillScopeLabel(
  org: { level: OrgLevel; name?: string } | null | undefined,
  platformLabel: string,
): string {
  if (!org || org.level === "Platform") return platformLabel;
  return org.name?.trim() || platformLabel;
}

/** "{Entity} · {Period}" — the period falls back to an "all periods" label when empty. */
export function drillContextText(entity: string, period: string, allPeriodsLabel: string): string {
  return `${entity} · ${period || allPeriodsLabel}`;
}

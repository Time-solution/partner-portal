/**
 * Shared tab styling so the SELECTED tab is always unmistakable across every tab bar.
 * Active = solid brand pill; inactive = muted with hover. Used by Finance / module /
 * partner sub-navs for a consistent, accessible active state (RTL-safe).
 */
export function tabLinkClass(isActive: boolean): string {
  return [
    "rounded-md px-3.5 py-2 text-sm font-semibold transition-colors",
    "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring",
    isActive
      ? "bg-primary text-primary-foreground shadow-sm"
      : "font-medium text-muted-foreground hover:bg-muted hover:text-foreground",
  ].join(" ");
}

/** Container for a horizontal tab bar (bottom divider + spacing). */
export const tabBarClass = "flex flex-wrap gap-1.5 border-b border-border pb-2";

/**
 * Active-route predicate shared by the sidebar and every tab bar: a link is "current"
 * when the location matches it exactly or is a descendant of it. Sibling routes
 * (e.g. `/finance/overview` vs `/finance/reports`) never both match, so the sidebar
 * Reports entry and the Finance Reports tab light up together — from either entry
 * point — without a stale double-highlight of the parent Finance item.
 */
export function isPathActive(pathname: string, target: string): boolean {
  return pathname === target || pathname.startsWith(`${target}/`);
}

/**
 * U4 — a MERCHANT's selection of a partner's PUBLISHED usage package (frontend mock, mirrors the backend
 * `UsagePackageSelection`). Records WHICH package a merchant is on for a partner + the active window
 * (activatedAt..endedAt) so U3 can prorate the base by calendar days and count usage to deactivation.
 * SELECTION/LINK ONLY — holding this row computes NO billing and posts NO journal.
 */

export type UsagePackageSelectionStatus = "Active" | "Ended";

export interface UsagePackageSelection {
  id: string;
  partnerId: string;
  /** Merchant tenant that selected the package. */
  tenantId: string;
  usagePackageId: string;
  merchantName: string;
  status: UsagePackageSelectionStatus;
  /** ISO date — start of the active window (for proration). */
  activatedAt: string;
  /** ISO date — set on mid-cycle deactivation; null while Active. */
  endedAt?: string;
}

export interface SelectUsagePackageInput {
  partnerId: string;
  usagePackageId: string;
  tenantId: string;
  merchantName: string;
  /** Defaults to "now" — instant activation. */
  activatedAt?: string;
}

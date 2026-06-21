/**
 * Wires the invoicing read models to the admin OrgProfile store and produces a
 * {@link ProformaDocument}. This is the single seam the UI uses, so the export buttons stay
 * one-liners. Reads only — no money is computed here.
 */
import type { SubscriptionBillingPeriod } from "@/lib/data/types";
import type { PartnerStatement } from "@/lib/reports/settlementReports";
import { readOrgProfile } from "@/lib/profile/orgProfileStore";
import type { Lang } from "@/lib/i18n";
import {
  buildInvoiceProforma,
  buildStatementProforma,
  type ProformaDocument,
} from "./proformaDocument";

const PLATFORM_SCOPE = { kind: "platform", id: "zahy" } as const;

/** Direction 1 — a merchant subscription INVOICE (receivable) for one billing period. */
export function invoiceProformaFromPeriod(
  period: SubscriptionBillingPeriod,
  lang: Lang,
): ProformaDocument {
  const issuer = readOrgProfile(PLATFORM_SCOPE);
  const billTo = period.tenantId
    ? readOrgProfile({ kind: "merchant", id: period.tenantId })
    : readOrgProfile({ kind: "merchant", id: "" });
  const partner = readOrgProfile({ kind: "partner", id: period.partnerId });

  return buildInvoiceProforma({
    period,
    issuer,
    billTo,
    viaPartnerName: partner.name || undefined,
    lang,
  });
}

/** Direction 2 — a partner SETTLEMENT STATEMENT (payable). A separate document. */
export function statementProformaFromPartner(
  statement: PartnerStatement,
  lang: Lang,
): ProformaDocument {
  const issuer = readOrgProfile(PLATFORM_SCOPE);
  const billTo = readOrgProfile({ kind: "partner", id: statement.partnerId });
  return buildStatementProforma({ statement, issuer, billTo, lang });
}

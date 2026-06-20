import { translations, type Lang } from "@/lib/i18n";

function label(lang: Lang, key: string, fallback: string): string {
  return translations[lang][key] ?? fallback;
}

export function teamName(lang: Lang, teamId: string, fallback: string): string {
  const key = `teamName_${teamId.replace(/-/g, "_")}`;
  return label(lang, key, fallback);
}

export function teamKindLabel(lang: Lang, kind: string): string {
  return label(lang, `teamKind_${kind}`, kind);
}

export function partnerStatusLabel(lang: Lang, status: string): string {
  return label(lang, `partnerStatus_${status}`, status);
}

export function participationModeLabel(lang: Lang, mode: string): string {
  return label(lang, `participationMode_${mode}`, mode);
}

export function offeringKindLabel(lang: Lang, kind: string): string {
  return label(lang, `offeringKind_${kind}`, kind);
}

export function activationStatusLabel(lang: Lang, status: string): string {
  return label(lang, `activationStatus_${status}`, status);
}

export function webhookEndpointStatusLabel(lang: Lang, status: string): string {
  return label(lang, `webhookEndpointStatus_${status}`, status);
}

export function webhookDeliveryStatusLabel(lang: Lang, status: string): string {
  return label(lang, `webhookDeliveryStatus_${status}`, status);
}

export function settlementStateLabel(lang: Lang, state: string): string {
  return label(lang, `settlementState_${state}`, state);
}

export function settlementBookLabel(lang: Lang, book: string): string {
  return label(lang, `settlementBook_${book}`, book);
}

export function billingPeriodStatusLabel(lang: Lang, status: string): string {
  return label(lang, `billingPeriodStatus_${status}`, status);
}

export function posSyncStatusLabel(lang: Lang, status: string): string {
  return label(lang, `posSyncStatus_${status}`, status);
}

export function journalDirectionLabel(lang: Lang, direction: string): string {
  return label(lang, `journalDirection_${direction}`, direction);
}

export function settlementAccountLabel(lang: Lang, account: string): string {
  return label(lang, `settlementAccount_${account}`, account);
}

const AUDIT_ACTION_KEYS: Record<string, string> = {
  "Approved activation": "auditApprovedActivation",
  "Set billing terms": "auditSetBillingTerms",
  "Invited user (mock — no email sent)": "auditInvitedUser",
  "Resent invite (mock — no email sent)": "auditResentInvite",
  "Updated user": "auditUpdatedUser",
  "Rotated client secret": "auditRotatedClientSecret",
  "Triggered reversal": "auditTriggeredReversal",
  "Generated invoice (BETA)": "auditGeneratedInvoice",
  "Advanced billing period": "auditAdvancedBillingPeriod",
};

export function auditActionLabel(lang: Lang, action: string): string {
  const key = AUDIT_ACTION_KEYS[action];
  return key ? label(lang, key, action) : action;
}

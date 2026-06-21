/** Issuer / partner / merchant organisation profile — mock-only until invoice phase. */

export type OrgProfileKind = "platform" | "partner" | "merchant";

export interface OrgProfileScope {
  kind: OrgProfileKind;
  /** platform → fixed "zahy"; partner → partnerId; merchant → tenantId */
  id: string;
}

export interface OrgProfileContacts {
  phone: string;
  email: string;
}

export interface OrgProfile {
  name: string;
  vatNumber: string;
  crNumber: string;
  nationalNumber?: string;
  address: string;
  contacts: OrgProfileContacts;
}

export type OrgProfileField = keyof OrgProfile | "phone" | "email";

export type OrgProfileFieldErrors = Partial<Record<OrgProfileField, string>>;

export function orgProfileScopeKey(scope: OrgProfileScope): string {
  return `${scope.kind}:${scope.id}`;
}

export function emptyOrgProfile(partial?: Partial<OrgProfile>): OrgProfile {
  return {
    name: partial?.name ?? "",
    vatNumber: partial?.vatNumber ?? "",
    crNumber: partial?.crNumber ?? "",
    nationalNumber: partial?.nationalNumber ?? "",
    address: partial?.address ?? "",
    contacts: {
      phone: partial?.contacts?.phone ?? "",
      email: partial?.contacts?.email ?? "",
    },
  };
}

const VAT_PATTERN = /^3\d{14}$/;
const CR_PATTERN = /^\d{10}$/;
const NATIONAL_PATTERN = /^[12]\d{9}$/;
const PHONE_PATTERN = /^\+?\d{8,15}$/;

function isValidEmail(value: string): boolean {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value.trim());
}

/** Display-level validation — not tax compliance. */
export function validateOrgProfile(
  profile: OrgProfile,
  options: { requireNationalNumber: boolean },
): OrgProfileFieldErrors {
  const errors: OrgProfileFieldErrors = {};
  const name = profile.name.trim();
  const vat = profile.vatNumber.trim();
  const cr = profile.crNumber.trim();
  const national = profile.nationalNumber?.trim() ?? "";
  const address = profile.address.trim();
  const phone = profile.contacts.phone.trim();
  const email = profile.contacts.email.trim();

  if (name.length < 2) errors.name = "required";
  if (!VAT_PATTERN.test(vat)) errors.vatNumber = "format";
  if (!CR_PATTERN.test(cr)) errors.crNumber = "format";
  if (options.requireNationalNumber) {
    if (!national) errors.nationalNumber = "required";
    else if (!NATIONAL_PATTERN.test(national)) errors.nationalNumber = "format";
  } else if (national && !NATIONAL_PATTERN.test(national)) {
    errors.nationalNumber = "format";
  }
  if (address.length < 5) errors.address = "required";
  if (!phone || !PHONE_PATTERN.test(phone.replace(/[\s-]/g, ""))) errors.phone = "format";
  if (!email || !isValidEmail(email)) errors.email = "format";

  return errors;
}

export function hasOrgProfileErrors(errors: OrgProfileFieldErrors): boolean {
  return Object.keys(errors).length > 0;
}

export const DEFAULT_PLATFORM_PROFILE: OrgProfile = {
  name: "Zahy Platform KSA",
  vatNumber: "300000000000003",
  crNumber: "1010000000",
  address: "برج زاهي، طريق الملك فهد، الرياض 12211، المملكة العربية السعودية",
  contacts: { phone: "+966112345678", email: "billing@zahy.sa" },
};

export const DEFAULT_PARTNER_PROFILES: Record<string, OrgProfile> = {
  "22222222-2222-2222-2222-222222222001": {
    name: "Salasa Logistics LLC",
    vatNumber: "310000000000001",
    crNumber: "1010123456",
    nationalNumber: "7000000001",
    address: "المنطقة الصناعية، جدة 21421، المملكة العربية السعودية",
    contacts: { phone: "+966126543210", email: "ops@salasa.sa" },
  },
  "22222222-2222-2222-2222-222222222004": {
    name: "Jahez Integration Services",
    vatNumber: "310000000000004",
    crNumber: "1010456789",
    nationalNumber: "7000000004",
    address: "حي العليا، الرياض 12333، المملكة العربية السعودية",
    contacts: { phone: "+966114567890", email: "billing@jahez.sa" },
  },
};

export const DEFAULT_MERCHANT_PROFILES: Record<string, OrgProfile> = {
  "11111111-1111-1111-1111-111111111099": {
    name: "Al Rajhi Demo Store",
    vatNumber: "310000000000099",
    crNumber: "1010999999",
    nationalNumber: "7000000099",
    address: "شارع التحلية، الرياض 11564، المملكة العربية السعودية",
    contacts: { phone: "+966501234567", email: "merchant@zahy.dev" },
  },
};

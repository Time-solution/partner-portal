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
    name: "WhatsApp Business Co.",
    vatNumber: "310000000000004",
    crNumber: "1010456789",
    nationalNumber: "7000000004",
    address: "حي العليا، الرياض 12333، المملكة العربية السعودية",
    contacts: { phone: "+966114567890", email: "billing@whatsapp.co" },
  },
  "22222222-2222-2222-2222-222222222006": {
    name: "The Chefz Food Delivery",
    vatNumber: "310000000000006",
    crNumber: "1010567890",
    nationalNumber: "7000000006",
    address: "حي الياسمين، الرياض 13325، المملكة العربية السعودية",
    contacts: { phone: "+966112345600", email: "partners@thechefz.co" },
  },
  "22222222-2222-2222-2222-222222222007": {
    name: "Oto Logistics KSA",
    vatNumber: "310000000000007",
    crNumber: "1010678901",
    nationalNumber: "7000000007",
    address: "طريق المدينة، جدة 23523، المملكة العربية السعودية",
    contacts: { phone: "+966126789012", email: "partners@oto.sa" },
  },
  "22222222-2222-2222-2222-222222222008": {
    name: "W there Technologies KSA",
    vatNumber: "310000000000008",
    crNumber: "1010789012",
    nationalNumber: "7000000008",
    address: "حي الملقا، الرياض 13525، المملكة العربية السعودية",
    contacts: { phone: "+966115678901", email: "billing@wthere.co" },
  },
};

export const DEFAULT_MERCHANT_PROFILES: Record<string, OrgProfile> = {
  "11111111-1111-1111-1111-111111111001": {
    name: "Burger Co",
    vatNumber: "310000000000101",
    crNumber: "1010101010",
    nationalNumber: "7000000101",
    address: "طريق الأمير محمد بن عبدالعزيز، الرياض 12241، المملكة العربية السعودية",
    contacts: { phone: "+966501111111", email: "manager@burger.co" },
  },
  "11111111-1111-1111-1111-111111111002": {
    name: "Coffee Spot",
    vatNumber: "310000000000102",
    crNumber: "1010202020",
    nationalNumber: "7000000102",
    address: "شارع العروبة، الخبر 34446، المملكة العربية السعودية",
    contacts: { phone: "+966502222222", email: "ops@coffeespot.sa" },
  },
  "11111111-1111-1111-1111-111111111003": {
    name: "Pizza House",
    vatNumber: "310000000000103",
    crNumber: "1010303030",
    nationalNumber: "7000000103",
    address: "حي النخيل، الدمام 32271، المملكة العربية السعودية",
    contacts: { phone: "+966503333333", email: "finance@pizzahouse.sa" },
  },
};

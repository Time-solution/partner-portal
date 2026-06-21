import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  DEFAULT_PLATFORM_PROFILE,
  emptyOrgProfile,
  hasOrgProfileErrors,
  orgProfileScopeKey,
  validateOrgProfile,
} from "./orgProfile";
import { readOrgProfile, resetOrgProfiles, writeOrgProfile } from "./orgProfileStore";

describe("validateOrgProfile", () => {
  const validPartner = {
    ...emptyOrgProfile(),
    name: "Salasa Logistics LLC",
    vatNumber: "310000000000001",
    crNumber: "1010123456",
    nationalNumber: "7000000001",
    address: "Industrial Area, Jeddah 21421, KSA",
    contacts: { phone: "+966126543210", email: "ops@salasa.sa" },
  };

  it("accepts platform profile without national number", () => {
    const errors = validateOrgProfile(DEFAULT_PLATFORM_PROFILE, { requireNationalNumber: false });
    expect(hasOrgProfileErrors(errors)).toBe(false);
    expect(errors.nationalNumber).toBeUndefined();
  });

  it("requires national number for partner/merchant profiles", () => {
    const withoutNational = { ...validPartner, nationalNumber: "" };
    const errors = validateOrgProfile(withoutNational, { requireNationalNumber: true });
    expect(errors.nationalNumber).toBe("required");
  });

  it("rejects invalid VAT format", () => {
    const errors = validateOrgProfile(
      { ...validPartner, vatNumber: "123" },
      { requireNationalNumber: true },
    );
    expect(errors.vatNumber).toBe("format");
  });

  it("rejects invalid email", () => {
    const errors = validateOrgProfile(
      { ...validPartner, contacts: { ...validPartner.contacts, email: "not-an-email" } },
      { requireNationalNumber: true },
    );
    expect(errors.email).toBe("format");
  });
});

describe("orgProfileStore", () => {
  beforeEach(() => {
    const store: Record<string, string> = {};
    vi.stubGlobal("localStorage", {
      getItem: (key: string) => store[key] ?? null,
      setItem: (key: string, value: string) => {
        store[key] = value;
      },
      removeItem: (key: string) => {
        delete store[key];
      },
    });
    resetOrgProfiles();
  });

  it("loads and saves scoped profiles independently", () => {
    const platformScope = { kind: "platform" as const, id: "zahy" };
    const partnerScope = { kind: "partner" as const, id: "partner-a" };
    const merchantScope = { kind: "merchant" as const, id: "tenant-a" };

    writeOrgProfile(partnerScope, {
      ...emptyOrgProfile(),
      name: "Partner A",
      vatNumber: "310000000000001",
      crNumber: "1010123456",
      nationalNumber: "7000000001",
      address: "Address A",
      contacts: { phone: "+966500000001", email: "a@partner.sa" },
    });
    writeOrgProfile(merchantScope, {
      ...emptyOrgProfile(),
      name: "Merchant A",
      vatNumber: "310000000000099",
      crNumber: "1010999999",
      nationalNumber: "7000000099",
      address: "Address M",
      contacts: { phone: "+966500000099", email: "m@merchant.sa" },
    });

    expect(readOrgProfile(platformScope).name).toBe(DEFAULT_PLATFORM_PROFILE.name);
    expect(readOrgProfile(partnerScope).name).toBe("Partner A");
    expect(readOrgProfile(merchantScope).name).toBe("Merchant A");
    expect(orgProfileScopeKey(partnerScope)).not.toBe(orgProfileScopeKey(merchantScope));
  });
});

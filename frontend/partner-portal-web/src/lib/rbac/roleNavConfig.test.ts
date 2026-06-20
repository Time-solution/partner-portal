import { describe, expect, it } from "vitest";
import {
  MOCK_PARTNER_FINANCE_PARTNER_ID,
  MOCK_PARTNER_PSM_PARTNER_ID,
  defaultLandingPath,
  mockScopedPartnerId,
  roleExperience,
} from "./roleNavConfig";

describe("roleNavConfig", () => {
  it("assigns mock partner scope to partner portal roles", () => {
    expect(mockScopedPartnerId("PartnerSuccessManager")).toBe(MOCK_PARTNER_PSM_PARTNER_ID);
    expect(mockScopedPartnerId("PartnerFinance")).toBe(MOCK_PARTNER_FINANCE_PARTNER_ID);
    expect(mockScopedPartnerId("PlatformAdmin")).toBeUndefined();
  });

  it("maps roles to experience buckets", () => {
    expect(roleExperience("PlatformAdmin")).toBe("admin");
    expect(roleExperience("PartnerSuccessManager")).toBe("partner");
    expect(roleExperience("PartnerFinance")).toBe("partner");
    expect(roleExperience("MerchantPreview")).toBe("merchant");
    expect(roleExperience("Accountant")).toBe("finance");
  });

  it("uses partner home for partner roles", () => {
    expect(defaultLandingPath("PartnerSuccessManager")).toContain(MOCK_PARTNER_PSM_PARTNER_ID);
    expect(defaultLandingPath("PartnerFinance")).toContain(MOCK_PARTNER_FINANCE_PARTNER_ID);
  });
});

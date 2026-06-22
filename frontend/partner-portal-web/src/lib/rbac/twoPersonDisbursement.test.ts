import { describe, expect, it } from "vitest";
import { canReleaseDisbursement } from "./portalRoles";

// FE mirror of the backend Disbursement.Release two-person rule (separation of duties).
describe("canReleaseDisbursement", () => {
  it("blocks the SAME person who reconciled from releasing the same item", () => {
    expect(
      canReleaseDisbursement({
        releaserRole: "PlatformAdmin",
        releaserActorId: "accountant",
        reconciledByActorId: "accountant",
      }),
    ).toBe(false);
  });

  it("compares actor ids case-insensitively / trimmed (not by role)", () => {
    expect(
      canReleaseDisbursement({
        releaserRole: "PlatformAdmin",
        releaserActorId: " Accountant ",
        reconciledByActorId: "accountant",
      }),
    ).toBe(false);
  });

  it("allows a DIFFERENT disburse-privileged person to release", () => {
    expect(
      canReleaseDisbursement({
        releaserRole: "PlatformAdmin",
        releaserActorId: "platform-admin",
        reconciledByActorId: "accountant",
      }),
    ).toBe(true);
  });

  it("blocks a releaser without the Disburse privilege regardless of identity", () => {
    expect(
      canReleaseDisbursement({
        releaserRole: "Accountant",
        releaserActorId: "different-accountant",
        reconciledByActorId: "accountant",
      }),
    ).toBe(false);
  });

  it("blocks an empty releaser id", () => {
    expect(
      canReleaseDisbursement({
        releaserRole: "PlatformAdmin",
        releaserActorId: "",
        reconciledByActorId: "accountant",
      }),
    ).toBe(false);
  });
});

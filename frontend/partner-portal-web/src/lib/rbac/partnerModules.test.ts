import { describe, expect, it } from "vitest";
import type { Partner } from "@/lib/data/types";
import { resolvePartnerModule } from "./partnerModules";

const base = (over: Partial<Partner>): Partner => ({
  id: "1",
  legalName: "Test",
  type: "Service",
  status: "Active",
  primaryContactEmail: "a@b.com",
  participationMode: "Principal",
  accentClass: "",
  ...over,
});

describe("resolvePartnerModule", () => {
  it("maps Salasa-style Principal to delivery-service", () => {
    expect(
      resolvePartnerModule(
        base({ type: "Carrier", participationMode: "Principal", tradeName: "Salasa" }),
      ),
    ).toBe("delivery-service");
  });

  it("maps noon-style ReflectionOnly commerce to commerce module", () => {
    expect(
      resolvePartnerModule(
        base({
          type: "Marketplace",
          participationMode: "ReflectionOnly",
          marketplaceCategory: "commerce",
        }),
      ),
    ).toBe("commerce");
  });

  it("maps HungerStation-style ReflectionOnly fnb to fnb module", () => {
    expect(
      resolvePartnerModule(
        base({
          type: "Aggregator",
          participationMode: "ReflectionOnly",
          marketplaceCategory: "fnb",
        }),
      ),
    ).toBe("fnb");
  });

  it("maps Jahez-style SubscriptionFee to subscriptions", () => {
    expect(resolvePartnerModule(base({ participationMode: "SubscriptionFee" }))).toBe(
      "subscriptions",
    );
  });

  it("maps Marketplace Principal to marketplace module", () => {
    expect(
      resolvePartnerModule(base({ type: "Marketplace", participationMode: "Principal" })),
    ).toBe("marketplace");
  });
});

import { describe, expect, it } from "vitest";
import { mapAbpConfigToAuthUser } from "./session";

describe("mapAbpConfigToAuthUser", () => {
  it("derives the permission list from granted policies", () => {
    const user = mapAbpConfigToAuthUser({
      currentUser: { id: "u1", userName: "ops", name: "Ops Admin", roles: ["Platform.PartnerOps"] },
      auth: {
        grantedPolicies: {
          "Zahy.Catalog.Read": true,
          "Zahy.Orders.Read": true,
          "Zahy.Payouts.Read": false,
        },
      },
    });

    expect(user.id).toBe("u1");
    expect(user.username).toBe("ops");
    expect(user.name).toBe("Ops Admin");
    expect(user.roles).toEqual(["Platform.PartnerOps"]);
    expect(user.permissions).toEqual(["Zahy.Catalog.Read", "Zahy.Orders.Read"]);
    expect(user.permissions).not.toContain("Zahy.Payouts.Read");
  });

  it("falls back to token claims when the current user is absent", () => {
    const user = mapAbpConfigToAuthUser(
      {},
      { id: "sub-123", username: "from-token", roles: ["Platform.ReadOnly"] },
    );

    expect(user.id).toBe("sub-123");
    expect(user.username).toBe("from-token");
    expect(user.roles).toEqual(["Platform.ReadOnly"]);
    expect(user.permissions).toEqual([]);
  });

  it("prefers userName for the display name when name is missing", () => {
    const user = mapAbpConfigToAuthUser({ currentUser: { userName: "jdoe" } });
    expect(user.name).toBe("jdoe");
  });
});

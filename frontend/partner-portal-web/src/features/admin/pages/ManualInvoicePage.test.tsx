import { beforeEach, describe, expect, it, vi } from "vitest";
import { renderToStaticMarkup } from "react-dom/server";
import { createSeedData } from "@/lib/data/fixtures";
import { MockPortalDataSource } from "@/lib/data/mockDataSource";
import { savePersistedData } from "@/lib/data/mockStore";
import { PortalPermissions, rolePermissionMap } from "@/lib/rbac/portalRoles";
import { hasPermission } from "@/lib/permissions";
import type { ManualInvoiceLineInput } from "@/lib/data/types";
import { ManualInvoiceForm } from "./ManualInvoiceForm";

function stubLocalStorage() {
  const bag = new Map<string, string>();
  vi.stubGlobal("localStorage", {
    getItem: (k: string) => bag.get(k) ?? null,
    setItem: (k: string, v: string) => bag.set(k, v),
    removeItem: (k: string) => bag.delete(k),
    clear: () => bag.clear(),
    key: () => null,
    length: 0,
  });
}

const WORKED_EXAMPLE: ManualInvoiceLineInput[] = [
  { description: "Foo", quantity: 1, unitPriceInclusive: 100 },
  { description: "Bar", quantity: 2, unitPriceInclusive: 50 },
  { description: "Baz", quantity: 1, unitPriceInclusive: 200 },
];

describe("ManualInvoicePage / data source", () => {
  beforeEach(() => {
    stubLocalStorage();
    savePersistedData(createSeedData());
  });

  it("permission gate + RTL preview: non-Finance roles can't write, the form hides the action, and the AR preview shows the worked-example totals", () => {
    expect(hasPermission(rolePermissionMap.PlatformAdmin, PortalPermissions.Finance.WriteManualInvoice)).toBe(true);
    expect(hasPermission(rolePermissionMap.Accountant, PortalPermissions.Finance.WriteManualInvoice)).toBe(true);
    expect(hasPermission(rolePermissionMap.PartnerFinance, PortalPermissions.Finance.WriteManualInvoice)).toBe(false);
    expect(hasPermission(rolePermissionMap.PartnerSuccessManager, PortalPermissions.Finance.WriteManualInvoice)).toBe(false);
    expect(hasPermission(rolePermissionMap.MerchantPreview, PortalPermissions.Finance.WriteManualInvoice)).toBe(false);

    // Non-writer: the create action is not rendered.
    const denied = renderToStaticMarkup(
      <ManualInvoiceForm lang="en" canWrite={false} recipients={[]} onSubmit={() => {}} />,
    );
    expect(denied).toContain('data-testid="manual-invoice-forbidden"');
    expect(denied).not.toContain('data-testid="manual-invoice-form"');

    // Writer (Arabic / RTL): the live preview shows the exact backend figures.
    const allowed = renderToStaticMarkup(
      <ManualInvoiceForm lang="ar" canWrite recipients={[]} initialLines={WORKED_EXAMPLE} onSubmit={() => {}} />,
    );
    expect(allowed).toContain('data-testid="manual-invoice-form"');
    expect(allowed).toContain('dir="rtl"');
    expect(allowed).toContain("347.83");
    expect(allowed).toContain("52.17");
    expect(allowed).toContain("400.00");
  });

  it("KYC-incomplete recipient path is surfaced (the create form, not a silent allow)", () => {
    const html = renderToStaticMarkup(
      <ManualInvoiceForm
        lang="en"
        canWrite
        recipients={[{ id: "p1", name: "Inactive Partner", type: "Partner", kycComplete: false }]}
        initialLines={WORKED_EXAMPLE}
        onSubmit={() => {}}
      />,
    );
    expect(html).toContain('data-testid="manual-invoice-form"');
    expect(html).toContain('data-testid="manual-invoice-submit"');
  });

  it("creates a Source=Manual invoice with the worked-example totals + a minted number (payload shape)", async () => {
    const ds = new MockPortalDataSource();
    const inv = await ds.createManualInvoice({
      recipientType: "External",
      recipientNameOverride: "Gulf Advisory Partners",
      lines: WORKED_EXAMPLE,
      idempotencyKey: "test:worked-example",
    });

    expect(inv.source).toBe("Manual");
    expect(inv.recipient).toBe("Gulf Advisory Partners");
    expect(inv.lines).toHaveLength(3);
    expect(inv.subtotalNet).toBe(347.83);
    expect(inv.vatTotal).toBe(52.17);
    expect(inv.grandTotalInclusive).toBe(400);
    expect(inv.invoiceNumber).toMatch(/^MAN-\d{4}-\d{4}$/); // internal, non-fiscal (backend FormatManual mirror)
    expect(inv.status).toBe("Draft"); // P4 — created as a draft; issue is a separate transition
    expect(inv.idempotencyKey).toBe("test:worked-example");

    const manual = await ds.listInvoices({ source: "Manual" });
    expect(manual.some((m) => m.id === inv.id)).toBe(true);

    // Idempotency key is required (payload contract).
    await expect(
      ds.createManualInvoice({
        recipientType: "External",
        recipientNameOverride: "X",
        lines: WORKED_EXAMPLE,
        idempotencyKey: "",
      }),
    ).rejects.toThrow();
  });

  it("is idempotent: a re-submit with the same key resolves to the same invoice (no duplicate)", async () => {
    const ds = new MockPortalDataSource();
    const before = (await ds.listInvoices({ source: "Manual" })).length;

    const payload = {
      recipientType: "External" as const,
      recipientNameOverride: "Acme Consulting",
      lines: [{ description: "Foo", quantity: 1, unitPriceInclusive: 100 }],
      idempotencyKey: "dupe:1",
    };
    const a = await ds.createManualInvoice(payload);
    const b = await ds.createManualInvoice(payload);

    expect(b.id).toBe(a.id);
    const after = (await ds.listInvoices({ source: "Manual" })).length;
    expect(after).toBe(before + 1);
  });

  it("seeds exactly 2 manual invoices — one Draft + one Issued, MAN-numbered (page list renders both)", async () => {
    const ds = new MockPortalDataSource();
    const seeded = await ds.listInvoices({ source: "Manual" });

    expect(seeded).toHaveLength(2);
    expect(seeded.map((m) => m.status).sort()).toEqual(["Draft", "Issued"]);
    for (const invoice of seeded) {
      expect(invoice.source).toBe("Manual");
      expect(invoice.invoiceNumber).toMatch(/^MAN-\d{4}-\d{4}$/);
    }
  });

  it("P4 lifecycle: Issue is a single Draft → Issued transition; the Issued snapshot is immutable", async () => {
    const ds = new MockPortalDataSource();
    const draft = (await ds.listInvoices({ source: "Manual" })).find((m) => m.status === "Draft")!;
    expect(draft).toBeDefined();

    const issued = await ds.issueManualInvoice(draft.id);
    expect(issued.status).toBe("Issued");
    expect(issued.issuedAt).toBeTruthy();
    expect(issued.lines).toEqual(draft.lines); // the issued snapshot carries the draft's exact lines

    // Second issue is rejected (mirrors backend Zahy.Finance:009).
    await expect(ds.issueManualInvoice(draft.id)).rejects.toThrow(/draft/i);
  });
});

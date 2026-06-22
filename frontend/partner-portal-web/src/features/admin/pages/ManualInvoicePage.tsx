import { useEffect, useState } from "react";
import { getPortalDataSource } from "@/lib/data";
import { PortalPermissions } from "@/lib/rbac/portalRoles";
import { usePortalSession } from "@/features/auth/usePortalSession";
import type { Lang } from "@/lib/i18n";
import type { CreateManualInvoiceInput, ManualInvoice } from "@/lib/data/types";
import { downloadManualInvoicePdf } from "@/lib/finance/manualInvoiceApi";
import { ManualInvoiceForm, type ManualRecipientOption } from "./ManualInvoiceForm";

export { ManualInvoiceForm } from "./ManualInvoiceForm";
export type { ManualRecipientOption } from "./ManualInvoiceForm";

/** Route page — wires session permission + the mock data source to {@link ManualInvoiceForm}. */
export function ManualInvoicePage({ lang }: { lang: Lang }) {
  const { can } = usePortalSession();
  const canWrite = can(PortalPermissions.Finance.WriteManualInvoice);

  const [recipients, setRecipients] = useState<ManualRecipientOption[]>([]);
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<ManualInvoice | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    void getPortalDataSource()
      .getPartners()
      .then((partners) =>
        setRecipients(
          partners.map((p) => ({
            id: p.id,
            name: p.tradeName || p.legalName,
            type: "Partner" as const,
            // Mock KYC readiness: an Active partner is treated as KYC-complete.
            kycComplete: p.status === "Active",
          })),
        ),
      );
  }, []);

  const handleSubmit = async (input: CreateManualInvoiceInput) => {
    setBusy(true);
    setError(null);
    try {
      setResult(await getPortalDataSource().createManualInvoice(input));
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };

  const handleDownload = async (invoice: ManualInvoice) => {
    try {
      await downloadManualInvoicePdf(invoice, lang);
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    }
  };

  return (
    <ManualInvoiceForm
      lang={lang}
      canWrite={canWrite}
      recipients={recipients}
      busy={busy}
      result={result}
      error={error}
      onSubmit={handleSubmit}
      onDownloadPdf={handleDownload}
      onReset={() => setResult(null)}
    />
  );
}

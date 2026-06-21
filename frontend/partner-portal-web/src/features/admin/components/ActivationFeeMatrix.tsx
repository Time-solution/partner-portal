import { useState } from "react";
import { Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { BetaBadge } from "@/components/brand/BetaBadge";
import { getPortalDataSource } from "@/lib/data";
import type { ActivationFeeConfig, ActivationFeeLine, ActivationFeePayer } from "@/lib/data/types";
import { MoneyAmount } from "@/components/MoneyAmount";
import { Riyal } from "@/components/Riyal";
import { feeBreakdown } from "@/lib/reports/activationFees";
import { useTranslator, type Lang } from "@/lib/i18n";

const EMPTY: ActivationFeeConfig = {
  subscription: { enabled: false, amountInclusive: { amount: 40, currency: "SAR", vatInclusive: true }, payer: "Merchant" },
  perTransaction: { enabled: false, amountInclusive: { amount: 1, currency: "SAR", vatInclusive: true }, payer: "Merchant" },
};

/**
 * Per-activation fee matrix — config + DISPLAY ONLY. Both lines may be ON at once and
 * each may bill either side. Persists to the mock store; NEVER posts a journal.
 */
export function ActivationFeeMatrix({
  activationId,
  fees,
  lang,
  canEdit,
  onSaved,
}: {
  activationId: string;
  fees?: ActivationFeeConfig;
  lang: Lang;
  canEdit: boolean;
  onSaved?: () => void;
}) {
  const t = useTranslator(lang);
  const [draft, setDraft] = useState<ActivationFeeConfig>(fees ?? EMPTY);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);

  const update = (kind: keyof ActivationFeeConfig, patch: Partial<ActivationFeeLine>) => {
    setSaved(false);
    setDraft((d) => ({ ...d, [kind]: { ...d[kind], ...patch } }));
  };

  const save = async () => {
    setSaving(true);
    try {
      await getPortalDataSource().setActivationFees(activationId, draft);
      setSaved(true);
      onSaved?.();
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="rounded-md border border-border p-3">
      <div className="mb-2 flex items-center gap-2">
        <h4 className="text-sm font-semibold">{t("feeMatrixTitle" as never)}</h4>
        <BetaBadge lang={lang} />
      </div>
      <p className="mb-3 text-xs text-muted-foreground">{t("feeMatrixDesc" as never)}</p>

      <table className="w-full text-sm">
        <thead>
          <tr className="border-b border-border text-xs text-muted-foreground">
            <th className="py-1.5 text-start font-medium">{t("feeMatrixFee" as never)}</th>
            <th className="py-1.5 text-center font-medium">{t("feeMatrixOn" as never)}</th>
            <th className="py-1.5 text-start font-medium">{t("feeMatrixAmount" as never)}</th>
            <th className="py-1.5 text-start font-medium">{t("feeMatrixPayer" as never)}</th>
            <th className="py-1.5 text-start font-medium">{t("feePreview" as never)}</th>
          </tr>
        </thead>
        <tbody>
          <FeeRow
            label={`${t("feeSubscription" as never)} (${t("feePerMonth" as never)})`}
            line={draft.subscription}
            canEdit={canEdit}
            onChange={(p) => update("subscription", p)}
            t={t}
          />
          <FeeRow
            label={`${t("feePerTransaction" as never)} (${t("feePerTxn" as never)})`}
            line={draft.perTransaction}
            canEdit={canEdit}
            onChange={(p) => update("perTransaction", p)}
            t={t}
          />
        </tbody>
      </table>

      {canEdit ? (
        <div className="mt-3 flex items-center gap-3">
          <Button size="sm" disabled={saving} onClick={() => void save()}>
            {saving ? <Loader2 className="h-4 w-4 animate-spin" /> : null}
            {t("feeSave" as never)}
          </Button>
          {saved ? <span className="text-xs text-emerald-600 dark:text-emerald-400">{t("feeSaved" as never)}</span> : null}
        </div>
      ) : null}
    </div>
  );
}

function FeeRow({
  label,
  line,
  canEdit,
  onChange,
  t,
}: {
  label: string;
  line: ActivationFeeLine;
  canEdit: boolean;
  onChange: (patch: Partial<ActivationFeeLine>) => void;
  t: (k: string) => string;
}) {
  const b = feeBreakdown(line.amountInclusive.amount);
  return (
    <tr className="border-b border-border/60">
      <td className="py-2 font-medium">{label}</td>
      <td className="py-2 text-center">
        <input
          type="checkbox"
          checked={line.enabled}
          disabled={!canEdit}
          onChange={(e) => onChange({ enabled: e.target.checked })}
          aria-label={label}
        />
      </td>
      <td className="py-2">
        <input
          type="number"
          min={0}
          step="0.5"
          className="h-8 w-24 rounded-md border border-input bg-background px-2 text-sm disabled:opacity-60"
          value={line.amountInclusive.amount}
          disabled={!canEdit}
          onChange={(e) =>
            onChange({ amountInclusive: { ...line.amountInclusive, amount: Number(e.target.value) || 0 } })
          }
        />
        <span className="ms-1 inline-flex items-baseline text-xs text-muted-foreground">
          <Riyal />
        </span>
      </td>
      <td className="py-2">
        <select
          className="h-8 rounded-md border border-input bg-background px-2 text-sm disabled:opacity-60"
          value={line.payer}
          disabled={!canEdit}
          onChange={(e) => onChange({ payer: e.target.value as ActivationFeePayer })}
        >
          <option value="Merchant">{t("feePayerMerchant" as never)}</option>
          <option value="Partner">{t("feePayerPartner" as never)}</option>
        </select>
      </td>
      <td className="py-2 text-xs tabular-nums text-muted-foreground">
        {line.enabled ? (
          <span className="inline-flex flex-wrap items-baseline gap-1">
            <MoneyAmount amount={b.inclusive} /> {t("feeIncl" as never)} →{" "}
            <MoneyAmount amount={b.net} /> {t("feeExVat" as never)} +{" "}
            <MoneyAmount amount={b.vat} /> {t("feeVat" as never)}
          </span>
        ) : (
          "—"
        )}
      </td>
    </tr>
  );
}

import { useMemo, useState } from "react";
import { Download, Loader2, Plus, Trash2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { MoneyAmount } from "@/components/MoneyAmount";
import { BetaBadge } from "@/components/brand/BetaBadge";
import { useTranslator, type Lang } from "@/lib/i18n";
import type {
  CreateManualInvoiceInput,
  ManualInvoice,
  ManualInvoiceLineInput,
  ManualInvoiceRecipientType,
} from "@/lib/data/types";
import {
  isManualInvoiceValid,
  previewManualInvoice,
  validateManualInvoiceLines,
} from "@/lib/invoice/manualInvoice";

/** A selectable recipient with its KYC readiness (mock derives this; live reads the profile). */
export interface ManualRecipientOption {
  id: string;
  name: string;
  type: ManualInvoiceRecipientType;
  kycComplete: boolean;
}

interface LineRow {
  description: string;
  quantity: string;
  unitPrice: string;
}

const EMPTY_LINE: LineRow = { description: "", quantity: "1", unitPrice: "" };

function todayIso(): string {
  return new Date().toISOString().slice(0, 10);
}

export interface ManualInvoiceFormProps {
  lang: Lang;
  canWrite: boolean;
  recipients: ManualRecipientOption[];
  busy?: boolean;
  result?: ManualInvoice | null;
  error?: string | null;
  /** Seed the line editor (used by tests / pre-fill); defaults to one empty line. */
  initialLines?: ManualInvoiceLineInput[];
  onSubmit: (input: CreateManualInvoiceInput) => void;
  onDownloadPdf?: (invoice: ManualInvoice) => void;
  onReset?: () => void;
}

/**
 * Presentational create form (props only — NO context hooks, NO data source import) so it
 * can be rendered in tests via renderToStaticMarkup. Totals preview uses the SAME locked
 * inclusive back-out as the backend, so the previewed grand total equals what is persisted.
 */
export function ManualInvoiceForm({
  lang,
  canWrite,
  recipients,
  busy = false,
  result = null,
  error = null,
  initialLines,
  onSubmit,
  onDownloadPdf,
  onReset,
}: ManualInvoiceFormProps) {
  const t = useTranslator(lang);
  const dir = lang === "ar" ? "rtl" : "ltr";

  const [recipientType, setRecipientType] = useState<ManualInvoiceRecipientType>("Partner");
  const [recipientId, setRecipientId] = useState("");
  const [externalName, setExternalName] = useState("");
  const [issueDate, setIssueDate] = useState(todayIso());
  const [notes, setNotes] = useState("");
  const [lines, setLines] = useState<LineRow[]>(() =>
    initialLines && initialLines.length > 0
      ? initialLines.map((l) => ({
          description: l.description,
          quantity: String(l.quantity),
          unitPrice: String(l.unitPriceInclusive),
        }))
      : [{ ...EMPTY_LINE }],
  );

  const inputs: ManualInvoiceLineInput[] = useMemo(
    () =>
      lines.map((l) => ({
        description: l.description,
        quantity: Number(l.quantity) || 0,
        unitPriceInclusive: Number(l.unitPrice) || 0,
      })),
    [lines],
  );

  const { lines: computed, totals } = useMemo(() => previewManualInvoice(inputs), [inputs]);
  const lineErrors = useMemo(() => validateManualInvoiceLines(inputs), [inputs]);
  const linesValid = isManualInvoiceValid(inputs);

  const recipientOptions = useMemo(
    () => recipients.filter((r) => r.type === recipientType),
    [recipients, recipientType],
  );
  const selectedRecipient =
    recipientType === "External" ? null : recipientOptions.find((r) => r.id === recipientId) ?? null;
  const recipientValid =
    recipientType === "External" ? externalName.trim().length > 0 : !!selectedRecipient;
  const kycComplete = recipientType === "External" ? true : !!selectedRecipient?.kycComplete;
  const kycBlocked = recipientValid && !kycComplete;

  const canSubmit = canWrite && recipientValid && kycComplete && linesValid && !busy;

  const inputClass = "w-full rounded-md border border-input bg-background px-3 py-2 text-sm";

  const updateLine = (index: number, patch: Partial<LineRow>) =>
    setLines((rows) => rows.map((row, i) => (i === index ? { ...row, ...patch } : row)));
  const addLine = () => setLines((rows) => [...rows, { ...EMPTY_LINE }]);
  const removeLine = (index: number) =>
    setLines((rows) => (rows.length <= 1 ? rows : rows.filter((_, i) => i !== index)));

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!canSubmit) return;
    const input: CreateManualInvoiceInput = {
      recipientType,
      recipientReference: recipientType === "External" ? undefined : recipientId,
      recipientNameOverride: recipientType === "External" ? externalName.trim() : undefined,
      lines: inputs,
      notes: notes.trim() || undefined,
      issueDate,
      currency: "SAR",
      idempotencyKey: crypto.randomUUID(),
    };
    onSubmit(input);
  };

  if (!canWrite) {
    return (
      <Card>
        <CardContent className="py-6">
          <p data-testid="manual-invoice-forbidden" className="text-sm text-muted-foreground">
            {t("manualInvoiceForbidden" as never)}
          </p>
        </CardContent>
      </Card>
    );
  }

  // Success view — minted number + PDF action.
  if (result) {
    return (
      <Card dir={dir} data-testid="manual-invoice-result">
        <CardHeader>
          <div className="flex items-center gap-2">
            <CardTitle className="text-lg">{t("manualInvoiceTitle" as never)}</CardTitle>
            <BetaBadge lang={lang} />
          </div>
          <CardDescription>{t("manualInvoiceBetaNote" as never)}</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <p className="text-sm font-semibold text-emerald-700 dark:text-emerald-300">
            {t("manualInvoiceCreated" as never).replace("{number}", result.invoiceNumber)}
          </p>
          <div className="flex flex-wrap items-center justify-between gap-3 rounded-md border border-border p-3">
            <div>
              <p className="font-medium">{result.recipient}</p>
              <p className="text-sm text-muted-foreground">{result.issueDate.slice(0, 10)}</p>
            </div>
            <p className="text-lg font-bold tabular-nums">
              <MoneyAmount amount={result.grandTotalInclusive} />
            </p>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <Button size="sm" onClick={() => onDownloadPdf?.(result)}>
              <Download className="h-4 w-4" />
              {t("manualInvoiceDownloadPdf" as never)}
            </Button>
            <Button size="sm" variant="ghost" onClick={() => onReset?.()}>
              <Plus className="h-4 w-4" />
              {t("manualInvoiceCreateAnother" as never)}
            </Button>
          </div>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card dir={dir}>
      <CardHeader>
        <div className="flex items-center gap-2">
          <CardTitle className="text-lg">{t("manualInvoiceTitle" as never)}</CardTitle>
          <BetaBadge lang={lang} />
        </div>
        <CardDescription>{t("manualInvoiceDesc" as never)}</CardDescription>
      </CardHeader>
      <CardContent>
        <form data-testid="manual-invoice-form" className="space-y-5" onSubmit={handleSubmit}>
          {/* Recipient */}
          <div className="grid gap-3 sm:grid-cols-3">
            <label className="text-sm">
              <span className="mb-1 block text-muted-foreground">{t("manualInvoiceRecipientType" as never)}</span>
              <select
                className={inputClass}
                value={recipientType}
                onChange={(e) => {
                  setRecipientType(e.target.value as ManualInvoiceRecipientType);
                  setRecipientId("");
                }}
              >
                <option value="Partner">{t("manualInvoiceRecipientPartner" as never)}</option>
                <option value="Merchant">{t("manualInvoiceRecipientMerchant" as never)}</option>
                <option value="External">{t("manualInvoiceRecipientExternal" as never)}</option>
              </select>
            </label>
            <label className="text-sm sm:col-span-2">
              <span className="mb-1 block text-muted-foreground">{t("manualInvoiceRecipient" as never)}</span>
              {recipientType === "External" ? (
                <input
                  type="text"
                  className={inputClass}
                  value={externalName}
                  placeholder={t("manualInvoiceRecipientName" as never)}
                  onChange={(e) => setExternalName(e.target.value)}
                />
              ) : (
                <select
                  className={inputClass}
                  value={recipientId}
                  onChange={(e) => setRecipientId(e.target.value)}
                >
                  <option value="">{t("manualInvoiceRecipientSelect" as never)}</option>
                  {recipientOptions.map((r) => (
                    <option key={r.id} value={r.id}>
                      {r.name}
                      {r.kycComplete ? "" : " ⚠"}
                    </option>
                  ))}
                </select>
              )}
            </label>
          </div>

          <div className="grid gap-3 sm:grid-cols-2">
            <label className="text-sm">
              <span className="mb-1 block text-muted-foreground">{t("manualInvoiceIssueDate" as never)}</span>
              <input
                type="date"
                className={inputClass}
                value={issueDate}
                dir="ltr"
                onChange={(e) => setIssueDate(e.target.value)}
              />
            </label>
          </div>

          {/* KYC block — surfaced, never silently allowed. */}
          {kycBlocked ? (
            <p
              data-testid="manual-invoice-kyc-block"
              className="rounded-md border border-amber-500/40 bg-amber-500/10 px-3 py-2 text-sm text-amber-700 dark:text-amber-300"
            >
              {t("manualInvoiceKycBlocked" as never)}
            </p>
          ) : null}

          {/* Lines */}
          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <span className="text-sm font-semibold">{t("manualInvoiceLines" as never)}</span>
              <Button type="button" size="sm" variant="outline" onClick={addLine}>
                <Plus className="h-3.5 w-3.5" />
                {t("manualInvoiceAddLine" as never)}
              </Button>
            </div>
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-border text-muted-foreground">
                    <th className="py-2 text-start font-medium">{t("manualInvoiceColLine" as never)}</th>
                    <th className="py-2 text-start font-medium">{t("manualInvoiceColDescription" as never)}</th>
                    <th className="py-2 text-end font-medium">{t("manualInvoiceColQty" as never)}</th>
                    <th className="py-2 text-end font-medium">{t("manualInvoiceColUnitPrice" as never)}</th>
                    <th className="py-2 text-end font-medium">{t("manualInvoiceColVat" as never)}</th>
                    <th className="py-2 text-end font-medium">{t("manualInvoiceColLineTotal" as never)}</th>
                    <th className="py-2 text-end font-medium">{t("manualInvoiceColActions" as never)}</th>
                  </tr>
                </thead>
                <tbody>
                  {lines.map((row, index) => (
                    <tr key={index} className="border-b border-border/60 align-top">
                      <td className="py-2 tabular-nums">{index + 1}</td>
                      <td className="py-2">
                        <input
                          type="text"
                          className={inputClass}
                          value={row.description}
                          onChange={(e) => updateLine(index, { description: e.target.value })}
                        />
                      </td>
                      <td className="py-2">
                        <input
                          type="number"
                          min="0"
                          step="1"
                          dir="ltr"
                          className={`${inputClass} text-end`}
                          value={row.quantity}
                          onChange={(e) => updateLine(index, { quantity: e.target.value })}
                        />
                      </td>
                      <td className="py-2">
                        <input
                          type="number"
                          min="0.01"
                          step="0.01"
                          dir="ltr"
                          className={`${inputClass} text-end`}
                          value={row.unitPrice}
                          onChange={(e) => updateLine(index, { unitPrice: e.target.value })}
                        />
                      </td>
                      <td className="py-2 text-end tabular-nums">
                        <MoneyAmount amount={computed[index]?.vatAmount ?? 0} />
                      </td>
                      <td className="py-2 text-end tabular-nums">
                        <MoneyAmount amount={computed[index]?.lineTotalInclusive ?? 0} />
                      </td>
                      <td className="py-2 text-end">
                        <Button
                          type="button"
                          size="sm"
                          variant="ghost"
                          disabled={lines.length <= 1}
                          onClick={() => removeLine(index)}
                          aria-label={t("manualInvoiceRemoveLine" as never)}
                        >
                          <Trash2 className="h-3.5 w-3.5" />
                        </Button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            {lineErrors.some((e) => e.field === "lines") ? (
              <p className="text-sm text-destructive">{t("manualInvoiceErrLines" as never)}</p>
            ) : null}
          </div>

          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t("manualInvoiceNotes" as never)}</span>
            <textarea
              className={inputClass}
              rows={2}
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
            />
          </label>

          {/* Live totals */}
          <div
            data-testid="manual-invoice-totals"
            className="ms-auto w-full max-w-xs space-y-1 rounded-lg border border-border bg-background p-4 text-sm"
          >
            <div className="flex justify-between">
              <span className="text-muted-foreground">{t("manualInvoiceSubtotal" as never)}</span>
              <span className="tabular-nums">
                <MoneyAmount amount={totals.subtotalNet} />
              </span>
            </div>
            <div className="flex justify-between">
              <span className="text-muted-foreground">{t("manualInvoiceVatTotal" as never)}</span>
              <span className="tabular-nums">
                <MoneyAmount amount={totals.vatTotal} />
              </span>
            </div>
            <div className="flex justify-between border-t border-border pt-1 font-semibold">
              <span>{t("manualInvoiceGrandTotal" as never)}</span>
              <span className="tabular-nums">
                <MoneyAmount amount={totals.grandTotalInclusive} />
              </span>
            </div>
          </div>

          {error ? <p className="text-sm text-destructive">{error}</p> : null}

          <div className="flex items-center gap-2">
            <Button type="submit" data-testid="manual-invoice-submit" disabled={!canSubmit}>
              {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : <Plus className="h-4 w-4" />}
              {t("manualInvoiceSubmit" as never)}
            </Button>
          </div>

          <p className="text-xs text-muted-foreground">{t("manualInvoiceBetaNote" as never)}</p>
        </form>
      </CardContent>
    </Card>
  );
}

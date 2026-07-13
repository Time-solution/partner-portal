import { useMemo, useState } from "react";
import { AlertTriangle, CheckCircle2, ChevronLeft, Lock } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { MoneyAmount } from "@/components/MoneyAmount";
import { DateRangeFilter } from "@/features/admin/components/DateRangeFilter";
import { ALL_DATES, inDateRange, type DateRange } from "@/lib/filters/dateRange";
import { usePortalSession } from "@/features/auth/usePortalSession";
import { useTranslator, type Lang } from "@/lib/i18n";
import {
  closeAggregatorStatement,
  getAggregatorStatement,
  listAggregatorStatements,
  resolveAggregatorException,
  type AggregatorStatement,
  type ReconcileGuardError,
} from "@/lib/reconcile/aggregatorStatements";

const statusToneClass: Record<AggregatorStatement["status"], string> = {
  Imported: "bg-muted text-muted-foreground",
  Matching: "bg-muted text-muted-foreground",
  Reconciled: "bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-300",
  HasExceptions: "bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-300",
  Closed: "bg-slate-200 text-slate-700 dark:bg-slate-800 dark:text-slate-300",
};

/**
 * Gate 1 — aggregator statement reconciliation (mock store; compute-only, flags OFF).
 * Statements list → drill-in: matched lines vs the exception queue; per-exception resolve with a
 * MANDATORY note; close gated by two-person (resolver ≠ closer) + zero open exceptions — all
 * enforced in the store/domain guards, the UI only surfaces the coded outcome.
 */
export function AggregatorReconcilePage({ lang }: { lang: Lang }) {
  const t = useTranslator(lang);
  const { orgUser } = usePortalSession();
  const actor = orgUser?.id ?? "unknown";
  const dir = lang === "ar" ? "rtl" : "ltr";

  const [range, setRange] = useState<DateRange>(ALL_DATES);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [version, setVersion] = useState(0); // bump to reload from the mock store
  const [notes, setNotes] = useState<Record<string, string>>({});
  const [error, setError] = useState<ReconcileGuardError | null>(null);

  const statements = useMemo(
    () => listAggregatorStatements().filter((s) => inDateRange(s.periodTo, range)),
    [range, version],
  );
  const selected = useMemo(
    () => (selectedId ? getAggregatorStatement(selectedId) : undefined),
    [selectedId, version],
  );

  const guardMessage = (guard: ReconcileGuardError): string =>
    t(
      (
        {
          noteRequired: "aggRecNoteRequired",
          openExceptions: "aggRecCloseBlockedOpen",
          sameActorAsResolver: "aggRecCloseBlockedTwoPerson",
          immutable: "aggRecImmutable",
        } as const
      )[guard] as never,
    );

  const onResolve = (statementId: string, exceptionId: string) => {
    setError(null);
    const guard = resolveAggregatorException(statementId, exceptionId, notes[exceptionId] ?? "", actor);
    if (guard) setError(guard);
    else setVersion((v) => v + 1);
  };

  const onClose = (statementId: string) => {
    setError(null);
    const guard = closeAggregatorStatement(statementId, actor);
    if (guard) setError(guard);
    else setVersion((v) => v + 1);
  };

  if (selected) {
    const open = selected.exceptions.filter((e) => !e.resolved);
    const resolved = selected.exceptions.filter((e) => e.resolved);
    const matchedLines = selected.lines.filter((l) => l.matched);

    return (
      <div dir={dir} className="space-y-4" data-testid="agg-rec-detail">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div className="flex items-center gap-2">
            <Button variant="outline" size="sm" onClick={() => { setSelectedId(null); setError(null); }}>
              <ChevronLeft className="h-4 w-4 rtl:rotate-180" aria-hidden="true" />
              {t("aggRecBack" as never)}
            </Button>
            <h2 className="text-lg font-semibold">
              {selected.source} · {selected.periodFrom} → {selected.periodTo}
            </h2>
            <span className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${statusToneClass[selected.status]}`}>
              {t(`aggRecStatus_${selected.status}` as never)}
            </span>
          </div>

          <Button
            size="sm"
            data-testid="agg-rec-close"
            disabled={selected.status === "Closed"}
            onClick={() => onClose(selected.id)}
          >
            <Lock className="h-4 w-4" aria-hidden="true" />
            {t("aggRecClose" as never)}
          </Button>
        </div>

        <p className="text-sm text-muted-foreground">{t("aggRecComputeOnlyNotice" as never)}</p>

        {error && (
          <div role="alert" data-testid="agg-rec-error"
            className="rounded-md border border-amber-400 bg-amber-50 px-3 py-2 text-sm text-amber-900 dark:bg-amber-950 dark:text-amber-200">
            {guardMessage(error)}
          </div>
        )}

        <div className="grid gap-3 sm:grid-cols-3">
          <SummaryTile label={t("aggRecDeclaredGross" as never)} amount={selected.declaredGross} />
          <SummaryTile label={t("aggRecDeclaredFees" as never)} amount={selected.declaredFees} />
          <SummaryTile label={t("aggRecDeclaredNet" as never)} amount={selected.declaredNet} />
        </div>

        <Card data-testid="agg-rec-exception-queue">
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <AlertTriangle className="h-4 w-4 text-amber-600" aria-hidden="true" />
              {t("aggRecExceptionQueue" as never)} ({open.length})
            </CardTitle>
            <CardDescription>{t("aggRecExceptionQueueDesc" as never)}</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {selected.exceptions.length === 0 && (
              <p className="text-sm text-muted-foreground">{t("aggRecNoExceptions" as never)}</p>
            )}
            {selected.exceptions.map((exception) => (
              <div key={exception.id} data-testid={`agg-rec-exception-${exception.id}`}
                className="rounded-md border border-border p-3">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div className="space-y-0.5 text-sm">
                    <p className="font-medium">
                      {t(`aggRecVariance_${exception.type}` as never)}
                      {exception.externalOrderRef ? ` · ${exception.externalOrderRef}` : ""}
                    </p>
                    <p className="text-xs text-muted-foreground">
                      {exception.expectedAmount != null && (
                        <>
                          {t("aggRecExpected" as never)}: <MoneyAmount amount={exception.expectedAmount} />{" · "}
                        </>
                      )}
                      {exception.actualAmount != null && (
                        <>
                          {t("aggRecActual" as never)}: <MoneyAmount amount={exception.actualAmount} />
                        </>
                      )}
                    </p>
                  </div>
                  {exception.resolved ? (
                    <span className="flex items-center gap-1 text-xs text-emerald-700 dark:text-emerald-300">
                      <CheckCircle2 className="h-3.5 w-3.5" aria-hidden="true" />
                      {t("aggRecResolvedBy" as never)} {exception.resolvedBy}
                    </span>
                  ) : (
                    <div className="flex items-center gap-2">
                      <input
                        data-testid={`agg-rec-note-${exception.id}`}
                        className="h-9 w-56 rounded-md border border-input bg-background px-3 text-sm"
                        placeholder={t("aggRecNotePlaceholder" as never)}
                        value={notes[exception.id] ?? ""}
                        onChange={(e) => setNotes((n) => ({ ...n, [exception.id]: e.target.value }))}
                      />
                      <Button size="sm" variant="outline" data-testid={`agg-rec-resolve-${exception.id}`}
                        onClick={() => onResolve(selected.id, exception.id)}>
                        {t("aggRecResolve" as never)}
                      </Button>
                    </div>
                  )}
                </div>
                {exception.resolved && exception.resolutionNote && (
                  <p className="mt-1 text-xs text-muted-foreground">"{exception.resolutionNote}"</p>
                )}
              </div>
            ))}
            {resolved.length > 0 && open.length === 0 && selected.status !== "Closed" && (
              <p className="text-xs text-muted-foreground">{t("aggRecAllResolvedHint" as never)}</p>
            )}
          </CardContent>
        </Card>

        <Card data-testid="agg-rec-matched">
          <CardHeader>
            <CardTitle className="text-base">
              {t("aggRecMatchedLines" as never)} ({matchedLines.length})
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-border text-start text-xs text-muted-foreground">
                    <th className="px-3 py-2 text-start">{t("aggRecColOrderRef" as never)}</th>
                    <th className="px-3 py-2 text-start">{t("aggRecColOrderDate" as never)}</th>
                    <th className="px-3 py-2 text-end">{t("aggRecColGross" as never)}</th>
                    <th className="px-3 py-2 text-end">{t("aggRecColFee" as never)}</th>
                    <th className="px-3 py-2 text-end">{t("aggRecColNet" as never)}</th>
                  </tr>
                </thead>
                <tbody>
                  {matchedLines.map((line) => (
                    <tr key={line.id} className="border-b border-border/60">
                      <td className="px-3 py-2 font-mono text-xs">{line.externalOrderRef}</td>
                      <td className="px-3 py-2">{line.orderDate}</td>
                      <td className="px-3 py-2 text-end"><MoneyAmount amount={line.gross} /></td>
                      <td className="px-3 py-2 text-end"><MoneyAmount amount={line.aggregatorFee} /></td>
                      <td className="px-3 py-2 text-end"><MoneyAmount amount={line.net} /></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div dir={dir} className="space-y-4" data-testid="agg-rec-list">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h2 className="text-lg font-semibold">{t("aggRecTitle" as never)}</h2>
          <p className="text-sm text-muted-foreground">{t("aggRecDesc" as never)}</p>
        </div>
        <DateRangeFilter range={range} onChange={setRange} lang={lang} />
      </div>

      <Card>
        <CardContent className="pt-4">
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-border text-xs text-muted-foreground">
                  <th className="px-3 py-2 text-start">{t("aggRecColSource" as never)}</th>
                  <th className="px-3 py-2 text-start">{t("aggRecColPeriod" as never)}</th>
                  <th className="px-3 py-2 text-end">{t("aggRecDeclaredNet" as never)}</th>
                  <th className="px-3 py-2 text-center">{t("aggRecColOpenExceptions" as never)}</th>
                  <th className="px-3 py-2 text-center">{t("aggRecColStatus" as never)}</th>
                  <th className="px-3 py-2" />
                </tr>
              </thead>
              <tbody>
                {statements.map((statement) => {
                  const openCount = statement.exceptions.filter((e) => !e.resolved).length;
                  return (
                    <tr key={statement.id} className="border-b border-border/60">
                      <td className="px-3 py-2 font-medium">{statement.source}</td>
                      <td className="px-3 py-2">{statement.periodFrom} → {statement.periodTo}</td>
                      <td className="px-3 py-2 text-end"><MoneyAmount amount={statement.declaredNet} /></td>
                      <td className="px-3 py-2 text-center">
                        {openCount > 0 ? (
                          <span className="rounded-full bg-amber-100 px-2 py-0.5 text-xs font-semibold text-amber-800 dark:bg-amber-900/40 dark:text-amber-300">
                            {openCount}
                          </span>
                        ) : (
                          <span className="text-xs text-muted-foreground">0</span>
                        )}
                      </td>
                      <td className="px-3 py-2 text-center">
                        <span className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${statusToneClass[statement.status]}`}>
                          {t(`aggRecStatus_${statement.status}` as never)}
                        </span>
                      </td>
                      <td className="px-3 py-2 text-end">
                        <Button size="sm" variant="outline" data-testid={`agg-rec-open-${statement.id}`}
                          onClick={() => setSelectedId(statement.id)}>
                          {t("aggRecOpen" as never)}
                        </Button>
                      </td>
                    </tr>
                  );
                })}
                {statements.length === 0 && (
                  <tr>
                    <td colSpan={6} className="px-3 py-6 text-center text-sm text-muted-foreground">
                      {t("aggRecEmpty" as never)}
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}

function SummaryTile({ label, amount }: { label: string; amount: number }) {
  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-sm font-medium text-muted-foreground">{label}</CardTitle>
      </CardHeader>
      <CardContent>
        <p className="text-2xl font-bold tabular-nums"><MoneyAmount amount={amount} /></p>
      </CardContent>
    </Card>
  );
}

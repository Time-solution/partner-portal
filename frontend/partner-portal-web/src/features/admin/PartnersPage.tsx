import { useCallback, useEffect, useState } from "react";
import { Check, Copy, Loader2, RefreshCw, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import {
  approvePartner,
  closePartner,
  fetchPartners,
  reactivatePartner,
  rejectPartner,
  suspendPartner,
  type PartnerApproveResult,
  type PartnerListItem,
  type PartnerStatus,
} from "@/lib/api";
import { useTranslator, type Lang } from "@/lib/i18n";

const STATUS_FILTERS: Array<{ key: "all" | PartnerStatus; labelKey: string }> = [
  { key: "all", labelKey: "partnersFilterAll" },
  { key: "Pending", labelKey: "partnersStatusPending" },
  { key: "Active", labelKey: "partnersStatusActive" },
  { key: "Suspended", labelKey: "partnersStatusSuspended" },
  { key: "Closed", labelKey: "partnersStatusClosed" },
];

const STATUS_NUM: Record<number, PartnerStatus> = {
  1: "Pending",
  2: "Active",
  3: "Suspended",
  4: "Closed",
};

function normalizeStatus(raw: PartnerStatus | number): PartnerStatus {
  if (typeof raw === "number") {
    return STATUS_NUM[raw] ?? "Pending";
  }
  return raw;
}

function statusLabel(t: (key: never) => string, status: PartnerStatus | number) {
  const normalized = normalizeStatus(status);
  const map: Record<PartnerStatus, string> = {
    Pending: t("partnersStatusPending" as never),
    Active: t("partnersStatusActive" as never),
    Suspended: t("partnersStatusSuspended" as never),
    Closed: t("partnersStatusClosed" as never),
  };
  return map[normalized];
}

function statusClass(status: PartnerStatus | number) {
  const normalized = normalizeStatus(status);
  const map: Record<PartnerStatus, string> = {
    Pending: "bg-amber-100 text-amber-900 dark:bg-amber-950 dark:text-amber-200",
    Active: "bg-emerald-100 text-emerald-900 dark:bg-emerald-950 dark:text-emerald-200",
    Suspended: "bg-orange-100 text-orange-900 dark:bg-orange-950 dark:text-orange-200",
    Closed: "bg-muted text-muted-foreground",
  };
  return map[normalized];
}

interface PartnersPageProps {
  lang: Lang;
}

export function PartnersPage({ lang }: PartnersPageProps) {
  const t = useTranslator(lang);
  const [filter, setFilter] = useState<"all" | PartnerStatus>("all");
  const [partners, setPartners] = useState<PartnerListItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [approveResult, setApproveResult] = useState<PartnerApproveResult | null>(null);
  const [copiedField, setCopiedField] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await fetchPartners({
        maxResultCount: 50,
        status: filter === "all" ? undefined : filter,
      });
      setPartners(result.items);
      setTotalCount(result.totalCount);
    } catch (err) {
      setError(err instanceof Error ? err.message : t("partnersLoadError" as never));
    } finally {
      setLoading(false);
    }
  }, [filter, t]);

  useEffect(() => {
    void load();
  }, [load]);

  const runAction = async (id: string, action: () => Promise<void>) => {
    setBusyId(id);
    setError(null);
    try {
      await action();
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : t("partnersActionError" as never));
    } finally {
      setBusyId(null);
    }
  };

  const copyValue = async (field: string, value: string) => {
    await navigator.clipboard.writeText(value);
    setCopiedField(field);
    setTimeout(() => setCopiedField(null), 2000);
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-sm text-muted-foreground">
          {t("partnersSummary" as never).replace("{count}", String(totalCount))}
        </p>
        <Button variant="outline" size="sm" onClick={() => void load()} disabled={loading}>
          <RefreshCw className={`h-4 w-4 ${loading ? "animate-spin" : ""}`} aria-hidden="true" />
          {t("partnersRefresh" as never)}
        </Button>
      </div>

      <div className="flex flex-wrap gap-2" role="tablist" aria-label={t("partnersFilterLabel" as never)}>
        {STATUS_FILTERS.map((item) => (
          <Button
            key={item.key}
            variant={filter === item.key ? "default" : "outline"}
            size="sm"
            role="tab"
            aria-selected={filter === item.key}
            onClick={() => setFilter(item.key)}
          >
            {t(item.labelKey as never)}
          </Button>
        ))}
      </div>

      {error ? (
        <div className="rounded-md border border-destructive/40 bg-destructive/10 px-4 py-3 text-sm text-destructive">
          {error}
        </div>
      ) : null}

      <Card>
        <CardHeader>
          <CardTitle>{t("navPartners" as never)}</CardTitle>
          <CardDescription>{t("partnersDescription" as never)}</CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="flex items-center gap-2 py-8 text-sm text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
              {t("partnersLoading" as never)}
            </div>
          ) : partners.length === 0 ? (
            <p className="py-8 text-center text-sm text-muted-foreground">{t("partnersEmpty" as never)}</p>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full min-w-[640px] text-sm">
                <thead>
                  <tr className="border-b border-border text-start text-muted-foreground">
                    <th className="px-2 py-2 font-medium">{t("partnersColName" as never)}</th>
                    <th className="px-2 py-2 font-medium">{t("partnersColEmail" as never)}</th>
                    <th className="px-2 py-2 font-medium">{t("partnersColStatus" as never)}</th>
                    <th className="px-2 py-2 font-medium">{t("partnersColIban" as never)}</th>
                    <th className="px-2 py-2 font-medium">{t("partnersColActions" as never)}</th>
                  </tr>
                </thead>
                <tbody>
                  {partners.map((partner) => {
                    const status = normalizeStatus(partner.status);
                    const isBusy = busyId === partner.id;
                    return (
                      <tr key={partner.id} className="border-b border-border/60 last:border-0">
                        <td className="px-2 py-3">
                          <div className="font-medium">{partner.legalName}</div>
                          {partner.tradeName ? (
                            <div className="text-xs text-muted-foreground">{partner.tradeName}</div>
                          ) : null}
                        </td>
                        <td className="px-2 py-3">{partner.primaryContactEmail}</td>
                        <td className="px-2 py-3">
                          <span
                            className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${statusClass(status)}`}
                          >
                            {statusLabel(t, status)}
                          </span>
                        </td>
                        <td className="px-2 py-3 font-mono text-xs">
                          {partner.maskedIban ?? "—"}
                        </td>
                        <td className="px-2 py-3">
                          <div className="flex flex-wrap gap-1">
                            {status === "Pending" ? (
                              <>
                                <Button
                                  size="sm"
                                  disabled={isBusy}
                                  onClick={() =>
                                    void runAction(partner.id, async () => {
                                      const result = await approvePartner(partner.id);
                                      setApproveResult(result);
                                    })
                                  }
                                >
                                  {isBusy ? (
                                    <Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden="true" />
                                  ) : (
                                    <Check className="h-3.5 w-3.5" aria-hidden="true" />
                                  )}
                                  {t("partnersApprove" as never)}
                                </Button>
                                <Button
                                  size="sm"
                                  variant="outline"
                                  disabled={isBusy}
                                  onClick={() =>
                                    void runAction(partner.id, () => rejectPartner(partner.id))
                                  }
                                >
                                  <X className="h-3.5 w-3.5" aria-hidden="true" />
                                  {t("partnersReject" as never)}
                                </Button>
                              </>
                            ) : null}
                            {status === "Active" ? (
                              <>
                                <Button
                                  size="sm"
                                  variant="outline"
                                  disabled={isBusy}
                                  onClick={() =>
                                    void runAction(partner.id, () => suspendPartner(partner.id))
                                  }
                                >
                                  {t("partnersSuspend" as never)}
                                </Button>
                                <Button
                                  size="sm"
                                  variant="outline"
                                  disabled={isBusy}
                                  onClick={() =>
                                    void runAction(partner.id, () => closePartner(partner.id))
                                  }
                                >
                                  {t("partnersClose" as never)}
                                </Button>
                              </>
                            ) : null}
                            {status === "Suspended" ? (
                              <>
                                <Button
                                  size="sm"
                                  disabled={isBusy}
                                  onClick={() =>
                                    void runAction(partner.id, () => reactivatePartner(partner.id))
                                  }
                                >
                                  {t("partnersReactivate" as never)}
                                </Button>
                                <Button
                                  size="sm"
                                  variant="outline"
                                  disabled={isBusy}
                                  onClick={() =>
                                    void runAction(partner.id, () => closePartner(partner.id))
                                  }
                                >
                                  {t("partnersClose" as never)}
                                </Button>
                              </>
                            ) : null}
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </CardContent>
      </Card>

      {approveResult ? (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
          role="dialog"
          aria-modal="true"
          aria-labelledby="approve-dialog-title"
        >
          <Card className="w-full max-w-lg">
            <CardHeader>
              <CardTitle id="approve-dialog-title">{t("partnersApproveSuccess" as never)}</CardTitle>
              <CardDescription>{t("partnersSecretsOnce" as never)}</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <SecretRow
                label={t("partnersM2mClientId" as never)}
                value={approveResult.clientId}
                field="clientId"
                copiedField={copiedField}
                onCopy={copyValue}
              />
              <SecretRow
                label={t("partnersM2mSecret" as never)}
                value={approveResult.clientSecret}
                field="clientSecret"
                copiedField={copiedField}
                onCopy={copyValue}
              />
              {approveResult.ownerSetPasswordToken ? (
                <SecretRow
                  label={t("partnersOwnerToken" as never)}
                  value={approveResult.ownerSetPasswordToken}
                  field="ownerToken"
                  copiedField={copiedField}
                  onCopy={copyValue}
                />
              ) : null}
              {approveResult.scopes.length > 0 ? (
                <div>
                  <p className="mb-1 text-xs font-medium text-muted-foreground">
                    {t("partnersScopes" as never)}
                  </p>
                  <p className="font-mono text-xs">{approveResult.scopes.join(" ")}</p>
                </div>
              ) : null}
              <Button className="w-full" onClick={() => setApproveResult(null)}>
                {t("partnersDismiss" as never)}
              </Button>
            </CardContent>
          </Card>
        </div>
      ) : null}
    </div>
  );
}

function SecretRow({
  label,
  value,
  field,
  copiedField,
  onCopy,
}: {
  label: string;
  value: string;
  field: string;
  copiedField: string | null;
  onCopy: (field: string, value: string) => Promise<void>;
}) {
  return (
    <div>
      <p className="mb-1 text-xs font-medium text-muted-foreground">{label}</p>
      <div className="flex items-center gap-2">
        <code className="flex-1 truncate rounded bg-muted px-2 py-1 text-xs">{value}</code>
        <Button
          type="button"
          variant="outline"
          size="icon"
          aria-label={label}
          onClick={() => void onCopy(field, value)}
        >
          {copiedField === field ? (
            <Check className="h-4 w-4" aria-hidden="true" />
          ) : (
            <Copy className="h-4 w-4" aria-hidden="true" />
          )}
        </Button>
      </div>
    </div>
  );
}

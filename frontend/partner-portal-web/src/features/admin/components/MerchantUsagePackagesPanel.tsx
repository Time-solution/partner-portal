import { useCallback, useEffect, useMemo, useState } from "react";
import { CheckCircle2, Loader2, Package } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { MoneyAmount } from "@/components/MoneyAmount";
import { BetaBadge } from "@/components/brand/BetaBadge";
import { getPortalDataSource } from "@/lib/data";
import { listUsagePackages } from "@/lib/usage/usagePackageStore";
import { scopeUsagePackage } from "@/lib/usage/usagePackage";
import type { UsagePackage } from "@/lib/usage/usagePackage";
import {
  activeSelectionFor,
  endUsagePackageSelection,
  selectUsagePackage,
} from "@/lib/usage/usagePackageSelectionStore";
import { listUsageRecords } from "@/lib/usage/usageStore";
import { usageForPeriod } from "@/lib/usage/usageReports";
import { buildUsageInvoiceBreakdown } from "@/lib/usage/usageInvoiceBreakdown";
import { UsageInvoiceBreakdownCard } from "./UsageInvoiceBreakdownCard";
import { useTranslator, type Lang } from "@/lib/i18n";

/** Merchant-view sell/fee price for a package (buy/margin are structurally absent here). */
function merchantPrice(pkg: UsagePackage): number {
  const scoped = scopeUsagePackage(pkg, "merchant");
  return scoped.base.sell?.amount ?? scoped.base.fee?.amount ?? 0;
}

/**
 * U4 — merchant browse + select of a partner's PUBLISHED usage packages, then the invoice breakdown for the
 * active selection. Merchant view: SELL/fee only (buy + margin absent). Instant self-service activation.
 * Breakdown ties to U3's `computeUsageBilling` (no recompute). Display only — nothing is posted.
 */
export function MerchantUsagePackagesPanel({
  lang,
  tenantId,
  merchantName,
  period = "2026-06",
}: {
  lang: Lang;
  tenantId: string;
  merchantName: string;
  period?: string;
}) {
  const t = useTranslator(lang);
  const isRtl = lang === "ar";
  const [partnerNames, setPartnerNames] = useState<Map<string, string>>(new Map());
  const [packages, setPackages] = useState<UsagePackage[]>([]);
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [tick, setTick] = useState(0);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const partners = await getPortalDataSource().getPartners();
      setPartnerNames(new Map(partners.map((p) => [p.id, p.tradeName ?? p.legalName])));
      setPackages(listUsagePackages().filter((p) => p.status === "Published"));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const byPartner = useMemo(() => {
    const groups = new Map<string, UsagePackage[]>();
    for (const pkg of packages) {
      const list = groups.get(pkg.partnerId) ?? [];
      list.push(pkg);
      groups.set(pkg.partnerId, list);
    }
    return [...groups.entries()].sort((a, b) =>
      (partnerNames.get(a[0]) ?? a[0]).localeCompare(partnerNames.get(b[0]) ?? b[0]),
    );
  }, [packages, partnerNames]);

  const usageRecords = useMemo(() => listUsageRecords(), [tick]);

  const handleSelect = (pkg: UsagePackage) => {
    setBusyId(pkg.id);
    try {
      selectUsagePackage({ partnerId: pkg.partnerId, usagePackageId: pkg.id, tenantId, merchantName });
      setTick((n) => n + 1);
    } finally {
      setBusyId(null);
    }
  };

  const handleEnd = (selectionId: string) => {
    setBusyId(selectionId);
    try {
      endUsagePackageSelection(selectionId);
      setTick((n) => n + 1);
    } finally {
      setBusyId(null);
    }
  };

  if (loading) {
    return (
      <div className="flex items-center gap-2 text-sm text-muted-foreground">
        <Loader2 className="h-5 w-5 animate-spin" />
        {t("loadingData" as never)}
      </div>
    );
  }

  return (
    <section className="space-y-4" dir={isRtl ? "rtl" : "ltr"} aria-labelledby="merchant-usage-packages-title">
      <div className="flex flex-wrap items-center gap-2">
        <Package className="h-5 w-5 text-teal-600" aria-hidden="true" />
        <div>
          <h2 id="merchant-usage-packages-title" className="text-lg font-semibold">
            {t("usageSelTitle" as never)}
          </h2>
          <p className="text-sm text-muted-foreground">{t("usageSelDesc" as never)}</p>
        </div>
        <BetaBadge lang={lang} />
      </div>

      {byPartner.length === 0 ? (
        <p className="text-sm text-muted-foreground">{t("usageSelEmpty" as never)}</p>
      ) : (
        byPartner.map(([partnerId, list]) => {
          const active = activeSelectionFor(tenantId, partnerId);
          const activePkg = active ? list.find((p) => p.id === active.usagePackageId) : undefined;
          const breakdown =
            active && activePkg
              ? buildUsageInvoiceBreakdown(
                  activePkg,
                  usageForPeriod(usageRecords, partnerId, tenantId, period),
                  period,
                  { viewer: "merchant", activatedAt: active.activatedAt, endedAt: active.endedAt },
                )
              : undefined;

          return (
            <Card key={partnerId}>
              <CardHeader className="pb-2">
                <CardTitle className="text-base">{partnerNames.get(partnerId) ?? partnerId}</CardTitle>
                <CardDescription>{t("usageSelDesc" as never)}</CardDescription>
              </CardHeader>
              <CardContent className="space-y-2">
                <ul className="space-y-2">
                  {list.map((pkg) => {
                    const isActive = active?.usagePackageId === pkg.id;
                    return (
                      <li
                        key={pkg.id}
                        className="flex flex-wrap items-center justify-between gap-2 rounded-md border border-border px-3 py-2"
                      >
                        <div>
                          <p className="font-medium">{pkg.name}</p>
                          <p className="text-xs text-muted-foreground">
                            {t(
                              (pkg.mode === "Subscription"
                                ? "usagePkgModeSubscription"
                                : "usagePkgModeResale") as never,
                            )}
                            {" · "}
                            {t("usageSelYourPrice" as never)}: <MoneyAmount amount={merchantPrice(pkg)} />
                          </p>
                        </div>
                        {isActive ? (
                          <span className="inline-flex items-center gap-1 rounded-full border border-teal-500/40 bg-teal-500/10 px-2.5 py-1 text-xs font-medium text-teal-800 dark:text-teal-200">
                            <CheckCircle2 className="h-3.5 w-3.5" aria-hidden="true" />
                            {t("usageSelActive" as never)}
                          </span>
                        ) : (
                          <Button size="sm" disabled={busyId === pkg.id} onClick={() => handleSelect(pkg)}>
                            {t("usageSelSelect" as never)}
                          </Button>
                        )}
                      </li>
                    );
                  })}
                </ul>

                {active && breakdown ? (
                  <div className="space-y-2 pt-1">
                    <div className="flex flex-wrap items-center justify-between gap-2 text-xs text-muted-foreground">
                      <span>
                        {t("usageSelActivatedOn" as never)}:{" "}
                        {new Date(active.activatedAt).toLocaleDateString(isRtl ? "ar-SA" : "en-GB")}
                        {" · "}
                        {t("usageSelInstant" as never)}
                      </span>
                      <Button
                        size="sm"
                        variant="outline"
                        disabled={busyId === active.id}
                        onClick={() => handleEnd(active.id)}
                      >
                        {t("usageSelEnd" as never)}
                      </Button>
                    </div>
                    <UsageInvoiceBreakdownCard lang={lang} breakdown={breakdown} />
                  </div>
                ) : null}
              </CardContent>
            </Card>
          );
        })
      )}
    </section>
  );
}

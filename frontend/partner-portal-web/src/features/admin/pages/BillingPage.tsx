import { useCallback, useEffect, useState } from "react";
import { Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { BetaBadge } from "@/components/brand/BetaBadge";
import { getPortalDataSource } from "@/lib/data";
import type { SubscriptionBillingPeriod } from "@/lib/data/types";
import { PortalPermissions } from "@/lib/rbac/portalRoles";
import { usePortalSession } from "@/features/auth/usePortalSession";
import { PageHeader } from "../components/PageHeader";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";

export function BillingPage({ lang }: { lang: Lang }) {
  const t = useTranslator(lang);
  const { scopedPartnerId, can } = usePortalSession();
  const [periods, setPeriods] = useState<SubscriptionBillingPeriod[]>([]);
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState<string | null>(null);

  const canManage = can(PortalPermissions.Billing.SetTerms);
  const partnerId = scopedPartnerId ?? "22222222-2222-2222-2222-222222222004";

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setPeriods(await getPortalDataSource().getBillingPeriods(scopedPartnerId));
    } finally {
      setLoading(false);
    }
  }, [scopedPartnerId]);

  useEffect(() => {
    void load();
  }, [load]);

  const generateInvoice = async (periodId: string) => {
    setBusyId(periodId);
    try {
      await getPortalDataSource().generateInvoice(periodId);
      await load();
    } finally {
      setBusyId(null);
    }
  };

  const advancePeriod = async () => {
    setBusyId("advance");
    try {
      await getPortalDataSource().advanceBillingPeriod(partnerId);
      await load();
    } finally {
      setBusyId(null);
    }
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title={t("navBilling" as never)}
        description={t("billingDesc" as never)}
        lang={lang}
        showBeta
      />
      {canManage ? (
        <Button variant="outline" disabled={busyId === "advance"} onClick={() => void advancePeriod()}>
          {t("billingAdvancePeriod" as never)}
        </Button>
      ) : null}
      <Card>
        <CardHeader>
          <CardTitle className="text-lg">{t("billingPeriodsTitle" as never)}</CardTitle>
          <CardDescription className="text-base">{t("billingVatOnFee" as never)}</CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="flex items-center gap-2 py-8 text-base text-muted-foreground">
              <Loader2 className="h-5 w-5 animate-spin" />
              {t("loadingData" as never)}
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full min-w-[800px] text-base">
                <thead>
                  <tr className="border-b border-border text-start text-muted-foreground">
                    <th className="px-2 py-2.5 font-medium">{t("colPeriod" as never)}</th>
                    <th className="px-2 py-2.5 font-medium">{t("colMerchant" as never)}</th>
                    <th className="px-2 py-2.5 text-end font-medium">{t("colFeeIncl" as never)}</th>
                    <th className="px-2 py-2.5 text-end font-medium">{t("colVat" as never)}</th>
                    <th className="px-2 py-2.5 text-end font-medium">{t("colNetFee" as never)}</th>
                    <th className="px-2 py-2.5 font-medium">{t("colInvoice" as never)}</th>
                    <th className="px-2 py-2.5 font-medium">{t("colStatus" as never)}</th>
                    <th className="px-2 py-2.5 font-medium">{t("colActions" as never)}</th>
                  </tr>
                </thead>
                <tbody>
                  {periods.map((p) => (
                    <tr key={p.id} className="border-b border-border/60">
                      <td className="px-2 py-2.5 font-medium">{p.periodKey}</td>
                      <td className="px-2 py-2.5">{p.merchantName}</td>
                      <td className="px-2 py-2.5 text-end tabular-nums">{p.feeInclusive.amount.toFixed(2)}</td>
                      <td className="px-2 py-2.5 text-end tabular-nums">{p.outputVat.toFixed(2)}</td>
                      <td className="px-2 py-2.5 text-end tabular-nums">{p.netFee.toFixed(2)}</td>
                      <td className="px-2 py-2.5 font-mono text-sm">
                        {p.invoiceNumber ? (
                          <span className="inline-flex items-center gap-1">
                            {p.invoiceNumber}
                            <BetaBadge lang={lang} />
                          </span>
                        ) : (
                          "—"
                        )}
                      </td>
                      <td className="px-2 py-2.5">
                        <span className="rounded bg-muted px-2 py-0.5 text-sm">{p.status}</span>
                        {p.journalBalanced ? " ✓" : ""}
                      </td>
                      <td className="px-2 py-2.5">
                        {canManage && p.status === "Charged" ? (
                          <Button
                            size="sm"
                            variant="outline"
                            disabled={busyId === p.id}
                            onClick={() => void generateInvoice(p.id)}
                          >
                            {t("billingGenerateInvoice" as never)}
                          </Button>
                        ) : null}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

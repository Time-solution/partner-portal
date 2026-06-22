import { useCallback, useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { MoneyAmount } from "@/components/MoneyAmount";
import { getPortalDataSource } from "@/lib/data";
import type { CommissionLedgerRow } from "@/lib/data/types";
import { PortalPermissions } from "@/lib/rbac/portalRoles";
import { usePortalSession } from "@/features/auth/usePortalSession";
import { PageHeader } from "@/features/admin/components/PageHeader";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";
import { tabBarClass, tabLinkClass } from "@/lib/ui/tabs";

type TabId = "Accrued" | "Approved" | "Paid" | "Reversed";

const TABS: { id: TabId; labelKey: string }[] = [
  { id: "Accrued", labelKey: "commissionTabPending" },
  { id: "Approved", labelKey: "commissionTabApproved" },
  { id: "Paid", labelKey: "commissionTabPaid" },
  { id: "Reversed", labelKey: "commissionTabReversed" },
];

function ReverseModal({
  lang,
  open,
  busy,
  onClose,
  onConfirm,
}: {
  lang: Lang;
  open: boolean;
  busy: boolean;
  onClose: () => void;
  onConfirm: (reason: string) => void;
}) {
  const t = useTranslator(lang);
  const [reason, setReason] = useState("");
  if (!open) return null;
  const valid = reason.trim().length >= 10;
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" role="dialog" aria-modal="true">
      <Card className="w-full max-w-md">
        <CardHeader>
          <CardTitle>{t("commissionReverseTitle" as never)}</CardTitle>
          <CardDescription>{t("commissionReverseDesc" as never)}</CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          <textarea
            className="min-h-24 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder={t("commissionReversePlaceholder" as never)}
          />
          {!valid && reason.length > 0 ? (
            <p className="text-xs text-destructive">{t("commissionReverseMinLength" as never)}</p>
          ) : null}
          <div className="flex justify-end gap-2">
            <Button type="button" variant="outline" onClick={onClose} disabled={busy}>
              {t("cancel" as never)}
            </Button>
            <Button type="button" disabled={!valid || busy} onClick={() => onConfirm(reason.trim())}>
              {t("commissionReverseConfirm" as never)}
            </Button>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}

export function CommissionApprovalsPage({ lang }: { lang: Lang }) {
  const t = useTranslator(lang);
  const { can, orgUser } = usePortalSession();
  const canApprove = can(PortalPermissions.Commission.Approve);
  const actorId = orgUser?.id ?? "unknown";
  const actorName = orgUser?.name ?? "Finance user";

  const [tab, setTab] = useState<TabId>("Accrued");
  const [rows, setRows] = useState<CommissionLedgerRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [reverseTarget, setReverseTarget] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setRows(await getPortalDataSource().listCommissionLedger({ status: tab }));
    } finally {
      setLoading(false);
    }
  }, [tab]);

  useEffect(() => {
    void load();
  }, [load]);

  const approve = async (id: string) => {
    setBusyId(id);
    try {
      await getPortalDataSource().approveCommissionEntry(id, actorId, actorName);
      await load();
    } finally {
      setBusyId(null);
    }
  };

  const markPaid = async (id: string) => {
    setBusyId(id);
    try {
      await getPortalDataSource().markCommissionPaid(id, actorId, actorName);
      await load();
    } finally {
      setBusyId(null);
    }
  };

  const reverse = async (reason: string) => {
    if (!reverseTarget) return;
    setBusyId(reverseTarget);
    try {
      await getPortalDataSource().reverseCommissionEntry(reverseTarget, reason, actorName);
      setReverseTarget(null);
      await load();
    } finally {
      setBusyId(null);
    }
  };

  const tabButtons = useMemo(
    () =>
      TABS.map((item) => (
        <button
          key={item.id}
          type="button"
          className={tabLinkClass(tab === item.id)}
          onClick={() => setTab(item.id)}
        >
          {t(item.labelKey as never)}
        </button>
      )),
    [tab, t],
  );

  if (!canApprove) {
    return <p className="text-sm text-muted-foreground">{t("commissionApprovalsForbidden" as never)}</p>;
  }

  return (
    <div className="space-y-6" dir={lang === "ar" ? "rtl" : "ltr"}>
      <PageHeader
        title={t("commissionApprovalsTitle" as never)}
        description={t("commissionApprovalsDesc" as never)}
        lang={lang}
        showBeta
      />
      <nav className={tabBarClass} aria-label={t("commissionApprovalsTitle" as never)}>
        {tabButtons}
      </nav>

      {loading ? (
        <div className="flex items-center gap-2 text-muted-foreground">
          <Loader2 className="h-4 w-4 animate-spin" />
          {t("loadingData" as never)}
        </div>
      ) : (
        <Card>
          <CardContent className="p-0">
            <div className="overflow-x-auto">
              <table className="w-full min-w-[720px] text-sm">
                <thead>
                  <tr className="border-b border-border text-muted-foreground">
                    <th className="px-3 py-2 text-start">{t("commissionColPartner" as never)}</th>
                    {tab === "Accrued" ? (
                      <th className="px-3 py-2 text-start">{t("commissionColEntryDate" as never)}</th>
                    ) : null}
                    <th className="px-3 py-2 text-end">{t("commissionColBasis" as never)}</th>
                    <th className="px-3 py-2 text-end">{t("commissionColCommission" as never)}</th>
                    {tab === "Accrued" ? (
                      <th className="px-3 py-2 text-start">{t("commissionColAccruedAt" as never)}</th>
                    ) : null}
                    {tab === "Approved" ? (
                      <>
                        <th className="px-3 py-2 text-start">{t("commissionColApprovedAt" as never)}</th>
                        <th className="px-3 py-2 text-start">{t("commissionColApprovedBy" as never)}</th>
                      </>
                    ) : null}
                    {tab === "Reversed" ? (
                      <th className="px-3 py-2 text-start">{t("commissionColReason" as never)}</th>
                    ) : null}
                    <th className="px-3 py-2 text-end">{t("actions" as never)}</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.length === 0 ? (
                    <tr>
                      <td colSpan={8} className="px-3 py-8 text-center text-muted-foreground">
                        {t("commissionApprovalsEmpty" as never)}
                      </td>
                    </tr>
                  ) : (
                    rows.map((row) => {
                      const sameActor = row.approvedByUserId === actorId;
                      return (
                        <tr key={row.id} className="border-b border-border/60">
                          <td className="px-3 py-2">
                            <p className="font-medium">{row.partnerName}</p>
                            {row.merchantName ? (
                              <p className="text-xs text-muted-foreground">{row.merchantName}</p>
                            ) : null}
                          </td>
                          {tab === "Accrued" ? (
                            <td className="px-3 py-2 tabular-nums">{row.entryDate}</td>
                          ) : null}
                          <td className="px-3 py-2 text-end tabular-nums">
                            <MoneyAmount amount={row.basisAmount} />
                          </td>
                          <td className="px-3 py-2 text-end tabular-nums">
                            <MoneyAmount amount={row.commissionAmount} />
                          </td>
                          {tab === "Accrued" ? (
                            <td className="px-3 py-2 text-xs text-muted-foreground">
                              {row.accruedAt.slice(0, 10)}
                            </td>
                          ) : null}
                          {tab === "Approved" ? (
                            <>
                              <td className="px-3 py-2 text-xs">{row.approvedAt?.slice(0, 10) ?? "—"}</td>
                              <td className="px-3 py-2 text-xs">{row.approvedByName ?? "—"}</td>
                            </>
                          ) : null}
                          {tab === "Reversed" ? (
                            <td className="px-3 py-2 text-xs">{row.reversalReason ?? "—"}</td>
                          ) : null}
                          <td className="px-3 py-2 text-end">
                            {tab === "Accrued" ? (
                              <div className="flex flex-wrap justify-end gap-2">
                                <Button
                                  size="sm"
                                  disabled={busyId === row.id}
                                  onClick={() => void approve(row.id)}
                                >
                                  {t("commissionApprove" as never)}
                                </Button>
                                <Button
                                  size="sm"
                                  variant="outline"
                                  disabled={busyId === row.id}
                                  onClick={() => setReverseTarget(row.id)}
                                >
                                  {t("commissionReverse" as never)}
                                </Button>
                              </div>
                            ) : null}
                            {tab === "Approved" ? (
                              <Button
                                size="sm"
                                disabled={busyId === row.id || sameActor}
                                title={sameActor ? t("commissionTwoPersonTooltip" as never) : undefined}
                                onClick={() => void markPaid(row.id)}
                              >
                                {t("commissionMarkPaid" as never)}
                              </Button>
                            ) : null}
                          </td>
                        </tr>
                      );
                    })
                  )}
                </tbody>
              </table>
            </div>
          </CardContent>
        </Card>
      )}

      <ReverseModal
        lang={lang}
        open={reverseTarget !== null}
        busy={busyId !== null}
        onClose={() => setReverseTarget(null)}
        onConfirm={(reason) => void reverse(reason)}
      />
    </div>
  );
}

export function CommissionApprovalsNavLink({ lang }: { lang: Lang }) {
  const t = useTranslator(lang);
  const { can } = usePortalSession();
  if (!can(PortalPermissions.Commission.Approve)) return null;
  return (
    <p className="text-sm">
      <Link to="/finance/commission-approvals" className="text-primary underline-offset-4 hover:underline">
        {t("commissionApprovalsLink" as never)}
      </Link>
    </p>
  );
}

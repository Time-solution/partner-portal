import { useCallback, useEffect, useState } from "react";
import { Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { getPortalDataSource } from "@/lib/data";
import type { MerchantActivationRow } from "@/lib/data/types";
import { PortalPermissions } from "@/lib/rbac/portalRoles";
import { usePortalSession } from "@/features/auth/usePortalSession";
import { ActivationWorkflow } from "../components/ActivationWorkflow";
import { PageHeader } from "../components/PageHeader";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";

export function ActivationsPage({ lang }: { lang: Lang }) {
  const t = useTranslator(lang);
  const { can, role, user } = usePortalSession();
  const [activations, setActivations] = useState<MerchantActivationRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setActivations(await getPortalDataSource().getActivations());
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const canRequest = can(PortalPermissions.Activations.Request);
  const canApprove = can(PortalPermissions.Activations.Approve);
  const canSetTerms = can(PortalPermissions.Billing.SetTerms);

  const runAction = async (id: string, action: "request" | "terms" | "approve") => {
    setBusyId(id);
    try {
      const ds = getPortalDataSource();
      const actor = user.name;
      if (action === "request") await ds.requestActivation(id, actor);
      else if (action === "terms") await ds.setActivationTerms(id, actor);
      else await ds.approveActivation(id, actor);
      await load();
    } finally {
      setBusyId(null);
    }
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title={t("navActivations" as never)}
        description={t("activationsDesc" as never)}
        lang={lang}
      />
      {loading ? (
        <div className="flex items-center gap-2 text-base text-muted-foreground">
          <Loader2 className="h-5 w-5 animate-spin" />
          {t("loadingData" as never)}
        </div>
      ) : (
        <div className="space-y-4">
          {activations.map(({ activation: a, workflow: wf }) => (
            <Card key={a.id}>
              <CardHeader className="flex flex-row flex-wrap items-start justify-between gap-3">
                <div>
                  <CardTitle className="text-lg">{a.merchantName}</CardTitle>
                  <CardDescription className="text-base">
                    {a.catalogItemName} · {a.resalePrice.amount.toFixed(2)} {a.resalePrice.currency} ·{" "}
                    {a.status}
                  </CardDescription>
                </div>
                <div className="flex flex-wrap gap-2">
                  {canRequest && wf.stage === "Pending" ? (
                    <Button
                      size="sm"
                      variant="outline"
                      disabled={busyId === a.id}
                      onClick={() => void runAction(a.id, "request")}
                    >
                      {t("activationRequest" as never)}
                    </Button>
                  ) : null}
                  {canSetTerms && wf.stage === "PsmRequested" ? (
                    <Button
                      size="sm"
                      variant="outline"
                      disabled={busyId === a.id}
                      onClick={() => void runAction(a.id, "terms")}
                    >
                      {t("activationSetTerms" as never)}
                    </Button>
                  ) : null}
                  {canSetTerms && wf.stage === "AccountantTermsSet" && !canApprove ? (
                    <span className="text-sm text-muted-foreground">{t("activationAwaitAdmin" as never)}</span>
                  ) : null}
                  {canApprove && wf.stage === "AccountantTermsSet" ? (
                    <Button
                      size="sm"
                      disabled={busyId === a.id}
                      onClick={() => void runAction(a.id, "approve")}
                    >
                      {t("activationApprove" as never)}
                    </Button>
                  ) : null}
                </div>
              </CardHeader>
              <CardContent className="space-y-3">
                <ActivationWorkflow workflow={wf} lang={lang} />
                <p className="text-xs text-muted-foreground">
                  {t("activationRoleHint" as never)}: {String(role)}
                </p>
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}

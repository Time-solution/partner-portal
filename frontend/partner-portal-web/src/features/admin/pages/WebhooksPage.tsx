import { useCallback, useEffect, useState } from "react";
import { Loader2, Pause, Play, RefreshCw, RotateCcw } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { getPortalDataSource } from "@/lib/data";
import type { WebhookDelivery, WebhookEndpoint } from "@/lib/data/types";
import { PortalPermissions } from "@/lib/rbac/portalRoles";
import { usePortalSession } from "@/features/auth/usePortalSession";
import { SecretReveal } from "../components/SecretReveal";
import { PageHeader } from "../components/PageHeader";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";

const statusClass: Record<string, string> = {
  Delivered: "text-emerald-600",
  Retrying: "text-warning",
  Dlq: "text-danger",
};

export function WebhooksPage({ lang }: { lang: Lang }) {
  const t = useTranslator(lang);
  const { scopedPartnerId, can } = usePortalSession();
  const [endpoints, setEndpoints] = useState<WebhookEndpoint[]>([]);
  const [deliveries, setDeliveries] = useState<WebhookDelivery[]>([]);
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [revealedSecret, setRevealedSecret] = useState<string | null>(null);
  const [registerUrl, setRegisterUrl] = useState("");
  const [registerPartnerId, setRegisterPartnerId] = useState(
    scopedPartnerId ?? "22222222-2222-2222-2222-222222222001",
  );

  const canManage = can(PortalPermissions.Webhooks.Manage);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const ds = getPortalDataSource();
      const [eps, dels] = await Promise.all([
        ds.getWebhookEndpoints(scopedPartnerId),
        ds.getWebhookDeliveries(),
      ]);
      setEndpoints(eps);
      setDeliveries(dels);
    } finally {
      setLoading(false);
    }
  }, [scopedPartnerId]);

  useEffect(() => {
    void load();
  }, [load]);

  const register = async () => {
    if (!registerUrl.trim()) return;
    setBusyId("register");
    try {
      const result = await getPortalDataSource().registerWebhook({
        partnerId: registerPartnerId,
        url: registerUrl.trim(),
        eventTypes: ["settlement.allocated", "order.reflected"],
      });
      setRevealedSecret(result.secretOnce);
      setRegisterUrl("");
      await load();
    } finally {
      setBusyId(null);
    }
  };

  const togglePause = async (endpoint: WebhookEndpoint) => {
    setBusyId(endpoint.id);
    try {
      await getPortalDataSource().setWebhookPaused(endpoint.id, endpoint.status === "Active");
      await load();
    } finally {
      setBusyId(null);
    }
  };

  const rotateSecret = async (endpointId: string) => {
    setBusyId(endpointId);
    try {
      const result = await getPortalDataSource().rotateWebhookSecret(endpointId);
      setRevealedSecret(result.secretOnce);
      await load();
    } finally {
      setBusyId(null);
    }
  };

  const retryDelivery = async (deliveryId: string) => {
    setBusyId(deliveryId);
    try {
      await getPortalDataSource().retryWebhookDelivery(deliveryId);
      await load();
      setTimeout(() => void load(), 1600);
    } finally {
      setBusyId(null);
    }
  };

  const simulate = async (endpointId: string) => {
    setBusyId(`sim-${endpointId}`);
    try {
      await getPortalDataSource().simulateWebhookDelivery(endpointId);
      await load();
    } finally {
      setBusyId(null);
    }
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title={t("navWebhooksManage" as never)}
        description={t("webhooksDesc" as never)}
        lang={lang}
      />

      {revealedSecret ? (
        <SecretReveal
          label={t("webhooksSigningSecret" as never)}
          secret={revealedSecret}
          warning={t("webhooksSecretOnce" as never)}
          onDismiss={() => setRevealedSecret(null)}
        />
      ) : null}

      {canManage ? (
        <Card>
          <CardHeader>
            <CardTitle className="text-lg">{t("webhooksRegisterTitle" as never)}</CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col gap-3 sm:flex-row sm:items-end">
            {!scopedPartnerId ? (
              <div className="flex-1 space-y-1">
                <Label htmlFor="wh-partner">{t("colPartner" as never)}</Label>
                <Input
                  id="wh-partner"
                  value={registerPartnerId}
                  onChange={(e) => setRegisterPartnerId(e.target.value)}
                  className="font-mono text-sm"
                />
              </div>
            ) : null}
            <div className="flex-[2] space-y-1">
              <Label htmlFor="wh-url">{t("webhooksUrl" as never)}</Label>
              <Input
                id="wh-url"
                type="url"
                placeholder="https://hooks.example.com/zahy"
                value={registerUrl}
                onChange={(e) => setRegisterUrl(e.target.value)}
              />
            </div>
            <Button disabled={busyId === "register"} onClick={() => void register()}>
              {t("webhooksRegister" as never)}
            </Button>
          </CardContent>
        </Card>
      ) : null}

      {loading ? (
        <div className="flex items-center gap-2 text-base text-muted-foreground">
          <Loader2 className="h-5 w-5 animate-spin" />
          {t("loadingData" as never)}
        </div>
      ) : (
        <>
          <div className="space-y-4">
            {endpoints.map((ep) => (
              <Card key={ep.id}>
                <CardHeader className="flex flex-row flex-wrap items-start justify-between gap-3">
                  <div>
                    <CardTitle className="text-lg">{ep.partnerName}</CardTitle>
                    <CardDescription className="break-all text-base">{ep.url}</CardDescription>
                    <p className="mt-1 text-sm text-muted-foreground">
                      {ep.eventTypes.join(", ")} · {ep.secretHint} ·{" "}
                      <span className={ep.status === "Active" ? "text-emerald-600" : "text-warning"}>
                        {ep.status}
                      </span>
                    </p>
                  </div>
                  {canManage ? (
                    <div className="flex flex-wrap gap-2">
                      <Button
                        size="sm"
                        variant="outline"
                        disabled={busyId === ep.id}
                        onClick={() => void togglePause(ep)}
                      >
                        {ep.status === "Active" ? (
                          <Pause className="h-4 w-4" />
                        ) : (
                          <Play className="h-4 w-4" />
                        )}
                        {ep.status === "Active" ? t("webhooksPause" as never) : t("webhooksResume" as never)}
                      </Button>
                      <Button
                        size="sm"
                        variant="outline"
                        disabled={busyId === ep.id}
                        onClick={() => void rotateSecret(ep.id)}
                      >
                        <RotateCcw className="h-4 w-4" />
                        {t("webhooksRotateSecret" as never)}
                      </Button>
                      <Button
                        size="sm"
                        variant="outline"
                        disabled={busyId === `sim-${ep.id}` || ep.status === "Paused"}
                        onClick={() => void simulate(ep.id)}
                      >
                        <RefreshCw className="h-4 w-4" />
                        {t("webhooksSimulate" as never)}
                      </Button>
                    </div>
                  ) : null}
                </CardHeader>
              </Card>
            ))}
          </div>

          <Card>
            <CardHeader>
              <CardTitle className="text-lg">{t("webhooksDeliveryLog" as never)}</CardTitle>
              <CardDescription>{t("webhooksDeliveryDesc" as never)}</CardDescription>
            </CardHeader>
            <CardContent>
              <div className="overflow-x-auto">
                <table className="w-full min-w-[640px] text-base">
                  <thead>
                    <tr className="border-b border-border text-start text-muted-foreground">
                      <th className="px-2 py-2.5 font-medium">{t("colEvent" as never)}</th>
                      <th className="px-2 py-2.5 font-medium">{t("colStatus" as never)}</th>
                      <th className="px-2 py-2.5 font-medium">{t("webhooksAttempts" as never)}</th>
                      <th className="px-2 py-2.5 font-medium">{t("colActions" as never)}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {deliveries.map((d) => (
                      <tr key={d.id} className="border-b border-border/60">
                        <td className="px-2 py-2.5 font-mono text-sm">{d.eventType}</td>
                        <td className={`px-2 py-2.5 font-medium ${statusClass[d.status] ?? ""}`}>
                          {d.status}
                          {d.responseCode ? ` (${d.responseCode})` : ""}
                        </td>
                        <td className="px-2 py-2.5 tabular-nums">{d.attemptCount}</td>
                        <td className="px-2 py-2.5">
                          {canManage && (d.status === "Dlq" || d.status === "Retrying") ? (
                            <Button
                              size="sm"
                              variant="outline"
                              disabled={busyId === d.id}
                              onClick={() => void retryDelivery(d.id)}
                            >
                              {t("webhooksRetry" as never)}
                            </Button>
                          ) : null}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </CardContent>
          </Card>
        </>
      )}
    </div>
  );
}

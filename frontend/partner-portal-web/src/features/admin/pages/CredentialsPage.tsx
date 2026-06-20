import { useCallback, useEffect, useState } from "react";
import { Copy, KeyRound, Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { getPortalDataSource } from "@/lib/data";
import type { PartnerCredential } from "@/lib/data/types";
import { PortalPermissions } from "@/lib/rbac/portalRoles";
import { usePortalSession } from "@/features/auth/usePortalSession";
import { SecretReveal } from "../components/SecretReveal";
import { PageHeader } from "../components/PageHeader";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";

export function CredentialsPage({ lang }: { lang: Lang }) {
  const t = useTranslator(lang);
  const { scopedPartnerId, can } = usePortalSession();
  const [credentials, setCredentials] = useState<PartnerCredential[]>([]);
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [revealedSecret, setRevealedSecret] = useState<{ partnerId: string; secret: string } | null>(
    null,
  );

  const canRotate = can(PortalPermissions.Credentials.Rotate);
  const readOnly = !canRotate;

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setCredentials(await getPortalDataSource().getPartnerCredentials(scopedPartnerId));
    } finally {
      setLoading(false);
    }
  }, [scopedPartnerId]);

  useEffect(() => {
    void load();
  }, [load]);

  const rotate = async (partnerId: string) => {
    setBusyId(partnerId);
    try {
      const result = await getPortalDataSource().rotateClientSecret(partnerId);
      setRevealedSecret({ partnerId, secret: result.secretOnce });
      await load();
    } finally {
      setBusyId(null);
    }
  };

  const copyClientId = async (clientId: string) => {
    await navigator.clipboard.writeText(clientId);
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title={t("navCredentials" as never)}
        description={t("credentialsDesc" as never)}
        lang={lang}
      />

      {revealedSecret ? (
        <SecretReveal
          label={t("credentialsSecretLabel" as never)}
          secret={revealedSecret.secret}
          warning={t("credentialsSecretOnce" as never)}
          onDismiss={() => setRevealedSecret(null)}
        />
      ) : null}

      {readOnly ? (
        <p className="text-sm text-muted-foreground">{t("credentialsReadOnly" as never)}</p>
      ) : null}

      {loading ? (
        <div className="flex items-center gap-2 text-base text-muted-foreground">
          <Loader2 className="h-5 w-5 animate-spin" />
          {t("loadingData" as never)}
        </div>
      ) : (
        <div className="space-y-4">
          {credentials.map((cred) => (
            <Card key={cred.partnerId}>
              <CardHeader>
                <CardTitle className="flex items-center gap-2 text-lg">
                  <KeyRound className="h-5 w-5 text-primary" aria-hidden="true" />
                  {cred.partnerName}
                </CardTitle>
                <CardDescription>{t("credentialsApiNote" as never)}</CardDescription>
              </CardHeader>
              <CardContent className="space-y-4 text-base">
                <dl className="grid gap-3 sm:grid-cols-2">
                  <div>
                    <dt className="text-sm text-muted-foreground">{t("credentialsClientId" as never)}</dt>
                    <dd className="flex items-center gap-2 font-mono text-sm">
                      {cred.clientId}
                      <Button
                        size="icon"
                        variant="ghost"
                        className="h-8 w-8"
                        aria-label="Copy client ID"
                        onClick={() => void copyClientId(cred.clientId)}
                      >
                        <Copy className="h-4 w-4" />
                      </Button>
                    </dd>
                  </div>
                  <div>
                    <dt className="text-sm text-muted-foreground">{t("credentialsSecretHint" as never)}</dt>
                    <dd className="font-mono text-sm">{cred.secretHint}</dd>
                  </div>
                  <div>
                    <dt className="text-sm text-muted-foreground">{t("credentialsTokenUrl" as never)}</dt>
                    <dd className="break-all font-mono text-sm">{cred.tokenEndpointUrl}</dd>
                  </div>
                  <div>
                    <dt className="text-sm text-muted-foreground">{t("credentialsScopes" as never)}</dt>
                    <dd className="text-sm">{cred.scopes.join(", ")}</dd>
                  </div>
                </dl>
                {canRotate ? (
                  <Button
                    variant="outline"
                    size="sm"
                    disabled={busyId === cred.partnerId}
                    onClick={() => void rotate(cred.partnerId)}
                  >
                    {t("credentialsRotate" as never)}
                  </Button>
                ) : null}
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}

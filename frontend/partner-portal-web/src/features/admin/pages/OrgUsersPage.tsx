import { Card, CardContent } from "@/components/ui/card";
import { usePortalSession } from "@/features/auth/usePortalSession";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";
import { OrgUsersManager } from "./OrgUsersManager";

/** Self-service users area scoped to the signed-in user's OWN org. */
export function OrgUsersPage({ lang }: { lang: Lang }) {
  const t = useTranslator(lang);
  const { org } = usePortalSession();

  if (!org) {
    return (
      <Card>
        <CardContent className="py-8 text-base text-muted-foreground">
          {t("orgAccessBlocked" as never)}
        </CardContent>
      </Card>
    );
  }

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-xl font-semibold">{t("orgUsersTitle" as never)}</h1>
        <p className="text-base text-muted-foreground">{org.name}</p>
      </div>
      <OrgUsersManager lang={lang} org={org} />
    </div>
  );
}

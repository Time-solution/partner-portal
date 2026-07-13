import { useMemo, useState } from "react";
import { Clock3, PackageCheck, Send, XCircle } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { MoneyAmount } from "@/components/MoneyAmount";
import { useTranslator, type Lang } from "@/lib/i18n";
import type { ListingRequirementRow } from "@/lib/catalog/listingSchema";
import {
  ANSWER_CAPS,
  createOrder,
  listOrdersForPartner,
  listOrdersForTenant,
  merchantAccept,
  merchantCancel,
  merchantOrderView,
  markDelivered,
  mutateOrder,
  partnerAccept,
  partnerDecline,
  partnerOrderView,
  requestRevision,
  saveOrder,
  submitRequirements,
  validateAnswers,
  type ServiceOrder,
  type ServiceOrderGuardError,
  type ServiceOrderHistoryRow,
} from "@/lib/orders/serviceOrders";

const INPUT_CLASS =
  "flex w-full rounded-md border border-input bg-surface px-3 py-2 text-sm text-surface-foreground shadow-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring";

const statusTone: Record<string, string> = {
  Draft: "bg-muted text-muted-foreground",
  RequirementsSubmitted: "bg-sky-100 text-sky-800 dark:bg-sky-900/40 dark:text-sky-300",
  InProgress: "bg-indigo-100 text-indigo-800 dark:bg-indigo-900/40 dark:text-indigo-300",
  Delivered: "bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-300",
  Accepted: "bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-300",
  Closed: "bg-slate-200 text-slate-700 dark:bg-slate-800 dark:text-slate-300",
  MerchantCancelled: "bg-rose-100 text-rose-800 dark:bg-rose-900/40 dark:text-rose-300",
  PartnerDeclined: "bg-rose-100 text-rose-800 dark:bg-rose-900/40 dark:text-rose-300",
};

function StatusBadge({ lang, status }: { lang: Lang; status: string }) {
  const t = useTranslator(lang);
  return (
    <span className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${statusTone[status] ?? statusTone.Draft}`}>
      {t(`orderStatus_${status}` as never)}
    </span>
  );
}

/** Status timeline straight from the APPEND-ONLY history — no synthesized state. */
function HistoryTimeline({ lang, history }: { lang: Lang; history: ServiceOrderHistoryRow[] }) {
  const t = useTranslator(lang);
  return (
    <ol className="space-y-1 border-s border-border ps-3 text-xs" data-testid="order-timeline">
      {history.map((row) => (
        <li key={row.orderIndex}>
          <span className="font-medium">{t(`orderAction_${row.action}` as never)}</span>
          <span className="text-muted-foreground"> · {row.actor}</span>
          {row.note ? <span className="text-muted-foreground"> — "{row.note}"</span> : null}
        </li>
      ))}
    </ol>
  );
}

/**
 * Gate 2b — merchant "order this service" form: the listing's typed requirements (caps,
 * MultiChoice select, FileUpload NAME field — no storage), then create + submit in one step.
 */
export function ServiceOrderForm({
  lang,
  catalogItemId,
  offeringName,
  partnerId,
  tenantId,
  participationMode,
  buy,
  sellOrFee,
  requirements,
  onDone,
  onCancel,
}: {
  lang: Lang;
  catalogItemId: string;
  offeringName: string;
  partnerId: string;
  tenantId: string;
  participationMode: "Principal" | "SubscriptionFee";
  buy?: number;
  sellOrFee: number;
  requirements: ListingRequirementRow[];
  onDone: () => void;
  onCancel: () => void;
}) {
  const t = useTranslator(lang);
  const [answers, setAnswers] = useState<Record<number, string>>({});
  const [error, setError] = useState<ServiceOrderGuardError | null>(null);

  const submit = () => {
    setError(null);
    const invalid = validateAnswers(requirements, answers);
    if (invalid) {
      setError(invalid);
      return;
    }

    const created = createOrder({
      catalogItemId,
      offeringName,
      partnerId,
      tenantId,
      participationMode,
      buy,
      sellOrFee,
      actor: tenantId,
      at: new Date().toISOString(),
    });
    if (created.error || !created.order) {
      setError(created.error ?? "orderErrIllegalTransition");
      return;
    }

    const guard = submitRequirements(created.order, requirements, answers, tenantId, new Date().toISOString());
    if (guard) {
      setError(guard);
      return;
    }
    saveOrder(created.order);
    onDone();
  };

  return (
    <Card dir={lang === "ar" ? "rtl" : "ltr"} data-testid="service-order-form">
      <CardHeader className="pb-3">
        <CardTitle className="text-base">
          {t("orderFormTitle" as never)} · {offeringName}
        </CardTitle>
        <CardDescription>
          {t("orderFormDesc" as never)} — <MoneyAmount amount={sellOrFee} />
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        {requirements.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t("orderFormNoRequirements" as never)}</p>
        ) : (
          [...requirements]
            .sort((a, b) => a.orderIndex - b.orderIndex)
            .map((requirement) => (
              <div key={requirement.id} className="space-y-1">
                <label className="text-sm font-medium">
                  {requirement.title}
                  <span className="ms-1 text-xs text-muted-foreground">
                    ({t(`listingReqType_${requirement.type}` as never)})
                  </span>
                </label>
                {requirement.type === "MultiChoice" ? (
                  <select
                    data-testid={`order-answer-${requirement.orderIndex}`}
                    className="h-9 w-full rounded-md border border-input bg-surface px-2 text-sm"
                    value={answers[requirement.orderIndex] ?? ""}
                    onChange={(e) => setAnswers((a) => ({ ...a, [requirement.orderIndex]: e.target.value }))}
                  >
                    <option value="">{t("orderFormPickChoice" as never)}</option>
                    {requirement.choices.map((choice) => (
                      <option key={choice} value={choice}>
                        {choice}
                      </option>
                    ))}
                  </select>
                ) : requirement.type === "LongText" ? (
                  <textarea
                    data-testid={`order-answer-${requirement.orderIndex}`}
                    rows={3}
                    maxLength={ANSWER_CAPS.LongText}
                    className={INPUT_CLASS}
                    value={answers[requirement.orderIndex] ?? ""}
                    onChange={(e) => setAnswers((a) => ({ ...a, [requirement.orderIndex]: e.target.value }))}
                  />
                ) : (
                  <input
                    data-testid={`order-answer-${requirement.orderIndex}`}
                    maxLength={ANSWER_CAPS[requirement.type]}
                    placeholder={
                      requirement.type === "FileUpload" ? t("orderFormFileNamePlaceholder" as never) : undefined
                    }
                    className={INPUT_CLASS}
                    value={answers[requirement.orderIndex] ?? ""}
                    onChange={(e) => setAnswers((a) => ({ ...a, [requirement.orderIndex]: e.target.value }))}
                  />
                )}
              </div>
            ))
        )}

        {error && (
          <p role="alert" data-testid="order-form-error" className="text-sm text-danger">
            {t(error as never)}
          </p>
        )}

        <div className="flex items-center justify-end gap-2">
          <Button size="sm" variant="outline" onClick={onCancel}>
            {t("orderFormCancel" as never)}
          </Button>
          <Button size="sm" data-testid="order-form-submit" onClick={submit}>
            <Send className="h-4 w-4" aria-hidden="true" />
            {t("orderFormSubmit" as never)}
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}

/** Merchant my-orders: list + detail (timeline, accept / revision-with-note, cancel where legal). */
export function MerchantServiceOrdersPanel({ lang, tenantId }: { lang: Lang; tenantId: string }) {
  const t = useTranslator(lang);
  const [version, setVersion] = useState(0);
  const [notes, setNotes] = useState<Record<string, string>>({});
  const [error, setError] = useState<ServiceOrderGuardError | null>(null);

  const orders = useMemo(
    () => listOrdersForTenant(tenantId).map(merchantOrderView),
    [tenantId, version],
  );

  const act = (orderId: string, action: (o: ServiceOrder) => ServiceOrderGuardError | null) => {
    setError(null);
    const guard = mutateOrder(orderId, action);
    if (guard) setError(guard);
    else setVersion((v) => v + 1);
  };

  return (
    <Card dir={lang === "ar" ? "rtl" : "ltr"} data-testid="merchant-orders-panel">
      <CardHeader className="pb-3">
        <CardTitle className="flex items-center gap-2 text-base">
          <PackageCheck className="h-4 w-4" aria-hidden="true" />
          {t("merchantOrdersTitle" as never)} ({orders.length})
        </CardTitle>
        <CardDescription>{t("merchantOrdersDesc" as never)}</CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        {error && (
          <p role="alert" data-testid="merchant-orders-error" className="text-sm text-danger">
            {t(error as never)}
          </p>
        )}
        {orders.length === 0 && <p className="text-sm text-muted-foreground">{t("ordersEmpty" as never)}</p>}
        {orders.map((order) => (
          <div key={order.id} className="space-y-2 rounded-md border border-border p-3" data-testid={`merchant-order-${order.id}`}>
            <div className="flex flex-wrap items-center justify-between gap-2">
              <div>
                <p className="text-sm font-medium">{order.offeringName}</p>
                <p className="text-xs text-muted-foreground">
                  {t("orderPrice" as never)}: <MoneyAmount amount={order.priceAmount} />
                  {order.revisionCount > 0 ? ` · ${t("orderRevisions" as never)}: ${order.revisionCount}` : ""}
                </p>
              </div>
              <StatusBadge lang={lang} status={order.status} />
            </div>

            {order.milestones.length > 0 && (
              <div className="rounded-md bg-muted/40 p-2 text-xs" data-testid="order-milestones">
                <p className="mb-1 font-medium">{t("orderMilestonePlan" as never)}</p>
                <ul className="space-y-0.5">
                  {order.milestones.map((m) => (
                    <li key={m.orderIndex} className="flex items-center justify-between">
                      <span>{m.title}</span>
                      <MoneyAmount amount={m.amount} />
                    </li>
                  ))}
                </ul>
              </div>
            )}

            <HistoryTimeline lang={lang} history={order.history} />

            <div className="flex flex-wrap items-center gap-2">
              {(order.status === "Draft" || order.status === "RequirementsSubmitted") && (
                <Button size="sm" variant="outline" data-testid={`order-cancel-${order.id}`}
                  onClick={() => act(order.id, (o) => merchantCancel(o, tenantId, new Date().toISOString()))}>
                  <XCircle className="h-4 w-4" aria-hidden="true" />
                  {t("orderCancel" as never)}
                </Button>
              )}
              {order.status === "Delivered" && (
                <>
                  <Button size="sm" data-testid={`order-accept-${order.id}`}
                    onClick={() => act(order.id, (o) => merchantAccept(o, tenantId, new Date().toISOString()))}>
                    {t("orderAcceptDelivery" as never)}
                  </Button>
                  <input
                    data-testid={`order-revision-note-${order.id}`}
                    className="h-9 w-56 rounded-md border border-input bg-surface px-3 text-sm"
                    placeholder={t("orderRevisionNotePlaceholder" as never)}
                    value={notes[order.id] ?? ""}
                    onChange={(e) => setNotes((n) => ({ ...n, [order.id]: e.target.value }))}
                  />
                  <Button size="sm" variant="outline" data-testid={`order-revision-${order.id}`}
                    onClick={() => act(order.id, (o) => requestRevision(o, tenantId, notes[order.id] ?? "", new Date().toISOString()))}>
                    {t("orderRequestRevision" as never)}
                  </Button>
                </>
              )}
            </div>
          </div>
        ))}
      </CardContent>
    </Card>
  );
}

/** Partner incoming orders: accept/decline-with-note, deliver; BUY side only. */
export function PartnerServiceOrdersPanel({ lang, partnerId }: { lang: Lang; partnerId: string }) {
  const t = useTranslator(lang);
  const [version, setVersion] = useState(0);
  const [notes, setNotes] = useState<Record<string, string>>({});
  const [error, setError] = useState<ServiceOrderGuardError | null>(null);

  const orders = useMemo(
    () => listOrdersForPartner(partnerId).map(partnerOrderView),
    [partnerId, version],
  );

  const act = (orderId: string, action: (o: ServiceOrder) => ServiceOrderGuardError | null) => {
    setError(null);
    const guard = mutateOrder(orderId, action);
    if (guard) setError(guard);
    else setVersion((v) => v + 1);
  };

  return (
    <Card dir={lang === "ar" ? "rtl" : "ltr"} data-testid="partner-orders-panel">
      <CardHeader className="pb-3">
        <CardTitle className="flex items-center gap-2 text-base">
          <Clock3 className="h-4 w-4" aria-hidden="true" />
          {t("partnerOrdersTitle" as never)} ({orders.length})
        </CardTitle>
        <CardDescription>{t("partnerOrdersDesc" as never)}</CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        {error && (
          <p role="alert" data-testid="partner-orders-error" className="text-sm text-danger">
            {t(error as never)}
          </p>
        )}
        {orders.length === 0 && <p className="text-sm text-muted-foreground">{t("ordersEmpty" as never)}</p>}
        {orders.map((order) => (
          <div key={order.id} className="space-y-2 rounded-md border border-border p-3" data-testid={`partner-order-${order.id}`}>
            <div className="flex flex-wrap items-center justify-between gap-2">
              <div>
                <p className="text-sm font-medium">{order.offeringName}</p>
                {order.buyAmount != null && (
                  <p className="text-xs text-muted-foreground">
                    {t("orderPartnerReceivable" as never)}: <MoneyAmount amount={order.buyAmount} />
                  </p>
                )}
              </div>
              <StatusBadge lang={lang} status={order.status} />
            </div>

            {order.answers.length > 0 && (
              <ul className="space-y-0.5 text-xs text-muted-foreground" data-testid="partner-order-answers">
                {order.answers.map((answer) => (
                  <li key={answer.orderIndex}>
                    <span className="font-medium text-foreground">{answer.requirementTitle}:</span> {answer.answerText}
                  </li>
                ))}
              </ul>
            )}

            <HistoryTimeline lang={lang} history={order.history} />

            <div className="flex flex-wrap items-center gap-2">
              {order.status === "RequirementsSubmitted" && (
                <>
                  <Button size="sm" data-testid={`partner-accept-${order.id}`}
                    onClick={() => act(order.id, (o) => partnerAccept(o, partnerId, new Date().toISOString()))}>
                    {t("orderPartnerAccept" as never)}
                  </Button>
                  <input
                    data-testid={`partner-decline-note-${order.id}`}
                    className="h-9 w-56 rounded-md border border-input bg-surface px-3 text-sm"
                    placeholder={t("orderDeclineNotePlaceholder" as never)}
                    value={notes[order.id] ?? ""}
                    onChange={(e) => setNotes((n) => ({ ...n, [order.id]: e.target.value }))}
                  />
                  <Button size="sm" variant="outline" data-testid={`partner-decline-${order.id}`}
                    onClick={() => act(order.id, (o) => partnerDecline(o, partnerId, notes[order.id] ?? "", new Date().toISOString()))}>
                    {t("orderPartnerDecline" as never)}
                  </Button>
                </>
              )}
              {order.status === "InProgress" && (
                <Button size="sm" data-testid={`partner-deliver-${order.id}`}
                  onClick={() => act(order.id, (o) => markDelivered(o, partnerId, new Date().toISOString()))}>
                  {t("orderMarkDelivered" as never)}
                </Button>
              )}
            </div>
          </div>
        ))}
      </CardContent>
    </Card>
  );
}

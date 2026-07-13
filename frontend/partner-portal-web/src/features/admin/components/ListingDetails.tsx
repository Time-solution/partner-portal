import { CheckCircle2, ListChecks, ScrollText, Workflow } from "lucide-react";
import { useTranslator, type Lang } from "@/lib/i18n";
import { isListingEmpty, type OfferingListing } from "@/lib/catalog/listingSchema";

/**
 * Gate 2a — MERCHANT read-only rendering of a structured listing: requirements ("what you'll need
 * to provide"), deliverables ("what you get" × quantity), execution steps, terms, and an FAQ
 * accordion. Absent sections render NOTHING; an empty listing renders nothing at all. Pure display
 * — no authoring, no ordering lifecycle, and (by construction of the schema) no cost/margin data.
 */
export function ListingDetails({ lang, listing }: { lang: Lang; listing: OfferingListing }) {
  const t = useTranslator(lang);

  if (isListingEmpty(listing)) {
    return null;
  }

  const ordered = <T extends { orderIndex: number }>(rows: T[]): T[] =>
    [...rows].sort((a, b) => a.orderIndex - b.orderIndex);

  return (
    <div className="space-y-3 text-sm" data-testid="listing-details" dir={lang === "ar" ? "rtl" : "ltr"}>
      {listing.requirements.length > 0 && (
        <section data-testid="listing-details-requirements">
          <h4 className="mb-1 flex items-center gap-1.5 font-medium">
            <ListChecks className="h-4 w-4 text-muted-foreground" aria-hidden="true" />
            {t("listingWhatYouProvide" as never)}
          </h4>
          <ul className="ms-5 list-disc space-y-0.5">
            {ordered(listing.requirements).map((row) => (
              <li key={row.id}>
                {row.title}
                <span className="ms-1 text-xs text-muted-foreground">
                  ({t(`listingReqType_${row.type}` as never)})
                </span>
                {row.type === "MultiChoice" && row.choices.length > 0 && (
                  <span className="ms-1 text-xs text-muted-foreground">— {row.choices.join(" / ")}</span>
                )}
              </li>
            ))}
          </ul>
        </section>
      )}

      {listing.deliverables.length > 0 && (
        <section data-testid="listing-details-deliverables">
          <h4 className="mb-1 flex items-center gap-1.5 font-medium">
            <CheckCircle2 className="h-4 w-4 text-muted-foreground" aria-hidden="true" />
            {t("listingWhatYouGet" as never)}
          </h4>
          <ul className="ms-5 list-disc space-y-0.5">
            {ordered(listing.deliverables).map((row) => (
              <li key={row.id}>
                {row.title} <span className="tabular-nums text-xs text-muted-foreground">× {row.quantity}</span>
              </li>
            ))}
          </ul>
        </section>
      )}

      {listing.executionSteps.length > 0 && (
        <section data-testid="listing-details-steps">
          <h4 className="mb-1 flex items-center gap-1.5 font-medium">
            <Workflow className="h-4 w-4 text-muted-foreground" aria-hidden="true" />
            {t("listingHowItRuns" as never)}
          </h4>
          <ol className="ms-5 list-decimal space-y-0.5">
            {ordered(listing.executionSteps).map((row) => (
              <li key={row.id}>{row.text}</li>
            ))}
          </ol>
        </section>
      )}

      {listing.terms.length > 0 && (
        <section data-testid="listing-details-terms">
          <h4 className="mb-1 flex items-center gap-1.5 font-medium">
            <ScrollText className="h-4 w-4 text-muted-foreground" aria-hidden="true" />
            {t("listingTermsTitle" as never)}
          </h4>
          <ul className="ms-5 list-disc space-y-0.5">
            {ordered(listing.terms).map((row) => (
              <li key={row.id}>{row.text}</li>
            ))}
          </ul>
        </section>
      )}

      {listing.faqs.length > 0 && (
        <section data-testid="listing-details-faqs" className="space-y-1">
          <h4 className="font-medium">{t("listingFaqsTitle" as never)}</h4>
          {ordered(listing.faqs).map((row) => (
            <details key={row.id} className="rounded-md border border-border/60 px-3 py-1.5">
              <summary className="cursor-pointer select-none">{row.question}</summary>
              <p className="mt-1 text-muted-foreground">{row.answer}</p>
            </details>
          ))}
        </section>
      )}
    </div>
  );
}

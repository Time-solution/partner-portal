import { useState } from "react";
import { ArrowDown, ArrowUp, Plus, Trash2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { useTranslator, type Lang } from "@/lib/i18n";
import {
  LISTING_CAPS,
  moveRow,
  validateListing,
  type ListingIssue,
  type ListingRequirementType,
  type OfferingListing,
} from "@/lib/catalog/listingSchema";

const INPUT_CLASS =
  "flex w-full rounded-md border border-input bg-surface px-3 py-2 text-sm text-surface-foreground shadow-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring";

const REQUIREMENT_TYPES: ListingRequirementType[] = [
  "ShortText",
  "LongText",
  "FileUpload",
  "Link",
  "MultiChoice",
];

let rowSeq = 0;
const newRowId = (): string => `lst-${Date.now()}-${++rowSeq}`;

/**
 * Gate 2a — five collapsible authoring sections for the structured listing (requirements,
 * deliverables, steps, terms, FAQs): add / edit / remove / reorder with cap counters and per-row
 * validation (the single listingSchema source, incl. the anti-disintermediation policy). Pure and
 * props-driven; the host wires the store. FileUpload is only a NAMED requirement type — no upload
 * machinery in this gate.
 */
export function ListingSectionsEditor({
  lang,
  listing,
  onSave,
  busy = false,
}: {
  lang: Lang;
  listing: OfferingListing;
  onSave: (next: OfferingListing) => void;
  busy?: boolean;
}) {
  const t = useTranslator(lang);
  const [draft, setDraft] = useState<OfferingListing>(listing);
  const [issues, setIssues] = useState<ListingIssue[]>([]);

  const set = (patch: Partial<OfferingListing>) => setDraft((d) => ({ ...d, ...patch }));

  const trySave = () => {
    const found = validateListing(draft);
    setIssues(found);
    if (found.length === 0) onSave(draft);
  };

  const sectionIssue = (section: ListingIssue["section"], rowIndex: number | null): ListingIssue | undefined =>
    issues.find((i) => i.section === section && i.rowIndex === rowIndex) ??
    (rowIndex === null ? issues.find((i) => i.section === section) : undefined);

  const issueText = (issue: ListingIssue | undefined) =>
    issue ? (
      <p className="text-xs text-danger" data-testid={`listing-issue-${issue.section}`}>
        {t(issue.error as never)}
      </p>
    ) : null;

  return (
    <div className="space-y-3" data-testid="listing-editor">
      <Section
        testId="requirements"
        title={`${t("listingRequirementsTitle" as never)} (${draft.requirements.length}/${LISTING_CAPS.requirements})`}
        onAdd={
          draft.requirements.length < LISTING_CAPS.requirements
            ? () =>
                set({
                  requirements: [
                    ...draft.requirements,
                    { id: newRowId(), orderIndex: draft.requirements.length, title: "", type: "ShortText", choices: [] },
                  ],
                })
            : undefined
        }
        addLabel={t("listingAddRow" as never)}
      >
        {draft.requirements.map((row, i) => (
          <RowShell
            key={row.id}
            index={i}
            count={draft.requirements.length}
            onMove={(dir) => set({ requirements: moveRow(draft.requirements, i, i + dir) })}
            onRemove={() => set({ requirements: draft.requirements.filter((r) => r.id !== row.id) })}
            moveLabel={t("listingMove" as never)}
            removeLabel={t("listingRemoveRow" as never)}
          >
            <input
              data-testid={`listing-req-title-${i}`}
              className={INPUT_CLASS}
              maxLength={LISTING_CAPS.requirementTitle}
              placeholder={t("listingRequirementPlaceholder" as never)}
              value={row.title}
              onChange={(e) =>
                set({
                  requirements: draft.requirements.map((r) => (r.id === row.id ? { ...r, title: e.target.value } : r)),
                })
              }
            />
            <div className="flex flex-wrap items-center gap-2">
              <select
                data-testid={`listing-req-type-${i}`}
                className="h-9 rounded-md border border-input bg-surface px-2 text-sm"
                value={row.type}
                onChange={(e) =>
                  set({
                    requirements: draft.requirements.map((r) =>
                      r.id === row.id
                        ? {
                            ...r,
                            type: e.target.value as ListingRequirementType,
                            choices: e.target.value === "MultiChoice" ? r.choices : [],
                          }
                        : r,
                    ),
                  })
                }
              >
                {REQUIREMENT_TYPES.map((type) => (
                  <option key={type} value={type}>
                    {t(`listingReqType_${type}` as never)}
                  </option>
                ))}
              </select>
              {row.type === "MultiChoice" ? (
                <input
                  data-testid={`listing-req-choices-${i}`}
                  className={`${INPUT_CLASS} flex-1`}
                  placeholder={t("listingChoicesPlaceholder" as never)}
                  value={row.choices.join("، ")}
                  onChange={(e) =>
                    set({
                      requirements: draft.requirements.map((r) =>
                        r.id === row.id
                          ? { ...r, choices: e.target.value.split(/[،,]/).map((c) => c.trim()).filter(Boolean) }
                          : r,
                      ),
                    })
                  }
                />
              ) : null}
            </div>
            {issueText(sectionIssue("requirements", i))}
          </RowShell>
        ))}
        {issueText(sectionIssue("requirements", null))}
      </Section>

      <Section
        testId="deliverables"
        title={`${t("listingDeliverablesTitle" as never)} (${draft.deliverables.length}/${LISTING_CAPS.deliverables})`}
        onAdd={
          draft.deliverables.length < LISTING_CAPS.deliverables
            ? () =>
                set({
                  deliverables: [
                    ...draft.deliverables,
                    { id: newRowId(), orderIndex: draft.deliverables.length, title: "", quantity: 1 },
                  ],
                })
            : undefined
        }
        addLabel={t("listingAddRow" as never)}
      >
        {draft.deliverables.map((row, i) => (
          <RowShell
            key={row.id}
            index={i}
            count={draft.deliverables.length}
            onMove={(dir) => set({ deliverables: moveRow(draft.deliverables, i, i + dir) })}
            onRemove={() => set({ deliverables: draft.deliverables.filter((r) => r.id !== row.id) })}
            moveLabel={t("listingMove" as never)}
            removeLabel={t("listingRemoveRow" as never)}
          >
            <div className="flex items-center gap-2">
              <input
                data-testid={`listing-del-title-${i}`}
                className={`${INPUT_CLASS} flex-1`}
                maxLength={LISTING_CAPS.deliverableTitle}
                placeholder={t("listingDeliverablePlaceholder" as never)}
                value={row.title}
                onChange={(e) =>
                  set({
                    deliverables: draft.deliverables.map((r) => (r.id === row.id ? { ...r, title: e.target.value } : r)),
                  })
                }
              />
              <Label className="text-xs text-muted-foreground">{t("listingQuantity" as never)}</Label>
              <input
                data-testid={`listing-del-qty-${i}`}
                type="number"
                min={LISTING_CAPS.deliverableQtyMin}
                max={LISTING_CAPS.deliverableQtyMax}
                className="h-9 w-20 rounded-md border border-input bg-surface px-2 text-sm tabular-nums"
                value={row.quantity}
                onChange={(e) =>
                  set({
                    deliverables: draft.deliverables.map((r) =>
                      r.id === row.id ? { ...r, quantity: Number(e.target.value) } : r,
                    ),
                  })
                }
              />
            </div>
            {issueText(sectionIssue("deliverables", i))}
          </RowShell>
        ))}
        {issueText(sectionIssue("deliverables", null))}
      </Section>

      <TextRowsSection
        testId="steps"
        section="executionSteps"
        title={`${t("listingStepsTitle" as never)} (${draft.executionSteps.length}/${LISTING_CAPS.executionSteps})`}
        rows={draft.executionSteps}
        cap={LISTING_CAPS.executionSteps}
        maxLength={LISTING_CAPS.executionStep}
        placeholder={t("listingStepPlaceholder" as never)}
        onChange={(rows) => set({ executionSteps: rows })}
        t={t}
        issue={sectionIssue}
        issueText={issueText}
      />

      <TextRowsSection
        testId="terms"
        section="terms"
        title={`${t("listingTermsTitle" as never)} (${draft.terms.length}/${LISTING_CAPS.terms})`}
        rows={draft.terms}
        cap={LISTING_CAPS.terms}
        maxLength={LISTING_CAPS.term}
        placeholder={t("listingTermPlaceholder" as never)}
        onChange={(rows) => set({ terms: rows })}
        t={t}
        issue={sectionIssue}
        issueText={issueText}
      />

      <Section
        testId="faqs"
        title={`${t("listingFaqsTitle" as never)} (${draft.faqs.length}/${LISTING_CAPS.faqs})`}
        onAdd={
          draft.faqs.length < LISTING_CAPS.faqs
            ? () =>
                set({
                  faqs: [
                    ...draft.faqs,
                    { id: newRowId(), orderIndex: draft.faqs.length, question: "", answer: "" },
                  ],
                })
            : undefined
        }
        addLabel={t("listingAddRow" as never)}
      >
        {draft.faqs.map((row, i) => (
          <RowShell
            key={row.id}
            index={i}
            count={draft.faqs.length}
            onMove={(dir) => set({ faqs: moveRow(draft.faqs, i, i + dir) })}
            onRemove={() => set({ faqs: draft.faqs.filter((r) => r.id !== row.id) })}
            moveLabel={t("listingMove" as never)}
            removeLabel={t("listingRemoveRow" as never)}
          >
            <input
              data-testid={`listing-faq-q-${i}`}
              className={INPUT_CLASS}
              maxLength={LISTING_CAPS.faqQuestion}
              placeholder={t("listingFaqQuestionPlaceholder" as never)}
              value={row.question}
              onChange={(e) =>
                set({ faqs: draft.faqs.map((r) => (r.id === row.id ? { ...r, question: e.target.value } : r)) })
              }
            />
            <textarea
              data-testid={`listing-faq-a-${i}`}
              rows={2}
              className={INPUT_CLASS}
              maxLength={LISTING_CAPS.faqAnswer}
              placeholder={t("listingFaqAnswerPlaceholder" as never)}
              value={row.answer}
              onChange={(e) =>
                set({ faqs: draft.faqs.map((r) => (r.id === row.id ? { ...r, answer: e.target.value } : r)) })
              }
            />
            {issueText(sectionIssue("faqs", i))}
          </RowShell>
        ))}
        {issueText(sectionIssue("faqs", null))}
      </Section>

      <div className="flex items-center justify-end gap-2">
        <Button size="sm" data-testid="listing-save" disabled={busy} onClick={trySave}>
          {t("listingSave" as never)}
        </Button>
      </div>
    </div>
  );
}

function Section({
  testId,
  title,
  onAdd,
  addLabel,
  children,
}: {
  testId: string;
  title: string;
  onAdd?: () => void;
  addLabel: string;
  children: React.ReactNode;
}) {
  return (
    <details open className="rounded-md border border-border p-3" data-testid={`listing-section-${testId}`}>
      <summary className="cursor-pointer select-none text-sm font-medium">{title}</summary>
      <div className="mt-3 space-y-3">
        {children}
        {onAdd ? (
          <Button size="sm" variant="outline" data-testid={`listing-add-${testId}`} onClick={onAdd}>
            <Plus className="h-4 w-4" aria-hidden="true" />
            {addLabel}
          </Button>
        ) : null}
      </div>
    </details>
  );
}

function RowShell({
  index,
  count,
  onMove,
  onRemove,
  moveLabel,
  removeLabel,
  children,
}: {
  index: number;
  count: number;
  onMove: (direction: -1 | 1) => void;
  onRemove: () => void;
  moveLabel: string;
  removeLabel: string;
  children: React.ReactNode;
}) {
  return (
    <div className="space-y-2 rounded-md border border-border/60 p-2">
      <div className="flex items-center justify-between gap-1">
        <span className="text-xs tabular-nums text-muted-foreground">#{index + 1}</span>
        <div className="flex items-center gap-1">
          <Button size="icon" variant="ghost" aria-label={`${moveLabel} ↑`} disabled={index === 0} onClick={() => onMove(-1)}>
            <ArrowUp className="h-3.5 w-3.5" aria-hidden="true" />
          </Button>
          <Button
            size="icon"
            variant="ghost"
            aria-label={`${moveLabel} ↓`}
            disabled={index === count - 1}
            onClick={() => onMove(1)}
          >
            <ArrowDown className="h-3.5 w-3.5" aria-hidden="true" />
          </Button>
          <Button size="icon" variant="ghost" aria-label={removeLabel} onClick={onRemove}>
            <Trash2 className="h-3.5 w-3.5" aria-hidden="true" />
          </Button>
        </div>
      </div>
      {children}
    </div>
  );
}

function TextRowsSection({
  testId,
  section,
  title,
  rows,
  cap,
  maxLength,
  placeholder,
  onChange,
  t,
  issue,
  issueText,
}: {
  testId: string;
  section: "executionSteps" | "terms";
  title: string;
  rows: { id: string; orderIndex: number; text: string }[];
  cap: number;
  maxLength: number;
  placeholder: string;
  onChange: (rows: { id: string; orderIndex: number; text: string }[]) => void;
  t: ReturnType<typeof useTranslator>;
  issue: (section: ListingIssue["section"], rowIndex: number | null) => ListingIssue | undefined;
  issueText: (issue: ListingIssue | undefined) => React.ReactNode;
}) {
  return (
    <Section
      testId={testId}
      title={title}
      onAdd={
        rows.length < cap
          ? () => onChange([...rows, { id: newRowId(), orderIndex: rows.length, text: "" }])
          : undefined
      }
      addLabel={t("listingAddRow" as never)}
    >
      {rows.map((row, i) => (
        <RowShell
          key={row.id}
          index={i}
          count={rows.length}
          onMove={(dir) => onChange(moveRow(rows, i, i + dir))}
          onRemove={() => onChange(rows.filter((r) => r.id !== row.id).map((r, idx) => ({ ...r, orderIndex: idx })))}
          moveLabel={t("listingMove" as never)}
          removeLabel={t("listingRemoveRow" as never)}
        >
          <input
            data-testid={`listing-${testId}-text-${i}`}
            className={INPUT_CLASS}
            maxLength={maxLength}
            placeholder={placeholder}
            value={row.text}
            onChange={(e) => onChange(rows.map((r) => (r.id === row.id ? { ...r, text: e.target.value } : r)))}
          />
          {issueText(issue(section, i))}
        </RowShell>
      ))}
      {issueText(issue(section, null))}
    </Section>
  );
}

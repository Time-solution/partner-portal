/**
 * Gate 1 — aggregator statement reconciliation (mock store, compute-only).
 * Mirrors the backend domain exactly: content-key import idempotency, matcher variance
 * classification (single tolerance source), note-mandatory resolution, TWO-PERSON close
 * (resolver ≠ closer — the reconciler ≠ releaser discipline), immutability after
 * Reconciled/Closed. No posting, no journal, no money movement — Zahy is PRINCIPAL, the
 * aggregator fee is our BUY side, gross is ReflectionOnly merchant sales.
 * localStorage sibling store (bankRegistryStore pattern) — platform-level, never partner/merchant scoped.
 */

export type AggregatorStatementStatus =
  | "Imported"
  | "Matching"
  | "Reconciled"
  | "HasExceptions"
  | "Closed";

export type AggregatorVarianceType =
  | "MissingInLedger"
  | "MissingInStatement"
  | "AmountMismatch"
  | "FeeVsBuySnapshotMismatch"
  | "DuplicateLine"
  | "NetTransferMismatch";

/** THE single FE tolerance source — mirrors SettlementAggregatorStatementConsts.AmountTolerance. */
export const AGGREGATOR_AMOUNT_TOLERANCE = 0.01;

export interface AggregatorStatementLine {
  id: string;
  externalOrderRef: string;
  orderDate: string; // ISO day
  gross: number;
  aggregatorFee: number;
  net: number;
  matched?: boolean;
}

export interface AggregatorStatementException {
  id: string;
  statementLineId?: string;
  type: AggregatorVarianceType;
  externalOrderRef: string;
  expectedAmount?: number;
  actualAmount?: number;
  details: string;
  resolved: boolean;
  resolutionNote?: string;
  resolvedBy?: string;
  resolvedAt?: string;
}

export interface AggregatorStatement {
  id: string;
  partnerId: string;
  partnerName: string;
  source: string;
  periodFrom: string;
  periodTo: string;
  importKey: string;
  declaredGross: number;
  declaredFees: number;
  declaredNet: number;
  currency: string;
  status: AggregatorStatementStatus;
  lines: AggregatorStatementLine[];
  exceptions: AggregatorStatementException[];
  importedAt: string;
  closedBy?: string;
  closedAt?: string;
}

/** A reflected order visible to matching (existing external-id mapping — a view, not a new table). */
export interface ReflectedOrderView {
  externalOrderRef: string;
  orderDate: string;
  gross: number;
  buyFee: number;
}

const round2 = (value: number): number => Math.round(value * 100) / 100;

/** Integer-cents comparison — mirrors the backend's exact decimal semantics (binary floats
 * would make |100.01 − 100.00| exceed 0.01). Tolerance stays sourced from the single const. */
const cents = (value: number): number => Math.round(value * 100);

const within = (a: number, b: number): boolean =>
  Math.abs(cents(a) - cents(b)) <= cents(AGGREGATOR_AMOUNT_TOLERANCE);

/** Per-order net via the SAME rule as the backend seam (CodNetTransferred): collected − fee. */
export const codNetTransferred = (gross: number, fee: number): number => round2(gross - fee);

const normalizeRef = (reference: string): string => reference.trim().toLowerCase();

const sameActor = (a?: string, b?: string): boolean =>
  !!a && !!b && a.trim().toLowerCase() === b.trim().toLowerCase();

/** Deterministic content key (mock mirror of the SHA-256 content hash): canonical + order-insensitive. */
export function statementContentKey(input: {
  partnerId: string;
  source: string;
  periodFrom: string;
  periodTo: string;
  declaredGross: number;
  declaredFees: number;
  declaredNet: number;
  currency: string;
  lines: Array<Pick<AggregatorStatementLine, "externalOrderRef" | "orderDate" | "gross" | "aggregatorFee" | "net">>;
}): string {
  const lines = input.lines
    .map((l) =>
      [normalizeRef(l.externalOrderRef), l.orderDate, round2(l.gross).toFixed(2), round2(l.aggregatorFee).toFixed(2), round2(l.net).toFixed(2)].join("|"),
    )
    .sort();
  const payload = [
    input.partnerId,
    input.source.trim().toLowerCase(),
    input.periodFrom,
    input.periodTo,
    round2(input.declaredGross).toFixed(2),
    round2(input.declaredFees).toFixed(2),
    round2(input.declaredNet).toFixed(2),
    input.currency.trim().toUpperCase(),
    ...lines,
  ].join("\n");

  // djb2 over the canonical payload — stable, dependency-free (mock only; BE uses SHA-256).
  let hash = 5381;
  for (let i = 0; i < payload.length; i++) {
    hash = (hash * 33) ^ payload.charCodeAt(i);
  }
  return `agg-${(hash >>> 0).toString(16)}-${payload.length}`;
}

export interface MatchComputation {
  lines: AggregatorStatementLine[];
  exceptions: AggregatorStatementException[];
  computedNet: number;
}

/** Pure matcher — mirrors AggregatorStatementMatcher (classification + strict net tie-out). */
export function matchStatement(
  statement: Pick<AggregatorStatement, "declaredNet" | "lines">,
  reflected: ReflectedOrderView[],
): MatchComputation {
  const exceptions: AggregatorStatementException[] = [];
  const byRef = new Map<string, ReflectedOrderView>();
  for (const view of reflected) {
    if (!byRef.has(normalizeRef(view.externalOrderRef))) byRef.set(normalizeRef(view.externalOrderRef), view);
  }

  let exceptionSeq = 0;
  const nextId = (): string => `exc-${++exceptionSeq}`;

  const seen = new Set<string>();
  const uniqueLines: AggregatorStatementLine[] = [];
  const lines = statement.lines.map((line) => ({ ...line, matched: false }));

  for (const line of lines) {
    const key = normalizeRef(line.externalOrderRef);
    if (seen.has(key)) {
      exceptions.push({
        id: nextId(),
        statementLineId: line.id,
        type: "DuplicateLine",
        externalOrderRef: line.externalOrderRef,
        actualAmount: line.gross,
        details: "duplicateLineDetails",
        resolved: false,
      });
      continue;
    }
    seen.add(key);
    uniqueLines.push(line);
  }

  for (const line of uniqueLines) {
    const view = byRef.get(normalizeRef(line.externalOrderRef));
    if (!view) {
      exceptions.push({
        id: nextId(),
        statementLineId: line.id,
        type: "MissingInLedger",
        externalOrderRef: line.externalOrderRef,
        actualAmount: line.gross,
        details: "missingInLedgerDetails",
        resolved: false,
      });
      continue;
    }

    const before = exceptions.length;
    if (!within(line.gross, view.gross)) {
      exceptions.push({
        id: nextId(),
        statementLineId: line.id,
        type: "AmountMismatch",
        externalOrderRef: line.externalOrderRef,
        expectedAmount: view.gross,
        actualAmount: line.gross,
        details: "amountMismatchDetails",
        resolved: false,
      });
    }
    if (!within(line.aggregatorFee, view.buyFee)) {
      exceptions.push({
        id: nextId(),
        statementLineId: line.id,
        type: "FeeVsBuySnapshotMismatch",
        externalOrderRef: line.externalOrderRef,
        expectedAmount: view.buyFee,
        actualAmount: line.aggregatorFee,
        details: "feeMismatchDetails",
        resolved: false,
      });
    }
    if (exceptions.length === before) line.matched = true;
  }

  const statementRefs = new Set(lines.map((l) => normalizeRef(l.externalOrderRef)));
  for (const view of reflected) {
    if (!statementRefs.has(normalizeRef(view.externalOrderRef))) {
      exceptions.push({
        id: nextId(),
        type: "MissingInStatement",
        externalOrderRef: view.externalOrderRef,
        expectedAmount: view.gross,
        details: "missingInStatementDetails",
        resolved: false,
      });
    }
  }

  // Statement-level tie-out (strict, in integer cents — an invariant, not a fuzzy match).
  const computedNet = round2(
    uniqueLines.reduce((sum, l) => sum + cents(codNetTransferred(l.gross, l.aggregatorFee)), 0) / 100,
  );
  if (cents(computedNet) !== cents(statement.declaredNet)) {
    exceptions.push({
      id: nextId(),
      type: "NetTransferMismatch",
      externalOrderRef: "",
      expectedAmount: computedNet,
      actualAmount: statement.declaredNet,
      details: "netTransferMismatchDetails",
      resolved: false,
    });
  }

  return { lines, exceptions, computedNet };
}

// ---------------------------------------------------------------------------------------------
// Guarded mutations (domain rules mirrored 1:1 — the UI never bypasses them)
// ---------------------------------------------------------------------------------------------

export type ReconcileGuardError =
  | "noteRequired"
  | "openExceptions"
  | "sameActorAsResolver"
  | "immutable";

export function resolveException(
  statement: AggregatorStatement,
  exceptionId: string,
  note: string,
  actor: string,
  at: string,
): ReconcileGuardError | null {
  if (statement.status === "Closed" || statement.status === "Reconciled") return "immutable";
  if (!note.trim()) return "noteRequired";

  const exception = statement.exceptions.find((e) => e.id === exceptionId);
  if (!exception || exception.resolved) return null; // idempotent no-op

  exception.resolved = true;
  exception.resolutionNote = note.trim();
  exception.resolvedBy = actor;
  exception.resolvedAt = at;
  return null;
}

export function closeStatement(
  statement: AggregatorStatement,
  actor: string,
  at: string,
): ReconcileGuardError | null {
  if (statement.status === "Closed") return null; // idempotent no-op
  if (statement.status !== "Reconciled" && statement.status !== "HasExceptions") return "immutable";

  if (statement.exceptions.some((e) => !e.resolved)) return "openExceptions";
  if (statement.exceptions.some((e) => e.resolved && sameActor(e.resolvedBy, actor))) {
    return "sameActorAsResolver";
  }

  statement.status = "Closed";
  statement.closedBy = actor;
  statement.closedAt = at;
  return null;
}

// ---------------------------------------------------------------------------------------------
// Mock store (localStorage sibling — bankRegistryStore pattern) + the demo seed
// ---------------------------------------------------------------------------------------------

const STORAGE_KEY = "zahy-aggregator-statements-v1";

const DEMO_PARTNER_ID = "22222222-2222-2222-2222-222222222002";

/** Reflected orders backing the demo period (the existing external-id mapping, as a view). */
export function demoReflectedOrders(): ReflectedOrderView[] {
  return [
    { externalOrderRef: "JHZ-88101", orderDate: "2026-06-05", gross: 113.0, buyFee: 10.0 },
    { externalOrderRef: "JHZ-88102", orderDate: "2026-06-08", gross: 226.0, buyFee: 20.0 },
    { externalOrderRef: "JHZ-88103", orderDate: "2026-06-12", gross: 56.5, buyFee: 5.0 },
    { externalOrderRef: "JHZ-88104", orderDate: "2026-06-17", gross: 79.1, buyFee: 7.0 },
    { externalOrderRef: "JHZ-88105", orderDate: "2026-06-21", gross: 90.4, buyFee: 8.0 },
    { externalOrderRef: "JHZ-88107", orderDate: "2026-06-24", gross: 98.0, buyFee: 9.0 }, // statement says 100.00
    { externalOrderRef: "JHZ-88108", orderDate: "2026-06-26", gross: 67.8, buyFee: 5.0 }, // statement fee 6.00
    // JHZ-88106 deliberately absent → MissingInLedger
  ];
}

/** One Jahez-shaped Pattern-A demo statement: ≥5 clean lines + the four deliberate variances. */
function seedDemoStatement(): AggregatorStatement {
  const mk = (
    id: string,
    ref: string,
    orderDate: string,
    gross: number,
    fee: number,
  ): AggregatorStatementLine => ({
    id,
    externalOrderRef: ref,
    orderDate,
    gross,
    aggregatorFee: fee,
    net: codNetTransferred(gross, fee),
  });

  const lines: AggregatorStatementLine[] = [
    mk("l-1", "JHZ-88101", "2026-06-05", 113.0, 10.0),
    mk("l-2", "JHZ-88102", "2026-06-08", 226.0, 20.0),
    mk("l-3", "JHZ-88103", "2026-06-12", 56.5, 5.0),
    mk("l-4", "JHZ-88104", "2026-06-17", 79.1, 7.0),
    mk("l-5", "JHZ-88105", "2026-06-21", 90.4, 8.0),
    mk("l-6", "JHZ-88106", "2026-06-23", 45.2, 4.0), // MissingInLedger
    mk("l-7", "JHZ-88107", "2026-06-24", 100.0, 9.0), // AmountMismatch (ledger 98.00 — off by 2.00)
    mk("l-8", "JHZ-88108", "2026-06-26", 67.8, 6.0), // FeeVsBuySnapshotMismatch (buy 5.00 — off by 1.00)
    mk("l-9", "JHZ-88101", "2026-06-05", 113.0, 10.0), // DuplicateLine
  ];

  const base = {
    partnerId: DEMO_PARTNER_ID,
    source: "Jahez",
    periodFrom: "2026-06-01",
    periodTo: "2026-06-30",
    declaredGross: 778.0,
    declaredFees: 69.0,
    declaredNet: 709.0, // ties Σ per-order (gross − fee) over unique lines — no NetTransferMismatch
    currency: "SAR",
    lines,
  };

  const computed = matchStatement({ declaredNet: base.declaredNet, lines }, demoReflectedOrders());

  return {
    ...base,
    id: "agg-stmt-jahez-2026-06",
    partnerName: "جاهز — Jahez",
    importKey: statementContentKey(base),
    status: computed.exceptions.length > 0 ? "HasExceptions" : "Reconciled",
    lines: computed.lines,
    exceptions: computed.exceptions,
    importedAt: "2026-07-01T09:00:00Z",
  };
}

function load(): AggregatorStatement[] {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return [seedDemoStatement()];
    const parsed = JSON.parse(raw) as AggregatorStatement[];
    return Array.isArray(parsed) && parsed.length > 0 ? parsed : [seedDemoStatement()];
  } catch {
    return [seedDemoStatement()];
  }
}

function save(list: AggregatorStatement[]): void {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(list));
  } catch {
    // mock store — persistence is best-effort
  }
}

export function listAggregatorStatements(): AggregatorStatement[] {
  return load();
}

export function getAggregatorStatement(id: string): AggregatorStatement | undefined {
  return load().find((s) => s.id === id);
}

/** Content-key idempotent import: identical content returns the existing statement, adds nothing. */
export function importAggregatorStatement(statement: AggregatorStatement): { id: string; isNew: boolean } {
  const list = load();
  const existing = list.find((s) => s.importKey === statement.importKey);
  if (existing) return { id: existing.id, isNew: false };
  list.push(statement);
  save(list);
  return { id: statement.id, isNew: true };
}

export function resolveAggregatorException(
  statementId: string,
  exceptionId: string,
  note: string,
  actor: string,
): ReconcileGuardError | null {
  const list = load();
  const statement = list.find((s) => s.id === statementId);
  if (!statement) return null;
  const error = resolveException(statement, exceptionId, note, actor, new Date().toISOString());
  if (!error) save(list);
  return error;
}

export function closeAggregatorStatement(statementId: string, actor: string): ReconcileGuardError | null {
  const list = load();
  const statement = list.find((s) => s.id === statementId);
  if (!statement) return null;
  const error = closeStatement(statement, actor, new Date().toISOString());
  if (!error) save(list);
  return error;
}

export function resetAggregatorStatements(): void {
  try {
    localStorage.removeItem(STORAGE_KEY);
  } catch {
    // mock store
  }
}

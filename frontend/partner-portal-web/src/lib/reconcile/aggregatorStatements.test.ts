import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  AGGREGATOR_AMOUNT_TOLERANCE,
  closeAggregatorStatement,
  codNetTransferred,
  demoReflectedOrders,
  getAggregatorStatement,
  importAggregatorStatement,
  listAggregatorStatements,
  matchStatement,
  resetAggregatorStatements,
  resolveAggregatorException,
  statementContentKey,
  type AggregatorStatement,
  type AggregatorStatementLine,
} from "./aggregatorStatements";

function stubLocalStorage() {
  const bag = new Map<string, string>();
  vi.stubGlobal("localStorage", {
    getItem: (k: string) => bag.get(k) ?? null,
    setItem: (k: string, v: string) => bag.set(k, v),
    removeItem: (k: string) => bag.delete(k),
    clear: () => bag.clear(),
    key: () => null,
    length: 0,
  });
}

const line = (id: string, ref: string, gross: number, fee: number): AggregatorStatementLine => ({
  id,
  externalOrderRef: ref,
  orderDate: "2026-06-10",
  gross,
  aggregatorFee: fee,
  net: codNetTransferred(gross, fee),
});

describe("aggregator statement matcher (mirrors the backend domain)", () => {
  it("demo seed classifies exactly the four deliberate variances and matches the clean five", () => {
    const seeded = listAggregatorStatementsFresh()[0];
    const types = seeded.exceptions.map((e) => e.type);

    expect(seeded.status).toBe("HasExceptions");
    expect(types).toHaveLength(4);
    expect(types).toContain("MissingInLedger");
    expect(types).toContain("AmountMismatch");
    expect(types).toContain("FeeVsBuySnapshotMismatch");
    expect(types).toContain("DuplicateLine");
    expect(types).not.toContain("NetTransferMismatch"); // declared net ties the invariant
    expect(seeded.lines.filter((l) => l.matched)).toHaveLength(5);
  });

  it("tolerance boundary: 0.01 matches, 0.02 is an AmountMismatch (single tolerance source)", () => {
    expect(AGGREGATOR_AMOUNT_TOLERANCE).toBe(0.01);

    const ok = matchStatement(
      { declaredNet: 91.01, lines: [line("l1", "R-1", 100.01, 9)] },
      [{ externalOrderRef: "R-1", orderDate: "2026-06-10", gross: 100.0, buyFee: 9 }],
    );
    expect(ok.exceptions).toHaveLength(0);

    const bad = matchStatement(
      { declaredNet: 91.02, lines: [line("l1", "R-1", 100.02, 9)] },
      [{ externalOrderRef: "R-1", orderDate: "2026-06-10", gross: 100.0, buyFee: 9 }],
    );
    expect(bad.exceptions.map((e) => e.type)).toEqual(["AmountMismatch"]);
  });

  it("fee vs snapshot BUY leg fires FeeVsBuySnapshotMismatch (our buy side — never commission)", () => {
    const outcome = matchStatement(
      { declaredNet: 61.8, lines: [line("l1", "R-2", 67.8, 6)] },
      [{ externalOrderRef: "R-2", orderDate: "2026-06-10", gross: 67.8, buyFee: 5 }],
    );
    expect(outcome.exceptions.map((e) => e.type)).toEqual(["FeeVsBuySnapshotMismatch"]);
    expect(outcome.exceptions[0].expectedAmount).toBe(5);
    expect(outcome.exceptions[0].actualAmount).toBe(6);
  });

  it("statement line with no reflected order is MissingInLedger", () => {
    const outcome = matchStatement(
      { declaredNet: 41.2, lines: [line("l1", "R-404", 45.2, 4)] },
      [], // nothing reflected for this ref
    );
    expect(outcome.exceptions.map((e) => e.type)).toEqual(["MissingInLedger"]);
    expect(outcome.exceptions[0].statementLineId).toBe("l1");
    expect(outcome.exceptions[0].actualAmount).toBe(45.2);
  });

  it("the same externalOrderRef twice is DuplicateLine; the first occurrence still matches", () => {
    const reflected = [{ externalOrderRef: "R-1", orderDate: "2026-06-10", gross: 100.0, buyFee: 9 }];
    const outcome = matchStatement(
      { declaredNet: 91.0, lines: [line("l1", "R-1", 100.0, 9), line("l2", "r-1 ", 100.0, 9)] },
      reflected,
    );
    expect(outcome.exceptions.map((e) => e.type)).toEqual(["DuplicateLine"]);
    expect(outcome.exceptions[0].statementLineId).toBe("l2"); // the repeat, not the original
    expect(outcome.lines.find((l) => l.id === "l1")!.matched).toBe(true);
    expect(outcome.computedNet).toBe(91.0); // duplicate excluded from the tie-out
  });

  it("reflected order absent from the statement is MissingInStatement", () => {
    const outcome = matchStatement({ declaredNet: 0, lines: [] as AggregatorStatementLine[] }, [
      { externalOrderRef: "R-9", orderDate: "2026-06-10", gross: 88, buyFee: 8 },
    ]);
    expect(outcome.exceptions.map((e) => e.type)).toContain("MissingInStatement");
  });

  it("declared net breaking Σ(gross − fee) is a statement-level NetTransferMismatch", () => {
    const outcome = matchStatement(
      { declaredNet: 96.0, lines: [line("l1", "R-1", 100.0, 9)] }, // computed = 91.00
      [{ externalOrderRef: "R-1", orderDate: "2026-06-10", gross: 100.0, buyFee: 9 }],
    );
    const net = outcome.exceptions.find((e) => e.type === "NetTransferMismatch");
    expect(net).toBeTruthy();
    expect(net!.statementLineId).toBeUndefined();
    expect(net!.expectedAmount).toBe(91.0);
    expect(outcome.computedNet).toBe(91.0);
  });
});

function listAggregatorStatementsFresh(): AggregatorStatement[] {
  resetAggregatorStatements();
  return listAggregatorStatements();
}

describe("aggregator statement store (import idempotency + resolution + two-person close)", () => {
  beforeEach(() => {
    stubLocalStorage();
    resetAggregatorStatements();
  });

  it("re-importing identical content is a no-op with zero new rows", () => {
    const seeded = listAggregatorStatements()[0];
    const clone: AggregatorStatement = JSON.parse(JSON.stringify(seeded));
    clone.id = "different-id"; // same CONTENT, different candidate id
    clone.importKey = statementContentKey(clone);

    const result = importAggregatorStatement(clone);

    expect(result.isNew).toBe(false);
    expect(result.id).toBe(seeded.id);
    expect(listAggregatorStatements()).toHaveLength(1);
  });

  it("resolution requires a note; two-person blocks the resolver from closing; another user closes", () => {
    const statement = listAggregatorStatements()[0];

    // Note mandatory.
    expect(resolveAggregatorException(statement.id, statement.exceptions[0].id, "   ", "amina")).toBe(
      "noteRequired",
    );

    // Close blocked while any exception is open.
    expect(closeAggregatorStatement(statement.id, "badr")).toBe("openExceptions");

    for (const exception of statement.exceptions) {
      expect(resolveAggregatorException(statement.id, exception.id, "checked with Jahez ops", "amina")).toBeNull();
    }

    // The resolver cannot close (mirror reconciler ≠ releaser)…
    expect(closeAggregatorStatement(statement.id, "amina")).toBe("sameActorAsResolver");

    // …a different human commits the close.
    expect(closeAggregatorStatement(statement.id, "badr")).toBeNull();
    expect(getAggregatorStatement(statement.id)!.status).toBe("Closed");
  });

  it("closed statements are immutable (append-only corrections)", () => {
    const statement = listAggregatorStatements()[0];
    for (const exception of statement.exceptions) {
      resolveAggregatorException(statement.id, exception.id, "ok", "amina");
    }
    closeAggregatorStatement(statement.id, "badr");

    expect(
      resolveAggregatorException(statement.id, statement.exceptions[0].id, "late note", "badr"),
    ).toBe("immutable");
    // Re-close is an idempotent no-op.
    expect(closeAggregatorStatement(statement.id, "third")).toBeNull();
    expect(getAggregatorStatement(statement.id)!.closedBy).toBe("badr");
  });

  it("happy path: a clean statement reaches Reconciled and closes without resolvers", () => {
    const clean = demoReflectedOrders().slice(0, 5);
    const lines = clean.map((v, i) => line(`c-${i}`, v.externalOrderRef, v.gross, v.buyFee));
    const declaredNet = lines.reduce((sum, l) => sum + l.net, 0);

    const computed = matchStatement({ declaredNet, lines }, clean);
    expect(computed.exceptions).toHaveLength(0);

    const statement: AggregatorStatement = {
      id: "agg-clean",
      partnerId: "p-demo",
      partnerName: "Jahez",
      source: "Jahez",
      periodFrom: "2026-06-01",
      periodTo: "2026-06-30",
      importKey: "key-clean",
      declaredGross: lines.reduce((s, l) => s + l.gross, 0),
      declaredFees: lines.reduce((s, l) => s + l.aggregatorFee, 0),
      declaredNet,
      currency: "SAR",
      status: "Reconciled",
      lines: computed.lines,
      exceptions: [],
      importedAt: "2026-07-01T09:00:00Z",
    };
    importAggregatorStatement(statement);

    expect(closeAggregatorStatement("agg-clean", "anyone")).toBeNull();
    expect(getAggregatorStatement("agg-clean")!.status).toBe("Closed");
  });
});

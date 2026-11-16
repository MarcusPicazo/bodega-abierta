# ADR 0002 — Append-only inventory ledger

- **Status:** Proposed
- **Date:** 2026-11-16

## Context

`InventoryLot` currently stores a mutable `QuantityOnHand` (decimal) that `Allocate` decrements in
place, protected by optimistic concurrency (`RowVersion`). That works while every client is online.

The warehouse PWA must work offline: two operators on two tablets, with no signal, can record intakes,
waste and deliveries against the same lot, and sync later. With a mutable quantity both edits start
from the same version, so one of them is rejected (409/412) or lost, and the operator who was working
offline has no good way to resolve it.

Earlier design work for Bodega Abierta already chose a ledger for this reason; the .NET backend
(ADR 0001) did not carry that decision over.

## Decision

Inventory becomes an **append-only ledger** of `StockMovement` rows:

- One row per event: `Intake`, `Allocation`, `Release`, `Adjustment`, `Waste`, `Transfer`.
- Quantities are **integer grams** (or integer units for products counted by unit), never decimals, so
  sums are exact.
- Rows are never updated or deleted. A mistake is corrected with a compensating movement
  (e.g. cancelling a distribution appends `Release` rows).
- Each movement carries a **client-generated id** (`ClientMovementId`, unique per food bank), so the PWA
  can resend a batch after a dropped connection without creating duplicates.
- A lot's balance is the sum of its movements. `QuantityOnHand` may stay as a cached column updated in
  the same transaction, but the ledger is the source of truth.
- A balance that goes negative after a sync is **not rejected**: the movement is stored and a
  reconciliation alert is raised for the coordinator, because the goods physically left the warehouse.

## Consequences

- Offline writes from different devices never conflict: appends commute.
- The impact report (kg rescued, waste %, CO₂e) becomes a single aggregate query over movements.
- The audit trail for stock is the ledger itself.
- `Allocate`, `Release`, `MoveTo` and `Distribution.Cancel` must be rewritten in terms of movements,
  and a migration must convert existing lots into opening `Intake` movements.
- Negative balances become possible and need a clear alert and resolution flow in the PWA.

## Open question

Whether to move from SQL Server to PostgreSQL (EF Core + Npgsql) in the same change, to lower hosting
costs for a nonprofit. To be decided in a separate ADR.

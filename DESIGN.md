# Design — Audit Logging

## Problem

Every admin write (CREATE / UPDATE / DELETE) must produce an audit event and deliver it to VictoriaLogs. Non-admin actors and read operations produce nothing.

---

## Architecture

```
┌─────────────────────────────────────────────────┐
│  Logicc.Api                                     │
│                                                 │
│  Request → [AdminOnly] → ProductService         │
│                               │                 │
│                               ▼                 │
│                          AdminLogService        │
│                          (gate + fire-forget)   │
│                               │                 │
│              ┌────────────────┴──────────────┐  │
│              │ success                       │  │
│              ▼                        failure▼  │
│         RabbitMQ              audit-failures/   │
│                               {date-hour}/      │
│                               {id}.json         │
│                                    │            │
│                          FileBufferWorker       │
│                          (polls every 30 s,     │
│                           re-publishes to MQ)   │
└─────────────────────────────────────────────────┘
                │
                │ AMQP
                ▼
┌─────────────────────────────────────────────────┐
│  Logicc.VictoriaLogSync                         │
│                                                 │
│  AuditLogConsumer                               │
│  (MassTransit retry: 3×, 5 s backoff)           │
│       │                                         │
│       ▼                                         │
│  VictoriaLogsClient → VictoriaLogs HTTP         │
│  (single attempt)                               │
└─────────────────────────────────────────────────┘
```

---

## Data Flow

1. Admin sends a write request (`POST / PUT / DELETE /api/products`).
2. `[AdminOnly]` filter checks the actor — returns 403 if not admin.
3. `ProductService` saves the entity, then calls `AdminLogService.Log*Async`.
4. `AdminLogService` verifies `AdminActorContext`, builds `AuditLogMessage`, and fires a background publish task — returning to the caller immediately.
5. **Background**: the task calls `IBus.Publish` (single attempt).
   - Success → message lands in RabbitMQ queue.
   - Failure → `FileBufferLogger` writes the event as JSON to `audit-failures/<date-hour>/{id}.json`.
6. `FileBufferWorker` (background service in the API process) polls every 30 seconds, picks up any `.json` files, re-publishes them to RabbitMQ, and deletes them on success.
7. `AuditLogConsumer` in `Logicc.VictoriaLogSync` receives the message and calls `VictoriaLogsClient` (single HTTP attempt).
8. On consumer failure: MassTransit retries `Consume()` up to 3 times with 5-second backoff, then moves the message to the error queue.

---

## Design Decisions

| Decision | Why |
|---|---|
| **Fire-and-forget publish** | The HTTP request must not be blocked by broker latency or availability. Audit delivery is best-effort and async. |
| **File buffer on publish failure** | Simple, durable, infrastructure-free fallback. Files survive restarts and are human-readable. |
| **No publish retry** | Fail-fast signals systemic issues immediately. The file buffer handles durability; stacking retries in the API would delay responses and mask failures. |
| **`IBus` (singleton) instead of `IPublishEndpoint` (scoped)** | The fire-and-forget task and the singleton `FileBufferWorker` both outlive the request scope. `IBus` is safe to use from any lifetime. |
| **FileBufferWorker inside the API process** | Co-locates file producer and file consumer — no shared volume or IPC required. |
| **Single HTTP attempt in consumer** | Retry responsibility belongs to MassTransit, which has full visibility into the consume lifecycle. Stacking Polly inside MassTransit creates nested retry loops. |
| **Consumer retry: 3× / 5 s** | Handles transient VictoriaLogs downtime. After exhaustion, MassTransit moves the message to the error queue for manual inspection. |
| **Type-safe actor gate** | `is not AdminActorContext` is a compile-time check. New actor types are automatically excluded without changing the gate logic. |
| **`AuditLogMessage.Id` as dedup key** | Crash recovery may re-publish a message. VictoriaLogs deduplicates on this ID, so duplicates are harmless. |

---

## Failure Scenarios

| Scenario | Outcome |
|---|---|
| RabbitMQ down at publish time | Event written to `audit-failures/`. `FileBufferWorker` re-publishes when broker recovers. |
| API crashes mid-background-publish | File was not yet written — event is lost. (Acceptable: fire-and-forget is best-effort.) |
| API crashes after file write | File persists on disk. `FileBufferWorker` picks it up on next startup. |
| `FileBufferWorker` fails to re-publish | File remains; retried on next poll cycle. |
| VictoriaLogs down | Consumer throws; MassTransit retries 3× then dead-letters the message. |
| Duplicate delivery | `AuditLogMessage.Id` allows downstream deduplication. |

---

## Trade-offs

| Area | Choice | Benefit | Cost |
|---|---|---|---|
| Coupling | Fire-and-forget + file buffer | API never blocked by broker | Events not guaranteed on crash |
| Simplicity | No outbox pattern | No DB schema, no CDC | Recovery relies on filesystem integrity |
| Retry ownership | MassTransit-level (consumer) | Single retry boundary, clean DLQ | Up to 15 s delay before dead-letter |
| Multi-instance | Single API instance assumed | No shared-volume complexity | File buffer breaks with horizontal scaling |

---

## Component Responsibilities

| Component | Owns |
|---|---|
| `AdminLogService` | Actor gate, message construction, fire-and-forget publish |
| `FileBufferLogger` | Writing failed events to disk |
| `FileBufferWorker` | Polling disk, re-publishing to RabbitMQ |
| `AuditLogConsumer` | Receiving from RabbitMQ, forwarding to VictoriaLogs |
| `VictoriaLogsClient` | Single HTTP POST to VictoriaLogs |

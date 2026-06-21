# Logicc Assessment — Audit Logging

A .NET 10 solution that captures admin write operations as audit events, publishes them to RabbitMQ, and forwards them to VictoriaLogs for long-term storage.

---

## Architecture

```
HTTP Request
    │
    ▼
Logicc.Api  ──fire-and-forget──►  RabbitMQ
    │                                 │
    │ (on publish failure)            ▼
    ▼                            Logicc.VictoriaLogSync
audit-failures/                       │
    │                                 ▼
    └──── FileBufferWorker ──►   VictoriaLogs
          (re-publishes to RabbitMQ)
```

---

## Services

| Container | Role | Port |
|---|---|---|
| `logicc-api` | REST API — accepts requests, fires audit events | 5093 |
| `logicc-worker` | Consumes from RabbitMQ, forwards to VictoriaLogs | — |
| `logicc-rabbitmq` | Message broker | 5672 / 15672 |
| `logicc-victorialogs` | Log storage | 9428 |

---

## Running

**Start everything:**
```bash
docker-compose up --build
```

**Verify:**
- API: `http://localhost:5093/swagger`
- RabbitMQ management: `http://localhost:15672` (guest / guest)
- VictoriaLogs: `http://localhost:9428/select/vmui`

---

## Local Development (without Docker for the apps)

```bash
docker-compose up rabbitmq victorialogs   # infrastructure only

# Terminal 1
cd Logicc.Api && dotnet run

# Terminal 2
cd Logicc.VictoriaLogSync && dotnet run
```

---

## Tests

```bash
dotnet test   # 29 tests, all passing
```

---

## Key Concepts

**Audit gate** — Only `AdminActorContext` produces audit events. All other actor types are silently skipped. The role is resolved from the `x-role` request header.

**Fire-and-forget** — `Log*Async` calls return immediately. Publishing to RabbitMQ happens in the background so the HTTP request is never blocked by broker latency.

**File buffer** — If a publish fails, the event is written to `audit-failures/<date-hour>/{id}.json`. The `FileBufferWorker` (running inside the API process) polls every 30 seconds and re-publishes any buffered events.

**Consumer** — `AuditLogConsumer` in `Logicc.VictoriaLogSync` receives from RabbitMQ and forwards to VictoriaLogs with a single HTTP attempt. MassTransit retries the consume operation up to 3 times (5 s backoff) on failure.

---

See [DESIGN.md](DESIGN.md) for architecture decisions and trade-offs.

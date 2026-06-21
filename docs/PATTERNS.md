# Patterns Used

## Architectural

| Pattern | Where | Why |
|---|---|---|
| Event-driven | API → RabbitMQ → Worker | Decouples write path from log delivery |
| Fire-and-forget | `AdminLogService` | HTTP request not blocked by broker |
| File buffer + background worker | `FileBufferLogger` + `FileBufferWorker` | Durable fallback without database dependency |
| Worker service | `Logicc.VictoriaLogSync` | Long-running consumer independent of API |


## Reliability

| Pattern | Where | Why |
|---|---|---|
| Single-attempt publish + disk fallback | `AdminLogService` | Fast failure detection; no blocking retries in the request path |
| Consumer retry (3×, 5 s) | MassTransit config | Handles transient VictoriaLogs downtime at the right boundary |
| Single HTTP attempt | `VictoriaLogsClient` | No stacked retry loops; MassTransit owns the retry lifecycle |
| Idempotent delivery | `AuditLogMessage.Id` | Re-published events deduplicated by VictoriaLogs |

## Structural

| Pattern | Where | Why |
|---|---|---|
| Extension method DI registration | `AddAuditLogging()` | Reusable, self-contained setup for any host |
| Immutable records | `AuditLogMessage`, actor contexts | Thread-safe; value semantics |
| Singleton vs scoped lifetime | `IBus` (singleton) used for fire-and-forget | Scoped `IPublishEndpoint` would be disposed before the background task completes |

## Testing

| Pattern | Where |
|---|---|
| Arrange-Act-Assert | All unit tests |
| Mock / spy (`Moq`) | `AdminLogServiceTests` |
| Recording test double | `RecordingAdminLogService` (integration tests) |
| `WebApplicationFactory` | `ProductsControllerTests` — full-stack without real infrastructure |

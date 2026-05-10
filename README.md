# Bjeek Finance API

ASP.NET Core 9 · SQL Server · Clean Architecture · EF Core 9

---

## Project Structure

```
BjeekFinance/
├── src/
│   ├── BjeekFinance.Domain/          # Entities, Enums, Domain Exceptions
│   ├── BjeekFinance.Application/     # Service interfaces, DTOs, Business logic
│   ├── BjeekFinance.Infrastructure/  # EF Core DbContext, Repos, Unit of Work
│   └── BjeekFinance.API/             # Controllers, Middleware, Program.cs
└── BjeekFinance.sln
```

---

## Quick Start

### 1. Prerequisites

- .NET 9 SDK
- SQL Server 2019+ (or SQL Server Express / Azure SQL)

### 2. Configure connection string

Edit `src/BjeekFinance.API/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=BjeekFinance_Dev;..."
  },
  "Jwt": {
    "Secret": "your-min-32-character-secret-key-here"
  }
}
```

Or use dotnet user-secrets (recommended):

```bash
cd src/BjeekFinance.API
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=..."
dotnet user-secrets set "Jwt:Secret" "your-min-32-char-secret"
```

### 3. Apply migrations

```bash
cd src/BjeekFinance.API
dotnet ef database update --project ../BjeekFinance.Infrastructure
```

Or let `Program.cs` auto-migrate in Development:

```bash
dotnet run
```

### 4. Open Swagger UI

Navigate to `https://localhost:{port}` — Swagger UI loads at the root.

---

## Architecture

### Layers

| Layer | Responsibility |
|-------|---------------|
| **Domain** | Entities, enums, domain exceptions. No dependencies. |
| **Application** | Service interfaces, business logic, DTOs. Depends on Domain only. |
| **Infrastructure** | EF Core, repository implementations, Unit of Work. |
| **API** | Controllers, middleware, DI wiring. |

### Key Patterns

**Saga / Atomic writes** — Every multi-wallet operation (`CollectPayment`, `SplitPay`, `InstantPay`) opens a DB transaction. All deltas succeed or all roll back. No partial states.

**Idempotency** — `CollectPayment` accepts an `IdempotencyKey`. Duplicate gateway webhooks are detected via unique index on `Transactions.IdempotencyKey` and silently deduplicated.

**Unit of Work** — All repositories share one `DbContext` per request scope via `IUnitOfWork`. `BeginTransactionAsync` / `CommitAsync` / `RollbackAsync` wrap multi-table writes.

**Immutable Audit Log** — `AuditService.WriteAsync` is called synchronously before the HTTP response is returned. SHA-256 tamper hash computed per entry. WORM — no UPDATE or DELETE in application layer.

**Admin-configurable parameters** — No financial thresholds are hardcoded. All live in `FinanceParameters` table with city/service-type scoping and full version history. Defaults seeded by `InitialCreate` migration.

---

## Domain Highlights

### Wallet Balance Split (Driver / Delivery)

| Sub-balance | Description |
|-------------|-------------|
| `BalanceAvailable` | Cleared earnings eligible for payout / Instant Pay |
| `BalancePending` | Earnings within 15-minute settlement window |
| `BalanceHold` | Admin-locked or dunning-held amount |
| `CashReceivable` | Outstanding cash commission — reduces AVAILABLE immediately |

### User Wallet Credit Consumption Order

1. `BalanceRefundCredit` — no expiry, highest priority
2. `BalancePromoCredit` — 30-day expiry, not applicable on Premium / corporate trips
3. `BalanceCourtesyCredit` — 90-day expiry, monthly cap (default SAR 100)
4. `BalanceAvailable` — real deposited funds

### Instant Pay Tiers

| Tier | Eligibility | Daily Limit | Fee |
|------|-------------|-------------|-----|
| A | < 50 trips | Manual cashout disabled | — |
| B | 50–499 trips | 3 cashouts/day | SAR 1.74 (incl. 15% VAT) |
| C | 500+ trips | 5 cashouts/day | SAR 0.87 (loyalty reward) |

Daily limit resets at local city midnight — not UTC.

### SARIE Window

IBAN transfers routed via SARIE: **Sun–Thu 08:00–16:00 AST (UTC+3)**.
Out-of-window requests are queued and actor is notified with the next window time. Never silently deferred.

### Dunning Buckets

| Age (days) | Bucket | Action |
|------------|--------|--------|
| 1–7 | Notify | SMS / push notification |
| 8–30 | HoldPayout | Payout and Instant Pay suspended |
| 31+ | HoldAssignments | New job assignments also suspended |

Auto-resolves when balance ≥ 0 and `CashReceivable` = 0.

---

## RBAC Policies

| Policy | Roles |
|--------|-------|
| `FinanceAdmin` | FinanceAdmin, SuperAdmin |
| `FinanceManager` | FinanceManager, VpFinance, Cfo, SuperAdmin |
| `VpFinance` | VpFinance, Cfo, SuperAdmin |
| `SuperAdmin` | SuperAdmin |
| `CorporateManager` | CorporateAccountManager, FinanceAdmin, SuperAdmin |
| `DriverOrDelivery` | Driver, Delivery |

---

## Approval Thresholds (all admin-configurable)

| Action | Threshold | Approver |
|--------|-----------|----------|
| Payout auto-approve | ≤ SAR 10,000 | None |
| Payout Finance Admin | SAR 10,001–37,000 | Finance Admin |
| Payout Super Admin | > SAR 37,000 | Super Admin |
| Write-off self-approve | < SAR 18,500 | Finance Manager |
| Write-off VP Finance | ≥ SAR 18,500 | VP Finance |
| Bulk adjustment | > SAR 50,000 total | Super Admin |

---

## Adding a Migration

```bash
dotnet ef migrations add YourMigrationName \
  --project src/BjeekFinance.Infrastructure \
  --startup-project src/BjeekFinance.API

dotnet ef database update \
  --project src/BjeekFinance.Infrastructure \
  --startup-project src/BjeekFinance.API
```

---

## Environment Variables (Production)

```bash
ConnectionStrings__DefaultConnection="Server=...;Database=BjeekFinance;..."
Jwt__Secret="your-production-secret-min-32-chars"
Jwt__Issuer="BjeekFinanceAPI"
Jwt__Audience="BjeekFinanceClients"
```

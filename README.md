# PayFlow

A production-ready **.NET 8 Web API** for payment processing and wallet management, built with **Clean Architecture** principles.

---

## Table of Contents

1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Project Structure](#project-structure)
4. [Technology Stack](#technology-stack)
5. [Prerequisites](#prerequisites)
6. [Running Locally](#running-locally)
7. [Database Migrations](#database-migrations)
8. [Running Tests](#running-tests)
9. [API Endpoints](#api-endpoints)
10. [Configuration](#configuration)
11. [Design Decisions](#design-decisions)

---

## Overview

PayFlow demonstrates production-grade .NET backend patterns common in fintech systems including idempotent webhook processing, atomic balance updates, and HMAC signature verification.

PayFlow is a payment-processing backend that provides:

- **User authentication** — register, login, profile management, and password changes backed by JWT Bearer tokens
- **Transaction management** — initiate, track, and list paginated payment transactions
- **Wallet management** — create per-user wallets with credit/debit balance tracking
- **Webhook processing** — receive asynchronous payment-status notifications from payment providers with full idempotency and HMAC-SHA512 signature verification

---

## Architecture

PayFlow follows **Clean Architecture** (also known as Onion Architecture), enforcing a strict dependency rule: inner layers know nothing about outer layers.

```
┌────────────────────────────────────────────────────────┐
│                      PayFlow.API                       │  ← Presentation
│    Controllers · Middleware · Program.cs               │
├────────────────────────────────────────────────────────┤
│                  PayFlow.Infrastructure                 │  ← Infrastructure
│       EF Core DbContext · Repositories · Postgres      │
├────────────────────────────────────────────────────────┤
│                  PayFlow.Application                   │  ← Application
│    Interfaces · DTOs · Services · Validators           │
├────────────────────────────────────────────────────────┤
│                    PayFlow.Domain                      │  ← Domain (core)
│              Entities · Enums · (no deps)              │
└────────────────────────────────────────────────────────┘
```

**Dependency direction:** API → Infrastructure → Application → Domain

The Domain layer has **zero external dependencies**. The Application layer depends only on Domain. Infrastructure implements the Application interfaces and is only wired together at the composition root (`Program.cs`).

---

## Project Structure

```
PayFlow/
├── PayFlow.sln
├── docker-compose.yml
├── README.md
│
├── PayFlow.Domain/
│   ├── Entities/
│   │   ├── Transaction.cs
│   │   ├── User.cs
│   │   ├── Wallet.cs
│   │   └── WebhookEvent.cs
│   ├── Enums/
│   │   ├── TransactionStatus.cs
│   │   └── TransactionType.cs
│   └── Constants/
│       └── CurrencyConstants.cs
│
├── PayFlow.Application/
│   ├── DTOs/
│   │   ├── AuthResponse.cs
│   │   ├── ChangePasswordRequest.cs
│   │   ├── CreateWalletRequest.cs / CreateWalletResponse.cs
│   │   ├── InitiatePaymentRequest.cs / InitiatePaymentResponse.cs
│   │   ├── LoginRequest.cs / RegisterRequest.cs
│   │   ├── PagedResult.cs
│   │   ├── TransactionStatusResponse.cs
│   │   ├── UpdateProfileRequest.cs / UpdateProfileResponse.cs
│   │   └── WebhookPayload.cs
│   ├── Exceptions/
│   │   └── PayFlowException.cs   ← ConflictException, NotFoundException, etc.
│   ├── Interfaces/
│   │   ├── IAuthService.cs
│   │   ├── ITransactionRepository.cs / ITransactionService.cs
│   │   ├── IUserRepository.cs
│   │   ├── IWalletRepository.cs / IWalletService.cs
│   │   ├── IWebhookEventRepository.cs / IWebhookService.cs
│   ├── Services/
│   │   ├── AuthService.cs
│   │   ├── TransactionService.cs
│   │   ├── WalletService.cs
│   │   └── WebhookService.cs
│   └── Validators/
│       ├── ChangePasswordRequestValidator.cs
│       ├── CreateWalletRequestValidator.cs
│       ├── InitiatePaymentRequestValidator.cs
│       ├── LoginRequestValidator.cs
│       ├── RegisterRequestValidator.cs
│       └── UpdateProfileRequestValidator.cs
│
├── PayFlow.Infrastructure/
│   ├── Configurations/           ← EF Core entity type configurations
│   ├── Data/
│   │   └── PayFlowDbContext.cs
│   ├── Extensions/
│   │   └── ServiceCollectionExtensions.cs
│   ├── Migrations/               ← generated by EF Core
│   └── Repositories/
│       ├── TransactionRepository.cs
│       ├── UserRepository.cs
│       ├── WalletRepository.cs
│       └── WebhookEventRepository.cs
│
├── PayFlow.API/
│   ├── Controllers/
│   │   ├── AuthController.cs
│   │   ├── TransactionController.cs
│   │   ├── UsersController.cs
│   │   ├── WalletController.cs
│   │   └── WebhookController.cs
│   ├── Middleware/
│   │   ├── CorrelationIdMiddleware.cs
│   │   └── GlobalExceptionMiddleware.cs
│   ├── Dockerfile
│   ├── Program.cs
│   ├── appsettings.json
│   └── appsettings.Development.json
│
├── PayFlow.Tests/
│   └── Services/
│       ├── TransactionServiceTests.cs
│       └── WebhookServiceTests.cs
│
└── PayFlow.Tests.Integration/
    ├── Api/
    │   └── PaymentFlowIntegrationTests.cs
    ├── IntegrationTestCollection.cs
    └── PayFlowWebApplicationFactory.cs
```

---

## Technology Stack

| Concern              | Choice                                  |
|----------------------|-----------------------------------------|
| Runtime              | .NET 8                                  |
| Web Framework        | ASP.NET Core 8 Web API                  |
| ORM                  | Entity Framework Core 8                 |
| Database             | PostgreSQL 16 (via Npgsql EF provider)  |
| Authentication       | JWT Bearer (Microsoft.IdentityModel)    |
| Password hashing     | BCrypt.Net                              |
| Validation           | FluentValidation 11                     |
| API Documentation    | Swagger / Swashbuckle                   |
| Unit Testing         | xUnit + Moq + FluentAssertions          |
| Integration Testing  | xUnit + WebApplicationFactory + Testcontainers |
| Containerisation     | Docker / Docker Compose                 |

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for PostgreSQL via Docker Compose)
- [EF Core CLI tools](https://learn.microsoft.com/en-us/ef/core/cli/dotnet) — install once with:
  ```bash
  dotnet tool install --global dotnet-ef
  ```

---

## One-Time Setup

### Trust the .NET HTTPS development certificate

```bash
dotnet dev-certs https --trust
```

Click **Allow** when macOS/Windows prompts. This lets the browser open Swagger without a security warning at `https://localhost:7000`.

---

## Running Locally

### Option A — Docker Compose (recommended)

Spins up both the API and PostgreSQL in containers.

```bash
docker compose up --build
```

API: **http://localhost:5000** · Swagger: **http://localhost:5000/swagger**

### Option B — API locally, PostgreSQL in Docker

**1. Start only the database:**
```bash
docker compose up postgres -d
```

**2. Apply EF Core migrations:**
```bash
dotnet ef database update \
  --project PayFlow.Infrastructure \
  --startup-project PayFlow.API
```

**3. Start the API:**
```bash
dotnet run --project PayFlow.API --launch-profile https
```

Swagger UI: **https://localhost:7000/swagger**

---

## Database Migrations

```bash
# Add a new migration (run from the solution root)
dotnet ef migrations add <MigrationName> \
  --project PayFlow.Infrastructure \
  --startup-project PayFlow.API

# Apply pending migrations
dotnet ef database update \
  --project PayFlow.Infrastructure \
  --startup-project PayFlow.API

# Revert the last migration
dotnet ef database update <PreviousMigrationName> \
  --project PayFlow.Infrastructure \
  --startup-project PayFlow.API
```

> In development mode, the API automatically calls `MigrateAsync()` on startup, so you rarely need to run `database update` manually.

---

## Running Tests

```bash
# Unit tests
dotnet test PayFlow.Tests

# Integration tests (requires Docker — spins up a real Postgres via Testcontainers)
dotnet test PayFlow.Tests.Integration

# All tests
dotnet test
```

| Test project                    | Coverage                                                                         |
|---------------------------------|----------------------------------------------------------------------------------|
| `TransactionServiceTests`       | Initiate credit/debit, duplicate reference, wallet not found, insufficient funds, pagination |
| `WebhookServiceTests`           | Successful event, failed event, duplicate event (idempotency), conflicting terminal status, unknown reference, debit balance deduction |
| `PaymentFlowIntegrationTests`   | End-to-end: register → create wallet → initiate transaction → webhook → balance check |

---

## API Endpoints

All endpoints except `/api/auth/*` and `/api/webhooks/notify` require a JWT Bearer token in the `Authorization` header.

### Auth — `POST /api/auth/register`

Registers a new user and returns a JWT token.

**Request body:**
```json
{
  "email":     "user@example.com",
  "password":  "S3cur3P@ssword",
  "firstName": "Ada",
  "lastName":  "Lovelace"
}
```

**Response `201`:**
```json
{
  "token":     "eyJhbGci...",
  "expiresAt": "2024-05-01T11:00:00Z",
  "email":     "user@example.com",
  "firstName": "Ada",
  "lastName":  "Lovelace"
}
```

---

### Auth — `POST /api/auth/login`

Authenticates a user and returns a JWT token.

**Request body:**
```json
{ "email": "user@example.com", "password": "S3cur3P@ssword" }
```

**Response `200`:** same shape as register.

---

### Users — `PUT /api/users/profile` · `PUT /api/users/change-password`

Update the authenticated user's profile fields or change their password. Both require a valid JWT.

---

### Wallets — `POST /api/wallets`

Creates a wallet for an owner. Each owner may only have one wallet.

**Request body:**
```json
{ "ownerId": "user-123", "currency": "NGN" }
```

**Response `201`:**
```json
{
  "id":        "7cb4e7c8-...",
  "ownerId":   "user-123",
  "balance":   0.00,
  "currency":  "NGN",
  "createdAt": "2024-04-01T08:00:00Z"
}
```

---

### Wallets — `GET /api/wallets/{ownerId}`

Returns the current wallet balance for a given owner.

| Status | Meaning         |
|--------|-----------------|
| 200    | Wallet found    |
| 404    | Wallet not found|

---

### Transactions — `POST /api/transactions`

Initiates a new payment transaction.

**Request body:**
```json
{
  "reference":     "TXN-20240501-001",
  "amount":        500.00,
  "currency":      "NGN",
  "type":          "Credit",
  "walletOwnerId": "user-123"
}
```

**Response `201`:**
```json
{
  "transactionId": "3fa85f64-...",
  "reference":     "TXN-20240501-001",
  "status":        "Pending",
  "createdAt":     "2024-05-01T10:00:00Z"
}
```

| Status | Meaning                          |
|--------|----------------------------------|
| 201    | Transaction created              |
| 400    | Validation error                 |
| 404    | Wallet not found                 |
| 409    | Duplicate reference              |
| 422    | Insufficient funds (debit)       |

---

### Transactions — `GET /api/transactions/{reference}`

Returns the current status of a transaction by its business reference.

---

### Transactions — `GET /api/transactions`

Returns a paginated list of transactions with an optional status filter.

| Parameter  | Type   | Default | Description                              |
|------------|--------|---------|------------------------------------------|
| `page`     | int    | 1       | Page number (1-based)                    |
| `pageSize` | int    | 20      | Records per page (max 100)               |
| `status`   | string | —       | Filter: `Pending`, `Successful`, `Failed`|

**Response `200`:**
```json
{
  "items":          [ ... ],
  "page":           1,
  "pageSize":       10,
  "totalCount":     42,
  "totalPages":     5,
  "hasNextPage":    true,
  "hasPreviousPage":false
}
```

---

### Webhooks — `POST /api/webhooks/notify`

Receives an asynchronous payment-status notification. The request body is verified against an **HMAC-SHA512** signature in the `X-PayFlow-Signature` header (same format used by Paystack and Flutterwave). If `Webhook:SecretKey` is not configured the check is skipped for development convenience.

The endpoint is **idempotent** — duplicate events for the same reference and terminal status are silently ignored.

**Request body:**
```json
{
  "transactionReference": "TXN-20240501-001",
  "status":               "Successful",
  "rawPayload":           "{\"event\":\"payment.success\"}",
  "eventTimestamp":       "2024-05-01T10:01:30Z"
}
```

**Response `200`:** `{ "message": "Webhook processed successfully." }`

| Status | Meaning                              |
|--------|--------------------------------------|
| 200    | Accepted and processed               |
| 400    | Malformed payload                    |
| 401    | Missing or invalid signature header  |
| 404    | Transaction reference not found      |

---

### Health Check — `GET /health`

Returns database connectivity status.

```json
{ "status": "Healthy" }
```

---

## Configuration

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=payflow_db;Username=payflow_user;Password=payflow_pass"
  },
  "Jwt": {
    "SecretKey":     "your-256-bit-secret",
    "Issuer":        "PayFlow",
    "Audience":      "PayFlowUsers",
    "ExpiryMinutes": 60
  },
  "Webhook": {
    "SecretKey": "your-webhook-secret"
  },
  "Cors": {
    "AllowedOrigins": ["https://your-frontend.com"]
  }
}
```

Environment variable override syntax (Docker / Kubernetes):
```
ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;...
Jwt__SecretKey=...
```

---

## Rate Limiting

Three named policies are applied at the controller level:

| Policy    | Applied to       | Limit                             | Algorithm      |
|-----------|------------------|-----------------------------------|----------------|
| `auth`    | `/api/auth`      | 10 requests / minute              | Fixed window   |
| `api`     | transactions, wallets | 60 requests / minute         | Sliding window |
| `webhook` | `/api/webhooks`  | 200 requests / minute             | Fixed window   |

Rejected requests receive `429 Too Many Requests`.

---

## Design Decisions

**Clean Architecture over MVC monolith** — decouples business logic from framework and infrastructure concerns. Swapping EF Core for Dapper, or PostgreSQL for another database, would only touch the Infrastructure layer.

**Repository pattern** — abstracts data access behind interfaces so services are fully unit-testable with Moq, without spinning up a database.

**JWT Bearer authentication** — stateless tokens mean the API scales horizontally without shared session state. BCrypt is used for password hashing; login errors are intentionally vague to avoid leaking whether an email is registered.

**Idempotency on webhook processing** — payment providers may deliver the same event more than once. The handler checks the current transaction state before applying any changes: duplicate terminal events are silently dropped.

**First-wins on conflicting terminal states** — if a transaction is already `Failed` and a late `Successful` event arrives, the event is ignored. Overwriting a terminal state could undo a refund trigger or cause incorrect wallet balance.

**Atomic webhook handling** — the transaction status update, wallet balance change, and webhook audit record are all persisted in a single `SaveChangesAsync` call. If any step fails, nothing is committed.

**HMAC-SHA512 webhook signature** — the same verification scheme used by Paystack and Flutterwave. `CryptographicOperations.FixedTimeEquals` is used for the comparison to prevent timing attacks.

**Correlation IDs** — every request gets an `X-Correlation-Id` header (generated if not supplied by the caller). The ID is echoed in the response and injected into the structured-logging scope, making it straightforward to trace a single request across all log entries.

**Per-policy rate limiting** — auth endpoints use a tight fixed-window limit to slow brute-force attacks; the webhook endpoint uses a generous limit because payment providers may legitimately burst.

**FluentValidation over Data Annotations** — keeps validation logic out of DTOs, composable, and independently testable.

**Global exception middleware** — maps all domain exceptions to RFC 7807 problem-detail responses, keeping controllers free of try/catch boilerplate.

**Auto-migrate on startup (dev only)** — new developers can run `docker compose up` and immediately have a working schema, without a separate migration step.

**Enums stored as strings in Postgres** — `TransactionStatus` and `TransactionType` are stored by name (e.g. `"Pending"`) rather than integer ordinal. This keeps database rows human-readable and avoids subtle bugs when enum values shift.

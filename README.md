# OpsDash

**Real time operational intelligence for multi tenant teams.** Ingest metrics, monitor live dashboards, and get alerts when values breach thresholds or when forecasts and anomaly signals say trouble is coming.

[![.NET 9](https://img.shields.io/badge/.NET-9-512BD4?style=flat-square&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Angular 19](https://img.shields.io/badge/Angular-19-DD0031?style=flat-square&logo=angular&logoColor=white)](https://angular.dev/)
[![SQL Server](https://img.shields.io/badge/SQL%20Server-CC2927?style=flat-square&logo=microsoftsqlserver&logoColor=white)](https://www.microsoft.com/sql-server)
[![SignalR](https://img.shields.io/badge/SignalR-512BD4?style=flat-square&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/apps/aspnet/signalr)
[![Redis](https://img.shields.io/badge/Redis-DC382D?style=flat-square&logo=redis&logoColor=white)](https://redis.io/)
[![Azure](https://img.shields.io/badge/Azure-0078D4?style=flat-square&logo=microsoftazure&logoColor=white)](https://azure.microsoft.com/)
[![xUnit](https://img.shields.io/badge/xUnit-6B4E97?style=flat-square&logo=xunit&logoColor=white)](https://xunit.net/)
[![TypeScript](https://img.shields.io/badge/TypeScript-3178C6?style=flat-square&logo=typescript&logoColor=white)](https://www.typescriptlang.org/)

**One line pitch:** a lightweight Datadog meets PagerDuty built on .NET 9, Angular 19, and SQL Server.

## What makes this different

OpsDash is not only charts and static thresholds. The **intelligence layer** rolls statistics on every ingestion: Z score anomalies, short horizon correlation across metrics, forecasts with weighted moving average or linear regression, predictive rules that watch forecasted values, a composite tenant health score, and automatic incident timelines when anomalies cluster in time.

## Architecture

```
[Angular 19 SPA] -> [ASP.NET Core 9 Web API] -> [SQL Server]
```

**Frontend:** standalone components, Signals with RxJS, lazy loaded routes, Angular Material, Chart.js.

**Backend:** clean architecture, dependency injection, JWT with RBAC, middleware pipeline, FluentValidation.

**Database:** EF Core 9, `TenantId` global query filter, stored procedures, indexed views, seed data.

**Real time:** SignalR hub with tenant scoped groups.

**Caching:** `IDistributedCache` (in memory in development, Redis in production style configurations).

**Cloud:** Azure App Service, Azure SQL, Azure Cache for Redis, Azure Blob Storage, Application Insights.

Azure deployment instructions will be added in Phase 3.

## Multi tenancy

The product uses a **shared database** model: every tenant scoped row carries a `TenantId`. Isolation relies on three cooperating layers: **middleware** that resolves the tenant from the JWT, an **EF Core global query filter** so queries only see the current tenant unless code explicitly opts out, and **API authorization** so routes cannot be abused across tenants.

## Features by phase

### Phase 1: Core platform

- Multi tenant architecture: shared database, EF Core global query filter, tenant resolution middleware
- JWT authentication with refresh tokens
- Role based access control: SuperAdmin, Admin, User
- User management with search, pagination, and role assignment
- Metric ingestion (single and batch) via `POST /api/v1/metrics`
- Dashboard with KPI cards, line and bar charts, date range filter, category filter
- Threshold based alert rules, configurable per metric
- Swagger and OpenAPI with JWT auth support
- Health check endpoints: `/health`, `/health/ready`
- Server side pagination and sorting across list APIs
- Environment based configuration with user secrets for local development

### Phase 1.5: Intelligence layer

- **Anomaly detection:** rolling Z score per metric per tenant, tiered severity (Warning, Critical, Severe), baselines recalculate on each ingestion
- **Metric correlation:** when an anomaly fires, a time window check (about fifteen minutes before and after) surfaces related metric movements
- **Forecasting:** weighted moving average (default) and linear regression; forecast appears as a dotted projection on charts
- **Predictive alerting:** rules can evaluate forecasted values with a configurable horizon, not only the latest raw sample
- **Tenant health score:** composite 0 to 100 score from normal metric share (40%), anomaly density (30%), trend direction (20%), mean time to acknowledge (10%), color coded green, yellow, red
- **Incident auto grouping:** anomalies inside rolling thirty minute windows cluster into incidents with lifecycle Open, Acknowledged, Investigating, Resolved, plus a full event timeline per incident

### Phase 2: Differentiators

- **Real time notifications:** SignalR tenant scoped groups and toast style alerts in Angular
- **Caching:** `IDistributedCache`, tenant scoped cache keys, invalidation hooks tied to ingestion
- **Report export:** CSV and PDF artifacts stored in Azure Blob Storage
- **Audit logging:** EF Core `SaveChanges` interceptor, old and new values as JSON, admin viewer in the app

## Getting started

### Prerequisites

| Requirement        | Notes                                      |
| ------------------ | ------------------------------------------ |
| .NET 9 SDK         | Matches solution target framework        |
| Node.js LTS        | For the Angular client                     |
| Angular CLI        | `npm i -g @angular/cli` if you prefer global |
| SQL Server LocalDB | Bundled with Visual Studio on Windows     |
| Git                | For clone and version control              |

### Connection string

Use a trusted connection to LocalDB while developing:

```
Server=(localdb)\MSSQLLocalDB;Database=OpsDash;Trusted_Connection=true;TrustServerCertificate=true
```

Place this in user secrets or `appsettings.Development.json` for `OpsDash.API` as your project already expects.

### Run the API

```bash
cd src/OpsDash.API
dotnet run
```

Default HTTPS URL from `launchSettings.json`: **https://localhost:7198**

Swagger: https://localhost:7198/swagger

### Run the Angular client

```bash
cd src/opsdash-client
ng serve
```

App URL: **http://localhost:4200**

## Demo credentials

Seed data loads **three demo tenants** with about ninety days of history, embedded anomalies, open incidents, and precomputed health scores.

| Tenant                 | Admin login              | Password   |
| ---------------------- | ------------------------ | ---------- |
| TechFlow Solutions     | admin@techflow.com       | `Demo@123` |
| Metro Health Network   | admin@metrohealth.com    | `Demo@123` |
| UrbanCart              | admin@urbancart.com      | `Demo@123` |

**TechFlow (SaaS) sample metrics:** `active_users`, `monthly_revenue`, `churn_rate`, `api_response_time`, `error_rate`

**Metro Health (Healthcare) sample metrics:** `er_wait_time`, `bed_occupancy`, `patient_throughput`, `staffing_ratio`

**UrbanCart (Ecommerce) sample metrics:** `checkout_conversion`, `cart_abandonment`, `page_load_time`, `payment_success_rate`

## API endpoints summary

| Area        | Endpoints |
| ----------- | --------- |
| **Auth**    | `POST /api/v1/auth/register`, `/login`, `/refresh`, `/revoke` |
| **Users**   | `GET`, `POST`, `PUT`, `DELETE` under `/api/v1/users` |
| **Metrics** | `POST /api/v1/metrics`; `GET /api/v1/metrics/summary`, `/history`, `/forecast` |
| **Anomalies** | `GET /api/v1/anomalies`, `/active`, `/{id}` |
| **Incidents** | `GET /api/v1/incidents`; `PUT /{id}/acknowledge`; `PUT /{id}/status` |
| **Alert rules** | `GET`, `POST`, `PUT`, `DELETE` under `/api/v1/alert-rules` |
| **Alerts**  | `GET /api/v1/alerts`; `PUT /{id}/acknowledge` |
| **Health score** | `GET /api/v1/health-score`, `/history` |
| **Audit logs** | `GET /api/v1/audit-logs` |
| **Reports** | `POST`, `GET` under `/api/v1/reports`; `GET /{id}/download` |
| **System**  | `GET /health`, `/health/ready` |

## Database schema overview

Tables are grouped by domain. Every business table participates in multi tenancy through a `TenantId` column where applicable.

| Domain | Tables |
| ------ | ------ |
| **Tenancy and identity** | `Tenants`, `Users`, `Roles`, `RefreshTokens` |
| **Metrics and baselines** | `Metrics`, `MetricBaselines`, `MetricForecasts` |
| **Intelligence** | `AnomalyScores`, `MetricCorrelations` |
| **Incidents** | `Incidents`, `IncidentEvents` |
| **Alerting** | `AlertRules`, `Alerts` |
| **Health** | `HealthScores` |
| **Operations** | `AuditLogs`, `Reports` |

## Solution structure

```
OpsDash/
  OpsDash.sln
  README.md
  src/
    OpsDash.API/            Controllers, middleware, Program.cs
    OpsDash.Application/    DTOs, interfaces, services, validators, mappings
    OpsDash.Domain/         Entities, enums, domain interfaces
    OpsDash.Infrastructure/ EF Core, repositories, seed, cache, tokens, tenant services
    opsdash-client/         Angular 19 standalone SPA
  tests/
    OpsDash.UnitTests/
    OpsDash.IntegrationTests/
```

## Running the tests

From the repository root:

```bash
dotnet test OpsDash.sln
```

This runs **xUnit** projects under `tests/OpsDash.UnitTests` and `tests/OpsDash.IntegrationTests`.
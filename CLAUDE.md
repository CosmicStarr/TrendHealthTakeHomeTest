# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

Take-home project: a patient **reminders** app. A .NET 10 Web API (`TrendHealthAPI`) on SQLite via EF Core, plus an Angular 22 SPA (`TrendHealthUI`). `README.md` covers the API contract and the recurrence and audit assumptions. Keep it up to date when behaviour changes.

## Commands

Backend (run from the repo root; the solution file is `TrendHealthTakeHomeTest.slnx`):

```bash
dotnet build TrendHealthTakeHomeTest.slnx
dotnet run --project TrendHealthAPI          # http://localhost:5192; creates/migrates trendhealth.db on startup
dotnet test TrendHealthAPI.Tests
dotnet test TrendHealthAPI.Tests --filter "FullyQualifiedName~RecurrenceScheduleTests"   # one class
```

A running API locks its build output, so stop it before you rebuild or test.

EF Core migrations (the DbContext lives in `Data`, the startup project is `TrendHealthAPI`):

```bash
dotnet ef migrations add <Name> --project Data --startup-project TrendHealthAPI
```

Frontend (run from `TrendHealthUI/`):

```bash
npm start                                   # ng serve on :4200, proxies /reminders -> :5192 (proxy.conf.json)
npx ng test --watch=false                   # Vitest via @angular/build:unit-test
npx ng test --watch=false --include src/app/reminders/reminder-form.spec.ts
npx ng build
npx prettier --write src                    # printWidth 100, single quotes
```

## Architecture

The projects reference each other in this order: `Models` ← `Data` ← `TrendHealthAPI` ← `TrendHealthAPI.Tests`.

- **Models**:
  - `Reminder`.
  - `ReminderEvent`: the audit trail, with init-only properties.
  - `RecurrenceSchedule.NextDueDate`: pure recurrence math. It anchors to the previous due date and skips missed slots, so the result is always after the completion time.
- **Data**:
  - `ApplicationDbContext` stores every `DateTime` as UTC; a converter marks values as UTC on read.
  - It **rejects any update or delete of a `ReminderEvent`** inside `SaveChanges`.
  - `Reminder.Status` is a concurrency token, and `SourceReminderId` is unique. Together these make a duplicate or racing complete fail instead of creating two next occurrences.
  - **Unit of Work + generic repository**: `IUnitOfWork.Repository<T>()` creates and caches a repository per entity type. Repository methods only stage changes; nothing is saved until `Complete()` is called.
  - `Get(id)` uses `FindAsync`, so the entity is tracked. Use it when you intend to change the entity. `GetAll`, `GetAllParams` and `GetFirstOrDefault` use `AsNoTracking`.
  - `CacheService` (Redis) is a best-effort cache. It is only registered when `ConnectionStrings:RedisConnection` is set, and nothing uses it today.
- **TrendHealthAPI**:
  - There is no service layer. `Controllers/RemindersController` (which inherits from `BaseController`) implements the use cases directly on `IUnitOfWork` / `IRepository<T>`. Every state change adds its events in the same `Complete()` call.
  - `BaseController` provides `[ApiController]`, JSON output, and the `NotFoundError` / `ConflictError` helpers. `RemindersController` overrides the base `api/[controller]` route with `reminders`.
  - DTOs live in `Models/Dtos` (namespace `Models.Dtos`). `TimeProvider` is injected so tests can control the clock.
- **Errors**: `CosmicStoreAPI.Error.*` (the namespace was inherited from another project).
  - Responses look like `{ statusCode, message }`.
  - `ApiValidationResponse` adds `errors`, a map from camelCase field name to messages. `InvalidModelStateResponseFactory` in `Program.cs` builds it, removes the .NET type names that JSON-converter messages contain, and drops the redundant parameter-level "required" error.
  - `ExceptionMiddleware` runs first in the pipeline.
- **Tests**:
  - `TestSupport.TestDatabase` runs the real migrations against in-memory SQLite and hands out a new controller (with its own unit of work) per "request". `ActionResults.Body()` / `.StatusCode()` unwrap action results. The controller logic is tested in `RemindersControllerTests`.
  - `RemindersApiTests` uses `WebApplicationFactory` with a temporary SQLite file. It captures all logs and checks that notes never appear in them.
- **UI** (`src/app/reminders/`):
  - Standalone components that keep their state in signals. Angular 22 is zoneless, so put state in signals, not plain fields.
  - `ReminderList` loads through a `switchMap` pipeline. `App` calls `list.showCreated(r)` after a create so the new row is on screen.
  - `describeError` turns API errors into a message plus per-field errors.

## Conventions

- **Logging**: log identifiers only. Never put notes, a full record or a DbUpdateException message in a log line.
- Enums are sent and received as strings (`JsonStringEnumConverter`), and query enums bind case-insensitively.
- Every `DateTime` is UTC. Request DTOs take `DateTimeOffset` and convert it with `.UtcDateTime`.
- `TrendHealthAPI/.gitignore` excludes `appsettings.json`. `Program.cs` therefore falls back to `Data Source=trendhealth.db` when no connection string is configured.

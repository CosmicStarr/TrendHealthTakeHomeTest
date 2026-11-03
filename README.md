# Trend Health – Patient Reminders

An ASP.NET Core 10 API (SQLite, EF Core) and an Angular 22 client for creating, listing and completing patient reminders. Daily and Weekly reminders recur, and every reminder keeps an append-only audit trail.

## Running it

You need the .NET 10 SDK and Node 20+. There's nothing else to install: the SQLite database file (`TrendHealthAPI/trendhealth.db`) is created and migrated automatically on first start.

```bash
# Terminal 1: API on http://localhost:5192
dotnet run --project TrendHealthAPI

# Terminal 2: UI on http://localhost:4200 (proxies /reminders to the API)
cd TrendHealthUI
npm install
npm start
```

Tests:

```bash
dotnet test TrendHealthAPI.Tests       # 30 tests: recurrence math, service + SQLite, HTTP pipeline
cd TrendHealthUI && npx ng test --watch=false   # 8 tests: service, form, app shell
```

`TrendHealthAPI/TrendHealthAPI.http` has a ready-made request for every endpoint.

## API

| Method | Route | Success | Errors |
|---|---|---|---|
| GET | `/reminders` | 200 paged list | 400 bad query |
| POST | `/reminders` | 201 + `Location` | 400 validation |
| GET | `/reminders/{id}` | 200 | 404 |
| POST | `/reminders/{id}/complete` | 200 `{ completed, nextOccurrence }` | 404, 409 already Done |
| GET | `/reminders/{id}/history` | 200 events in order | 404 |

**Query parameters for `GET /reminders`:**
- `page` (default 1) and `pageSize` (1–50, default 10).
- `status`: `Open` or `Done`.
- `dueFrom` and `dueTo`: ISO 8601. Both bounds are inclusive.
- `sortBy`: `dueDate` or `createdAt`.
- `sortDir`: `asc` or `desc`.

The response is `{ items, page, pageSize, totalCount, totalPages, hasNextPage, hasPreviousPage }`. Id is a secondary sort key, so pages stay stable when several rows share a timestamp.

**Errors:** every error has the shape `{ statusCode, message }`. A 400 also includes `errors`, a map from each field (camelCase, as it was sent) to its messages:

```json
{ "statusCode": 400, "message": "One or more validation errors occurred.",
  "errors": { "title": ["The title field is required."], "recurrence": ["The value is not valid for this field."] } }
```

Enums are sent and received as strings (`"Open"`, `"Daily"`). All timestamps are UTC with a `Z` suffix.

## Design notes

- **Audit trail.** `ReminderEvent` rows (`Created`, `Completed`, `NextOccurrenceGenerated`) are only ever inserted. `ApplicationDbContext` throws if anything tries to save an update or delete of one. A state change and its events are saved in the same `SaveChanges`, so neither can exist without the other. Events can point to the other reminder involved through `relatedReminderId`, which links each occurrence to the one it came from and to the one it generated. The current `Status` is stored on the reminder rather than worked out from the events. This is "event-style", not full event sourcing.
- **Layers.** `Models` holds the entities and the `RecurrenceSchedule` function (pure, no I/O). `Data` holds the DbContext, a generic repository and the unit of work. In `TrendHealthAPI`, `RemindersController` implements the use cases directly on `IUnitOfWork` / `IRepository<T>`.
- **Safe logging.** Log lines contain only reminder IDs. EF's SQL logging shows parameter placeholders, not values, because sensitive-data logging is off. When a complete request loses a race, only the exception *type* is logged. An integration test creates a reminder with a known secret in its notes, runs it through every endpoint while capturing all log output, and checks that the secret never appears.

## Assumptions and edge cases

1. **The next due date is anchored to the previous due date, not to when the reminder was completed.** A daily 09:00 reminder completed at 15:00 is next due at 09:00 the following day, so time of day and weekday stay the same.
2. **Late completion skips missed occurrences.** If a Daily reminder due Monday 09:00 is completed on Thursday at 21:00, the next one is due Friday 09:00. It is not Tuesday 09:00, which would already be overdue, and no reminders are generated for the days that were missed. The new due date is always strictly after the completion time. That includes completion exactly at the next slot, which moves on one more interval.
3. **Early completion** still gives `dueDate + interval`.
4. **Intervals are fixed UTC durations** (24 hours, 7 days), because all times are stored in UTC. A reminder created at "09:00 local" shows as 08:00 or 10:00 local after a daylight-saving change. Keeping wall-clock time would mean storing the patient's time zone, which is out of scope.
5. **Completing a reminder that is already Done returns 409** and changes nothing. A retried request therefore can't create a second next occurrence.
6. **Two requests completing the same reminder at once:** `Status` is a concurrency token, so the second save matches zero rows and returns 409. A unique index on `SourceReminderId` backs this up: a reminder can have at most one next occurrence. There is a test that simulates the race.
7. **The next occurrence copies** patient, title, notes and recurrence. It gets a fresh `createdAt`, status `Open`, and `sourceReminderId` pointing to the reminder it came from. Its history starts with its own `Created` event.
8. **On create:** `status` is always Open (the client can't set it). `notes` is optional (up to 2000 characters). `patientName` and `title` are required (up to 200 characters, whitespace trimmed). `dueDate` must include an offset and may be in the past, for example when recording a reminder late.
9. **There's no update or delete endpoint**, because the spec doesn't ask for one. If one were added, it would append its own event type.

## Frontend

The UI is a single page with a create form above a list.
- **List:** filter by status, sort, and Previous/Next paging driven by the API's metadata. An Open row has a **Mark done** button, so you can see recurrence work from the UI.
- **Form:** client-side validation, a loading state while saving, and errors from the server shown next to the matching field.
- **After a create:** the list switches to "newest first", page 1, and clears a `Done` filter if one is set. That guarantees the new reminder is on screen, and it's highlighted.

# Chatbot events and survey links

The .NET API owns `ChatEvents`, `ChatSurveys`, and durable `ChatContentIndexJobs` in the existing PostgreSQL database. Flask classifies `/ask` requests, searches the dedicated `frank_vectors."events-and-surveys-chats"` pgvector table (namespace `chat-content`), and fetches current published records through .NET using the user's JWT. React's admin panel has Events and Surveys screens. `/ask` keeps its `response` string and adds `links: [{label, url, type, recordId}]` for content answers.

## Runtime configuration

.NET environment variables:

- `ChatContent__FlaskBaseUrl`: reachable Flask service base URL.
- `ChatContent__IndexKey`: a shared service secret for indexing calls.
- Existing `ConnectionStrings__DefaultConnection`: the same PostgreSQL database.

Flask environment variables:

- `CHAT_CONTENT_ENABLED=true`: enables classification. Leave false during deployment until the migration and both APIs are ready.
- `CHAT_CONTENT_API_BASE_URL`: reachable .NET base URL; falls back to `JOB_DB_API_BASE_URL`.
- `CHAT_CONTENT_INDEX_KEY`: must match .NET's `ChatContent__IndexKey`.
- `CHAT_CONTENT_TIMEZONE=Asia/Beirut`: timezone used to interpret relative dates.
- `CHAT_CLASSIFIER_MODEL`: optional; defaults to the existing `FAST_MODEL` configuration. Must support strict JSON-schema chat-completion output.
- `CHAT_CONTENT_MIN_SCORE=0.45`: initial similarity floor; calibrate with actual university event questions before broad use.
- Existing `PGVECTOR_DATABASE_URL` / `DATABASE_URL`, `PGUSER`, `PGPASSWORD`, and model settings remain in use.

Use HTTPS URLs for cross-service production traffic. Do not expose the indexing key to the frontend. .NET admin mutations require role `Admin`; read endpoints require a valid user JWT and hide drafts/archived records, including surveys whose associated event is unpublished.

Flask knowledge-management endpoints (`/upload_pdf`, `/fetch_and_process`, `/fetch_urls`, `/transcribe`,
`/sds`, and GET/DELETE `/embeddings`) require a validated JWT with the exact `Admin` role. Missing/invalid
tokens return 401 and authenticated non-admins return 403. `/ask` continues to accept ordinary user JWTs;
the internal indexing API uses its separate shared key.

## Deployment order

1. Back up the database and apply the new EF migration from `franc_backend` (`dotnet ef database update`). This work only generated migration files; it did not change a live database.
2. Deploy .NET with the indexing worker configuration. Its jobs survive restarts and retry up to five times with increasing delays.
3. Deploy Flask with the shared key and API base, initially with `CHAT_CONTENT_ENABLED=false`.
4. Deploy React; create a draft, publish it, and verify its index becomes ready. Retry indexing from the admin screen if needed.
5. Set `CHAT_CONTENT_ENABLED=true`. Verify an ordinary university question, named event, date listing, event survey, and follow-up using separate test accounts.
6. Compare response latency logs before/after enabling. `Chat classification/retrieval elapsed_ms` measures classification and content-path work; it is not a benchmark of production model latency.

Rollback: set `CHAT_CONTENT_ENABLED=false` to restore the existing general-knowledge chat flow. Keep the additive tables and queued jobs until the rollout is understood. Reverting a migration is a separate database operation.

## Behavior

- Date listings use inclusive `from` and exclusive `to`, show 20 records per page, and report totals.
- The classifier's `availability_filter` becomes the .NET `availability` query parameter. Use `all` for historical/all-record lists, or an explicit status for closed surveys, cancelled events, and similar requests. SQL applies status filters before counts and pagination. Ordinary upcoming/open lists use `available`; a mixed university/survey list needs no event dates.
- A selected event candidate scopes survey listings through `eventId`, overriding previous event context. Unknown selected IDs request clarification and never fall back to all surveys.
- Named content uses pgvector, then .NET revalidates current status and returns current URLs/dates. Ambiguous matches ask the user to choose.
- Open surveys and scheduled or ongoing events receive action links; closed surveys, events that ended (or started without a known end), and cancelled events remain explainable by name.
- Publication changes enqueue indexing work atomically with the record save. Stable vector IDs and version guards prevent delayed older jobs from overwriting newer content. Unpublishes retain non-searchable vector tombstones for the same reason.
- Search by name may be unavailable until indexing succeeds. Current details for an already selected record are available directly from .NET.
- Survey links can be standalone or associated with an event. The feature does not create questionnaires or collect answers.
- Context is keyed by authenticated user in Flask memory. It is shared between that user's tabs and lost on process restart. One server instance still needs one worker process for consistent in-memory follow-ups; multiple workers require shared state in a future change.
- No new production secrets or URLs are committed. Indexing errors log exception type and record identity rather than response bodies or credentials.

## API contract

- `GET /api/chat-content/{event|survey}`: `ids` (repeatable), `from`, `to`, `eventId`, `query`, `includeUnavailable`, `page`, `pageSize` (max 100), and `admin=true` for role-checked administration. Returns `{items,total,pageNumber,pageSize}`.
- Optional `availability` overrides `includeUnavailable`: `available`, `all`, or `cancelled` for both types; `scheduled`, `ongoing`, `started`, `ended` for events; `open`, `closed`, `not_open` for surveys. Invalid combinations return 400. Omitting the filter retains previous behavior.
- `GET /api/chat-content/{type}/{id}`: current published record.
- `POST /api/chat-content/{type}` and `PUT /api/chat-content/{type}/{id}`: admin input with name, description, dates, timezone, URL, status (`draft|published|archived`), cancellation flag, and optional event ID. Updates require the last returned `version`; stale edits return HTTP 409.
- `POST /api/chat-content/{type}/{id}/retry-index`: admin retry.
- `POST /internal/chat-content/index`: .NET-to-Flask only, protected by `X-Chat-Content-Key`; receives the current versioned record.

## Validation

Automated tests cover routing validation, record references, user isolation, current-record retrieval, mixed requests, service authentication, backend validation and PostgreSQL SQL translation, admin saves, URL filtering, and timezone conversion. They stub paid model calls and do not modify the deployment database. Real model classification quality, vector similarity thresholds, and deployed service connectivity need the rollout smoke check above.


## Dedicated event/survey vector table

Event/survey searches and writes use `chat_content_index`, backed by `frank_vectors."events-and-surveys-chats"`.
General university knowledge keeps using `frank_vectors."document-chats"`.
The event table uses the same stored embedding dimension and cosine metric as the existing corpus.
The internal `chat-content` namespace is retained within the dedicated table.
Admin embedding listing, metadata fetching, and ID/prefix/all deletion use that namespace too, including
when the legacy index name is supplied. Document knowledge management retains the empty namespace.

For environments that already indexed event content into the university table, run from Frank-LLM:

```bash
python scripts/migrate_chat_content_index.py          # counts only
python scripts/migrate_chat_content_index.py --apply  # copy and verify, no model calls
```

Then restart Flask so all new searches and indexing writes use the dedicated table.
After all old Flask processes have stopped, optional cleanup removes only the verified legacy
`event`/`survey` vectors in the old `chat-content` namespace, leaving university documents intact:

```bash
python scripts/migrate_chat_content_index.py --apply --remove-legacy
```

The copy is idempotent and preserves IDs, metadata, tombstones and embeddings; newer destination
versions are never overwritten by older source rows. Original rows remain as a backup unless
`--remove-legacy` is explicitly supplied. The local Azure development database's existing test
vector was copied and verified; the legacy backup was retained pending Flask restart.


## Clearer index names

The PostgreSQL tables and index catalog entries were renamed in place:

- `antonine-university` → `document-chats` (admin label: Document chats).
- `chat-content` → `events-and-surveys-chats` (admin label: Events and surveys chats).

Restart Flask after applying this change. Table identity, vectors, metadata, namespaces and
constraints are preserved; no re-embedding is needed. The runtime accepts legacy API index
names as aliases, but returns and displays canonical names. API route paths and the internal
`chat-content` namespace are unchanged.

For another environment, run `python scripts/rename_chat_indexes.py` to inspect, then
`python scripts/rename_chat_indexes.py --apply` to rename transactionally. Do not run old
Flask processes against the renamed tables. Historical Pinecone migration tooling keeps its
original source index names; follow it with this rename script when using a fresh destination.


## Automatic descriptions and related surveys

Event answers (including listings and follow-ups) include the event description and automatically
retrieve associated published surveys. Flask requests surveys for all selected events in one batched
`.NET GET /api/chat-content/survey?eventIds=...` query per page, includes their descriptions, and attaches
available action links. No separate survey question is required. Survey association is revalidated
against the selected events and duplicate surveys are removed. The event remains the conversation focus.

Restart both .NET and Flask after this update. No additional database migration is required.

For the production-review fixes, restart .NET before Flask so `availability` is supported before the
classifier starts sending it. No new secret or environment setting is needed.

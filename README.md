# franc_backend

## Production deploy (admin analytics + Phase 2)

### 1. Apply database schema (required before deploying new code)

Run **once** on production DB:

```text
Scripts/apply-phase2-production.sql
```

Or:

```powershell
dotnet ef database update --project FrancProject.csproj --context DataContext
```

(If `dotnet ef` fails on older migrations, use the SQL script — it stamps already-applied changes and adds Phase 2.)

### 2. Deploy backend

Push/deploy the new API. Old and new code both work after step 1.

### 3. Analytics config (optional, in appsettings or env)

```json
"Analytics": {
  "UseActivityEvents": false,
  "EnableResponseCache": true,
  "CacheDurationDays": 1
}
```

| Setting | `false` (default) | `true` |
|---------|-------------------|--------|
| **UseActivityEvents** | Reads activity from original tables (SDS, files, etc.) | Reads from `ActivityEvents` when table has rows; otherwise same as `false` |
| **After backfill** | Still works | Faster analytics; run local `Scripts/backfill-activity-events.sql` then set `true` |

### 4. Smoke tests

- User sign-in / sign-up
- SDS, upload, game (unchanged flows)
- Admin JWT → `/api/admin/analytics/overview`

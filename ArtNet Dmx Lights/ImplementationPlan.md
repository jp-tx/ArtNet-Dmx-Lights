# Implementation Plan (V1)

This plan starts with the API, builds and tests each endpoint, then implements
the UI. The UI phase ends with a V1 UI draft for user critique.

## Phase 0: Prep and Ground Rules
1) ~~Confirm `SPEC.md` is final and closed to avoid mid-build changes.~~
2) ~~Decide on project structure for JSON persistence and the API/UI layers.~~
3) ~~Define a minimal data storage layout and file naming conventions.~~
4) ~~Choose a test framework for unit tests in the current .NET setup.~~
5) ~~Add project references and test project scaffolding (no feature logic yet).~~

## Phase 1: Core Models and Storage
1) ~~Define data models that mirror `SPEC.md`:~~
   - ~~Settings~~
   - ~~FixtureGroup~~
   - ~~Preset~~
   - ~~Schedule~~
   - ~~LogEntry~~
   - ~~Backup bundle~~
2) ~~Implement JSON persistence:~~
   - ~~Read/write with file locks or atomic swaps.~~
   - ~~In-memory cache + persist on change.~~
3) ~~Add validation helpers:~~
   - ~~Channel range checks (1-512).~~
   - ~~Universe range checks (per config).~~
   - ~~Value range checks (0-255).~~
4) ~~Unit tests:~~
   - ~~Serialization round-trip for each model.~~
   - ~~Validation edge cases (min/max, bad inputs).~~

## Phase 2: API - Settings
1) ~~Implement `GET /api/v1/settings`.~~
2) ~~Implement `PUT /api/v1/settings` (replace settings, validate input).~~
3) ~~Implement time zone resolution and lat/long lookups:~~
   - ~~Integrate Zippopotam.us + Open-Meteo (timezone=auto).~~
   - ~~Store resolved values for offline fallback.~~
4) ~~Unit tests:~~
   - ~~GET returns current values.~~
   - ~~PUT validates and persists.~~
   - ~~Zip lookup failures fall back to cached values.~~

## Phase 3: API - Fixture Groups
1) ~~Implement `GET /api/v1/groups`.~~
2) ~~Implement `POST /api/v1/groups`.~~
3) ~~Implement `GET /api/v1/groups/{groupId}`.~~
4) ~~Implement `PUT /api/v1/groups/{groupId}`:~~
   - ~~Apply channel count adjustments to presets (truncate/extend).~~
5) ~~Implement `DELETE /api/v1/groups/{groupId}`:~~
   - ~~Remove from presets; delete presets with no groups.~~
6) Unit tests:
   - ~~CRUD lifecycle.~~
   - ~~Edit/delete side effects on presets.~~
   - ~~Channel overlap policy (highest value wins).~~

## Phase 4: API - Presets
1) ~~Implement `GET /api/v1/presets`.~~
2) ~~Implement `POST /api/v1/presets`.~~
3) ~~Implement `GET /api/v1/presets/{presetId}`.~~
4) ~~Implement `PUT /api/v1/presets/{presetId}`.~~
5) ~~Implement `DELETE /api/v1/presets/{presetId}`:~~
   - ~~Remove schedules that reference the preset.~~
6) ~~Implement `POST /api/v1/presets/{presetId}/activate`:~~
   - ~~Send DMX values with fade.~~
   - ~~Apply channel overlap resolution (highest value).~~
7) ~~Unit tests:~~
   - ~~CRUD lifecycle.~~
   - ~~Activation applies correct channel values.~~
   - ~~Fade uses linear interpolation and duration in ms.~~
   - ~~Logging writes a single event per activation.~~

## Phase 5: API - Schedules
1) ~~Implement `GET /api/v1/schedules`.~~
2) ~~Implement `POST /api/v1/schedules`.~~
3) ~~Implement `GET /api/v1/schedules/{scheduleId}`.~~
4) ~~Implement `PUT /api/v1/schedules/{scheduleId}`.~~
5) ~~Implement `DELETE /api/v1/schedules/{scheduleId}`.~~
6) ~~Implement scheduler engine:~~
   - ~~Daily recurrence using local resolved time zone.~~
   - ~~Sunrise/sunset resolution with offsets.~~
   - ~~Conflict resolution uses last-updated schedule.~~
7) ~~Unit tests:~~
   - ~~Schedule evaluation for fixed time.~~
   - ~~Sunrise/sunset offset calculations.~~
   - ~~Conflict resolution with updatedAt ordering.~~

## Phase 6: API - Logs
1) ~~Implement `GET /api/v1/logs` with filters:~~
   - ~~from/to time range~~
   - ~~type~~
   - ~~presetId / groupId~~
2) ~~Implement 72-hour retention cleanup.~~
3) ~~Unit tests:~~
   - ~~Filter combinations.~~
   - ~~Retention trimming behavior.~~

## Phase 7: API - Backup and Status
1) ~~Implement `GET /api/v1/backup` (config only).~~
2) ~~Implement `POST /api/v1/backup` (replace all config; rehydrate in place).~~
3) ~~Implement `GET /api/v1/status`.~~
4) ~~Unit tests:~~
   - ~~Backup import replaces config.~~
   - ~~Logs are preserved on import.~~
   - ~~Status reflects active preset/source/last scheduled time.~~

## Phase 8: ArtNet Sender and Startup Behavior
1) ~~Implement ArtNet 3 packet builder and UDP sender.~~
2) ~~Honor global Net/SubNet and per-group Universe mapping.~~
3) ~~On startup:~~
   - ~~Apply the last scheduled change if one exists.~~
   - ~~Else set all fixture group channels to 0.~~
   - ~~If no fixture groups exist, do nothing.~~
4) ~~Unit tests:~~
   - ~~Packet shape and addressing fields.~~
   - ~~Startup behavior rules.~~

## Phase 9: API Integration Tests
1) ~~Add integration tests that exercise full API flows:~~
   - ~~Settings -> Groups -> Presets -> Activate~~
   - ~~Schedules -> Trigger -> Active preset~~
   - ~~Backup export/import~~
2) ~~Verify logging coverage and retention in these flows.~~

## Phase 10: UI Implementation (V1 Draft)
1) ~~Build UI pages with API-backed flows:~~
   - ~~Settings~~
   - ~~Groups~~
   - ~~Preset editor~~
   - ~~Main control~~
   - ~~Scheduling~~
   - ~~Logs~~
   - ~~Backup~~
2) ~~Ensure all UI actions use `/api/v1`.~~
3) ~~Provide a V1 UI draft for critique and feedback.~~
4) Iterate UI based on critique (post-V1 changes).

## Phase 11: Final Validation
1) ~~Confirm spec alignment and acceptance criteria.~~
2) ~~Run unit tests and integration tests.~~
3) Verify end-to-end DMX send on a LAN setup.
4) ~~Prepare `ARCHITECTURE.md` (created at implementation time).~~

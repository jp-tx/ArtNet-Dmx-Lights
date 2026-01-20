# ArtNet DMX Web Interface Specification

## Overview
Build a web interface that controls an ArtNet DMX controller over a local LAN.
The app provides configuration pages, fixture grouping, preset creation, preset
activation, and scheduled preset changes (including astronomical times).
All frontend-to-backend interactions use an HTTP API that is documented for
external integration.

## Primary Goals
- Provide a LAN-only web UI to send DMX values to an ArtNet controller.
- Support configuring controller network and connection settings.
- Allow defining fixture groups with channel assignments and saving them.
- Allow creating presets for a fixture group using sliders.
- Provide a main page to switch between presets.
- Provide scheduling to switch presets, including sunrise/sunset with offsets
  based on a zip code.
- Persist data using JSON files inside the container context.
- Provide a backup page to download current configuration JSON.
- Log key events and provide a filterable log UI.
- Ensure all UI actions use the documented HTTP API.
- Document the final architecture in ARCHITECTURE.md.

## Non-Goals
- User accounts, authentication, or access control.
- Internet exposure or HTTPS termination inside the container.
- Receive ArtNet data (send only).

## Users and Deployment
- Users: local LAN operators only.
- Deployment: Docker container, HTTP only. External reverse proxy handles HTTPS
  (out of scope).

## ArtNet and DMX Requirements
- Controller: PKNight ArtNet2-CR021R.
- Protocol: ArtNet 3.
- Direction: send-only.
- Universes: 2 total.
- Channels per universe: 512.
- Channel numbering: 1-512 in UI, API, and storage.
- Universe numbering: user-configurable base (0-based or 1-based) and applied
  consistently across UI, API, and storage. Changing the base updates existing
  stored data to preserve the same physical universe targeting.
- DMX value range: 0-255.
- Transport: UDP, on-demand sending is acceptable.
- Addressing: unicast with support for static IP and DNS hostname.
- ArtNet addressing: configure Net and SubNet globally; Universe is explicit
  per fixture group.
- ArtNet UDP port: configurable with default 6454.
- Channel overlap resolution: when multiple fixture groups set the same channel
  in a preset, the highest value wins.
- Discovery: send ArtPoll broadcasts and parse ArtPollReply responses to find
  ArtNet nodes on the LAN.

## Data Persistence
Data is stored as JSON files on disk inside the container (no volume mount
required). The data directory can be overridden via `ARTNET_DATA_PATH`; if not
set, it defaults to `{content root}/data`. A backup page provides a downloadable
config JSON. Data includes:
- Network and connection settings (including manual time zone/coordinate overrides).
- Fixture groups and their channel mappings.
- Presets (can span one or multiple fixture groups).
- Schedules (including astronomical rules and offsets).
- Logs.
Backup export includes configuration data only and excludes logs.

## UI Pages
1) Network and connection settings page.
   - Includes ArtNet device discovery and selection.
   - Manual overrides for time zone and coordinates.
2) Fixture group management page:
   - Assign groups of channels to fixture groups.
   - Name and save groups.
3) Preset editor page:
   - Slider controls for channel values for a fixture group.
   - Save presets.
   - Live preview: slider changes apply immediately via API without saving.
4) Main control page:
   - Switch between presets.
5) Scheduling page:
   - Change presets on a schedule.
   - Support astronomical time (sunrise/sunset) with offsets by zip code.
6) Logs page:
   - Filterable log view.
7) Backup page:
   - Download current configuration JSON.
   - Import a previously downloaded configuration JSON to restore.
   - Import rehydrates in place without restarting the app.
   - Import replaces all current configuration data.

## Presets and Fixture Groups
Presets store, at minimum:
- Preset name.
- List order (integer) for UI ordering.
- Grid location (integer) for grid ordering.
- One or more fixture groups included in the preset.
- For each included group: values for each channel in the group.
- Fade time (single value applied to all channels simultaneously).
  - Unit: milliseconds.
- Fade applies to both manual and scheduled changes.
- Fade curve: linear interpolation.
- Preset application: if a preset omits channels/groups, those channels remain
  unchanged.
- New presets append to the end of the list by using (max list order + 1).
- New presets append to the end of the grid by using (max grid location + 1).
- Reordering swaps list order values with the nearest neighbor.
- Grid location is a 0-based slot index; slots 0-35 are the first grid, 36-71
  the second, and so on.
- Dragging onto a blank slot leaves a gap; dragging onto an occupied slot shifts
  intervening presets and reassigns grid locations.
- Scenes and presets are the same concept; use "preset" in UI and API.
- Preset maintenance on group changes:
  - If a group channel count increases, extend values with zeros.
  - If a group channel count decreases, truncate values.
  - If a group is deleted, remove it from presets; if a preset has no groups,
    delete the preset.

Fixture groups:
- User-defined mapping of a channel range to a named group.
- Each group includes at least: universe, starting channel, and channel count.
- Physical fixtures may share a channel range without additional modeling.

## Scheduling
Scheduling supports:
- Fixed time schedules.
- Astronomical time schedules (sunrise/sunset) with offsets.
- Zip code based location input.
- Manual changes override schedule until the next scheduled change.
- Schedules are daily recurring (no weekday or skip rules).
- If multiple schedules trigger at the same time, the last-updated schedule
  wins.
- Astronomical times are fetched via an external lookup service.
- The most recent sunrise/sunset values are stored for offline fallback.
- Zip code scope: US only.
- UI shows the resolved time zone.
- Manual time zone and coordinate overrides take precedence when set.
- Fixed schedule times are evaluated in the resolved local time zone.
- Astronomical lookup services:
  - Zippopotam.us for zip -> lat/long.
  - sunrise-sunset.org for sunrise/sunset times.
  - Open-Meteo for lat/long -> time zone (timezone=auto) and DST.
- Astronomical offsets: minutes, allow positive (after) and negative (before).

## Logging
Logs are required with a 72-hour retention window. Log events include:
- DMX value changes.
- Preset saves, changes, and deletions.
- Sunrise and sunset ticks.
Logs are displayed in a filterable UI page.
Filters include time range, event type, and preset/group.
Backup import does not modify logs.
- DMX change logs record preset names only (no per-channel values).
- Fades log a single change event rather than per-tick entries.

## API and Endpoints
- All frontend actions use a backend HTTP API.
- API endpoints are documented for external service integration.
- No authentication is required (LAN-only usage).
- Base path: `/api/v1`.
- JSON request/response bodies for all endpoints.
- IDs are UUID strings.
- Common fields use UTC ISO 8601 timestamps unless noted.
- Fixed schedule times use `HH:mm` in the resolved local time zone.

### Settings
- `GET /api/v1/settings` returns network, ArtNet, and scheduling settings.
- `PUT /api/v1/settings` replaces settings.
  - Fields: `controllerHost`, `artnetPort`, `artnetNet`, `artnetSubNet`,
    `universeBase`, `zipCode`, `manualTimeZone`, `manualLat`, `manualLon`.
  - Read-only fields: `resolvedTimeZone`, `resolvedLat`, `resolvedLon`.

### ArtNet Discovery
- `GET /api/v1/artnet/discover` scans for ArtNet nodes on the LAN.
  - Optional query: `timeoutMs` (200-10000, default 1500).
  - Response: `{ nodes: [{ ipAddress, port, shortName, longName }], warning, timeoutMs }`.
  - Discovery is implemented via ArtPoll broadcasts and ArtPollReply responses.

### Fixture Groups
- `GET /api/v1/groups` lists groups.
- `POST /api/v1/groups` creates a group.
- `GET /api/v1/groups/{groupId}` returns a group.
- `PUT /api/v1/groups/{groupId}` replaces a group.
- `DELETE /api/v1/groups/{groupId}` deletes a group.
  - Group fields: `id`, `name`, `universe`, `startChannel`, `channelCount`.

### Presets
- `GET /api/v1/presets` lists presets.
- `POST /api/v1/presets` creates a preset.
- `GET /api/v1/presets/{presetId}` returns a preset.
- `PUT /api/v1/presets/{presetId}` replaces a preset.
- `DELETE /api/v1/presets/{presetId}` deletes a preset.
  - Deleting a preset removes schedules that reference it.
- `POST /api/v1/presets/{presetId}/activate` activates a preset.
- `POST /api/v1/presets/preview` applies a preset payload without saving.
  - Preset fields: `id`, `name`, `listOrder`, `gridLocation`, `fadeMs`, `groups`.
  - Preset group entry: `groupId`, `values` (array of 0-255, length =
    `channelCount` for the referenced group).
- `POST /api/v1/presets/{presetId}/move?direction=up|down` swaps list order with
  the adjacent preset. If already first/last, no change.
- `POST /api/v1/presets/{presetId}/grid?targetIndex=###` reorders presets in the
  grid by moving the preset to the target index; if the target is occupied, it
  shifts intervening entries.
- `POST /api/v1/presets/fix-order` normalizes list and grid order values by
  reassigning list order from 1..N and grid locations from 0..N-1.

### Schedules
- `GET /api/v1/schedules` lists schedules.
- `POST /api/v1/schedules` creates a schedule.
- `GET /api/v1/schedules/{scheduleId}` returns a schedule.
- `PUT /api/v1/schedules/{scheduleId}` replaces a schedule.
- `DELETE /api/v1/schedules/{scheduleId}` deletes a schedule.
  - Schedule fields: `id`, `name`, `presetId`, `type` (`fixed`, `sunrise`,
    `sunset`), `time` (`HH:mm`, required for `fixed`), `offsetMinutes` (for
    `sunrise`/`sunset`), `enabled`, `updatedAt`.

### Logs
- `GET /api/v1/logs` lists logs.
  - Query params: `from`, `to`, `type`, `presetId`, `groupId`.

### Backup
- `GET /api/v1/backup` downloads configuration JSON.
- `POST /api/v1/backup` imports configuration JSON.

### Status
- `GET /api/v1/status` returns current runtime state.
  - Fields: `activePresetId`, `activeSource` (`manual` or `schedule`),
    `lastScheduledAt`.

## Acceptance Criteria
- All specified functionality is implemented; no "TODO" or placeholder
  functionality remains.
- All configuration, group, preset, and schedule data persists across restarts.
- ArtNet DMX send works reliably on LAN for both universes.
- UI communicates with the backend exclusively via the documented API.
- ARCHITECTURE.md documents the final design and components.
- Startup applies the last scheduled change if one exists; otherwise sets all
  fixture group channels to 0. If no fixture groups exist, do nothing.

## Open Questions (Must Be Resolved Before Implementation)
- None.

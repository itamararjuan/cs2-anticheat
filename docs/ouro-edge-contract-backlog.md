# Ouro Edge Contract Backlog

## Purpose
This document defines the production plugin-side contract for `ouro-edge` telemetry ingestion. The first release uses one private batch ingestion route with Bearer authentication and edge-side SteamID enrichment.

## Current Plugin Config Shape
The plugin currently expects these telemetry settings:
- `CollectionEnabled`
- `UploadEnabled`
- `ServerId`
- `ServerLabel`
- `ServerRegion`
- `MatchSource`
- `MatchId`
- `BaseUrl`
- `RelativePath`
- `BearerToken`
- `ReportingIntervalSeconds`
- `LogBatchSummaries`
- `LogObservationSummaries`

## Production Route
Production route:
- `POST /api/cs2/observations`

Required behavior:
- accept one plugin batch at a time
- store the raw batch payload for replay/debugging
- preserve payload `MatchId` as sent by the plugin
- resolve each incoming player/observation `SteamID` to the corresponding Ouro user when possible
- resolve each SteamID/user to the player's currently active match when possible
- store both `payloadMatchId` and `resolvedMatchId` when they differ
- return `2xx` on accepted payloads

Authentication:
- plugin sends `Authorization: Bearer <SECRET_API_TOKEN>`
- plugin production base URL is `https://www.ouro.is/edge/`

## Batch Shape
The plugin currently builds batches containing:
- plugin name and version
- server identity placeholders
- match source placeholders
- map name
- flush reason
- batch sequence
- round number
- server tick
- bomb plant/defuse counts
- per-player snapshots
- observation records

### Player snapshot examples
- identity: SteamID, player name, slot, bot/human
- combat counters: shots, hits, damage, kills, deaths, headshots
- utility counters: flashes, smokes, molotovs, utility damage
- audio counters: footsteps, sounds
- per-weapon stats

### Observation record examples
- detector module findings
- interesting kills with smoke/wallbang/blind context
- kill bursts
- weapon bursts
- pistol-heavy profiles
- revolver/scout-heavy profiles
- utility-damage profile milestones
- zero-utility profiles
- repeated blind-kill profiles

## Edge Enrichment Expectations
During ingestion, `ouro-edge` should derive and store:
- `playerContexts[]` keyed by `steamId`
- resolved `userId` when the SteamID maps to a known user
- resolved `activeMatchId` when the user or SteamID appears in a non-terminal match roster
- `resolutionStatus` describing whether the batch was resolved cleanly, unresolved, ambiguous, or mismatched against `payloadMatchId`

## Future Node.js Route Backlog
The first release keeps a single batch route. Later work can still split responsibilities if real production traffic justifies it.

Possible future routes:
- `POST /api/cs2/observations`
  - ingest raw plugin batches
- `POST /api/cs2/matches/:matchId/observations`
  - match-aware ingestion once MatchZy data is available
- `POST /api/cs2/players/:steamId/profile-snapshots`
  - normalized long-term player profile updates
- `POST /api/cs2/servers/:serverId/health`
  - plugin health, batch failures, and config diagnostics

## Open Questions For Future Sessions
- which suspicious scores are computed plugin-side versus server-side
- how to correlate MatchZy matches with plugin batches
- whether server provisioning should fetch `latest` or pin exact semver releases

# CS2 Telemetry Fork / TB Anti-Cheat
Telemetry-first Counter-Strike 2 plugin for [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp). This fork is intended to collect suspicious combat, weapon, utility, and match-level observations from CS2 servers and forward them to an external API such as `ouro-edge`.

This fork is intentionally moving away from the original public anti-cheat shape:
- no built-in ban flow
- no Discord webhook integration
- no player-facing messages that reveal the plugin exists
- periodic observation batches instead of immediate punitive actions

## Current Focus
### Runtime
- [x] JSON-backed config
- [x] Silent operation for players
- [x] Observation pipeline for suspicious signals
- [x] Periodic batch reporting scaffolding
- [x] Finalized `ouro-edge` API contract
- [x] Match-scoped recording via `ouro_record_start` / `ouro_record_stop` (RCON)
- [ ] MatchZy-aware match metadata

### Signal Families
- [x] Module-backed suspicious observations
- [x] Kill burst tracking
- [x] Match-signal tracker (`TelemetryMatchSignalTracker`) — combat-profile bursts and reconnect-phase heuristics on the same live batch path as other `TelemetryManager` observations; uses `TelemetryMatchSession` rosters for opposing-team sizing
- [x] Smoke, wallbang, blind, noscope, and airborne kill context
- [x] Weapon-profile telemetry, including pistol-heavy and short-window high-signal weapon bursts
- [x] Utility usage and utility damage telemetry
- [x] Audio-context counters (`player_footstep`, `player_sound`)
- [ ] Higher-confidence positional wallhack heuristics

## What The Plugin Collects
The plugin currently collects and batches:
- server identity and optional future match placeholders
- player identity and session context
- round and map context
- shots, hits, damage, kills, deaths, headshots
- flash, smoke, molotov, and utility-damage metrics
- direct economy purchase events
- buy-phase and round-boundary economy snapshots with money and inventory
- suspicious kill context such as smoke kills, wallbangs, flashed kills, and multi-kill bursts
- match-level combat-profile signals from the in-process tracker (repeated burst milestones and reconnect/post-phase spike heuristics), merged into the same observation batches as module and kill telemetry
- weapon-level counters so downstream services can score unusual weapon usage patterns
- profile observations such as zero-utility rounds, blind-kill profiles, pistol-heavy profiles, and revolver/scout concentration

See:
- `docs/telemetry-architecture.md`
- `docs/ouro-edge-contract-backlog.md`
- `docs/release-and-deployment.md`

## Silent Operation
This fork is intended to be invisible to normal players:
- no chat output
- no HUD or center-screen messages
- no user-message output that reveals telemetry collection
- server-side logs and upstream API reports only

## Installation
1. Install [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp).
2. Build or download a packaged release artifact.
3. Place the `TBAntiCheat` folder into `game/csgo/addons/counterstrikesharp/plugins`.
4. Adjust the JSON files in the deployed plugin's `Configs/` directory.

## Configuration
Runtime config files are stored under the deployed plugin directory:
- `Configs/Telemetry.json`
- `Configs/Aimbot.json`
- `Configs/RapidFire.json`
- `Configs/UntrustedAngles.json`
- `Configs/BunnyHop.json`

`Telemetry.json` still carries defaults such as `ServerId`, `ServerLabel`, `ServerRegion`, `MatchSource`, and `MatchId`; during an active recording session the plugin overwrites these from the **`ouro_record_start`** payload (base64 JSON on the RCON command line).

### Telemetry lifecycle (Ouro ↔ game server)

1. **`ouro_record_start <base64-json>`** — Ouro sends match id, server fields, map, optional `reportingIntervalSeconds`, and **`team1` / `team2` rosters** with `steamId64` and player names. The plugin starts a session, matches live humans to the roster by name, and uses roster **SteamID64** as the canonical `SteamID` on every emitted row. Bots are ignored and never uploaded.
2. **Live anticheat stream** — While the session is active (and upload gating rules pass), periodic batches go to:
   - `BaseUrl` + **`/api/cs2/observations`**
   - `BearerToken` → `Authorization: Bearer <token>`
   - Each batch includes non-empty **`MatchId`** and SteamID64-only identity on players, observations, and inline economy rows. `ouro-edge` stores the raw batch and resolves users / active matches from those ids.
3. **`ouro_record_stop`** — Stops recording, builds **one** full-match economy recap from session state, uploads it to **`/api/cs2/match-economy-summary`**, then clears session state. There is **no** final flush to the live observations route on stop.

Rollout caveat: the stricter `/api/cs2/observations` validation assumes this match-scoped plugin flow and the corresponding `gameCoordinator` rollout are live together. Older batches with empty `MatchId`, Steam2 ids, or bot rows will be rejected by `ouro-edge`.

Economy in **live** batches (during the match):

- `EconomyEvents[]` — explicit purchase rows from `item_purchase`
- `EconomySnapshots[]` — money and inventory snapshots from buy-phase and round-boundary hooks
- sell/refund/rebuy flows are not first-class plugin events in V1; those are inferred later on `ouro-edge`

The checked-in `Config/` folder contains example files that mirror the runtime config shape.

## Commands
Admin/server-side commands currently intended for maintenance only:
- `tbac_reload`
- `tbac_aimbot_enable`
- `tbac_aimbot_angle`
- `tbac_aimbot_detections`
- `tbac_rapidfire_enable`
- `tbac_untrustedangles_enable`
- `tbac_bhop_enable`

These commands are not meant to produce player-facing output.

## Development
- .NET 8 SDK is required to build locally.
- The GitHub Actions workflow is the authoritative build and release path for this fork.
- Semver tags such as `v0.1.0` are intended to produce release artifacts for S3 distribution.

## Credits
[Original Creators of SMAC](https://forums.alliedmods.net/forumdisplay.php?f=133) - AlliedModders<br />
[Fork of SMAC](https://github.com/Silenci0/SMAC) - Silenci0<br />
[CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp) - Michael Wilson<br />

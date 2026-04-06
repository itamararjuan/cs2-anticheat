# Release And Deployment

## Goal
Use GitHub Actions as the source of truth for building and packaging the plugin, and publish semver-tagged releases to S3 for later consumption by spawned game servers.

## Local Build
If you have the .NET 8 SDK installed locally:

```bash
dotnet restore
dotnet build --configuration Release
```

Build outputs are expected under `bin/Release/net8.0/`.

## GitHub Actions Build Flow
The workflow should do two things:
- branch and PR validation: restore and build the plugin in Release mode, then upload a packaged artifact
- semver tag release: package the plugin folder and upload the versioned artifact to S3

## Release Tag Convention
Use Git tags like:

```bash
git tag v0.1.0
git push origin v0.1.0
```

That tag is the semver version used for packaging and S3 keys.

## Suggested Packaged Output
The release artifact should contain:
- `TBAntiCheat.dll`
- `TBAntiCheat.pdb` when present
- `Configs/*.json` example files
- repo docs that operators may need during deployment

Suggested archive name:
- `TBAntiCheat-v0.1.0.tar.gz`

## Suggested S3 Layout
```text
s3://<bucket>/cs2-anticheat/releases/v0.1.0/TBAntiCheat-v0.1.0.tar.gz
s3://<bucket>/cs2-anticheat/releases/v0.1.0/manifest.json
s3://<bucket>/cs2-anticheat/releases/latest.json
```

## Suggested GitHub Secrets / Variables
- `AWS_ACCESS_KEY_ID`
- `AWS_SECRET_ACCESS_KEY`
- `AWS_SESSION_TOKEN` when temporary credentials are used
- `AWS_REGION`
- `TBAC_S3_BUCKET`

Prefer IAM credentials limited to the target bucket/path and rotate them periodically.

## Deployment Expectation On Servers
Server provisioning should:
1. resolve the desired plugin version
2. download the corresponding archive from S3
3. unpack the `TBAntiCheat` folder into CounterStrikeSharp plugins
4. provide or template the runtime `Configs/Telemetry.json`
5. inject the production telemetry bearer token into `BearerToken`

## Operational Notes
- production uploads target `https://www.ouro.is/edge/api/cs2/observations`
- the plugin authenticates with `Authorization: Bearer <SECRET_API_TOKEN>`
- `MatchId` remains a plugin-supplied payload field, while `ouro-edge` enriches SteamIDs to user and active-match context on ingest
- use exact semver tags for production rollouts
- treat `latest.json` as an optional convenience pointer, not the source of truth

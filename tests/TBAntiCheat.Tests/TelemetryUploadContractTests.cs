using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using TBAntiCheat.Telemetry;
using Xunit;

namespace TBAntiCheat.Tests;

public sealed class TelemetryUploadContractTests
{
    [Fact]
    public void TelemetryConfigDefaultsUseProductionEdgeRoute()
    {
        TelemetryConfigData config = new();

        Assert.Equal("https://www.ouro.is/edge/", config.BaseUrl);
        Assert.Equal("/api/cs2/observations", config.RelativePath);
        Assert.Equal(string.Empty, config.BearerToken);
    }

    [Fact]
    public void CreateUploadRequestUsesBearerAuthorization()
    {
        TelemetryConfigData config = new()
        {
            BaseUrl = "https://www.ouro.is/edge/",
            RelativePath = "/api/cs2/observations",
            BearerToken = "secret-token",
        };

        using HttpRequestMessage request = TelemetryRequestFactory.CreateUploadRequest(
            config,
            "{\"PluginName\":\"TB Anti-Cheat\"}"
        );

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(
            "https://www.ouro.is/edge/api/cs2/observations",
            request.RequestUri?.ToString()
        );
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("secret-token", request.Headers.Authorization?.Parameter);
        Assert.Equal("application/json", request.Content?.Headers.ContentType?.MediaType);
    }

    [Fact]
    public void CreateUploadRequestWithExplicitRouteSelectsObservationsOrEconomySummaryPath()
    {
        TelemetryConfigData config = new()
        {
            BaseUrl = "https://www.ouro.is/edge/",
            RelativePath = "/api/cs2/observations",
        };

        using HttpRequestMessage obs = TelemetryRequestFactory.CreateUploadRequest(
            config,
            "{}",
            TelemetryUploadRoutes.Observations
        );
        Assert.Equal("https://www.ouro.is/edge/api/cs2/observations", obs.RequestUri?.ToString());

        using HttpRequestMessage economy = TelemetryRequestFactory.CreateUploadRequest(
            config,
            "{}",
            TelemetryUploadRoutes.MatchEconomySummary
        );
        Assert.Equal("https://www.ouro.is/edge/api/cs2/match-economy-summary", economy.RequestUri?.ToString());
    }

    [Fact]
    public void BuildRequestUriOverrideIgnoresConfigRelativePathWhenProvided()
    {
        TelemetryConfigData config = new()
        {
            BaseUrl = "https://www.ouro.is/edge/",
            RelativePath = "/api/cs2/observations",
        };

        Uri uri = TelemetryRequestFactory.BuildRequestUri(config, TelemetryUploadRoutes.MatchEconomySummary);

        Assert.Equal("https://www.ouro.is/edge/api/cs2/match-economy-summary", uri.ToString());
    }

    [Fact]
    public void OuroRecordStartPayloadParserRejectsInvalidBase64()
    {
        bool ok = OuroTelemetryRecordCommands.TryDecodeRecordStartPayload("@@@", out TelemetryMatchSession? session, out string? error);

        Assert.False(ok);
        Assert.Null(session);
        Assert.NotNull(error);
    }

    [Fact]
    public void OuroRecordStartPayloadParserAcceptsValidBase64Json()
    {
        const string json = """
            {"matchId":"m1","matchSource":"ouro","serverId":"s1","serverLabel":"l","serverRegion":"eu","mapName":"de_dust2","team1":[{"steamId64":"76561198850110308","name":"a"}],"team2":[{"steamId64":"76561198179279779","name":"b"}]}
            """;
        string b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json.Trim()));

        bool ok = OuroTelemetryRecordCommands.TryDecodeRecordStartPayload(b64, out TelemetryMatchSession? session, out string? error);

        Assert.True(ok);
        Assert.NotNull(session);
        Assert.Null(error);
        Assert.Equal("m1", session!.MatchId);
    }

    [Fact]
    public void TelemetryLiveIdentityPrefersRosterSteamId64ForMatchedHumanName()
    {
        TelemetryMatchSession session = new()
        {
            MatchId = "m1",
            MatchSource = "ouro",
            ServerId = "s1",
            ServerLabel = "label",
            ServerRegion = "eu-central",
            MapName = "de_mirage",
            Team1 =
            [
                new TelemetryRosterPlayer
                {
                    SteamId64 = "76561198850110308",
                    Name = "PlayerOne",
                },
            ],
        };

        Assert.False(
            TelemetryLiveIdentity.TryResolveCanonicalSteamId64(session, "PlayerOne", isBot: true, out string _)
        );
        Assert.False(
            TelemetryLiveIdentity.TryResolveCanonicalSteamId64(session, "not-on-roster", isBot: false, out string _)
        );
        Assert.True(
            TelemetryLiveIdentity.TryResolveCanonicalSteamId64(session, "playerone", isBot: false, out string steamId)
        );
        Assert.Equal("76561198850110308", steamId);
    }

    [Fact]
    public void TelemetryLiveIdentityPrefersDirectSteamId64MatchBeforeNameFallback()
    {
        TelemetryMatchSession session = new()
        {
            MatchId = "m1",
            MatchSource = "ouro",
            ServerId = "s1",
            ServerLabel = "label",
            ServerRegion = "eu-central",
            MapName = "de_mirage",
            Team1 =
            [
                new TelemetryRosterPlayer
                {
                    SteamId64 = "76561198850110308",
                    Name = "PlayerOne",
                },
            ],
            Team2 =
            [
                new TelemetryRosterPlayer
                {
                    SteamId64 = "76561198179279779",
                    Name = "PlayerTwo",
                },
            ],
        };

        Assert.True(
            TelemetryLiveIdentity.TryResolveCanonicalSteamId64(
                session,
                playerName: "wrong-name",
                steamId64: "76561198179279779",
                isBot: false,
                out string steamId
            )
        );
        Assert.Equal("76561198179279779", steamId);
    }

    [Fact]
    public void TelemetryLiveIdentityFallsBackToNameWhenSteamId64IsUnavailable()
    {
        TelemetryMatchSession session = new()
        {
            MatchId = "m1",
            MatchSource = "ouro",
            ServerId = "s1",
            ServerLabel = "label",
            ServerRegion = "eu-central",
            MapName = "de_mirage",
            Team1 =
            [
                new TelemetryRosterPlayer
                {
                    SteamId64 = "76561198850110308",
                    Name = "PlayerOne",
                },
            ],
            Team2 =
            [
                new TelemetryRosterPlayer
                {
                    SteamId64 = "76561198179279779",
                    Name = "PlayerTwo",
                },
            ],
        };

        Assert.True(
            TelemetryLiveIdentity.TryResolveCanonicalSteamId64(
                session,
                playerName: " playertwo ",
                steamId64: "STEAM_1:0:123",
                isBot: false,
                out string steamId
            )
        );
        Assert.Equal("76561198179279779", steamId);
    }

    [Fact]
    public void TelemetryLiveIdentityMatchesCanonicalRosterSteamId64Only()
    {
        TelemetryMatchSession session = new()
        {
            MatchId = "m1",
            MatchSource = "ouro",
            ServerId = "s1",
            ServerLabel = "label",
            ServerRegion = "eu-central",
            MapName = "de_mirage",
            Team1 =
            [
                new TelemetryRosterPlayer
                {
                    SteamId64 = "76561198850110308",
                    Name = "PlayerOne",
                },
            ],
            Team2 =
            [
                new TelemetryRosterPlayer
                {
                    SteamId64 = "76561198179279779",
                    Name = "PlayerTwo",
                },
            ],
        };

        Assert.True(TelemetryLiveIdentity.MatchesRosterSteamId64(session, "76561198850110308"));
        Assert.False(TelemetryLiveIdentity.MatchesRosterSteamId64(session, "STEAM_1:0:123"));
        Assert.False(TelemetryLiveIdentity.MatchesRosterSteamId64(session, ""));
    }

    [Fact]
    public void TelemetryRecordingWindowPolicyPreservesOrClearsActiveSessionAsRequested()
    {
        TelemetryMatchSession session = new()
        {
            MatchId = "m1",
            MatchSource = "ouro",
            ServerId = "s1",
            ServerLabel = "label",
            ServerRegion = "eu-central",
            MapName = "de_mirage",
            Team1 =
            [
                new TelemetryRosterPlayer
                {
                    SteamId64 = "76561198850110308",
                    Name = "PlayerOne",
                },
            ],
            Team2 =
            [
                new TelemetryRosterPlayer
                {
                    SteamId64 = "76561198179279779",
                    Name = "PlayerTwo",
                },
            ],
        };

        Assert.Same(
            session,
            TelemetryRecordingWindowPolicy.ResolveActiveSessionAfterBufferReset(
                session,
                preserveActiveSession: true
            )
        );
        Assert.Null(
            TelemetryRecordingWindowPolicy.ResolveActiveSessionAfterBufferReset(
                session,
                preserveActiveSession: false
            )
        );
    }

    [Fact]
    public void TelemetryRecordingWindowPolicyAllowsCollectionDuringActiveSessionWithoutTwoHumans()
    {
        TelemetryMatchSession session = new()
        {
            MatchId = "m1",
            MatchSource = "ouro",
            ServerId = "s1",
            ServerLabel = "label",
            ServerRegion = "eu-central",
            MapName = "de_mirage",
        };

        Assert.True(
            TelemetryRecordingWindowPolicy.CanCollectLiveTelemetry(
                collectionEnabled: true,
                activeSession: session
            )
        );
        Assert.False(
            TelemetryRecordingWindowPolicy.CanCollectLiveTelemetry(
                collectionEnabled: false,
                activeSession: session
            )
        );
        Assert.False(
            TelemetryRecordingWindowPolicy.CanCollectLiveTelemetry(
                collectionEnabled: true,
                activeSession: null
            )
        );
    }

    [Fact]
    public void TelemetryRecordingWindowPolicyRequiresTwoHumansOnlyForFlush()
    {
        TelemetryMatchSession session = new()
        {
            MatchId = "m1",
            MatchSource = "ouro",
            ServerId = "s1",
            ServerLabel = "label",
            ServerRegion = "eu-central",
            MapName = "de_mirage",
        };

        Assert.False(
            TelemetryRecordingWindowPolicy.CanFlushLiveTelemetry(
                collectionEnabled: true,
                activeSession: session,
                rosterMatchedHumansPresent: 1
            )
        );
        Assert.True(
            TelemetryRecordingWindowPolicy.CanFlushLiveTelemetry(
                collectionEnabled: true,
                activeSession: session,
                rosterMatchedHumansPresent: 2
            )
        );
    }

    [Fact]
    public void TelemetryLiveBatchPolicyRejectsPresenceOnlyActivity()
    {
        TelemetryLiveActivityCounters[] players =
        [
            new TelemetryLiveActivityCounters
            {
                Connects = 1,
                Disconnects = 1,
                RoundsPlayed = 1,
            },
        ];

        bool hasMeaningfulActivity = TelemetryLiveBatchPolicy.HasMeaningfulHumanActivity(
            players,
            observationCount: 0,
            economyEventCount: 0,
            economySnapshotCount: 0,
            bombPlants: 0,
            bombDefuses: 0
        );

        Assert.False(hasMeaningfulActivity);
    }

    [Fact]
    public void TelemetryLiveBatchPolicyAcceptsCombatEconomyAndBombSignals()
    {
        TelemetryLiveActivityCounters[] combatPlayers =
        [
            new TelemetryLiveActivityCounters
            {
                ShotsFired = 3,
            },
        ];

        Assert.True(
            TelemetryLiveBatchPolicy.HasMeaningfulHumanActivity(
                combatPlayers,
                observationCount: 0,
                economyEventCount: 0,
                economySnapshotCount: 0,
                bombPlants: 0,
                bombDefuses: 0
            )
        );
        Assert.True(
            TelemetryLiveBatchPolicy.HasMeaningfulHumanActivity(
                [],
                observationCount: 0,
                economyEventCount: 1,
                economySnapshotCount: 0,
                bombPlants: 0,
                bombDefuses: 0
            )
        );
        Assert.True(
            TelemetryLiveBatchPolicy.HasMeaningfulHumanActivity(
                [],
                observationCount: 0,
                economyEventCount: 0,
                economySnapshotCount: 0,
                bombPlants: 1,
                bombDefuses: 0
            )
        );
    }

    [Fact]
    public void TelemetryConfigDefaultReportingIntervalIs120Seconds()
    {
        TelemetryConfigData config = new();

        Assert.Equal(120, config.ReportingIntervalSeconds);
    }

    [Fact]
    public void TelemetryMatchSessionDeserializesCamelCasePayloadAndResolvesRosterPlayer()
    {
        const string payload = """
            {
              "matchId": "edge-match-id",
              "matchSource": "ouro",
              "serverId": "server-id",
              "serverLabel": "frankfurt-01",
              "serverRegion": "eu-central",
              "mapName": "de_mirage",
              "reportingIntervalSeconds": 120,
              "team1": [
                {
                  "steamId64": "76561198850110308",
                  "name": "chiarezza",
                  "userId": "bffa7765-5419-4298-87c0-61ff23282d9b"
                }
              ],
              "team2": [
                {
                  "steamId64": "76561198179279779",
                  "name": "Hubert5002"
                }
              ]
            }
            """;

        TelemetryMatchSession? session = JsonSerializer.Deserialize<TelemetryMatchSession>(payload);

        Assert.NotNull(session);
        Assert.Equal("edge-match-id", session!.MatchId);
        Assert.Equal(120, session.ReportingIntervalSeconds);
        Assert.Single(session.Team1);
        Assert.Single(session.Team2);
        Assert.True(session.TryResolveRosterPlayer("hubert5002", out TelemetryRosterPlayer? rosterPlayer));
        Assert.Equal("76561198179279779", rosterPlayer!.SteamId64);
        Assert.True(session.IsValid(out string? reason));
        Assert.Null(reason);
    }

    [Fact]
    public void TelemetryMatchSessionAllowsEmptyMapNameWhenOtherFieldsAreValid()
    {
        TelemetryMatchSession session = new()
        {
            MatchId = "m1",
            MatchSource = "ouro",
            ServerId = "s1",
            ServerLabel = "label",
            ServerRegion = "eu-central",
            MapName = "",
            ReportingIntervalSeconds = 120,
            Team1 =
            [
                new TelemetryRosterPlayer
                {
                    SteamId64 = "76561198850110308",
                    Name = "a",
                },
            ],
            Team2 =
            [
                new TelemetryRosterPlayer
                {
                    SteamId64 = "76561198179279779",
                    Name = "b",
                },
            ],
        };

        Assert.True(session.IsValid(out string? reason));
        Assert.Null(reason);
    }

    [Fact]
    public void OuroRecordStartPayloadParserAcceptsValidBase64JsonWithEmptyMapName()
    {
        const string json = """
            {"matchId":"m1","matchSource":"ouro","serverId":"s1","serverLabel":"l","serverRegion":"eu","mapName":"","team1":[{"steamId64":"76561198850110308","name":"a"}],"team2":[{"steamId64":"76561198179279779","name":"b"}]}
            """;
        string b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json.Trim()));

        bool ok = OuroTelemetryRecordCommands.TryDecodeRecordStartPayload(b64, out TelemetryMatchSession? session, out string? error);

        Assert.True(ok);
        Assert.NotNull(session);
        Assert.Null(error);
        Assert.True(session!.IsValid(out string? reason));
        Assert.Null(reason);
    }

    [Fact]
    public void TelemetryMatchSessionValidationFailsWhenRosterHasFewerThanTwoPlayers()
    {
        TelemetryMatchSession empty = new();
        Assert.False(empty.IsValid(out string? errEmpty));
        Assert.NotNull(errEmpty);

        TelemetryMatchSession onePlayer = new()
        {
            MatchId = "m1",
            MatchSource = "ouro",
            ServerId = "s1",
            ServerLabel = "label",
            ServerRegion = "eu-central",
            MapName = "de_mirage",
            Team1 =
            [
                new TelemetryRosterPlayer
                {
                    SteamId64 = "76561198850110308",
                    Name = "solo",
                },
            ],
        };
        Assert.False(onePlayer.IsValid(out string? errOne));
        Assert.NotNull(errOne);
    }

    [Fact]
    public void TelemetryMatchSessionValidationFailsForMissingMetadataOrInvalidRosterEntry()
    {
        TelemetryMatchSession session = new()
        {
            MatchId = "m1",
            MatchSource = "ouro",
            ServerId = "s1",
            ServerLabel = "",
            ServerRegion = "eu-central",
            MapName = "de_mirage",
            Team1 =
            [
                new TelemetryRosterPlayer
                {
                    SteamId64 = "",
                    Name = "a",
                },
            ],
            Team2 =
            [
                new TelemetryRosterPlayer
                {
                    SteamId64 = "76561198179279779",
                    Name = "b",
                },
            ],
        };

        Assert.False(session.IsValid(out string? reason));
        Assert.NotNull(reason);
    }

    [Fact]
    public void TelemetryMatchSessionValidationFailsForDuplicateNormalizedRosterNames()
    {
        TelemetryMatchSession session = new()
        {
            MatchId = "m1",
            MatchSource = "ouro",
            ServerId = "s1",
            ServerLabel = "label",
            ServerRegion = "eu-central",
            MapName = "de_mirage",
            Team1 =
            [
                new TelemetryRosterPlayer
                {
                    SteamId64 = "76561198850110308",
                    Name = " Alpha ",
                },
            ],
            Team2 =
            [
                new TelemetryRosterPlayer
                {
                    SteamId64 = "76561198179279779",
                    Name = "alpha",
                },
            ],
        };

        Assert.False(session.IsValid(out string? reason));
        Assert.NotNull(reason);
    }

    [Fact]
    public void MatchEconomySummaryBuildCanRepresentTeamRoundWithSpentTotal()
    {
        TelemetryMatchSession session = new()
        {
            MatchId = "m1",
            MatchSource = "ouro",
            ServerId = "s1",
            ServerLabel = "label",
            ServerRegion = "eu-central",
            MapName = "de_mirage",
            Team1 =
            [
                new TelemetryRosterPlayer
                {
                    SteamId64 = "76561198850110308",
                    Name = "a",
                },
            ],
            Team2 =
            [
                new TelemetryRosterPlayer
                {
                    SteamId64 = "76561198179279779",
                    Name = "b",
                },
            ],
        };

        MatchEconomySummary summary = TelemetryEconomySummaryBuilder.Build(
            pluginVersion: "0.0.0",
            session: session,
            economyEvents: [],
            economySnapshots: []
        );
        summary.Teams.Add(
            new TeamEconomySummary
            {
                Team = 2,
                Rounds =
                [
                    new TeamEconomyRoundSummary
                    {
                        RoundNumber = 1,
                        StartBudgetTotal = 16800,
                        EndBudgetTotal = 7950,
                        SpentTotal = 8850,
                    },
                ],
            }
        );

        Assert.Equal("m1", summary.MatchId);
        Assert.Equal("de_mirage", summary.MapName);
        Assert.Single(summary.Teams);
        Assert.Single(summary.Teams[0].Rounds);
        Assert.Equal(8850, summary.Teams[0].Rounds[0].SpentTotal);
    }

    [Fact]
    public void TelemetryEconomySummaryBuilderOrdersPurchasesByTickAndAggregatesTeamRoundTotals()
    {
        TelemetryMatchSession session = new()
        {
            MatchId = "m1",
            MatchSource = "ouro",
            ServerId = "s1",
            ServerLabel = "label",
            ServerRegion = "eu-central",
            MapName = "de_mirage",
            Team1 =
            [
                new TelemetryRosterPlayer
                {
                    SteamId64 = "76561198850110308",
                    Name = "Alice",
                },
            ],
            Team2 =
            [
                new TelemetryRosterPlayer
                {
                    SteamId64 = "76561198179279779",
                    Name = "Bob",
                },
            ],
        };

        DateTime t0 = DateTime.UtcNow;
        EconomyEvent[] events =
        [
            new EconomyEvent
            {
                SteamID = "76561198850110308",
                PlayerName = "Alice",
                Slot = 1,
                Team = 3,
                EventType = "purchase",
                Item = "weapon_ak47",
                RoundNumber = 1,
                ServerTick = 200,
                ObservedAtUtc = t0.AddMilliseconds(2),
                MoneyBefore = 4400,
                MoneyAfter = 200,
            },
            new EconomyEvent
            {
                SteamID = "76561198850110308",
                PlayerName = "Alice",
                Slot = 1,
                Team = 3,
                EventType = "purchase",
                Item = "weapon_glock",
                RoundNumber = 1,
                ServerTick = 100,
                ObservedAtUtc = t0.AddMilliseconds(1),
                MoneyBefore = 800,
                MoneyAfter = 400,
            },
            new EconomyEvent
            {
                SteamID = "76561198179279779",
                PlayerName = "Bob",
                Slot = 2,
                Team = 2,
                EventType = "purchase",
                Item = "weapon_galilar",
                RoundNumber = 1,
                ServerTick = 150,
                ObservedAtUtc = t0,
                MoneyBefore = 800,
                MoneyAfter = 0,
            },
        ];

        EconomySnapshot[] snapshots =
        [
            new EconomySnapshot
            {
                SteamID = "76561198850110308",
                PlayerName = "Alice",
                Slot = 1,
                Team = 3,
                SnapshotKind = "round_freeze_end",
                RoundNumber = 1,
                ServerTick = 50,
                ObservedAtUtc = t0.AddTicks(-1),
                Money = 800,
                StartAccount = 800,
                CashSpentThisRound = 0,
            },
            new EconomySnapshot
            {
                SteamID = "76561198850110308",
                PlayerName = "Alice",
                Slot = 1,
                Team = 3,
                SnapshotKind = "round_end",
                RoundNumber = 1,
                ServerTick = 300,
                ObservedAtUtc = t0.AddSeconds(1),
                Money = 200,
                StartAccount = 800,
                CashSpentThisRound = 4200,
            },
            new EconomySnapshot
            {
                SteamID = "76561198179279779",
                PlayerName = "Bob",
                Slot = 2,
                Team = 2,
                SnapshotKind = "round_end",
                RoundNumber = 1,
                ServerTick = 160,
                ObservedAtUtc = t0.AddMilliseconds(5),
                Money = 0,
                StartAccount = 800,
                CashSpentThisRound = 800,
            },
        ];

        MatchEconomySummary summary = TelemetryEconomySummaryBuilder.Build(
            "1.0.0",
            session,
            events,
            snapshots
        );

        Assert.Equal(2, summary.Players.Count);
        PlayerEconomySummary? alice = summary.Players.FirstOrDefault(p => p.SteamID == "76561198850110308");
        Assert.NotNull(alice);
        Assert.Equal(3, alice!.Team);
        Assert.Single(alice.Rounds);
        Assert.Equal(1, alice.Rounds[0].RoundNumber);
        Assert.Equal(800, alice.Rounds[0].StartMoney);
        Assert.Equal(200, alice.Rounds[0].EndMoney);
        Assert.Equal(4600, alice.Rounds[0].SpentTotal);
        Assert.Equal(2, alice.Rounds[0].Purchases.Count);
        Assert.Equal("weapon_glock", alice.Rounds[0].Purchases[0].Item);
        Assert.Equal("weapon_ak47", alice.Rounds[0].Purchases[1].Item);

        PlayerEconomySummary? bob = summary.Players.FirstOrDefault(p => p.SteamID == "76561198179279779");
        Assert.NotNull(bob);
        Assert.Equal(800, bob!.Rounds[0].SpentTotal);

        TeamEconomySummary? team3 = summary.Teams.FirstOrDefault(t => t.Team == 3);
        TeamEconomySummary? team2 = summary.Teams.FirstOrDefault(t => t.Team == 2);
        Assert.NotNull(team3);
        Assert.NotNull(team2);
        Assert.Equal(800, team3!.Rounds[0].StartBudgetTotal);
        Assert.Equal(200, team3.Rounds[0].EndBudgetTotal);
        Assert.Equal(4600, team3.Rounds[0].SpentTotal);
        Assert.Equal(800, team2!.Rounds[0].StartBudgetTotal);
        Assert.Equal(0, team2.Rounds[0].EndBudgetTotal);
        Assert.Equal(800, team2.Rounds[0].SpentTotal);
    }

    [Fact]
    public void TelemetryEconomySummaryBuilderAggregatesTeamTotalsUsingRoundSpecificSideAfterSideSwitch()
    {
        TelemetryMatchSession session = new()
        {
            MatchId = "m1",
            MatchSource = "ouro",
            ServerId = "s1",
            ServerLabel = "label",
            ServerRegion = "eu-central",
            MapName = "de_mirage",
            Team1 =
            [
                new TelemetryRosterPlayer
                {
                    SteamId64 = "76561198850110308",
                    Name = "Switcher",
                },
            ],
            Team2 =
            [
                new TelemetryRosterPlayer
                {
                    SteamId64 = "76561198179279779",
                    Name = "Anchor",
                },
            ],
        };

        DateTime t0 = DateTime.UtcNow;
        EconomySnapshot[] snapshots =
        [
            new EconomySnapshot
            {
                SteamID = "76561198850110308",
                PlayerName = "Switcher",
                Slot = 1,
                Team = 2,
                SnapshotKind = "round_end",
                RoundNumber = 1,
                ServerTick = 100,
                ObservedAtUtc = t0,
                Money = 200,
                StartAccount = 800,
                CashSpentThisRound = 600,
            },
            new EconomySnapshot
            {
                SteamID = "76561198850110308",
                PlayerName = "Switcher",
                Slot = 1,
                Team = 3,
                SnapshotKind = "round_end",
                RoundNumber = 13,
                ServerTick = 1300,
                ObservedAtUtc = t0.AddMinutes(1),
                Money = 500,
                StartAccount = 1000,
                CashSpentThisRound = 500,
            },
            new EconomySnapshot
            {
                SteamID = "76561198179279779",
                PlayerName = "Anchor",
                Slot = 2,
                Team = 3,
                SnapshotKind = "round_end",
                RoundNumber = 1,
                ServerTick = 110,
                ObservedAtUtc = t0.AddSeconds(1),
                Money = 100,
                StartAccount = 800,
                CashSpentThisRound = 700,
            },
        ];

        MatchEconomySummary summary = TelemetryEconomySummaryBuilder.Build(
            "1.0.0",
            session,
            [],
            snapshots
        );

        TeamEconomySummary? roundOneTeam2 = summary.Teams.FirstOrDefault(t => t.Team == 2);
        TeamEconomySummary? roundOneTeam3 = summary.Teams.FirstOrDefault(t => t.Team == 3);

        Assert.NotNull(roundOneTeam2);
        Assert.NotNull(roundOneTeam3);
        Assert.Equal(800, roundOneTeam2!.Rounds.Single(r => r.RoundNumber == 1).StartBudgetTotal);
        Assert.Equal(200, roundOneTeam2.Rounds.Single(r => r.RoundNumber == 1).EndBudgetTotal);
        Assert.Equal(600, roundOneTeam2.Rounds.Single(r => r.RoundNumber == 1).SpentTotal);
        Assert.Equal(800, roundOneTeam3!.Rounds.Single(r => r.RoundNumber == 1).StartBudgetTotal);
        Assert.Equal(100, roundOneTeam3.Rounds.Single(r => r.RoundNumber == 1).EndBudgetTotal);
        Assert.Equal(700, roundOneTeam3.Rounds.Single(r => r.RoundNumber == 1).SpentTotal);
        Assert.Equal(1000, roundOneTeam3.Rounds.Single(r => r.RoundNumber == 13).StartBudgetTotal);
        Assert.Equal(500, roundOneTeam3.Rounds.Single(r => r.RoundNumber == 13).EndBudgetTotal);
    }

    [Fact]
    public void TelemetryEconomySummaryBuilderIgnoresNonRosterEconomyRows()
    {
        TelemetryMatchSession session = new()
        {
            MatchId = "m1",
            MatchSource = "ouro",
            ServerId = "s1",
            ServerLabel = "label",
            ServerRegion = "eu-central",
            MapName = "de_mirage",
            Team1 =
            [
                new TelemetryRosterPlayer
                {
                    SteamId64 = "76561198850110308",
                    Name = "a",
                },
            ],
            Team2 =
            [
                new TelemetryRosterPlayer
                {
                    SteamId64 = "76561198179279779",
                    Name = "b",
                },
            ],
        };

        EconomyEvent[] events =
        [
            new EconomyEvent
            {
                SteamID = "99999999999999999",
                PlayerName = "intruder",
                Team = 2,
                EventType = "purchase",
                Item = "weapon_awp",
                RoundNumber = 1,
                ServerTick = 1,
                ObservedAtUtc = DateTime.UtcNow,
                MoneyBefore = 4750,
                MoneyAfter = 2550,
            },
        ];

        MatchEconomySummary summary = TelemetryEconomySummaryBuilder.Build("1.0.0", session, events, []);

        Assert.Empty(summary.Players);
        Assert.Empty(summary.Teams);
    }

    [Fact]
    public void MatchEconomySummarySerializesToMatchEconomySummaryRouteShape()
    {
        TelemetryMatchSession session = new()
        {
            MatchId = "edge-match-id",
            MatchSource = "ouro",
            ServerId = "server-id",
            ServerLabel = "frankfurt-01",
            ServerRegion = "eu-central",
            MapName = "de_mirage",
            Team1 =
            [
                new TelemetryRosterPlayer { SteamId64 = "76561198850110308", Name = "p1" },
            ],
            Team2 =
            [
                new TelemetryRosterPlayer { SteamId64 = "76561198179279779", Name = "p2" },
            ],
        };

        MatchEconomySummary summary = TelemetryEconomySummaryBuilder.Build("0.4.1", session, [], []);
        summary.MapName = "de_mirage";

        TelemetryConfigData config = new()
        {
            BaseUrl = "https://www.ouro.is/edge/",
            RelativePath = "/api/cs2/observations",
            BearerToken = "t",
        };

        string json = JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true });
        using HttpRequestMessage request = TelemetryRequestFactory.CreateUploadRequest(
            config,
            json,
            TelemetryUploadRoutes.MatchEconomySummary
        );

        Assert.Equal("https://www.ouro.is/edge/api/cs2/match-economy-summary", request.RequestUri?.ToString());
        Assert.Contains("edge-match-id", json, StringComparison.Ordinal);
        Assert.Contains("PluginName", json, StringComparison.Ordinal);
    }

    [Fact]
    public void TelemetryManagerUsesBoundedTimeoutForFinalMatchEconomySummaryUpload()
    {
        Type? telemetryManagerType = typeof(TelemetryUploadRoutes).Assembly.GetType("TBAntiCheat.Telemetry.TelemetryManager");
        Assert.NotNull(telemetryManagerType);

        FieldInfo? timeoutField = telemetryManagerType!.GetField(
            "finalMatchEconomySummaryUploadTimeout",
            BindingFlags.NonPublic | BindingFlags.Static
        );

        Assert.NotNull(timeoutField);

        object? rawValue = timeoutField!.GetValue(null);
        Assert.NotNull(rawValue);

        TimeSpan timeout = Assert.IsType<TimeSpan>(rawValue);
        Assert.True(timeout > TimeSpan.Zero);
        Assert.True(timeout <= TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void TelemetryManager_OnRoundEnd_TakesWinningTeamIndexAndReplacesParameterlessOverload()
    {
        Type telemetryManagerType = typeof(TelemetryManager);

        MethodInfo? withWinner = telemetryManagerType.GetMethod(
            "OnRoundEnd",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(int)],
            modifiers: null
        );

        Assert.NotNull(withWinner);

        MethodInfo? parameterless = telemetryManagerType.GetMethod(
            "OnRoundEnd",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null
        );

        Assert.Null(parameterless);
    }

    [Fact]
    public void ResolveSlotForMatchSignalSteamId_UsesTelemetryStateWhenPlayerIsKnown()
    {
        const string canonicalSteamId = "76561198850110308";
        Type telemetryManagerType = typeof(TelemetryManager);

        FieldInfo? playerStatesField = telemetryManagerType.GetField(
            "playerStates",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        Assert.NotNull(playerStatesField);

        object? originalStates = playerStatesField!.GetValue(null);

        try
        {
            PlayerTelemetryState[] states = new PlayerTelemetryState[8];
            states[7] = new PlayerTelemetryState
            {
                Slot = 7,
                SteamID = canonicalSteamId,
                PlayerName = "Alpha",
                IsBot = false,
            };
            playerStatesField.SetValue(null, states);

            Assert.Equal(7, TelemetryManager.ResolveSlotForMatchSignalSteamId(canonicalSteamId));
            Assert.Equal(0, TelemetryManager.ResolveSlotForMatchSignalSteamId("76561199999999999"));
        }
        finally
        {
            playerStatesField.SetValue(
                null,
                originalStates ?? Array.Empty<PlayerTelemetryState>()
            );
        }
    }

    [Fact]
    public void TelemetryMatchSignalTracker_IsRecreatedWhenResetStateDiscardsSession()
    {
        Type? telemetryManagerType = typeof(TelemetryUploadRoutes).Assembly.GetType("TBAntiCheat.Telemetry.TelemetryManager");
        Assert.NotNull(telemetryManagerType);

        FieldInfo? trackerField = telemetryManagerType.GetField(
            "matchSignalTracker",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        Assert.NotNull(trackerField);

        MethodInfo? resetState = telemetryManagerType.GetMethod(
            "ResetState",
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            types: [typeof(string), typeof(bool)],
            modifiers: null
        );
        Assert.NotNull(resetState);

        object? trackerBefore = trackerField!.GetValue(null);
        Assert.NotNull(trackerBefore);

        _ = resetState!.Invoke(null, ["de_dust2", false]);
        object? trackerAfterDiscard = trackerField.GetValue(null);
        Assert.NotNull(trackerAfterDiscard);
        Assert.NotSame(trackerBefore, trackerAfterDiscard);

        _ = resetState.Invoke(null, ["de_mirage", true]);
        object? trackerAfterPreserve = trackerField.GetValue(null);
        Assert.NotNull(trackerAfterPreserve);
        Assert.Same(trackerAfterDiscard, trackerAfterPreserve);
    }

    [Fact]
    public void GetOpposingRecordedRosterSize_ReturnsOtherTeamCountForCanonicalSteamId()
    {
        TelemetryMatchSession session = new()
        {
            MatchId = "match-1",
            MatchSource = "test",
            ServerId = "server-1",
            ServerLabel = "label",
            ServerRegion = "eu",
            Team1 =
            [
                new TelemetryRosterPlayer { SteamId64 = "76561198850110308", Name = "Alpha" },
                new TelemetryRosterPlayer { SteamId64 = "76561198850110309", Name = "Beta" },
            ],
            Team2 =
            [
                new TelemetryRosterPlayer { SteamId64 = "76561198179279779", Name = "Gamma" },
            ],
        };

        Assert.Equal(1, TelemetryManager.GetOpposingRecordedRosterSize(session, "76561198850110308"));
        Assert.Equal(2, TelemetryManager.GetOpposingRecordedRosterSize(session, "76561198179279779"));
    }

    [Fact]
    public void GetOpposingRecordedRosterSize_ReturnsZeroWhenSessionOrSteamIdIsUnknown()
    {
        TelemetryMatchSession session = new()
        {
            MatchId = "match-1",
            MatchSource = "test",
            ServerId = "server-1",
            ServerLabel = "label",
            ServerRegion = "eu",
            Team1 = [new TelemetryRosterPlayer { SteamId64 = "76561198850110308", Name = "Alpha" }],
            Team2 = [],
        };

        Assert.Equal(0, TelemetryManager.GetOpposingRecordedRosterSize(null, "76561198850110308"));
        Assert.Equal(0, TelemetryManager.GetOpposingRecordedRosterSize(session, ""));
        Assert.Equal(0, TelemetryManager.GetOpposingRecordedRosterSize(session, "99999999999999999"));
    }

    [Theory]
    [InlineData(1, 1, true)]
    [InlineData(2, 1, true)]
    [InlineData(1, 0, false)]
    public void ShouldTreatJoinAsReconnect_HandlesMatchStartPresentPlayers(
        int connects,
        int disconnects,
        bool expected
    )
    {
        bool reconnectAfterDisconnect = TelemetryManager.ShouldTreatJoinAsReconnect(connects, disconnects);

        Assert.Equal(expected, reconnectAfterDisconnect);
    }
}

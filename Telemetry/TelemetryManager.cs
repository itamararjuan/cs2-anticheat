using System.Globalization;
using System.Text;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using TBAntiCheat.Core;
using TBAntiCheat.Handlers;

namespace TBAntiCheat.Telemetry
{
    internal sealed class WeaponTelemetryState
    {
        internal required string Weapon { get; init; }
        internal required string WeaponFamily { get; init; }

        internal int ShotsFired { get; set; }
        internal int HitsLanded { get; set; }
        internal int Kills { get; set; }
        internal int DamageDealt { get; set; }

        internal WeaponTelemetrySnapshot ToSnapshot()
        {
            return new WeaponTelemetrySnapshot()
            {
                Weapon = Weapon,
                WeaponFamily = WeaponFamily,
                ShotsFired = ShotsFired,
                HitsLanded = HitsLanded,
                Kills = Kills,
                DamageDealt = DamageDealt
            };
        }
    }

    internal sealed class PlayerTelemetryState
    {
        internal required int Slot { get; init; }
        internal bool IsBot { get; set; }
        internal string SteamID { get; set; } = string.Empty;
        internal string PlayerName { get; set; } = string.Empty;

        internal int Connects { get; set; }
        internal int Disconnects { get; set; }
        internal int RoundsPlayed { get; set; }
        internal int ShotsFired { get; set; }
        internal int HitsLanded { get; set; }
        internal int BulletImpacts { get; set; }
        internal int DamageDealt { get; set; }
        internal int DamageTaken { get; set; }
        internal int UtilityDamageDealt { get; set; }
        internal int UtilityDamageTaken { get; set; }
        internal int Kills { get; set; }
        internal int Deaths { get; set; }
        internal int Headshots { get; set; }
        internal int KillsWhileBlind { get; set; }
        internal int DamageWhileBlind { get; set; }
        internal int BlindsReceived { get; set; }
        internal double BlindDurationSeconds { get; set; }
        internal int FlashbangsThrown { get; set; }
        internal int SmokesThrown { get; set; }
        internal int MolotovsThrown { get; set; }
        internal int Footsteps { get; set; }
        internal int Sounds { get; set; }
        internal int LastWeaponProfileObservationKillCount { get; set; }
        internal int LastUtilityProfileObservationBucket { get; set; }
        internal int LastBlindKillObservationCount { get; set; }
        internal int LastZeroUtilityObservationKillCount { get; set; }
        internal int LastKnownMoneyAccount { get; set; } = -1;
        internal int LastKnownCashSpentThisRound { get; set; } = -1;
        internal int LastKnownTotalCashSpent { get; set; } = -1;
        internal int LastKnownEconomyRoundNumber { get; set; } = -1;
        internal Dictionary<string, int> LastWeaponFocusObservationCounts { get; } = new(StringComparer.OrdinalIgnoreCase);
        internal DateTime BlindUntilUtc { get; set; } = DateTime.MinValue;
        internal Queue<DateTime> RecentKills { get; } = new();
        internal Dictionary<string, Queue<DateTime>> RecentKillsByWeapon { get; } = new(StringComparer.OrdinalIgnoreCase);
        internal Dictionary<string, WeaponTelemetryState> Weapons { get; } = new(StringComparer.OrdinalIgnoreCase);

        internal bool HasActivity()
        {
            return Connects > 0 || Disconnects > 0 || RoundsPlayed > 0 || ShotsFired > 0 || HitsLanded > 0 ||
                BulletImpacts > 0 || DamageDealt > 0 || DamageTaken > 0 || UtilityDamageDealt > 0 ||
                UtilityDamageTaken > 0 || Kills > 0 || Deaths > 0 || BlindsReceived > 0 ||
                FlashbangsThrown > 0 || SmokesThrown > 0 || MolotovsThrown > 0 || Footsteps > 0 || Sounds > 0;
        }

        internal bool IsCurrentlyBlind(DateTime nowUtc)
        {
            return BlindUntilUtc > nowUtc;
        }

        internal WeaponTelemetryState GetOrCreateWeapon(string weapon)
        {
            if (Weapons.TryGetValue(weapon, out WeaponTelemetryState? existing))
            {
                return existing;
            }

            WeaponTelemetryState created = new WeaponTelemetryState()
            {
                Weapon = weapon,
                WeaponFamily = TelemetryManager.GetWeaponFamily(weapon)
            };

            Weapons[weapon] = created;
            return created;
        }

        internal int GetKillsByFamily(string family)
        {
            int kills = 0;
            foreach (WeaponTelemetryState weapon in Weapons.Values)
            {
                if (string.Equals(weapon.WeaponFamily, family, StringComparison.OrdinalIgnoreCase))
                {
                    kills += weapon.Kills;
                }
            }

            return kills;
        }

        internal PlayerTelemetrySnapshot ToSnapshot()
        {
            List<WeaponTelemetrySnapshot> weapons = [];
            foreach (WeaponTelemetryState weapon in Weapons.Values.OrderByDescending(x => x.Kills).ThenBy(x => x.Weapon))
            {
                weapons.Add(weapon.ToSnapshot());
            }

            return new PlayerTelemetrySnapshot()
            {
                SteamID = SteamID,
                PlayerName = PlayerName,
                Slot = Slot,
                IsBot = IsBot,
                Connects = Connects,
                Disconnects = Disconnects,
                RoundsPlayed = RoundsPlayed,
                ShotsFired = ShotsFired,
                HitsLanded = HitsLanded,
                BulletImpacts = BulletImpacts,
                DamageDealt = DamageDealt,
                DamageTaken = DamageTaken,
                UtilityDamageDealt = UtilityDamageDealt,
                UtilityDamageTaken = UtilityDamageTaken,
                Kills = Kills,
                Deaths = Deaths,
                Headshots = Headshots,
                KillsWhileBlind = KillsWhileBlind,
                DamageWhileBlind = DamageWhileBlind,
                BlindsReceived = BlindsReceived,
                BlindDurationSeconds = BlindDurationSeconds,
                FlashbangsThrown = FlashbangsThrown,
                SmokesThrown = SmokesThrown,
                MolotovsThrown = MolotovsThrown,
                Footsteps = Footsteps,
                Sounds = Sounds,
                Weapons = weapons
            };
        }
    }

    internal sealed class LiveEconomySample
    {
        internal required string SteamID { get; init; }
        internal required string PlayerName { get; init; }
        internal required int Slot { get; init; }
        internal required int Team { get; init; }
        internal required int Account { get; init; }
        internal required int StartAccount { get; init; }
        internal required int CashSpentThisRound { get; init; }
        internal required int TotalCashSpent { get; init; }
        internal required List<string> InventoryItems { get; init; }
    }

    internal static class TelemetryManager
    {
        private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions()
        {
            WriteIndented = true
        };

        private static readonly HttpClient httpClient = new HttpClient();
        private static readonly TimeSpan finalMatchEconomySummaryUploadTimeout = TimeSpan.FromSeconds(15);
        private static readonly List<ObservationRecord> pendingObservations = [];
        private static readonly List<EconomyEvent> pendingEconomyEvents = [];
        private static readonly List<EconomySnapshot> pendingEconomySnapshots = [];

        /// <summary>
        /// Full-match economy history retained across live flush windows (pending lists are cleared after each upload).
        /// </summary>
        private static readonly List<EconomyEvent> matchEconomyEvents = [];
        private static readonly List<EconomySnapshot> matchEconomySnapshots = [];

        private static PlayerTelemetryState[] playerStates = [];
        private static ACCore? plugin;
        private static string currentMap = string.Empty;
        private static int roundNumber;
        private static int bombPlants;
        private static int bombDefuses;
        private static long batchSequence;
        private static DateTime lastFlushUtc = DateTime.UtcNow;
        private static bool uploadInProgress;
        private static bool loggedUploadDisabled;
        private static bool loggedMissingEndpoint;
        private static TelemetryMatchSession? matchSession;
        private static TelemetryMatchSignalTracker matchSignalTracker = new();

        internal static void Initialize(ACCore core)
        {
            plugin = core;
            TelemetryConfig.Initialize();
            ResetState(string.Empty, preserveActiveSession: false);

            CommandHandler.RegisterCommand(
                "ouro_record_start",
                "Starts Ouro match-scoped telemetry recording",
                Globals.OnOuRoRecordStartCommand
            );
            CommandHandler.RegisterCommand(
                "ouro_record_stop",
                "Stops Ouro match-scoped telemetry recording",
                Globals.OnOuRoRecordStopCommand
            );

            Globals.Log("[TBAC] TelemetryManager Initialized");
        }

        internal static void OnConfigReloaded()
        {
            loggedUploadDisabled = false;
            loggedMissingEndpoint = false;
            Globals.Log("[TBAC] Telemetry config reload applied");
        }

        internal static void HandleOuRoRecordStartCommand(CommandInfo command)
        {
            if (command.ArgCount < 2)
            {
                Globals.Log("[TBAC] ouro_record_start: missing base64 payload");
                return;
            }

            StringBuilder payload = new StringBuilder();
            for (int index = 1; index < command.ArgCount; index++)
            {
                if (index > 1)
                {
                    payload.Append(' ');
                }

                payload.Append(command.ArgByIndex(index));
            }

            if (
                OuroTelemetryRecordCommands.TryDecodeRecordStartPayload(payload.ToString(), out TelemetryMatchSession? session, out string? error) ==
                    false
                || session == null
            )
            {
                Globals.Log($"[TBAC] ouro_record_start: invalid payload -> {error}");
                return;
            }

            BeginMatchRecordingSession(session);
            Globals.Log($"[TBAC] Ouro telemetry recording started for match {session.MatchId}");
        }

        internal static void HandleOuRoRecordStopCommand(CommandInfo _)
        {
            TelemetryMatchSession? session = matchSession;
            if (session == null)
            {
                Globals.Log("[TBAC] ouro_record_stop: no active recording session");
                ResetState(string.Empty, preserveActiveSession: false);
                return;
            }

            List<EconomyEvent> eventsCopy = [.. matchEconomyEvents];
            List<EconomySnapshot> snapshotsCopy = [.. matchEconomySnapshots];
            string mapName = currentMap;
            string pluginVersion = plugin?.ModuleVersion ?? string.Empty;

            // Disable live collection immediately so stop does not trigger any final live flushes.
            matchSession = null;

            Globals.Log("[TBAC] Ouro telemetry recording stopped");
            UploadFinalMatchEconomySummary(
                session,
                eventsCopy,
                snapshotsCopy,
                mapName,
                pluginVersion
            );
            ResetState(string.Empty, preserveActiveSession: false);
        }

        private static void BeginMatchRecordingSession(TelemetryMatchSession session)
        {
            ResetState(session.MapName, preserveActiveSession: false);
            DiscardRecordingBuffers();
            matchSession = session;
            currentMap = session.MapName;
            lastFlushUtc = DateTime.UtcNow;
        }

        private static void DiscardRecordingBuffers()
        {
            playerStates = [];
            pendingObservations.Clear();
            pendingEconomyEvents.Clear();
            pendingEconomySnapshots.Clear();
            matchEconomyEvents.Clear();
            matchEconomySnapshots.Clear();
            roundNumber = 0;
            bombPlants = 0;
            bombDefuses = 0;
        }

        private static bool ShouldEmitLiveTelemetry()
        {
            return TelemetryRecordingWindowPolicy.CanCollectLiveTelemetry(
                TelemetryConfig.Get().Config.CollectionEnabled,
                matchSession
            );
        }

        internal static int GetOpposingRecordedRosterSize(TelemetryMatchSession? session, string steamId64)
        {
            if (session == null || string.IsNullOrWhiteSpace(steamId64))
            {
                return 0;
            }

            foreach (TelemetryRosterPlayer rosterPlayer in session.Team1)
            {
                if (string.Equals(rosterPlayer.SteamId64, steamId64, StringComparison.Ordinal))
                {
                    return session.Team2.Count;
                }
            }

            foreach (TelemetryRosterPlayer rosterPlayer in session.Team2)
            {
                if (string.Equals(rosterPlayer.SteamId64, steamId64, StringComparison.Ordinal))
                {
                    return session.Team1.Count;
                }
            }

            return 0;
        }

        internal static bool ShouldTreatJoinAsReconnect(int connects, int disconnects)
        {
            return disconnects > 0 && connects >= disconnects && connects <= disconnects + 1;
        }

        private static int CountPresentRosterMatchedHumans()
        {
            int count = 0;
            foreach (PlayerData? tracked in Globals.Players)
            {
                if (tracked == null)
                {
                    continue;
                }

                if (TryResolveRosterMatchedHuman(tracked, out string _) == false)
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        private static int GetLiveReportingIntervalSeconds()
        {
            TelemetryConfigData config = TelemetryConfig.Get().Config;
            int interval = matchSession?.ReportingIntervalSeconds ?? config.ReportingIntervalSeconds;
            return Math.Max(5, interval);
        }

        private static string GetTelemetrySteamIdForMetadata(PlayerData player)
        {
            if (TryResolveRosterMatchedHuman(player, out string canonicalSteamId))
            {
                return canonicalSteamId;
            }

            return player.SteamID;
        }

        internal static void OnMapStart(string mapName)
        {
            Flush("map_start", force: true);
            ResetState(mapName, preserveActiveSession: true);
        }

        internal static void OnMapEnd()
        {
            Flush("map_end", force: true);
        }

        internal static void OnRoundStart()
        {
            if (ShouldEmitLiveTelemetry() == false)
            {
                return;
            }

            roundNumber++;
        }

        internal static void OnRoundEnd(int winningTeam)
        {
            CaptureEconomySnapshots("round_end");
            if (ShouldEmitLiveTelemetry())
            {
                foreach (MatchSignalObservation observation in matchSignalTracker.RecordRoundResult(roundNumber, winningTeam))
                {
                    pendingObservations.Add(ToObservationRecord(observation));
                }
            }

            Flush("round_end", force: false);
        }

        internal static void OnGameTick()
        {
            Flush("interval", force: false);
        }

        internal static void OnPlayerJoined(PlayerData player)
        {
            if (ShouldEmitLiveTelemetry() == false)
            {
                return;
            }

            if (TryGetRosterMatchedHumanState(player, out PlayerTelemetryState? state) == false)
            {
                return;
            }

            state.Connects++;
            int teamNumber = player.TeamNumber;
            if (ShouldTreatJoinAsReconnect(state.Connects, state.Disconnects))
            {
                matchSignalTracker.RecordReconnect(state.SteamID, roundNumber, teamNumber);
            }
            else
            {
                matchSignalTracker.UpdatePlayerTeam(state.SteamID, state.PlayerName, teamNumber);
            }
        }

        internal static void OnPlayerLeft(PlayerData player)
        {
            if (ShouldEmitLiveTelemetry() == false)
            {
                return;
            }

            if (TryGetRosterMatchedHumanState(player, out PlayerTelemetryState? state) == false)
            {
                return;
            }

            state.Disconnects++;
            matchSignalTracker.RecordDisconnect(state.SteamID, roundNumber);
        }

        internal static void OnPlayerSpawned(PlayerData player)
        {
            if (ShouldEmitLiveTelemetry() == false)
            {
                return;
            }

            if (TryGetRosterMatchedHumanState(player, out PlayerTelemetryState? state) == false)
            {
                return;
            }

            state.RoundsPlayed++;
            matchSignalTracker.UpdatePlayerTeam(state.SteamID, state.PlayerName, player.TeamNumber);
        }

        internal static void OnItemPurchase(PlayerData player, string item, int loadout, int _)
        {
            if (ShouldEmitLiveTelemetry() == false)
            {
                return;
            }

            if (TryGetRosterMatchedHumanState(player, out PlayerTelemetryState? state) == false)
            {
                return;
            }

            if (TryBuildLiveEconomySample(player, out LiveEconomySample? sample) == false || sample == null)
            {
                return;
            }

            int moneySpentSinceLastRead =
                state.LastKnownEconomyRoundNumber == roundNumber &&
                state.LastKnownCashSpentThisRound >= 0 &&
                sample.CashSpentThisRound >= state.LastKnownCashSpentThisRound
                    ? sample.CashSpentThisRound - state.LastKnownCashSpentThisRound
                    : sample.CashSpentThisRound;
            int moneyAfter = Math.Max(0, sample.Account);
            int moneyBefore = Math.Max(moneyAfter, moneyAfter + Math.Max(0, moneySpentSinceLastRead));
            string normalizedItem = NormalizeEconomyItemName(item);

            EconomyEvent economyEvent = new EconomyEvent()
            {
                SteamID = sample.SteamID,
                PlayerName = sample.PlayerName,
                Slot = sample.Slot,
                Team = sample.Team,
                EventType = "purchase",
                Item = normalizedItem,
                Loadout = loadout,
                RoundNumber = roundNumber,
                ServerTick = Server.TickCount,
                ObservedAtUtc = DateTime.UtcNow,
                MoneyBefore = moneyBefore,
                MoneyAfter = moneyAfter,
                CashSpentThisRound = sample.CashSpentThisRound,
                StartAccount = sample.StartAccount
            };

            pendingEconomyEvents.Add(economyEvent);
            matchEconomyEvents.Add(economyEvent);

            UpdateEconomyBaseline(state, sample);
        }

        internal static void OnEnterBuyzone(PlayerData player)
        {
            CaptureEconomySnapshots("enter_buyzone", player);
        }

        internal static void OnExitBuyzone(PlayerData player)
        {
            CaptureEconomySnapshots("exit_buyzone", player);
        }

        internal static void OnBuytimeEnded()
        {
            CaptureEconomySnapshots("buytime_ended");
        }

        internal static void OnRoundFreezeEnd()
        {
            CaptureEconomySnapshots("round_freeze_end");
        }

        internal static void OnWeaponFire(PlayerData player, string weapon)
        {
            if (ShouldEmitLiveTelemetry() == false)
            {
                return;
            }

            if (TryGetRosterMatchedHumanState(player, out PlayerTelemetryState? state) == false)
            {
                return;
            }

            state.ShotsFired++;

            string normalizedWeapon = NormalizeWeaponName(weapon);
            if (string.IsNullOrEmpty(normalizedWeapon) == false)
            {
                state.GetOrCreateWeapon(normalizedWeapon).ShotsFired++;
            }
        }

        internal static void OnBulletImpact(PlayerData player)
        {
            if (ShouldEmitLiveTelemetry() == false)
            {
                return;
            }

            if (TryGetRosterMatchedHumanState(player, out PlayerTelemetryState? state) == false)
            {
                return;
            }

            state.BulletImpacts++;
        }

        internal static void OnPlayerBlind(PlayerData victim, PlayerData? attacker, float blindDuration)
        {
            if (ShouldEmitLiveTelemetry() == false)
            {
                return;
            }

            if (TryGetRosterMatchedHumanState(victim, out PlayerTelemetryState? state) == false)
            {
                return;
            }

            state.BlindsReceived++;
            state.BlindDurationSeconds += blindDuration;

            DateTime blindUntilUtc = DateTime.UtcNow.AddSeconds(Math.Max(0f, blindDuration));
            if (blindUntilUtc > state.BlindUntilUtc)
            {
                state.BlindUntilUtc = blindUntilUtc;
            }

            if (
                blindDuration >= 2.5f
                && attacker != null
                && TryResolveRosterMatchedHuman(attacker, out string attackerSteamId)
            )
            {
                RecordObservation(
                    victim,
                    "utility_telemetry",
                    "player_blinded",
                    $"Blinded for {blindDuration.ToString("0.##", CultureInfo.InvariantCulture)}s",
                    metadata: new Dictionary<string, string>()
                    {
                        ["attackerSteamId"] = attackerSteamId,
                        ["attackerName"] = attacker.PlayerName,
                        ["blindDurationSeconds"] = blindDuration.ToString("0.##", CultureInfo.InvariantCulture)
                    }
                );
            }
        }

        internal static void OnFlashbangDetonate(PlayerData player)
        {
            if (ShouldEmitLiveTelemetry() == false)
            {
                return;
            }

            if (TryGetRosterMatchedHumanState(player, out PlayerTelemetryState? state) == false)
            {
                return;
            }

            state.FlashbangsThrown++;
        }

        internal static void OnSmokeDetonate(PlayerData player)
        {
            if (ShouldEmitLiveTelemetry() == false)
            {
                return;
            }

            if (TryGetRosterMatchedHumanState(player, out PlayerTelemetryState? state) == false)
            {
                return;
            }

            state.SmokesThrown++;
        }

        internal static void OnMolotovDetonate(PlayerData player)
        {
            if (ShouldEmitLiveTelemetry() == false)
            {
                return;
            }

            if (TryGetRosterMatchedHumanState(player, out PlayerTelemetryState? state) == false)
            {
                return;
            }

            state.MolotovsThrown++;
        }

        internal static void OnPlayerFootstep(PlayerData player)
        {
            if (ShouldEmitLiveTelemetry() == false)
            {
                return;
            }

            if (TryGetRosterMatchedHumanState(player, out PlayerTelemetryState? state) == false)
            {
                return;
            }

            state.Footsteps++;
        }

        internal static void OnPlayerSound(PlayerData player)
        {
            if (ShouldEmitLiveTelemetry() == false)
            {
                return;
            }

            if (TryGetRosterMatchedHumanState(player, out PlayerTelemetryState? state) == false)
            {
                return;
            }

            state.Sounds++;
        }

        internal static void OnBombPlanted(PlayerData player)
        {
            if (ShouldEmitLiveTelemetry() == false)
            {
                return;
            }

            if (TryGetRosterMatchedHumanState(player, out PlayerTelemetryState? _) == false)
            {
                return;
            }

            bombPlants++;
        }

        internal static void OnBombDefused(PlayerData player)
        {
            if (ShouldEmitLiveTelemetry() == false)
            {
                return;
            }

            if (TryGetRosterMatchedHumanState(player, out PlayerTelemetryState? _) == false)
            {
                return;
            }

            bombDefuses++;
        }

        internal static void OnPlayerHurt(PlayerData victim, PlayerData? attacker, int damageHealth, string weapon, int hitgroup)
        {
            if (ShouldEmitLiveTelemetry() == false)
            {
                return;
            }

            if (TryGetRosterMatchedHumanState(victim, out PlayerTelemetryState? victimState) == false)
            {
                return;
            }

            string normalizedWeapon = NormalizeWeaponName(weapon);
            victimState.DamageTaken += Math.Max(0, damageHealth);

            if (IsUtilityWeapon(normalizedWeapon))
            {
                victimState.UtilityDamageTaken += Math.Max(0, damageHealth);
            }

            if (
                attacker == null
                || attacker.Index == victim.Index
                || TryGetRosterMatchedHumanState(attacker, out PlayerTelemetryState? attackerState) == false
            )
            {
                return;
            }

            attackerState.HitsLanded++;
            attackerState.DamageDealt += Math.Max(0, damageHealth);

            if (string.IsNullOrEmpty(normalizedWeapon) == false)
            {
                WeaponTelemetryState weaponState = attackerState.GetOrCreateWeapon(normalizedWeapon);
                weaponState.HitsLanded++;
                weaponState.DamageDealt += Math.Max(0, damageHealth);
            }

            if (attackerState.IsCurrentlyBlind(DateTime.UtcNow))
            {
                attackerState.DamageWhileBlind += Math.Max(0, damageHealth);
            }

            if (IsUtilityWeapon(normalizedWeapon))
            {
                attackerState.UtilityDamageDealt += Math.Max(0, damageHealth);

                int utilityDamageBucket = attackerState.UtilityDamageDealt / 100;
                if (utilityDamageBucket > attackerState.LastUtilityProfileObservationBucket)
                {
                    attackerState.LastUtilityProfileObservationBucket = utilityDamageBucket;

                    RecordObservation(
                        attacker,
                        "utility_telemetry",
                        "utility_damage_profile",
                        $"Reached {attackerState.UtilityDamageDealt} utility damage",
                        weapon: normalizedWeapon,
                        metadata: new Dictionary<string, string>()
                        {
                            ["utilityDamageDealt"] = attackerState.UtilityDamageDealt.ToString(CultureInfo.InvariantCulture),
                            ["victimSteamId"] = GetTelemetrySteamIdForMetadata(victim),
                            ["victimName"] = victim.PlayerName,
                            ["hitgroup"] = hitgroup.ToString(CultureInfo.InvariantCulture)
                        }
                    );
                }
            }
        }

        internal static void OnPlayerDeath(PlayerData victim, PlayerData? attacker, string weapon, bool headshot, bool thruSmoke, int penetrated, bool attackerBlind, bool noScope, bool attackerInAir, float distance)
        {
            if (ShouldEmitLiveTelemetry() == false)
            {
                return;
            }

            if (TryGetRosterMatchedHumanState(victim, out PlayerTelemetryState? victimState) == false)
            {
                return;
            }

            victimState.Deaths++;

            if (
                attacker == null
                || attacker.Index == victim.Index
                || TryGetRosterMatchedHumanState(attacker, out PlayerTelemetryState? attackerState) == false
            )
            {
                return;
            }

            attackerState.Kills++;

            string normalizedWeapon = NormalizeWeaponName(weapon);
            if (string.IsNullOrEmpty(normalizedWeapon) == false)
            {
                attackerState.GetOrCreateWeapon(normalizedWeapon).Kills++;
            }

            if (headshot)
            {
                attackerState.Headshots++;
            }

            DateTime nowUtc = DateTime.UtcNow;
            bool attackerCurrentlyBlind = attackerBlind || attackerState.IsCurrentlyBlind(nowUtc);
            if (attackerCurrentlyBlind)
            {
                attackerState.KillsWhileBlind++;
            }

            int killWindowCount = TrackKillWindow(attackerState.RecentKills, nowUtc, TimeSpan.FromSeconds(5));
            int weaponKillWindowCount = TrackWeaponKillWindow(attackerState, normalizedWeapon, nowUtc, TimeSpan.FromSeconds(5));

            bool interestingKill =
                thruSmoke ||
                penetrated > 0 ||
                attackerCurrentlyBlind ||
                noScope ||
                attackerInAir ||
                killWindowCount >= 3 ||
                (weaponKillWindowCount >= 2 && IsHighSignalWeapon(normalizedWeapon));

            matchSignalTracker.RecordKill(
                attackerState.SteamID,
                attackerState.PlayerName,
                attacker.TeamNumber,
                interestingKill
            );

            if (interestingKill)
            {
                Dictionary<string, string> metadata = new Dictionary<string, string>()
                {
                    ["victimSteamId"] = GetTelemetrySteamIdForMetadata(victim),
                    ["victimName"] = victim.PlayerName,
                    ["killWindowCount5s"] = killWindowCount.ToString(CultureInfo.InvariantCulture),
                    ["weaponKillWindowCount5s"] = weaponKillWindowCount.ToString(CultureInfo.InvariantCulture),
                    ["headshot"] = headshot.ToString(CultureInfo.InvariantCulture),
                    ["thruSmoke"] = thruSmoke.ToString(CultureInfo.InvariantCulture),
                    ["penetrated"] = penetrated.ToString(CultureInfo.InvariantCulture),
                    ["attackerBlind"] = attackerCurrentlyBlind.ToString(CultureInfo.InvariantCulture),
                    ["noScope"] = noScope.ToString(CultureInfo.InvariantCulture),
                    ["attackerInAir"] = attackerInAir.ToString(CultureInfo.InvariantCulture),
                    ["distance"] = distance.ToString("0.##", CultureInfo.InvariantCulture)
                };

                RecordObservation(
                    attacker,
                    "combat_telemetry",
                    "interesting_kill",
                    BuildKillSummary(normalizedWeapon, thruSmoke, penetrated, attackerCurrentlyBlind, noScope, attackerInAir, killWindowCount, weaponKillWindowCount),
                    weapon: normalizedWeapon,
                    metadata: metadata
                );
            }

            if (killWindowCount >= 3)
            {
                RecordObservation(
                    attacker,
                    "combat_telemetry",
                    "kill_burst",
                    $"Recorded {killWindowCount} kills within 5 seconds",
                    weapon: normalizedWeapon,
                    metadata: new Dictionary<string, string>()
                    {
                        ["killWindowCount5s"] = killWindowCount.ToString(CultureInfo.InvariantCulture)
                    }
                );

                int opposingRosterSize = GetOpposingRecordedRosterSize(matchSession, attackerState.SteamID);
                foreach (MatchSignalObservation signalObservation in matchSignalTracker.RegisterBurst(
                    attackerState.SteamID,
                    attackerState.PlayerName,
                    roundNumber,
                    killWindowCount,
                    opposingRosterSize
                ))
                {
                    pendingObservations.Add(ToObservationRecord(signalObservation));
                }
            }

            if (weaponKillWindowCount >= 2 && IsHighSignalWeapon(normalizedWeapon))
            {
                RecordObservation(
                    attacker,
                    "weapon_profile",
                    "weapon_burst",
                    $"Recorded {weaponKillWindowCount} quick kills with {normalizedWeapon}",
                    weapon: normalizedWeapon,
                    metadata: new Dictionary<string, string>()
                    {
                        ["weaponKillWindowCount5s"] = weaponKillWindowCount.ToString(CultureInfo.InvariantCulture)
                    }
                );
            }

            int pistolKills = attackerState.GetKillsByFamily("pistol");
            if (attackerState.Kills >= 5 &&
                pistolKills * 100 / Math.Max(1, attackerState.Kills) >= 70 &&
                attackerState.LastWeaponProfileObservationKillCount != attackerState.Kills)
            {
                attackerState.LastWeaponProfileObservationKillCount = attackerState.Kills;

                RecordObservation(
                    attacker,
                    "weapon_profile",
                    "pistol_heavy_profile",
                    $"Kill profile is {pistolKills}/{attackerState.Kills} pistol-heavy",
                    weapon: normalizedWeapon,
                    metadata: new Dictionary<string, string>()
                    {
                        ["pistolKills"] = pistolKills.ToString(CultureInfo.InvariantCulture),
                        ["totalKills"] = attackerState.Kills.ToString(CultureInfo.InvariantCulture)
                    }
                );
            }
        }

        internal static void RecordModuleDetection(DetectionMetadata metadata)
        {
            if (ShouldEmitLiveTelemetry() == false)
            {
                return;
            }

            if (TryResolveRosterMatchedHuman(metadata.player, out string _) == false)
            {
                return;
            }

            RecordObservation(
                metadata.player,
                "detection_module",
                metadata.module.Name,
                metadata.reason,
                weapon: metadata.player.GetWeapon()?.DesignerName ?? string.Empty,
                observedAtUtc: metadata.time,
                metadata: new Dictionary<string, string>()
                {
                    ["module"] = metadata.module.Name
                }
            );
        }

        private static void Flush(string reason, bool force)
        {
            TelemetryConfigData config = TelemetryConfig.Get().Config;
            if (
                TelemetryRecordingWindowPolicy.CanCollectLiveTelemetry(
                    config.CollectionEnabled,
                    matchSession
                ) == false
            )
            {
                return;
            }

            if (uploadInProgress)
            {
                return;
            }

            DateTime nowUtc = DateTime.UtcNow;
            if (force == false && nowUtc - lastFlushUtc < TimeSpan.FromSeconds(GetLiveReportingIntervalSeconds()))
            {
                return;
            }

            TelemetryBatch batch = BuildBatch(reason);
            if (batch.IsEmpty)
            {
                lastFlushUtc = nowUtc;
                return;
            }

            if (
                TelemetryRecordingWindowPolicy.CanFlushLiveTelemetry(
                    config.CollectionEnabled,
                    matchSession,
                    CountPresentRosterMatchedHumans()
                ) == false
            )
            {
                return;
            }

            if (BatchHasMeaningfulHumanActivity(batch) == false)
            {
                ClearPendingBatchData();
                lastFlushUtc = nowUtc;
                return;
            }

            ClearPendingBatchData();
            lastFlushUtc = nowUtc;

            if (config.LogBatchSummaries)
            {
                Globals.Log($"[TBAC] Telemetry batch #{batch.BatchSequence} ({reason}) -> players: {batch.Players.Count}, observations: {batch.Observations.Count}, economyEvents: {batch.EconomyEvents.Count}, economySnapshots: {batch.EconomySnapshots.Count}");
            }

            if (config.UploadEnabled == false)
            {
                if (loggedUploadDisabled == false)
                {
                    Globals.Log("[TBAC] Telemetry upload disabled; collecting locally only");
                    loggedUploadDisabled = true;
                }

                return;
            }

            if (string.IsNullOrWhiteSpace(config.BaseUrl))
            {
                if (loggedMissingEndpoint == false)
                {
                    Globals.Log("[TBAC] Telemetry upload enabled but BaseUrl is empty");
                    loggedMissingEndpoint = true;
                }

                return;
            }

            uploadInProgress = true;
            _ = UploadBatchAsync(batch);
        }

        private static async Task UploadBatchAsync(TelemetryBatch batch)
        {
            try
            {
                TelemetryConfigData config = TelemetryConfig.Get().Config;

                string json = JsonSerializer.Serialize(batch, jsonOptions);
                using HttpRequestMessage request = TelemetryRequestFactory.CreateUploadRequest(
                    config,
                    json,
                    TelemetryUploadRoutes.Observations
                );

                using HttpResponseMessage response = await httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode == false)
                {
                    Globals.Log($"[TBAC] Telemetry upload failed -> {(int)response.StatusCode} {response.ReasonPhrase}");
                }
            }
            catch (Exception e)
            {
                Globals.Log($"[TBAC] Telemetry upload failed -> {e.Message}");
            }
            finally
            {
                uploadInProgress = false;
            }
        }

        private static void UploadFinalMatchEconomySummary(
            TelemetryMatchSession session,
            IReadOnlyList<EconomyEvent> economyEvents,
            IReadOnlyList<EconomySnapshot> economySnapshots,
            string mapName,
            string pluginVersion
        )
        {
            UploadFinalMatchEconomySummaryAsync(
                session,
                economyEvents,
                economySnapshots,
                mapName,
                pluginVersion
            ).GetAwaiter().GetResult();
        }

        private static async Task UploadFinalMatchEconomySummaryAsync(
            TelemetryMatchSession session,
            IReadOnlyList<EconomyEvent> economyEvents,
            IReadOnlyList<EconomySnapshot> economySnapshots,
            string mapName,
            string pluginVersion
        )
        {
            try
            {
                TelemetryConfigData config = TelemetryConfig.Get().Config;
                MatchEconomySummary summary = TelemetryEconomySummaryBuilder.Build(
                    pluginVersion,
                    session,
                    economyEvents,
                    economySnapshots
                );
                summary.MapName = string.IsNullOrEmpty(mapName) == false ? mapName : session.MapName;

                if (config.UploadEnabled == false)
                {
                    if (loggedUploadDisabled == false)
                    {
                        Globals.Log("[TBAC] Match economy summary upload skipped (upload disabled)");
                        loggedUploadDisabled = true;
                    }

                    return;
                }

                if (string.IsNullOrWhiteSpace(config.BaseUrl))
                {
                    if (loggedMissingEndpoint == false)
                    {
                        Globals.Log("[TBAC] Match economy summary upload skipped (BaseUrl empty)");
                        loggedMissingEndpoint = true;
                    }

                    return;
                }

                string json = JsonSerializer.Serialize(summary, jsonOptions);
                using HttpRequestMessage request = TelemetryRequestFactory.CreateUploadRequest(
                    config,
                    json,
                    TelemetryUploadRoutes.MatchEconomySummary
                );
                using CancellationTokenSource cancellation = new CancellationTokenSource(
                    finalMatchEconomySummaryUploadTimeout
                );

                using HttpResponseMessage response = await httpClient
                    .SendAsync(request, cancellation.Token)
                    .ConfigureAwait(false);
                if (response.IsSuccessStatusCode == false)
                {
                    Globals.Log(
                        $"[TBAC] Match economy summary upload failed -> {(int)response.StatusCode} {response.ReasonPhrase}"
                    );
                }
            }
            catch (OperationCanceledException)
            {
                Globals.Log(
                    $"[TBAC] Match economy summary upload timed out after {finalMatchEconomySummaryUploadTimeout.TotalSeconds:0}s"
                );
            }
            catch (Exception e)
            {
                Globals.Log($"[TBAC] Match economy summary upload failed -> {e.Message}");
            }
        }

        private static TelemetryBatch BuildBatch(string reason)
        {
            TelemetryConfigData config = TelemetryConfig.Get().Config;
            TelemetryMatchSession? session = matchSession;

            List<PlayerTelemetrySnapshot> players = [];
            foreach (PlayerTelemetryState? state in playerStates)
            {
                if (state == null || state.HasActivity() == false || state.IsBot)
                {
                    continue;
                }

                if (
                    session == null
                    || TelemetryLiveIdentity.MatchesRosterSteamId64(session, state.SteamID) == false
                )
                {
                    continue;
                }

                AppendProfileObservations(state);
                players.Add(state.ToSnapshot());
            }

            List<ObservationRecord> observations = [];
            foreach (ObservationRecord observation in pendingObservations)
            {
                if (
                    observation.IsBot
                    || session == null
                    || TelemetryLiveIdentity.MatchesRosterSteamId64(session, observation.SteamID) == false
                )
                {
                    continue;
                }

                observations.Add(observation);
            }

            List<EconomyEvent> economyEvents = [];
            foreach (EconomyEvent economyEvent in pendingEconomyEvents)
            {
                if (
                    session == null
                    || TelemetryLiveIdentity.MatchesRosterSteamId64(session, economyEvent.SteamID) == false
                )
                {
                    continue;
                }

                economyEvents.Add(economyEvent);
            }

            List<EconomySnapshot> economySnapshots = [];
            foreach (EconomySnapshot economySnapshot in pendingEconomySnapshots)
            {
                if (
                    session == null
                    || TelemetryLiveIdentity.MatchesRosterSteamId64(session, economySnapshot.SteamID) == false
                )
                {
                    continue;
                }

                economySnapshots.Add(economySnapshot);
            }

            return new TelemetryBatch()
            {
                PluginVersion = plugin?.ModuleVersion ?? string.Empty,
                ServerId = session?.ServerId ?? config.ServerId,
                ServerLabel = session?.ServerLabel ?? config.ServerLabel,
                ServerRegion = session?.ServerRegion ?? config.ServerRegion,
                MatchSource = session?.MatchSource ?? config.MatchSource,
                MatchId = session?.MatchId ?? config.MatchId,
                MapName = currentMap,
                FlushReason = reason,
                BatchSequence = ++batchSequence,
                RoundNumber = roundNumber,
                ServerTick = Server.TickCount,
                BombPlants = bombPlants,
                BombDefuses = bombDefuses,
                GeneratedAtUtc = DateTime.UtcNow,
                Players = players,
                Observations = observations,
                EconomyEvents = economyEvents,
                EconomySnapshots = economySnapshots
            };
        }

        private static void ResetState(string mapName, bool preserveActiveSession)
        {
            matchSession = TelemetryRecordingWindowPolicy.ResolveActiveSessionAfterBufferReset(
                matchSession,
                preserveActiveSession
            );
            playerStates = [];
            ClearPendingBatchData();
            if (preserveActiveSession == false)
            {
                matchEconomyEvents.Clear();
                matchEconomySnapshots.Clear();
                matchSignalTracker = new();
            }

            currentMap = mapName;
            roundNumber = 0;
            bombPlants = 0;
            bombDefuses = 0;
            lastFlushUtc = DateTime.UtcNow;
            uploadInProgress = false;
        }

        private static PlayerTelemetryState GetOrCreateState(PlayerData player)
        {
            EnsureStateCapacity(player.Index);
            PlayerTelemetryState? existing = playerStates[player.Index];
            if (existing == null)
            {
                existing = new PlayerTelemetryState()
                {
                    Slot = player.Index
                };

                playerStates[player.Index] = existing;
            }

            existing.IsBot = player.IsBot;
            existing.PlayerName = player.PlayerName;
            if (TryResolveRosterMatchedHuman(player, out string canonicalSteamId))
            {
                existing.SteamID = canonicalSteamId;
            }
            else
            {
                existing.SteamID = player.SteamID;
            }

            return existing;
        }

        private static void CaptureEconomySnapshots(string snapshotKind, PlayerData? player = null)
        {
            if (ShouldEmitLiveTelemetry() == false)
            {
                return;
            }

            if (player != null)
            {
                RecordEconomySnapshot(player, snapshotKind);
                return;
            }

            foreach (PlayerData? trackedPlayer in Globals.Players)
            {
                if (trackedPlayer == null)
                {
                    continue;
                }

                RecordEconomySnapshot(trackedPlayer, snapshotKind);
            }
        }

        private static void RecordEconomySnapshot(PlayerData player, string snapshotKind)
        {
            if (TryBuildLiveEconomySample(player, out LiveEconomySample? sample) == false || sample == null)
            {
                return;
            }

            PlayerTelemetryState state = GetOrCreateState(player);
            EconomySnapshot economySnapshot = new EconomySnapshot()
            {
                SteamID = sample.SteamID,
                PlayerName = sample.PlayerName,
                Slot = sample.Slot,
                Team = sample.Team,
                SnapshotKind = snapshotKind,
                RoundNumber = roundNumber,
                ServerTick = Server.TickCount,
                ObservedAtUtc = DateTime.UtcNow,
                Money = sample.Account,
                StartAccount = sample.StartAccount,
                CashSpentThisRound = sample.CashSpentThisRound,
                TotalCashSpent = sample.TotalCashSpent,
                InventoryItems = sample.InventoryItems
            };

            pendingEconomySnapshots.Add(economySnapshot);
            matchEconomySnapshots.Add(economySnapshot);

            UpdateEconomyBaseline(state, sample);
        }

        private static bool TryBuildLiveEconomySample(PlayerData player, out LiveEconomySample? sample)
        {
            sample = null;

            if (TryResolveRosterMatchedHuman(player, out string canonicalSteamId) == false)
            {
                return false;
            }

            CCSPlayerController_InGameMoneyServices? moneyServices = player.GetMoneyServices();
            if (moneyServices == null)
            {
                return false;
            }

            List<string> inventoryItems = [];
            foreach (string item in player.GetInventoryItems())
            {
                string normalizedItem = NormalizeWeaponName(item);
                if (string.IsNullOrEmpty(normalizedItem))
                {
                    continue;
                }

                inventoryItems.Add(normalizedItem);
            }

            inventoryItems.Sort(StringComparer.Ordinal);

            sample = new LiveEconomySample()
            {
                SteamID = canonicalSteamId,
                PlayerName = player.PlayerName,
                Slot = player.Index,
                Team = player.TeamNumber,
                Account = Math.Max(0, moneyServices.Account),
                StartAccount = Math.Max(0, moneyServices.StartAccount),
                CashSpentThisRound = Math.Max(0, moneyServices.CashSpentThisRound),
                TotalCashSpent = Math.Max(0, moneyServices.TotalCashSpent),
                InventoryItems = inventoryItems
            };

            return true;
        }

        private static void UpdateEconomyBaseline(PlayerTelemetryState state, LiveEconomySample sample)
        {
            state.LastKnownMoneyAccount = sample.Account;
            state.LastKnownCashSpentThisRound = sample.CashSpentThisRound;
            state.LastKnownTotalCashSpent = sample.TotalCashSpent;
            state.LastKnownEconomyRoundNumber = roundNumber;
        }

        private static void EnsureStateCapacity(int playerIndex)
        {
            if (playerIndex < 0)
            {
                return;
            }

            if (playerStates.Length > playerIndex)
            {
                return;
            }

            Array.Resize(ref playerStates, playerIndex + 1);
        }

        private static bool TryResolveRosterMatchedHuman(PlayerData player, out string canonicalSteamId)
        {
            canonicalSteamId = string.Empty;

            TelemetryMatchSession? session = matchSession;
            if (session == null)
            {
                return false;
            }

            return TelemetryLiveIdentity.TryResolveCanonicalSteamId64(
                session,
                player.PlayerName,
                player.SteamID64,
                player.IsBot,
                out canonicalSteamId
            );
        }

        private static bool TryGetRosterMatchedHumanState(PlayerData player, out PlayerTelemetryState? state)
        {
            state = null;
            if (TryResolveRosterMatchedHuman(player, out string _) == false)
            {
                return false;
            }

            state = GetOrCreateState(player);
            return true;
        }

        private static void ClearPendingBatchData()
        {
            pendingObservations.Clear();
            pendingEconomyEvents.Clear();
            pendingEconomySnapshots.Clear();
        }

        private static bool BatchHasMeaningfulHumanActivity(TelemetryBatch batch)
        {
            List<TelemetryLiveActivityCounters> playerCounters = [];
            foreach (PlayerTelemetrySnapshot player in batch.Players)
            {
                playerCounters.Add(new TelemetryLiveActivityCounters()
                {
                    Connects = player.Connects,
                    Disconnects = player.Disconnects,
                    RoundsPlayed = player.RoundsPlayed,
                    ShotsFired = player.ShotsFired,
                    HitsLanded = player.HitsLanded,
                    BulletImpacts = player.BulletImpacts,
                    DamageDealt = player.DamageDealt,
                    DamageTaken = player.DamageTaken,
                    UtilityDamageDealt = player.UtilityDamageDealt,
                    UtilityDamageTaken = player.UtilityDamageTaken,
                    Kills = player.Kills,
                    Deaths = player.Deaths,
                    FlashbangsThrown = player.FlashbangsThrown,
                    SmokesThrown = player.SmokesThrown,
                    MolotovsThrown = player.MolotovsThrown,
                });
            }

            return TelemetryLiveBatchPolicy.HasMeaningfulHumanActivity(
                playerCounters,
                batch.Observations.Count,
                batch.EconomyEvents.Count,
                batch.EconomySnapshots.Count,
                batch.BombPlants,
                batch.BombDefuses
            );
        }

        private static void RecordObservation(PlayerData player, string source, string kind, string summary, string weapon = "", DateTime? observedAtUtc = null, Dictionary<string, string>? metadata = null)
        {
            PlayerTelemetryState state = GetOrCreateState(player);
            string normalizedWeapon = NormalizeWeaponName(weapon);

            ObservationRecord observation = new ObservationRecord()
            {
                SteamID = state.SteamID,
                PlayerName = state.PlayerName,
                Slot = state.Slot,
                IsBot = state.IsBot,
                Source = source,
                Kind = kind,
                Summary = summary,
                Weapon = normalizedWeapon,
                WeaponFamily = GetWeaponFamily(normalizedWeapon),
                RoundNumber = roundNumber,
                ServerTick = Server.TickCount,
                ObservedAtUtc = observedAtUtc ?? DateTime.UtcNow,
                Metadata = metadata ?? []
            };

            pendingObservations.Add(observation);

            if (TelemetryConfig.Get().Config.LogObservationSummaries)
            {
                Globals.Log($"[TBAC] Observation -> {state.PlayerName}: {source}/{kind} ({summary})");
            }
        }

        private static int TrackKillWindow(Queue<DateTime> kills, DateTime nowUtc, TimeSpan window)
        {
            kills.Enqueue(nowUtc);
            TrimWindow(kills, nowUtc, window);
            return kills.Count;
        }

        private static void AppendProfileObservations(PlayerTelemetryState state)
        {
            if (state.KillsWhileBlind >= 2 && state.KillsWhileBlind > state.LastBlindKillObservationCount)
            {
                state.LastBlindKillObservationCount = state.KillsWhileBlind;
                RecordProfileObservation(
                    state,
                    "combat_profile",
                    "blind_kill_profile",
                    $"Recorded {state.KillsWhileBlind} kills while blind",
                    metadata: new Dictionary<string, string>()
                    {
                        ["killsWhileBlind"] = state.KillsWhileBlind.ToString(CultureInfo.InvariantCulture),
                        ["blindDurationSeconds"] = state.BlindDurationSeconds.ToString("0.##", CultureInfo.InvariantCulture)
                    }
                );
            }

            int utilityActions = state.FlashbangsThrown + state.SmokesThrown + state.MolotovsThrown;
            if (state.Kills >= 5 &&
                utilityActions == 0 &&
                state.UtilityDamageDealt == 0 &&
                state.Kills > state.LastZeroUtilityObservationKillCount)
            {
                state.LastZeroUtilityObservationKillCount = state.Kills;
                RecordProfileObservation(
                    state,
                    "utility_profile",
                    "zero_utility_profile",
                    $"Recorded {state.Kills} kills with no utility usage or utility damage",
                    metadata: new Dictionary<string, string>()
                    {
                        ["kills"] = state.Kills.ToString(CultureInfo.InvariantCulture),
                        ["flashbangsThrown"] = state.FlashbangsThrown.ToString(CultureInfo.InvariantCulture),
                        ["smokesThrown"] = state.SmokesThrown.ToString(CultureInfo.InvariantCulture),
                        ["molotovsThrown"] = state.MolotovsThrown.ToString(CultureInfo.InvariantCulture)
                    }
                );
            }

            foreach (WeaponTelemetryState weapon in state.Weapons.Values)
            {
                if (weapon.Kills < 3)
                {
                    continue;
                }

                if (weapon.WeaponFamily != "revolver" && weapon.WeaponFamily != "scout")
                {
                    continue;
                }

                if (weapon.Kills * 100 / Math.Max(1, state.Kills) < 50)
                {
                    continue;
                }

                if (state.LastWeaponFocusObservationCounts.TryGetValue(weapon.Weapon, out int previousKills) && previousKills == weapon.Kills)
                {
                    continue;
                }

                state.LastWeaponFocusObservationCounts[weapon.Weapon] = weapon.Kills;

                RecordProfileObservation(
                    state,
                    "weapon_profile",
                    "high_signal_weapon_focus",
                    $"Recorded {weapon.Kills}/{state.Kills} kills with {weapon.Weapon}",
                    weapon: weapon.Weapon,
                    metadata: new Dictionary<string, string>()
                    {
                        ["weaponKills"] = weapon.Kills.ToString(CultureInfo.InvariantCulture),
                        ["totalKills"] = state.Kills.ToString(CultureInfo.InvariantCulture),
                        ["weaponFamily"] = weapon.WeaponFamily
                    }
                );
            }
        }

        private static int TrackWeaponKillWindow(PlayerTelemetryState state, string weapon, DateTime nowUtc, TimeSpan window)
        {
            if (string.IsNullOrEmpty(weapon))
            {
                return 0;
            }

            if (state.RecentKillsByWeapon.TryGetValue(weapon, out Queue<DateTime>? kills) == false)
            {
                kills = new Queue<DateTime>();
                state.RecentKillsByWeapon[weapon] = kills;
            }

            kills.Enqueue(nowUtc);
            TrimWindow(kills, nowUtc, window);

            return kills.Count;
        }

        private static void TrimWindow(Queue<DateTime> values, DateTime nowUtc, TimeSpan window)
        {
            while (values.Count > 0 && nowUtc - values.Peek() > window)
            {
                values.Dequeue();
            }
        }

        internal static int ResolveSlotForMatchSignalSteamId(string steamId)
        {
            if (string.IsNullOrEmpty(steamId))
            {
                return 0;
            }

            foreach (PlayerTelemetryState? state in playerStates)
            {
                if (state == null)
                {
                    continue;
                }

                if (string.Equals(state.SteamID, steamId, StringComparison.Ordinal))
                {
                    return state.Slot;
                }
            }

            return 0;
        }

        private static ObservationRecord ToObservationRecord(MatchSignalObservation observation)
        {
            ObservationRecord record = new ObservationRecord()
            {
                SteamID = observation.SteamId,
                PlayerName = observation.PlayerName,
                Slot = ResolveSlotForMatchSignalSteamId(observation.SteamId),
                IsBot = false,
                Source = observation.Source,
                Kind = observation.Kind,
                Summary = observation.Summary,
                Weapon = string.Empty,
                WeaponFamily = string.Empty,
                RoundNumber = roundNumber,
                ServerTick = Server.TickCount,
                ObservedAtUtc = DateTime.UtcNow,
                Metadata = new Dictionary<string, string>(observation.Metadata, StringComparer.Ordinal)
            };

            if (TelemetryConfig.Get().Config.LogObservationSummaries)
            {
                Globals.Log($"[TBAC] Observation -> {observation.PlayerName}: {observation.Source}/{observation.Kind} ({observation.Summary})");
            }

            return record;
        }

        private static void RecordProfileObservation(PlayerTelemetryState state, string source, string kind, string summary, string weapon = "", Dictionary<string, string>? metadata = null)
        {
            ObservationRecord observation = new ObservationRecord()
            {
                SteamID = state.SteamID,
                PlayerName = state.PlayerName,
                Slot = state.Slot,
                IsBot = state.IsBot,
                Source = source,
                Kind = kind,
                Summary = summary,
                Weapon = NormalizeWeaponName(weapon),
                WeaponFamily = GetWeaponFamily(weapon),
                RoundNumber = roundNumber,
                ServerTick = Server.TickCount,
                ObservedAtUtc = DateTime.UtcNow,
                Metadata = metadata ?? []
            };

            pendingObservations.Add(observation);

            if (TelemetryConfig.Get().Config.LogObservationSummaries)
            {
                Globals.Log($"[TBAC] Observation -> {state.PlayerName}: {source}/{kind} ({summary})");
            }
        }

        private static string BuildKillSummary(string weapon, bool thruSmoke, int penetrated, bool attackerBlind, bool noScope, bool attackerInAir, int killWindowCount, int weaponKillWindowCount)
        {
            List<string> tags = [];

            if (thruSmoke)
            {
                tags.Add("smoke");
            }

            if (penetrated > 0)
            {
                tags.Add("wallbang");
            }

            if (attackerBlind)
            {
                tags.Add("blind");
            }

            if (noScope)
            {
                tags.Add("noscope");
            }

            if (attackerInAir)
            {
                tags.Add("airborne");
            }

            if (killWindowCount >= 3)
            {
                tags.Add($"{killWindowCount}k_5s");
            }

            if (weaponKillWindowCount >= 2)
            {
                tags.Add($"{weaponKillWindowCount}x_{GetWeaponFamily(weapon)}");
            }

            if (tags.Count == 0)
            {
                return $"Interesting kill with {weapon}";
            }

            return $"Interesting kill with {weapon}: {string.Join(", ", tags)}";
        }

        internal static string NormalizeWeaponName(string weapon)
        {
            if (string.IsNullOrWhiteSpace(weapon))
            {
                return string.Empty;
            }

            string normalized = weapon.Trim().ToLowerInvariant();
            if (normalized.StartsWith("weapon_", StringComparison.Ordinal))
            {
                return normalized;
            }

            return normalized switch
            {
                "inferno" => normalized,
                "molotov" => normalized,
                "hegrenade" => "weapon_hegrenade",
                _ => $"weapon_{normalized}"
            };
        }

        internal static string NormalizeEconomyItemName(string item)
        {
            if (string.IsNullOrWhiteSpace(item))
            {
                return string.Empty;
            }

            string normalized = item.Trim().ToLowerInvariant();
            if (normalized.StartsWith("weapon_", StringComparison.Ordinal) || normalized.StartsWith("item_", StringComparison.Ordinal))
            {
                return normalized;
            }

            return normalized switch
            {
                "vest" or "vesthelm" or "defuser" => $"item_{normalized}",
                _ => NormalizeWeaponName(normalized)
            };
        }

        internal static string GetWeaponFamily(string weapon)
        {
            string normalized = NormalizeWeaponName(weapon);
            return normalized switch
            {
                "" => "unknown",
                "weapon_glock" or "weapon_hkp2000" or "weapon_usp_silencer" or "weapon_p250" or "weapon_fiveseven" or
                "weapon_tec9" or "weapon_cz75a" or "weapon_elite" or "weapon_deagle" => "pistol",
                "weapon_revolver" => "revolver",
                "weapon_ssg08" => "scout",
                "weapon_awp" or "weapon_scar20" or "weapon_g3sg1" => "sniper",
                "weapon_mac10" or "weapon_mp9" or "weapon_mp7" or "weapon_mp5sd" or "weapon_ump45" or "weapon_bizon" or "weapon_p90" => "smg",
                "weapon_nova" or "weapon_xm1014" or "weapon_sawedoff" or "weapon_mag7" => "shotgun",
                "weapon_negev" or "weapon_m249" => "machinegun",
                "weapon_famas" or "weapon_galilar" or "weapon_ak47" or "weapon_m4a1" or "weapon_m4a1_silencer" or "weapon_aug" or "weapon_sg556" => "rifle",
                "weapon_hegrenade" or "weapon_flashbang" or "weapon_smokegrenade" or "weapon_molotov" or "weapon_incgrenade" or "inferno" or "molotov" => "utility",
                "weapon_knife" or "weapon_knife_t" or "weapon_bayonet" or "weapon_knife_css" => "melee",
                _ => "other"
            };
        }

        private static bool IsUtilityWeapon(string weapon)
        {
            return string.Equals(GetWeaponFamily(weapon), "utility", StringComparison.Ordinal);
        }

        private static bool IsHighSignalWeapon(string weapon)
        {
            string normalized = NormalizeWeaponName(weapon);
            return normalized is "weapon_revolver" or "weapon_ssg08";
        }
    }

    public static class OuroTelemetryRecordCommands
    {
        private static readonly JsonSerializerOptions recordStartJsonOptions = new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true
        };

        public static bool TryDecodeRecordStartPayload(string base64Payload, out TelemetryMatchSession? session, out string? error)
        {
            session = null;
            error = null;

            if (string.IsNullOrWhiteSpace(base64Payload))
            {
                error = "Payload is empty.";
                return false;
            }

            try
            {
                byte[] jsonBytes = Convert.FromBase64String(base64Payload.Trim());
                string json = Encoding.UTF8.GetString(jsonBytes);
                TelemetryMatchSession? parsed = JsonSerializer.Deserialize<TelemetryMatchSession>(json, recordStartJsonOptions);
                if (parsed == null)
                {
                    error = "JSON deserialized to null.";
                    return false;
                }

                if (parsed.IsValid(out string? validationError) == false)
                {
                    error = validationError;
                    return false;
                }

                session = parsed;
                return true;
            }
            catch (FormatException ex)
            {
                error = $"Invalid base64: {ex.Message}";
                return false;
            }
            catch (JsonException ex)
            {
                error = $"Invalid JSON: {ex.Message}";
                return false;
            }
        }
    }

    public static class TelemetryLiveIdentity
    {
        public static bool TryResolveCanonicalSteamId64(
            TelemetryMatchSession session,
            string playerName,
            bool isBot,
            out string steamId64
        )
        {
            return TryResolveCanonicalSteamId64(session, playerName, steamId64: null, isBot, out steamId64);
        }

        public static bool TryResolveCanonicalSteamId64(
            TelemetryMatchSession session,
            string? playerName,
            string? steamId64,
            bool isBot,
            out string canonicalSteamId64
        )
        {
            canonicalSteamId64 = string.Empty;

            if (isBot)
            {
                return false;
            }

            if (MatchesRosterSteamId64(session, steamId64))
            {
                canonicalSteamId64 = steamId64!;
                return true;
            }

            if (
                string.IsNullOrWhiteSpace(playerName) ||
                session.TryResolveRosterPlayer(playerName, out TelemetryRosterPlayer? roster) == false ||
                roster == null
            )
            {
                return false;
            }

            canonicalSteamId64 = roster.SteamId64;
            return true;
        }

        public static bool MatchesRosterSteamId64(TelemetryMatchSession session, string? steamId64)
        {
            if (string.IsNullOrWhiteSpace(steamId64))
            {
                return false;
            }

            foreach (TelemetryRosterPlayer rosterPlayer in session.GetAllRosterPlayers())
            {
                if (string.Equals(rosterPlayer.SteamId64, steamId64, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public static class TelemetryRecordingWindowPolicy
    {
        public static bool CanCollectLiveTelemetry(
            bool collectionEnabled,
            TelemetryMatchSession? activeSession
        )
        {
            return collectionEnabled && activeSession != null;
        }

        public static bool CanFlushLiveTelemetry(
            bool collectionEnabled,
            TelemetryMatchSession? activeSession,
            int rosterMatchedHumansPresent
        )
        {
            return CanCollectLiveTelemetry(collectionEnabled, activeSession) &&
                TelemetryLiveBatchPolicy.HasEnoughRosterMatchedHumans(rosterMatchedHumansPresent);
        }

        public static TelemetryMatchSession? ResolveActiveSessionAfterBufferReset(
            TelemetryMatchSession? activeSession,
            bool preserveActiveSession
        )
        {
            return preserveActiveSession ? activeSession : null;
        }
    }

    public sealed class TelemetryLiveActivityCounters
    {
        public int Connects { get; set; }
        public int Disconnects { get; set; }
        public int RoundsPlayed { get; set; }
        public int ShotsFired { get; set; }
        public int HitsLanded { get; set; }
        public int BulletImpacts { get; set; }
        public int DamageDealt { get; set; }
        public int DamageTaken { get; set; }
        public int UtilityDamageDealt { get; set; }
        public int UtilityDamageTaken { get; set; }
        public int Kills { get; set; }
        public int Deaths { get; set; }
        public int FlashbangsThrown { get; set; }
        public int SmokesThrown { get; set; }
        public int MolotovsThrown { get; set; }
    }

    public static class TelemetryLiveBatchPolicy
    {
        public static bool HasEnoughRosterMatchedHumans(int rosterMatchedHumansPresent)
        {
            return rosterMatchedHumansPresent >= 2;
        }

        public static bool HasMeaningfulHumanActivity(
            IEnumerable<TelemetryLiveActivityCounters> players,
            int observationCount,
            int economyEventCount,
            int economySnapshotCount,
            int bombPlants,
            int bombDefuses
        )
        {
            if (
                observationCount > 0 ||
                economyEventCount > 0 ||
                economySnapshotCount > 0 ||
                bombPlants > 0 ||
                bombDefuses > 0
            )
            {
                return true;
            }

            foreach (TelemetryLiveActivityCounters player in players)
            {
                if (
                    player.ShotsFired > 0 ||
                    player.HitsLanded > 0 ||
                    player.BulletImpacts > 0 ||
                    player.DamageDealt > 0 ||
                    player.DamageTaken > 0 ||
                    player.UtilityDamageDealt > 0 ||
                    player.UtilityDamageTaken > 0 ||
                    player.Kills > 0 ||
                    player.Deaths > 0 ||
                    player.FlashbangsThrown > 0 ||
                    player.SmokesThrown > 0 ||
                    player.MolotovsThrown > 0
                )
                {
                    return true;
                }
            }

            return false;
        }
    }
}

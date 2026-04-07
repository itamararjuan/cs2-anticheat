using System.Globalization;
using System.Text.Json;
using CounterStrikeSharp.API;
using TBAntiCheat.Core;

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

    internal static class TelemetryManager
    {
        private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions()
        {
            WriteIndented = true
        };

        private static readonly HttpClient httpClient = new HttpClient();
        private static readonly List<ObservationRecord> pendingObservations = [];

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

        internal static void Initialize(ACCore core)
        {
            plugin = core;
            TelemetryConfig.Initialize();
            ResetState(string.Empty);

            Globals.Log("[TBAC] TelemetryManager Initialized");
        }

        internal static void OnConfigReloaded()
        {
            loggedUploadDisabled = false;
            loggedMissingEndpoint = false;
            Globals.Log("[TBAC] Telemetry config reload applied");
        }

        internal static void OnMapStart(string mapName)
        {
            Flush("map_start", force: true);
            ResetState(mapName);
        }

        internal static void OnMapEnd()
        {
            Flush("map_end", force: true);
        }

        internal static void OnRoundStart()
        {
            if (TelemetryConfig.Get().Config.CollectionEnabled == false)
            {
                return;
            }

            roundNumber++;
        }

        internal static void OnRoundEnd()
        {
            Flush("round_end", force: false);
        }

        internal static void OnGameTick()
        {
            Flush("interval", force: false);
        }

        internal static void OnPlayerJoined(PlayerData player)
        {
            if (TelemetryConfig.Get().Config.CollectionEnabled == false)
            {
                return;
            }

            PlayerTelemetryState state = GetOrCreateState(player);
            state.Connects++;
        }

        internal static void OnPlayerLeft(PlayerData player)
        {
            if (TelemetryConfig.Get().Config.CollectionEnabled == false)
            {
                return;
            }

            PlayerTelemetryState state = GetOrCreateState(player);
            state.Disconnects++;
        }

        internal static void OnPlayerSpawned(PlayerData player)
        {
            if (TelemetryConfig.Get().Config.CollectionEnabled == false)
            {
                return;
            }

            PlayerTelemetryState state = GetOrCreateState(player);
            state.RoundsPlayed++;
        }

        internal static void OnWeaponFire(PlayerData player, string weapon)
        {
            if (TelemetryConfig.Get().Config.CollectionEnabled == false)
            {
                return;
            }

            PlayerTelemetryState state = GetOrCreateState(player);
            state.ShotsFired++;

            string normalizedWeapon = NormalizeWeaponName(weapon);
            if (string.IsNullOrEmpty(normalizedWeapon) == false)
            {
                state.GetOrCreateWeapon(normalizedWeapon).ShotsFired++;
            }
        }

        internal static void OnBulletImpact(PlayerData player)
        {
            if (TelemetryConfig.Get().Config.CollectionEnabled == false)
            {
                return;
            }

            PlayerTelemetryState state = GetOrCreateState(player);
            state.BulletImpacts++;
        }

        internal static void OnPlayerBlind(PlayerData victim, PlayerData? attacker, float blindDuration)
        {
            if (TelemetryConfig.Get().Config.CollectionEnabled == false)
            {
                return;
            }

            PlayerTelemetryState state = GetOrCreateState(victim);
            state.BlindsReceived++;
            state.BlindDurationSeconds += blindDuration;

            DateTime blindUntilUtc = DateTime.UtcNow.AddSeconds(Math.Max(0f, blindDuration));
            if (blindUntilUtc > state.BlindUntilUtc)
            {
                state.BlindUntilUtc = blindUntilUtc;
            }

            if (blindDuration >= 2.5f && attacker != null)
            {
                RecordObservation(
                    victim,
                    "utility_telemetry",
                    "player_blinded",
                    $"Blinded for {blindDuration.ToString("0.##", CultureInfo.InvariantCulture)}s",
                    metadata: new Dictionary<string, string>()
                    {
                        ["attackerSteamId"] = attacker.SteamID,
                        ["attackerName"] = attacker.PlayerName,
                        ["blindDurationSeconds"] = blindDuration.ToString("0.##", CultureInfo.InvariantCulture)
                    }
                );
            }
        }

        internal static void OnFlashbangDetonate(PlayerData player)
        {
            if (TelemetryConfig.Get().Config.CollectionEnabled == false)
            {
                return;
            }

            GetOrCreateState(player).FlashbangsThrown++;
        }

        internal static void OnSmokeDetonate(PlayerData player)
        {
            if (TelemetryConfig.Get().Config.CollectionEnabled == false)
            {
                return;
            }

            GetOrCreateState(player).SmokesThrown++;
        }

        internal static void OnMolotovDetonate(PlayerData player)
        {
            if (TelemetryConfig.Get().Config.CollectionEnabled == false)
            {
                return;
            }

            GetOrCreateState(player).MolotovsThrown++;
        }

        internal static void OnPlayerFootstep(PlayerData player)
        {
            if (TelemetryConfig.Get().Config.CollectionEnabled == false)
            {
                return;
            }

            GetOrCreateState(player).Footsteps++;
        }

        internal static void OnPlayerSound(PlayerData player)
        {
            if (TelemetryConfig.Get().Config.CollectionEnabled == false)
            {
                return;
            }

            GetOrCreateState(player).Sounds++;
        }

        internal static void OnBombPlanted(PlayerData player)
        {
            if (TelemetryConfig.Get().Config.CollectionEnabled == false)
            {
                return;
            }

            bombPlants++;
            GetOrCreateState(player);
        }

        internal static void OnBombDefused(PlayerData player)
        {
            if (TelemetryConfig.Get().Config.CollectionEnabled == false)
            {
                return;
            }

            bombDefuses++;
            GetOrCreateState(player);
        }

        internal static void OnPlayerHurt(PlayerData victim, PlayerData? attacker, int damageHealth, string weapon, int hitgroup)
        {
            if (TelemetryConfig.Get().Config.CollectionEnabled == false)
            {
                return;
            }

            string normalizedWeapon = NormalizeWeaponName(weapon);
            PlayerTelemetryState victimState = GetOrCreateState(victim);
            victimState.DamageTaken += Math.Max(0, damageHealth);

            if (IsUtilityWeapon(normalizedWeapon))
            {
                victimState.UtilityDamageTaken += Math.Max(0, damageHealth);
            }

            if (attacker == null || attacker.Index == victim.Index)
            {
                return;
            }

            PlayerTelemetryState attackerState = GetOrCreateState(attacker);
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
                            ["victimSteamId"] = victim.SteamID,
                            ["victimName"] = victim.PlayerName,
                            ["hitgroup"] = hitgroup.ToString(CultureInfo.InvariantCulture)
                        }
                    );
                }
            }
        }

        internal static void OnPlayerDeath(PlayerData victim, PlayerData? attacker, string weapon, bool headshot, bool thruSmoke, int penetrated, bool attackerBlind, bool noScope, bool attackerInAir, float distance)
        {
            if (TelemetryConfig.Get().Config.CollectionEnabled == false)
            {
                return;
            }

            PlayerTelemetryState victimState = GetOrCreateState(victim);
            victimState.Deaths++;

            if (attacker == null || attacker.Index == victim.Index)
            {
                return;
            }

            PlayerTelemetryState attackerState = GetOrCreateState(attacker);
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

            if (interestingKill)
            {
                Dictionary<string, string> metadata = new Dictionary<string, string>()
                {
                    ["victimSteamId"] = victim.SteamID,
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
            if (TelemetryConfig.Get().Config.CollectionEnabled == false)
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
            if (config.CollectionEnabled == false)
            {
                return;
            }

            if (uploadInProgress)
            {
                return;
            }

            DateTime nowUtc = DateTime.UtcNow;
            if (force == false && nowUtc - lastFlushUtc < TimeSpan.FromSeconds(Math.Max(5, config.ReportingIntervalSeconds)))
            {
                return;
            }

            TelemetryBatch batch = BuildBatch(reason);
            if (batch.IsEmpty)
            {
                lastFlushUtc = nowUtc;
                return;
            }

            pendingObservations.Clear();
            lastFlushUtc = nowUtc;

            if (config.LogBatchSummaries)
            {
                Globals.Log($"[TBAC] Telemetry batch #{batch.BatchSequence} ({reason}) -> players: {batch.Players.Count}, observations: {batch.Observations.Count}");
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
                using HttpRequestMessage request = TelemetryRequestFactory.CreateUploadRequest(config, json);

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

        private static TelemetryBatch BuildBatch(string reason)
        {
            TelemetryConfigData config = TelemetryConfig.Get().Config;
            List<PlayerTelemetrySnapshot> players = [];
            foreach (PlayerTelemetryState? state in playerStates)
            {
                if (state == null || state.HasActivity() == false)
                {
                    continue;
                }

                AppendProfileObservations(state);
                players.Add(state.ToSnapshot());
            }

            List<ObservationRecord> observations = [];
            foreach (ObservationRecord observation in pendingObservations)
            {
                observations.Add(observation);
            }

            return new TelemetryBatch()
            {
                PluginVersion = plugin?.ModuleVersion ?? string.Empty,
                ServerId = config.ServerId,
                ServerLabel = config.ServerLabel,
                ServerRegion = config.ServerRegion,
                MatchSource = config.MatchSource,
                MatchId = config.MatchId,
                MapName = currentMap,
                FlushReason = reason,
                BatchSequence = ++batchSequence,
                RoundNumber = roundNumber,
                ServerTick = Server.TickCount,
                BombPlants = bombPlants,
                BombDefuses = bombDefuses,
                GeneratedAtUtc = DateTime.UtcNow,
                Players = players,
                Observations = observations
            };
        }

        private static void ResetState(string mapName)
        {
            playerStates = [];
            pendingObservations.Clear();
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
            existing.SteamID = player.SteamID;
            existing.PlayerName = player.PlayerName;

            return existing;
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
}

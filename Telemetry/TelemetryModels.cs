using System.Text.Json.Serialization;

namespace TBAntiCheat.Telemetry
{
    internal sealed class WeaponTelemetrySnapshot
    {
        public string Weapon { get; set; } = string.Empty;
        public string WeaponFamily { get; set; } = string.Empty;
        public int ShotsFired { get; set; }
        public int HitsLanded { get; set; }
        public int Kills { get; set; }
        public int DamageDealt { get; set; }
    }

    internal sealed class PlayerTelemetrySnapshot
    {
        public string SteamID { get; set; } = string.Empty;
        public string PlayerName { get; set; } = string.Empty;
        public int Slot { get; set; }
        public bool IsBot { get; set; }
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
        public int Headshots { get; set; }
        public int KillsWhileBlind { get; set; }
        public int DamageWhileBlind { get; set; }
        public int BlindsReceived { get; set; }
        public double BlindDurationSeconds { get; set; }
        public int FlashbangsThrown { get; set; }
        public int SmokesThrown { get; set; }
        public int MolotovsThrown { get; set; }
        public int Footsteps { get; set; }
        public int Sounds { get; set; }
        public List<WeaponTelemetrySnapshot> Weapons { get; set; } = [];
    }

    internal sealed class ObservationRecord
    {
        public string SteamID { get; set; } = string.Empty;
        public string PlayerName { get; set; } = string.Empty;
        public int Slot { get; set; }
        public bool IsBot { get; set; }
        public string Source { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string Weapon { get; set; } = string.Empty;
        public string WeaponFamily { get; set; } = string.Empty;
        public int RoundNumber { get; set; }
        public int ServerTick { get; set; }
        public DateTime ObservedAtUtc { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = [];
    }

    public sealed class EconomyEvent
    {
        public string SteamID { get; set; } = string.Empty;
        public string PlayerName { get; set; } = string.Empty;
        public int Slot { get; set; }
        public int Team { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string Item { get; set; } = string.Empty;
        public int Loadout { get; set; }
        public int RoundNumber { get; set; }
        public int ServerTick { get; set; }
        public DateTime ObservedAtUtc { get; set; }
        public int MoneyBefore { get; set; }
        public int MoneyAfter { get; set; }
        public int CashSpentThisRound { get; set; }
        public int StartAccount { get; set; }
    }

    public sealed class EconomySnapshot
    {
        public string SteamID { get; set; } = string.Empty;
        public string PlayerName { get; set; } = string.Empty;
        public int Slot { get; set; }
        public int Team { get; set; }
        public string SnapshotKind { get; set; } = string.Empty;
        public int RoundNumber { get; set; }
        public int ServerTick { get; set; }
        public DateTime ObservedAtUtc { get; set; }
        public int Money { get; set; }
        public int StartAccount { get; set; }
        public int CashSpentThisRound { get; set; }
        public int TotalCashSpent { get; set; }
        public List<string> InventoryItems { get; set; } = [];
    }

    internal sealed class TelemetryBatch
    {
        public string PluginName { get; set; } = "TB Anti-Cheat";
        public string PluginVersion { get; set; } = string.Empty;
        public string ServerId { get; set; } = string.Empty;
        public string ServerLabel { get; set; } = string.Empty;
        public string ServerRegion { get; set; } = string.Empty;
        public string MatchSource { get; set; } = string.Empty;
        public string MatchId { get; set; } = string.Empty;
        public string MapName { get; set; } = string.Empty;
        public string FlushReason { get; set; } = string.Empty;
        public long BatchSequence { get; set; }
        public int RoundNumber { get; set; }
        public int ServerTick { get; set; }
        public int BombPlants { get; set; }
        public int BombDefuses { get; set; }
        public DateTime GeneratedAtUtc { get; set; }
        public List<PlayerTelemetrySnapshot> Players { get; set; } = [];
        public List<ObservationRecord> Observations { get; set; } = [];
        public List<EconomyEvent> EconomyEvents { get; set; } = [];
        public List<EconomySnapshot> EconomySnapshots { get; set; } = [];

        [JsonIgnore]
        public bool IsEmpty =>
            Players.Count == 0 &&
            Observations.Count == 0 &&
            EconomyEvents.Count == 0 &&
            EconomySnapshots.Count == 0;
    }

    public sealed class MatchEconomyPurchaseSummary
    {
        public string Item { get; set; } = string.Empty;
        public int MoneyBefore { get; set; }
        public int MoneyAfter { get; set; }
    }

    public sealed class PlayerEconomyRoundSummary
    {
        public int RoundNumber { get; set; }
        public int StartMoney { get; set; }
        public int EndMoney { get; set; }
        public int SpentTotal { get; set; }
        public List<MatchEconomyPurchaseSummary> Purchases { get; set; } = [];
    }

    public sealed class PlayerEconomySummary
    {
        public string SteamID { get; set; } = string.Empty;
        public string PlayerName { get; set; } = string.Empty;
        public int Team { get; set; }
        public List<PlayerEconomyRoundSummary> Rounds { get; set; } = [];
    }

    public sealed class TeamEconomyRoundSummary
    {
        public int RoundNumber { get; set; }
        public int StartBudgetTotal { get; set; }
        public int EndBudgetTotal { get; set; }
        public int SpentTotal { get; set; }
    }

    public sealed class TeamEconomySummary
    {
        public int Team { get; set; }
        public List<TeamEconomyRoundSummary> Rounds { get; set; } = [];
    }

    public sealed class MatchEconomySummary
    {
        public string PluginName { get; set; } = "TB Anti-Cheat";
        public string PluginVersion { get; set; } = string.Empty;
        public string MatchId { get; set; } = string.Empty;
        public string MatchSource { get; set; } = string.Empty;
        public string ServerId { get; set; } = string.Empty;
        public string ServerLabel { get; set; } = string.Empty;
        public string ServerRegion { get; set; } = string.Empty;
        public string MapName { get; set; } = string.Empty;
        public DateTime GeneratedAtUtc { get; set; }
        public List<PlayerEconomySummary> Players { get; set; } = [];
        public List<TeamEconomySummary> Teams { get; set; } = [];
    }
}

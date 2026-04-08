using System.Text.Json.Serialization;

namespace TBAntiCheat.Telemetry
{
    public sealed class TelemetryRosterPlayer
    {
        [JsonPropertyName("steamId64")]
        public string SteamId64 { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("userId")]
        public string? UserId { get; set; }
    }

    public sealed class TelemetryMatchSession
    {
        private List<TelemetryRosterPlayer> team1 = [];
        private List<TelemetryRosterPlayer> team2 = [];

        [JsonPropertyName("matchId")]
        public string MatchId { get; set; } = string.Empty;

        [JsonPropertyName("matchSource")]
        public string MatchSource { get; set; } = string.Empty;

        [JsonPropertyName("serverId")]
        public string ServerId { get; set; } = string.Empty;

        [JsonPropertyName("serverLabel")]
        public string ServerLabel { get; set; } = string.Empty;

        [JsonPropertyName("serverRegion")]
        public string ServerRegion { get; set; } = string.Empty;

        [JsonPropertyName("mapName")]
        public string MapName { get; set; } = string.Empty;

        [JsonPropertyName("reportingIntervalSeconds")]
        public int ReportingIntervalSeconds { get; set; } = 120;

        [JsonPropertyName("team1")]
        public List<TelemetryRosterPlayer> Team1
        {
            get => team1;
            set => team1 = value ?? [];
        }

        [JsonPropertyName("team2")]
        public List<TelemetryRosterPlayer> Team2
        {
            get => team2;
            set => team2 = value ?? [];
        }

        public IEnumerable<TelemetryRosterPlayer> GetAllRosterPlayers()
        {
            foreach (TelemetryRosterPlayer rosterPlayer in Team1)
            {
                yield return rosterPlayer;
            }

            foreach (TelemetryRosterPlayer rosterPlayer in Team2)
            {
                yield return rosterPlayer;
            }
        }

        public bool TryResolveRosterPlayer(string playerName, out TelemetryRosterPlayer? rosterPlayer)
        {
            rosterPlayer = null;

            if (string.IsNullOrWhiteSpace(playerName))
            {
                return false;
            }

            string normalizedPlayerName = NormalizePlayerName(playerName);
            foreach (TelemetryRosterPlayer candidate in GetAllRosterPlayers())
            {
                if (
                    string.Equals(
                        NormalizePlayerName(candidate.Name),
                        normalizedPlayerName,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    rosterPlayer = candidate;
                    return true;
                }
            }

            return false;
        }

        public bool IsValid(out string? reason)
        {
            if (string.IsNullOrWhiteSpace(MatchId))
            {
                reason = "MatchId is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(MatchSource))
            {
                reason = "MatchSource is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(ServerId))
            {
                reason = "ServerId is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(ServerLabel))
            {
                reason = "ServerLabel is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(ServerRegion))
            {
                reason = "ServerRegion is required.";
                return false;
            }

            if (ReportingIntervalSeconds <= 0)
            {
                reason = "ReportingIntervalSeconds must be greater than 0.";
                return false;
            }

            if (TryValidateRoster(Team1, "Team1", out reason) == false)
            {
                return false;
            }

            if (TryValidateRoster(Team2, "Team2", out reason) == false)
            {
                return false;
            }

            if (TryValidateUniqueRosterNames(out reason) == false)
            {
                return false;
            }

            if (Team1.Count + Team2.Count < 2)
            {
                reason = "Combined roster must contain at least 2 players.";
                return false;
            }

            reason = null;
            return true;
        }

        private bool TryValidateUniqueRosterNames(out string? reason)
        {
            HashSet<string> normalizedNames = new(StringComparer.OrdinalIgnoreCase);
            foreach (TelemetryRosterPlayer rosterPlayer in GetAllRosterPlayers())
            {
                string normalizedPlayerName = NormalizePlayerName(rosterPlayer.Name);
                if (normalizedNames.Add(normalizedPlayerName) == false)
                {
                    reason = $"Duplicate roster player name '{normalizedPlayerName}' is not allowed.";
                    return false;
                }
            }

            reason = null;
            return true;
        }

        public bool TryValidate(out string? reason)
        {
            return IsValid(out reason);
        }

        private static bool TryValidateRoster(
            IReadOnlyList<TelemetryRosterPlayer> roster,
            string rosterName,
            out string? reason
        )
        {
            for (int index = 0; index < roster.Count; index++)
            {
                TelemetryRosterPlayer rosterPlayer = roster[index];
                if (string.IsNullOrWhiteSpace(rosterPlayer.SteamId64))
                {
                    reason = $"{rosterName}[{index}] SteamId64 is required.";
                    return false;
                }

                if (IsValidSteamId64(rosterPlayer.SteamId64) == false)
                {
                    reason = $"{rosterName}[{index}] SteamId64 must be a 15-20 digit string.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(rosterPlayer.Name))
                {
                    reason = $"{rosterName}[{index}] Name is required.";
                    return false;
                }
            }

            reason = null;
            return true;
        }

        private static bool IsValidSteamId64(string steamId64)
        {
            if (steamId64.Length < 15 || steamId64.Length > 20)
            {
                return false;
            }

            foreach (char character in steamId64)
            {
                if (char.IsDigit(character) == false)
                {
                    return false;
                }
            }

            return true;
        }

        private static string NormalizePlayerName(string playerName)
        {
            return playerName.Trim();
        }
    }
}

using System.Globalization;

namespace TBAntiCheat.Telemetry;

internal sealed record MatchSignalObservation(
    string SteamId,
    string PlayerName,
    string Source,
    string Kind,
    string Summary,
    Dictionary<string, string> Metadata
);

internal sealed record MatchSignalPlayerSnapshot(
    int TotalBurstOccurrences,
    int RoundsWithBurst,
    int LargestBurstCount,
    double LargestEnemyShare,
    int BurstsAt50PercentOrHigher,
    int BurstsAt75PercentOrHigher
);

internal sealed class MatchSignalPlayerState
{
    internal required string SteamId { get; init; }
    internal string PlayerName { get; set; } = string.Empty;
    internal bool IsConnected { get; set; }
    internal int CurrentTeamNumber { get; set; }
    internal int LastBurstRoundNumber { get; set; }
    internal int TotalBurstOccurrences { get; set; }
    internal int RoundsWithBurst { get; set; }
    internal int LargestBurstCount { get; set; }
    internal double LargestEnemyShare { get; set; }
    internal int BurstsAt50PercentOrHigher { get; set; }
    internal int BurstsAt75PercentOrHigher { get; set; }
    internal int DisconnectCount { get; set; }
    internal int LastDisconnectRound { get; set; } = -1;
    internal int LastReconnectRound { get; set; } = -1;
    internal bool HasActiveReconnectPhase { get; set; }
    internal bool ReconnectObservationEmitted { get; set; }
    internal int PreReconnectRounds { get; set; }
    internal int PostReconnectRounds { get; set; }
    internal int PreReconnectKills { get; set; }
    internal int PostReconnectKills { get; set; }
    internal int PreReconnectBurstOccurrences { get; set; }
    internal int PostReconnectBurstOccurrences { get; set; }
    internal int PreReconnectHighSignalKills { get; set; }
    internal int PostReconnectHighSignalKills { get; set; }
    internal int PreReconnectTeamRoundWins { get; set; }
    internal int PostReconnectTeamRoundWins { get; set; }
}

internal sealed class TelemetryMatchSignalTracker
{
    private readonly Dictionary<string, MatchSignalPlayerState> players = new(StringComparer.Ordinal);

    internal MatchSignalObservation[] RegisterBurst(
        string steamId,
        string playerName,
        int roundNumber,
        int burstKillCount,
        int opposingRosterSize)
    {
        MatchSignalPlayerState state = GetOrCreatePlayer(steamId, playerName);

        if (state.HasActiveReconnectPhase)
        {
            state.PostReconnectBurstOccurrences++;
        }
        else
        {
            state.PreReconnectBurstOccurrences++;
        }

        double enemyShare = opposingRosterSize <= 0 ? 0d : (double)burstKillCount / opposingRosterSize;

        state.TotalBurstOccurrences++;
        state.LargestBurstCount = Math.Max(state.LargestBurstCount, burstKillCount);
        state.LargestEnemyShare = Math.Max(state.LargestEnemyShare, enemyShare);

        if (enemyShare >= 0.5d)
        {
            state.BurstsAt50PercentOrHigher++;
        }

        if (enemyShare >= 0.75d)
        {
            state.BurstsAt75PercentOrHigher++;
        }

        if (state.LastBurstRoundNumber != roundNumber)
        {
            state.LastBurstRoundNumber = roundNumber;
            state.RoundsWithBurst++;
        }

        List<MatchSignalObservation> observations = [];
        if (state.TotalBurstOccurrences is 2 or 3)
        {
            observations.Add(
                new MatchSignalObservation(
                    steamId,
                    state.PlayerName,
                    "combat_profile",
                    "match_repeated_kill_bursts",
                    $"Player reached {state.TotalBurstOccurrences} burst occurrences in the recorded match",
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["totalBurstOccurrences"] = state.TotalBurstOccurrences.ToString(),
                        ["roundsWithBurst"] = state.RoundsWithBurst.ToString(),
                        ["largestBurstCount"] = state.LargestBurstCount.ToString(),
                        ["largestEnemyShare"] = state.LargestEnemyShare.ToString("0.###", CultureInfo.InvariantCulture),
                    }
                )
            );
        }

        return [.. observations];
    }

    internal MatchSignalObservation[] RecordRoundResult(int roundNumber, int winningTeam)
    {
        if (winningTeam <= 1)
        {
            return [];
        }

        List<MatchSignalObservation> observations = [];

        foreach (MatchSignalPlayerState state in players.Values)
        {
            if (state.IsConnected == false || state.CurrentTeamNumber <= 1)
            {
                continue;
            }

            if (state.HasActiveReconnectPhase == false)
            {
                state.PreReconnectRounds++;
                if (state.CurrentTeamNumber == winningTeam)
                {
                    state.PreReconnectTeamRoundWins++;
                }

                continue;
            }

            state.PostReconnectRounds++;
            if (state.CurrentTeamNumber == winningTeam)
            {
                state.PostReconnectTeamRoundWins++;
            }

            bool enoughRounds = state.PostReconnectRounds >= 2;
            bool personalSpike =
                state.PostReconnectKills > state.PreReconnectKills &&
                state.PostReconnectBurstOccurrences > state.PreReconnectBurstOccurrences;
            bool teamSpike = state.PostReconnectTeamRoundWins > state.PreReconnectTeamRoundWins;

            if (enoughRounds && personalSpike && teamSpike && state.ReconnectObservationEmitted == false)
            {
                state.ReconnectObservationEmitted = true;
                observations.Add(
                    new MatchSignalObservation(
                        state.SteamId,
                        state.PlayerName,
                        "combat_profile",
                        "reconnect_post_phase_spike",
                        $"Player impact increased after reconnect in rounds {state.LastReconnectRound}-{roundNumber}",
                        new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["disconnectCount"] = state.DisconnectCount.ToString(),
                            ["disconnectRound"] = state.LastDisconnectRound.ToString(),
                            ["reconnectRound"] = state.LastReconnectRound.ToString(),
                            ["preReconnectKills"] = state.PreReconnectKills.ToString(),
                            ["postReconnectKills"] = state.PostReconnectKills.ToString(),
                            ["preReconnectBurstOccurrences"] = state.PreReconnectBurstOccurrences.ToString(),
                            ["postReconnectBurstOccurrences"] = state.PostReconnectBurstOccurrences.ToString(),
                            ["preReconnectTeamRoundWins"] = state.PreReconnectTeamRoundWins.ToString(),
                            ["postReconnectTeamRoundWins"] = state.PostReconnectTeamRoundWins.ToString(),
                        }
                    )
                );
            }
        }

        return [.. observations];
    }

    internal void RecordDisconnect(string steamId, int roundNumber)
    {
        MatchSignalPlayerState state = GetOrCreatePlayer(steamId, playerName: string.Empty);
        state.DisconnectCount++;
        state.IsConnected = false;
        state.CurrentTeamNumber = 0;
        state.LastDisconnectRound = roundNumber;
    }

    internal void RecordReconnect(string steamId, int roundNumber, int teamNumber)
    {
        MatchSignalPlayerState state = GetOrCreatePlayer(steamId, playerName: string.Empty);
        bool hasPendingDisconnect = state.LastDisconnectRound > state.LastReconnectRound;

        if (hasPendingDisconnect == false)
        {
            state.IsConnected = true;
            state.CurrentTeamNumber = teamNumber;
            return;
        }

        if (state.HasActiveReconnectPhase)
        {
            state.PreReconnectKills += state.PostReconnectKills;
            state.PreReconnectBurstOccurrences += state.PostReconnectBurstOccurrences;
            state.PreReconnectHighSignalKills += state.PostReconnectHighSignalKills;
            state.PreReconnectRounds += state.PostReconnectRounds;
            state.PreReconnectTeamRoundWins += state.PostReconnectTeamRoundWins;
        }

        state.HasActiveReconnectPhase = true;
        state.IsConnected = true;
        state.LastReconnectRound = roundNumber;
        state.CurrentTeamNumber = teamNumber;
        state.PostReconnectKills = 0;
        state.PostReconnectBurstOccurrences = 0;
        state.PostReconnectHighSignalKills = 0;
        state.PostReconnectRounds = 0;
        state.PostReconnectTeamRoundWins = 0;
        state.ReconnectObservationEmitted = false;
    }

    internal void UpdatePlayerTeam(string steamId, string playerName, int teamNumber)
    {
        MatchSignalPlayerState state = GetOrCreatePlayer(steamId, playerName);
        state.IsConnected = true;
        state.CurrentTeamNumber = teamNumber;
    }

    internal void RecordKill(string steamId, string playerName, int teamNumber, bool isHighSignalKill)
    {
        MatchSignalPlayerState state = GetOrCreatePlayer(steamId, playerName);
        state.IsConnected = true;
        state.CurrentTeamNumber = teamNumber;

        if (state.HasActiveReconnectPhase)
        {
            state.PostReconnectKills++;
            if (isHighSignalKill)
            {
                state.PostReconnectHighSignalKills++;
            }

            return;
        }

        state.PreReconnectKills++;
        if (isHighSignalKill)
        {
            state.PreReconnectHighSignalKills++;
        }
    }

    internal MatchSignalPlayerSnapshot GetSnapshot(string steamId)
    {
        MatchSignalPlayerState state = players[steamId];
        return new MatchSignalPlayerSnapshot(
            state.TotalBurstOccurrences,
            state.RoundsWithBurst,
            state.LargestBurstCount,
            state.LargestEnemyShare,
            state.BurstsAt50PercentOrHigher,
            state.BurstsAt75PercentOrHigher
        );
    }

    private MatchSignalPlayerState GetOrCreatePlayer(string steamId, string playerName)
    {
        if (players.TryGetValue(steamId, out MatchSignalPlayerState? state))
        {
            if (string.IsNullOrWhiteSpace(playerName) == false)
            {
                state.PlayerName = playerName;
            }

            return state;
        }

        state = new MatchSignalPlayerState { SteamId = steamId, PlayerName = playerName, LastBurstRoundNumber = -1 };
        players[steamId] = state;
        return state;
    }
}

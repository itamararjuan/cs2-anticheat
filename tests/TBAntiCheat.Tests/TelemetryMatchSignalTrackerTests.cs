using TBAntiCheat.Telemetry;
using Xunit;

namespace TBAntiCheat.Tests;

public sealed class TelemetryMatchSignalTrackerTests
{
    [Fact]
    public void RegisterBurstCountsOccurrencesAndRoundsOnlyOncePerRound()
    {
        TelemetryMatchSignalTracker tracker = new();

        MatchSignalObservation[] first = tracker.RegisterBurst(
            steamId: "76561198850110308",
            playerName: "PlayerOne",
            roundNumber: 3,
            burstKillCount: 3,
            opposingRosterSize: 5
        );
        MatchSignalObservation[] second = tracker.RegisterBurst(
            steamId: "76561198850110308",
            playerName: "PlayerOne",
            roundNumber: 3,
            burstKillCount: 4,
            opposingRosterSize: 5
        );
        MatchSignalPlayerSnapshot snapshot = tracker.GetSnapshot("76561198850110308");

        Assert.Empty(first);
        Assert.Single(second);
        Assert.Equal(2, snapshot.TotalBurstOccurrences);
        Assert.Equal(1, snapshot.RoundsWithBurst);
        Assert.Equal(4, snapshot.LargestBurstCount);
    }

    [Fact]
    public void RegisterBurstTracksEnemyShareSeverityThresholds()
    {
        TelemetryMatchSignalTracker tracker = new();

        tracker.RegisterBurst("76561198179279779", "PlayerTwo", roundNumber: 7, burstKillCount: 3, opposingRosterSize: 5);
        tracker.RegisterBurst("76561198179279779", "PlayerTwo", roundNumber: 8, burstKillCount: 4, opposingRosterSize: 5);

        MatchSignalPlayerSnapshot snapshot = tracker.GetSnapshot("76561198179279779");

        Assert.Equal(0.8d, snapshot.LargestEnemyShare, 3);
        Assert.Equal(2, snapshot.BurstsAt50PercentOrHigher);
        Assert.Equal(1, snapshot.BurstsAt75PercentOrHigher);
    }

    [Fact]
    public void ReconnectSignalRequiresDisconnectReconnectAndMinimumPostRounds()
    {
        TelemetryMatchSignalTracker tracker = new();

        tracker.RecordKill("76561198850110308", "PlayerOne", teamNumber: 2, isHighSignalKill: false);
        tracker.RecordDisconnect("76561198850110308", roundNumber: 6);
        tracker.RecordReconnect("76561198850110308", roundNumber: 7, teamNumber: 2);
        tracker.RecordKill("76561198850110308", "PlayerOne", teamNumber: 2, isHighSignalKill: true);
        MatchSignalObservation[] observations = tracker.RecordRoundResult(roundNumber: 7, winningTeam: 2);

        Assert.Empty(observations);
    }

    [Fact]
    public void ReconnectSignalEmitsWhenPersonalImpactAndTeamWinsBothIncrease()
    {
        TelemetryMatchSignalTracker tracker = new();

        tracker.RecordKill("76561198850110308", "PlayerOne", teamNumber: 2, isHighSignalKill: false);
        tracker.RecordRoundResult(roundNumber: 4, winningTeam: 3);
        tracker.RecordDisconnect("76561198850110308", roundNumber: 6);
        tracker.RecordReconnect("76561198850110308", roundNumber: 7, teamNumber: 2);

        tracker.RecordKill("76561198850110308", "PlayerOne", teamNumber: 2, isHighSignalKill: true);
        tracker.RegisterBurst("76561198850110308", "PlayerOne", roundNumber: 7, burstKillCount: 3, opposingRosterSize: 5);
        tracker.RecordRoundResult(roundNumber: 7, winningTeam: 2);
        tracker.RecordKill("76561198850110308", "PlayerOne", teamNumber: 2, isHighSignalKill: true);
        tracker.RegisterBurst("76561198850110308", "PlayerOne", roundNumber: 8, burstKillCount: 4, opposingRosterSize: 5);
        MatchSignalObservation[] observations = tracker.RecordRoundResult(roundNumber: 8, winningTeam: 2);

        MatchSignalObservation spike = Assert.Single(observations);
        Assert.Equal("reconnect_post_phase_spike", spike.Kind);
        Assert.Equal("2", spike.Metadata["postReconnectBurstOccurrences"]);
        Assert.Equal("2", spike.Metadata["postReconnectTeamRoundWins"]);
    }

    [Fact]
    public void ReconnectSignalRequiresRealDisconnectBeforeReconnectPhaseStarts()
    {
        TelemetryMatchSignalTracker tracker = new();

        tracker.RecordKill("76561198850110308", "PlayerOne", teamNumber: 2, isHighSignalKill: false);
        tracker.RecordRoundResult(roundNumber: 4, winningTeam: 3);

        tracker.RecordReconnect("76561198850110308", roundNumber: 7, teamNumber: 2);
        tracker.RecordKill("76561198850110308", "PlayerOne", teamNumber: 2, isHighSignalKill: true);
        tracker.RegisterBurst("76561198850110308", "PlayerOne", roundNumber: 7, burstKillCount: 3, opposingRosterSize: 5);
        tracker.RecordRoundResult(roundNumber: 7, winningTeam: 2);
        tracker.RecordKill("76561198850110308", "PlayerOne", teamNumber: 2, isHighSignalKill: true);
        tracker.RegisterBurst("76561198850110308", "PlayerOne", roundNumber: 8, burstKillCount: 4, opposingRosterSize: 5);
        MatchSignalObservation[] observations = tracker.RecordRoundResult(roundNumber: 8, winningTeam: 2);

        Assert.Empty(observations);
    }

    [Fact]
    public void ReconnectSignalUsesAllPriorMatchActivityAsBaselineAfterLaterReconnect()
    {
        TelemetryMatchSignalTracker tracker = new();

        tracker.RecordKill("76561198850110308", "PlayerOne", teamNumber: 2, isHighSignalKill: false);

        tracker.RecordDisconnect("76561198850110308", roundNumber: 6);
        tracker.RecordReconnect("76561198850110308", roundNumber: 7, teamNumber: 2);
        tracker.RecordKill("76561198850110308", "PlayerOne", teamNumber: 2, isHighSignalKill: true);
        tracker.RegisterBurst("76561198850110308", "PlayerOne", roundNumber: 7, burstKillCount: 3, opposingRosterSize: 5);
        tracker.RecordRoundResult(roundNumber: 7, winningTeam: 2);
        tracker.RecordKill("76561198850110308", "PlayerOne", teamNumber: 2, isHighSignalKill: true);
        tracker.RegisterBurst("76561198850110308", "PlayerOne", roundNumber: 8, burstKillCount: 4, opposingRosterSize: 5);
        MatchSignalObservation[] firstReconnectObservations = tracker.RecordRoundResult(roundNumber: 8, winningTeam: 2);

        Assert.Single(firstReconnectObservations);

        tracker.RecordDisconnect("76561198850110308", roundNumber: 9);
        tracker.RecordReconnect("76561198850110308", roundNumber: 10, teamNumber: 2);
        tracker.RecordKill("76561198850110308", "PlayerOne", teamNumber: 2, isHighSignalKill: true);
        tracker.RegisterBurst("76561198850110308", "PlayerOne", roundNumber: 10, burstKillCount: 3, opposingRosterSize: 5);
        tracker.RecordRoundResult(roundNumber: 10, winningTeam: 2);
        tracker.RecordKill("76561198850110308", "PlayerOne", teamNumber: 2, isHighSignalKill: true);
        tracker.RegisterBurst("76561198850110308", "PlayerOne", roundNumber: 11, burstKillCount: 4, opposingRosterSize: 5);
        MatchSignalObservation[] secondReconnectObservations = tracker.RecordRoundResult(roundNumber: 11, winningTeam: 2);

        Assert.Empty(secondReconnectObservations);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ReconnectSignalDoesNotEmitWhenWinningTeamIsInvalid(int invalidWinningTeam)
    {
        TelemetryMatchSignalTracker tracker = new();

        tracker.RecordKill("76561198850110308", "PlayerOne", teamNumber: 2, isHighSignalKill: false);
        tracker.RecordRoundResult(roundNumber: 4, winningTeam: 3);
        tracker.RecordDisconnect("76561198850110308", roundNumber: 6);
        tracker.RecordReconnect("76561198850110308", roundNumber: 7, teamNumber: 2);

        tracker.RecordKill("76561198850110308", "PlayerOne", teamNumber: 2, isHighSignalKill: true);
        tracker.RegisterBurst("76561198850110308", "PlayerOne", roundNumber: 7, burstKillCount: 3, opposingRosterSize: 5);
        tracker.RecordRoundResult(roundNumber: 7, winningTeam: 2);
        tracker.RecordKill("76561198850110308", "PlayerOne", teamNumber: 2, isHighSignalKill: true);
        tracker.RegisterBurst("76561198850110308", "PlayerOne", roundNumber: 8, burstKillCount: 4, opposingRosterSize: 5);
        MatchSignalObservation[] observations = tracker.RecordRoundResult(roundNumber: 8, winningTeam: invalidWinningTeam);

        Assert.Empty(observations);
    }
}

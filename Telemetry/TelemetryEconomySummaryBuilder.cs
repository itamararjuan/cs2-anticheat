namespace TBAntiCheat.Telemetry
{
    public static class TelemetryEconomySummaryBuilder
    {
        public static MatchEconomySummary Build(
            string pluginVersion,
            TelemetryMatchSession session,
            IReadOnlyList<EconomyEvent>? economyEvents,
            IReadOnlyList<EconomySnapshot>? economySnapshots
        )
        {
            ArgumentNullException.ThrowIfNull(session);

            economyEvents ??= [];
            economySnapshots ??= [];

            List<EconomyEvent> rosterEvents = [];
            foreach (EconomyEvent economyEvent in economyEvents)
            {
                if (TelemetryLiveIdentity.MatchesRosterSteamId64(session, economyEvent.SteamID) == false)
                {
                    continue;
                }

                rosterEvents.Add(economyEvent);
            }

            List<EconomySnapshot> rosterSnapshots = [];
            foreach (EconomySnapshot snapshot in economySnapshots)
            {
                if (TelemetryLiveIdentity.MatchesRosterSteamId64(session, snapshot.SteamID) == false)
                {
                    continue;
                }

                rosterSnapshots.Add(snapshot);
            }

            Dictionary<string, HashSet<int>> roundsBySteam = [];
            foreach (EconomyEvent economyEvent in rosterEvents)
            {
                AddRound(roundsBySteam, economyEvent.SteamID, economyEvent.RoundNumber);
            }

            foreach (EconomySnapshot snapshot in rosterSnapshots)
            {
                AddRound(roundsBySteam, snapshot.SteamID, snapshot.RoundNumber);
            }

            Dictionary<(string SteamId, int RoundNumber), int> roundTeams = [];
            List<PlayerEconomySummary> players = [];
            foreach (TelemetryRosterPlayer rosterPlayer in session.GetAllRosterPlayers())
            {
                string steamId = rosterPlayer.SteamId64;
                if (roundsBySteam.TryGetValue(steamId, out HashSet<int>? roundSet) == false || roundSet.Count == 0)
                {
                    continue;
                }

                int team = ResolveTeamForPlayer(session, steamId, rosterEvents, rosterSnapshots);
                string displayName = rosterPlayer.Name;
                List<int> roundsSorted = [.. roundSet];
                roundsSorted.Sort();

                List<PlayerEconomyRoundSummary> playerRounds = [];
                foreach (int roundNumber in roundsSorted)
                {
                    List<EconomyEvent> purchases = [];
                    foreach (EconomyEvent economyEvent in rosterEvents)
                    {
                        if (
                            string.Equals(economyEvent.SteamID, steamId, StringComparison.Ordinal) == false
                            || economyEvent.RoundNumber != roundNumber
                        )
                        {
                            continue;
                        }

                        if (string.Equals(economyEvent.EventType, "purchase", StringComparison.OrdinalIgnoreCase) == false)
                        {
                            continue;
                        }

                        purchases.Add(economyEvent);
                    }

                    purchases.Sort(ComparePurchaseOrder);

                    List<EconomySnapshot> roundSnapshots = [];
                    foreach (EconomySnapshot snapshot in rosterSnapshots)
                    {
                        if (
                            string.Equals(snapshot.SteamID, steamId, StringComparison.Ordinal) == false
                            || snapshot.RoundNumber != roundNumber
                        )
                        {
                            continue;
                        }

                        roundSnapshots.Add(snapshot);
                    }

                    roundSnapshots.Sort(CompareSnapshotOrder);

                    int spentFromPurchases = 0;
                    List<MatchEconomyPurchaseSummary> purchaseSummaries = [];
                    foreach (EconomyEvent purchase in purchases)
                    {
                        int delta = Math.Max(0, purchase.MoneyBefore - purchase.MoneyAfter);
                        spentFromPurchases += delta;
                        purchaseSummaries.Add(
                            new MatchEconomyPurchaseSummary()
                            {
                                Item = purchase.Item,
                                MoneyBefore = purchase.MoneyBefore,
                                MoneyAfter = purchase.MoneyAfter
                            }
                        );
                    }

                    int startMoney = 0;
                    if (roundSnapshots.Count > 0)
                    {
                        startMoney = Math.Max(0, roundSnapshots[0].StartAccount);
                    }
                    else if (purchases.Count > 0)
                    {
                        startMoney = Math.Max(0, purchases[0].MoneyBefore);
                    }

                    int endMoney;
                    if (roundSnapshots.Count > 0)
                    {
                        endMoney = Math.Max(0, roundSnapshots[^1].Money);
                    }
                    else if (purchases.Count > 0)
                    {
                        endMoney = Math.Max(0, purchases[^1].MoneyAfter);
                    }
                    else
                    {
                        endMoney = startMoney;
                    }

                    int spentTotal = spentFromPurchases;
                    if (spentTotal == 0 && roundSnapshots.Count > 0)
                    {
                        int lastCashSpent = Math.Max(0, roundSnapshots[^1].CashSpentThisRound);
                        if (lastCashSpent > 0)
                        {
                            spentTotal = lastCashSpent;
                        }
                    }

                    roundTeams[(steamId, roundNumber)] = ResolveTeamForRound(
                        purchases,
                        roundSnapshots,
                        team
                    );

                    playerRounds.Add(
                        new PlayerEconomyRoundSummary()
                        {
                            RoundNumber = roundNumber,
                            StartMoney = startMoney,
                            EndMoney = endMoney,
                            SpentTotal = spentTotal,
                            Purchases = purchaseSummaries
                        }
                    );
                }

                players.Add(
                    new PlayerEconomySummary()
                    {
                        SteamID = steamId,
                        PlayerName = displayName,
                        Team = team,
                        Rounds = playerRounds
                    }
                );
            }

            players.Sort(ComparePlayerOrder);

            Dictionary<(int Team, int Round), TeamEconomyAccumulator> teamRoundTotals = [];
            foreach (PlayerEconomySummary player in players)
            {
                foreach (PlayerEconomyRoundSummary round in player.Rounds)
                {
                    int roundTeam = player.Team;
                    if (roundTeams.TryGetValue((player.SteamID, round.RoundNumber), out int observedRoundTeam))
                    {
                        roundTeam = observedRoundTeam;
                    }

                    (int Team, int Round) key = (roundTeam, round.RoundNumber);
                    if (teamRoundTotals.TryGetValue(key, out TeamEconomyAccumulator? acc) == false)
                    {
                        acc = new TeamEconomyAccumulator();
                        teamRoundTotals[key] = acc;
                    }

                    acc.StartBudgetTotal += round.StartMoney;
                    acc.EndBudgetTotal += round.EndMoney;
                    acc.SpentTotal += round.SpentTotal;
                }
            }

            List<TeamEconomySummary> teams = [];
            HashSet<int> teamIds = [];
            foreach ((int Team, int Round) key in teamRoundTotals.Keys)
            {
                teamIds.Add(key.Team);
            }

            List<int> teamOrder = [.. teamIds];
            teamOrder.Sort();

            foreach (int teamId in teamOrder)
            {
                List<int> teamRounds = [];
                foreach (KeyValuePair<(int Team, int Round), TeamEconomyAccumulator> entry in teamRoundTotals)
                {
                    if (entry.Key.Team == teamId)
                    {
                        teamRounds.Add(entry.Key.Round);
                    }
                }

                teamRounds.Sort();
                List<TeamEconomyRoundSummary> teamRoundSummaries = [];
                foreach (int r in teamRounds)
                {
                    TeamEconomyAccumulator acc = teamRoundTotals[(teamId, r)];
                    teamRoundSummaries.Add(
                        new TeamEconomyRoundSummary()
                        {
                            RoundNumber = r,
                            StartBudgetTotal = acc.StartBudgetTotal,
                            EndBudgetTotal = acc.EndBudgetTotal,
                            SpentTotal = acc.SpentTotal
                        }
                    );
                }

                teams.Add(
                    new TeamEconomySummary()
                    {
                        Team = teamId,
                        Rounds = teamRoundSummaries
                    }
                );
            }

            return new MatchEconomySummary
            {
                PluginVersion = pluginVersion,
                MatchId = session.MatchId,
                MatchSource = session.MatchSource,
                ServerId = session.ServerId,
                ServerLabel = session.ServerLabel,
                ServerRegion = session.ServerRegion,
                MapName = session.MapName,
                GeneratedAtUtc = DateTime.UtcNow,
                Players = players,
                Teams = teams,
            };
        }

        private static void AddRound(Dictionary<string, HashSet<int>> roundsBySteam, string steamId, int roundNumber)
        {
            if (string.IsNullOrEmpty(steamId) || roundNumber <= 0)
            {
                return;
            }

            if (roundsBySteam.TryGetValue(steamId, out HashSet<int>? set) == false)
            {
                set = [];
                roundsBySteam[steamId] = set;
            }

            set.Add(roundNumber);
        }

        private static int ComparePurchaseOrder(EconomyEvent a, EconomyEvent b)
        {
            int tickCompare = a.ServerTick.CompareTo(b.ServerTick);
            if (tickCompare != 0)
            {
                return tickCompare;
            }

            return a.ObservedAtUtc.CompareTo(b.ObservedAtUtc);
        }

        private static int CompareSnapshotOrder(EconomySnapshot a, EconomySnapshot b)
        {
            int tickCompare = a.ServerTick.CompareTo(b.ServerTick);
            if (tickCompare != 0)
            {
                return tickCompare;
            }

            return a.ObservedAtUtc.CompareTo(b.ObservedAtUtc);
        }

        private static int ComparePlayerOrder(PlayerEconomySummary a, PlayerEconomySummary b)
        {
            int teamCompare = a.Team.CompareTo(b.Team);
            if (teamCompare != 0)
            {
                return teamCompare;
            }

            return string.Compare(a.SteamID, b.SteamID, StringComparison.Ordinal);
        }

        private static int ResolveTeamForRound(
            IReadOnlyList<EconomyEvent> purchases,
            IReadOnlyList<EconomySnapshot> roundSnapshots,
            int fallbackTeam
        )
        {
            int bestTick = int.MinValue;
            DateTime bestTime = DateTime.MinValue;
            int resolvedTeam = 0;

            foreach (EconomyEvent purchase in purchases)
            {
                if (purchase.ServerTick > bestTick || (purchase.ServerTick == bestTick && purchase.ObservedAtUtc >= bestTime))
                {
                    bestTick = purchase.ServerTick;
                    bestTime = purchase.ObservedAtUtc;
                    resolvedTeam = purchase.Team;
                }
            }

            foreach (EconomySnapshot snapshot in roundSnapshots)
            {
                if (snapshot.ServerTick > bestTick || (snapshot.ServerTick == bestTick && snapshot.ObservedAtUtc >= bestTime))
                {
                    bestTick = snapshot.ServerTick;
                    bestTime = snapshot.ObservedAtUtc;
                    resolvedTeam = snapshot.Team;
                }
            }

            return resolvedTeam != 0 ? resolvedTeam : fallbackTeam;
        }

        private static int ResolveTeamForPlayer(
            TelemetryMatchSession session,
            string steamId64,
            IReadOnlyList<EconomyEvent> events,
            IReadOnlyList<EconomySnapshot> snapshots
        )
        {
            int bestRound = -1;
            int bestTick = int.MinValue;
            DateTime bestTime = DateTime.MinValue;
            int resolvedTeam = 0;

            foreach (EconomyEvent economyEvent in events)
            {
                if (string.Equals(economyEvent.SteamID, steamId64, StringComparison.Ordinal) == false)
                {
                    continue;
                }

                if (IsBetterTelemetryRow(economyEvent.RoundNumber, economyEvent.ServerTick, economyEvent.ObservedAtUtc, bestRound, bestTick, bestTime))
                {
                    bestRound = economyEvent.RoundNumber;
                    bestTick = economyEvent.ServerTick;
                    bestTime = economyEvent.ObservedAtUtc;
                    resolvedTeam = economyEvent.Team;
                }
            }

            foreach (EconomySnapshot snapshot in snapshots)
            {
                if (string.Equals(snapshot.SteamID, steamId64, StringComparison.Ordinal) == false)
                {
                    continue;
                }

                if (IsBetterTelemetryRow(snapshot.RoundNumber, snapshot.ServerTick, snapshot.ObservedAtUtc, bestRound, bestTick, bestTime))
                {
                    bestRound = snapshot.RoundNumber;
                    bestTick = snapshot.ServerTick;
                    bestTime = snapshot.ObservedAtUtc;
                    resolvedTeam = snapshot.Team;
                }
            }

            if (resolvedTeam != 0)
            {
                return resolvedTeam;
            }

            foreach (TelemetryRosterPlayer rosterPlayer in session.Team1)
            {
                if (string.Equals(rosterPlayer.SteamId64, steamId64, StringComparison.Ordinal))
                {
                    return 2;
                }
            }

            foreach (TelemetryRosterPlayer rosterPlayer in session.Team2)
            {
                if (string.Equals(rosterPlayer.SteamId64, steamId64, StringComparison.Ordinal))
                {
                    return 3;
                }
            }

            return 0;
        }

        private static bool IsBetterTelemetryRow(
            int round,
            int tick,
            DateTime observedAtUtc,
            int bestRound,
            int bestTick,
            DateTime bestTime
        )
        {
            if (round > bestRound)
            {
                return true;
            }

            if (round < bestRound)
            {
                return false;
            }

            if (tick > bestTick)
            {
                return true;
            }

            if (tick < bestTick)
            {
                return false;
            }

            return observedAtUtc >= bestTime;
        }

        private sealed class TeamEconomyAccumulator
        {
            internal int StartBudgetTotal;
            internal int EndBudgetTotal;
            internal int SpentTotal;
        }
    }
}

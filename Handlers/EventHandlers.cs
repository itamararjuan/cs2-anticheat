using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using TBAntiCheat.Core;
using TBAntiCheat.Detections;
using TBAntiCheat.Telemetry;

namespace TBAntiCheat.Handlers
{
    internal static class EventHandlers
    {
        internal static void Initialize(BasePlugin plugin, bool hotReload)
        {
            plugin.RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
            plugin.RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect, HookMode.Pre);
            plugin.RegisterEventHandler<EventPlayerActivate>(OnPlayerActivate);
            plugin.RegisterEventHandler<EventPlayerSpawned>(OnPlayerSpawned);

            plugin.RegisterEventHandler<EventPlayerJump>(OnPlayerJump);
            plugin.RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt);
            plugin.RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
            plugin.RegisterEventHandler<EventPlayerBlind>(OnPlayerBlind);

            plugin.RegisterEventHandler<EventWeaponFire>(OnWeaponFire);
            plugin.RegisterEventHandler<EventBulletImpact>(OnBulletImpact);
            plugin.RegisterEventHandler<EventFlashbangDetonate>(OnFlashbangDetonate);
            plugin.RegisterEventHandler<EventSmokegrenadeDetonate>(OnSmokegrenadeDetonate);
            plugin.RegisterEventHandler<EventMolotovDetonate>(OnMolotovDetonate);
            plugin.RegisterEventHandler<EventPlayerFootstep>(OnPlayerFootstep);
            plugin.RegisterEventHandler<EventPlayerSound>(OnPlayerSound);

            plugin.RegisterEventHandler<EventRoundStart>(OnRoundStart);
            plugin.RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
            plugin.RegisterEventHandler<EventRoundFreezeEnd>(OnRoundFreezeEnd);
            plugin.RegisterEventHandler<EventBuytimeEnded>(OnBuytimeEnded);
            plugin.RegisterEventHandler<EventItemPurchase>(OnItemPurchase);
            plugin.RegisterEventHandler<EventEnterBuyzone>(OnEnterBuyzone);
            plugin.RegisterEventHandler<EventExitBuyzone>(OnExitBuyzone);
            plugin.RegisterEventHandler<EventBombPlanted>(OnBombPlanted);
            plugin.RegisterEventHandler<EventBombDefused>(OnBombDefused);

            Globals.Log($"[TBAC] EventHandlers Initialized");

            if (hotReload == true)
            {
                for (int i = 0; i < Server.MaxPlayers; i++)
                {
                    CCSPlayerController? controller = Utilities.GetPlayerFromSlot(i);
                    if (controller == null || controller.IsValid == false)
                    {
                        continue;
                    }

                    if (controller.Connected != PlayerConnectedState.PlayerConnected)
                    {
                        continue;
                    }

                    OnPlayerJoined(controller);
                }
            }
        }

        private static HookResult OnPlayerActivate(EventPlayerActivate activateEvent, GameEventInfo _)
        {
            CCSPlayerController? controller = activateEvent.Userid;
            if (controller == null || controller.IsValid == false)
            {
                return HookResult.Continue;
            }

            // Normal players are already getting handled correctly. No need to do that here
            if (controller.IsBot == false)
            {
                return HookResult.Continue;
            }

            // Skip out on SourceTV since we don't want to track a "player" like that
            if (controller.IsHLTV == true)
            {
                return HookResult.Continue;
            }

            //Globals.Log($"[TBAC] Bot activated -> {controller.Slot} | {controller.PlayerName}");
            OnPlayerJoined(controller);

            return HookResult.Continue;
        }

        private static HookResult OnPlayerConnectFull(EventPlayerConnectFull connectEvent, GameEventInfo _)
        {
            OnPlayerJoined(connectEvent.Userid);

            return HookResult.Continue;
        }

        private static HookResult OnPlayerSpawned(EventPlayerSpawned spawnEvent, GameEventInfo _)
        {
            PlayerData? player = TryGetTrackedPlayer(spawnEvent.Userid);
            if (player == null)
            {
                return HookResult.Continue;
            }

            CCSPlayerPawn? pawn = spawnEvent.Userid?.PlayerPawn.Value;
            if (pawn != null)
            {
                player.Pawn = pawn;
            }

            TelemetryManager.OnPlayerSpawned(player);
            return HookResult.Continue;
        }

        private static HookResult OnPlayerDisconnect(EventPlayerDisconnect disconnectEvent, GameEventInfo _)
        {
            OnPlayerLeft(disconnectEvent.Userid);

            return HookResult.Continue;
        }

        private static HookResult OnPlayerJump(EventPlayerJump jumpEvent, GameEventInfo _)
        {
            PlayerData? player = TryGetTrackedPlayer(jumpEvent.Userid);
            if (player == null)
            {
                return HookResult.Continue;
            }

            BaseCaller.OnPlayerJump(player);

            return HookResult.Continue;
        }

        private static HookResult OnPlayerHurt(EventPlayerHurt hurtEvent, GameEventInfo _)
        {
            PlayerData? victim = TryGetTrackedPlayer(hurtEvent.Userid);
            if (victim == null)
            {
                return HookResult.Continue;
            }

            PlayerData? shooter = TryGetTrackedPlayer(hurtEvent.Attacker);

            TelemetryManager.OnPlayerHurt(victim, shooter, hurtEvent.DmgHealth, hurtEvent.Weapon, hurtEvent.Hitgroup);

            if (shooter != null)
            {
                BaseCaller.OnPlayerHurt(victim, shooter, (HitGroup_t)hurtEvent.Hitgroup);
            }

            return HookResult.Continue;
        }

        private static HookResult OnPlayerDeath(EventPlayerDeath deathEvent, GameEventInfo _)
        {
            PlayerData? victim = TryGetTrackedPlayer(deathEvent.Userid);
            if (victim == null)
            {
                return HookResult.Continue;
            }

            PlayerData? shooter = TryGetTrackedPlayer(deathEvent.Attacker);

            TelemetryManager.OnPlayerDeath(
                victim,
                shooter,
                deathEvent.Weapon,
                deathEvent.Headshot,
                deathEvent.Thrusmoke,
                deathEvent.Penetrated,
                deathEvent.Attackerblind,
                deathEvent.Noscope,
                deathEvent.Attackerinair,
                deathEvent.Distance
            );

            if (shooter != null)
            {
                BaseCaller.OnPlayerDead(victim, shooter);
            }

            return HookResult.Continue;
        }

        private static HookResult OnWeaponFire(EventWeaponFire shootEvent, GameEventInfo _)
        {
            PlayerData? shooter = TryGetTrackedPlayer(shootEvent.Userid);
            if (shooter == null)
            {
                return HookResult.Continue;
            }

            TelemetryManager.OnWeaponFire(shooter, shooter.GetWeapon()?.DesignerName ?? string.Empty);
            BaseCaller.OnPlayerShoot(shooter);

            return HookResult.Continue;
        }

        private static HookResult OnBulletImpact(EventBulletImpact impactEvent, GameEventInfo _)
        {
            PlayerData? player = TryGetTrackedPlayer(impactEvent.Userid);
            if (player == null)
            {
                return HookResult.Continue;
            }

            TelemetryManager.OnBulletImpact(player);
            return HookResult.Continue;
        }

        private static HookResult OnPlayerBlind(EventPlayerBlind blindEvent, GameEventInfo _)
        {
            PlayerData? victim = TryGetTrackedPlayer(blindEvent.Userid);
            if (victim == null)
            {
                return HookResult.Continue;
            }

            PlayerData? attacker = TryGetTrackedPlayer(blindEvent.Attacker);
            TelemetryManager.OnPlayerBlind(victim, attacker, blindEvent.BlindDuration);
            return HookResult.Continue;
        }

        private static HookResult OnFlashbangDetonate(EventFlashbangDetonate flashEvent, GameEventInfo _)
        {
            PlayerData? player = TryGetTrackedPlayer(flashEvent.Userid);
            if (player == null)
            {
                return HookResult.Continue;
            }

            TelemetryManager.OnFlashbangDetonate(player);
            return HookResult.Continue;
        }

        private static HookResult OnSmokegrenadeDetonate(EventSmokegrenadeDetonate smokeEvent, GameEventInfo _)
        {
            PlayerData? player = TryGetTrackedPlayer(smokeEvent.Userid);
            if (player == null)
            {
                return HookResult.Continue;
            }

            TelemetryManager.OnSmokeDetonate(player);
            return HookResult.Continue;
        }

        private static HookResult OnMolotovDetonate(EventMolotovDetonate molotovEvent, GameEventInfo _)
        {
            PlayerData? player = TryGetTrackedPlayer(molotovEvent.Userid);
            if (player == null)
            {
                return HookResult.Continue;
            }

            TelemetryManager.OnMolotovDetonate(player);
            return HookResult.Continue;
        }

        private static HookResult OnPlayerFootstep(EventPlayerFootstep footstepEvent, GameEventInfo _)
        {
            PlayerData? player = TryGetTrackedPlayer(footstepEvent.Userid);
            if (player == null)
            {
                return HookResult.Continue;
            }

            TelemetryManager.OnPlayerFootstep(player);
            return HookResult.Continue;
        }

        private static HookResult OnPlayerSound(EventPlayerSound soundEvent, GameEventInfo _)
        {
            PlayerData? player = TryGetTrackedPlayer(soundEvent.Userid);
            if (player == null)
            {
                return HookResult.Continue;
            }

            TelemetryManager.OnPlayerSound(player);
            return HookResult.Continue;
        }

        private static HookResult OnRoundStart(EventRoundStart roundStartEvent, GameEventInfo _)
        {
            TelemetryManager.OnRoundStart();
            BaseCaller.OnRoundStart();

            return HookResult.Continue;
        }

        private static HookResult OnRoundEnd(EventRoundEnd roundEndEvent, GameEventInfo _)
        {
            BaseCaller.OnRoundEnd();
            TelemetryManager.OnRoundEnd();

            return HookResult.Continue;
        }

        private static HookResult OnRoundFreezeEnd(EventRoundFreezeEnd _, GameEventInfo __)
        {
            TelemetryManager.OnRoundFreezeEnd();
            return HookResult.Continue;
        }

        private static HookResult OnBuytimeEnded(EventBuytimeEnded _, GameEventInfo __)
        {
            TelemetryManager.OnBuytimeEnded();
            return HookResult.Continue;
        }

        private static HookResult OnItemPurchase(EventItemPurchase purchaseEvent, GameEventInfo _)
        {
            PlayerData? player = TryGetTrackedPlayer(purchaseEvent.Userid);
            if (player == null)
            {
                return HookResult.Continue;
            }

            TelemetryManager.OnItemPurchase(player, purchaseEvent.Weapon, purchaseEvent.Loadout, purchaseEvent.Team);
            return HookResult.Continue;
        }

        private static HookResult OnEnterBuyzone(EventEnterBuyzone buyzoneEvent, GameEventInfo _)
        {
            PlayerData? player = TryGetTrackedPlayer(buyzoneEvent.Userid);
            if (player == null)
            {
                return HookResult.Continue;
            }

            TelemetryManager.OnEnterBuyzone(player);
            return HookResult.Continue;
        }

        private static HookResult OnExitBuyzone(EventExitBuyzone buyzoneEvent, GameEventInfo _)
        {
            PlayerData? player = TryGetTrackedPlayer(buyzoneEvent.Userid);
            if (player == null)
            {
                return HookResult.Continue;
            }

            TelemetryManager.OnExitBuyzone(player);
            return HookResult.Continue;
        }

        private static HookResult OnBombPlanted(EventBombPlanted bombPlantedEvent, GameEventInfo _)
        {
            PlayerData? player = TryGetTrackedPlayer(bombPlantedEvent.Userid);
            if (player == null)
            {
                return HookResult.Continue;
            }

            TelemetryManager.OnBombPlanted(player);
            return HookResult.Continue;
        }

        private static HookResult OnBombDefused(EventBombDefused bombDefusedEvent, GameEventInfo _)
        {
            PlayerData? player = TryGetTrackedPlayer(bombDefusedEvent.Userid);
            if (player == null)
            {
                return HookResult.Continue;
            }

            TelemetryManager.OnBombDefused(player);
            return HookResult.Continue;
        }

        // ----- Helper Functions ----- \\

        private static void OnPlayerJoined(CCSPlayerController? controller)
        {
            if (controller == null || controller.IsValid == false)
            {
                Globals.Log($"[TBAC] WARNING: Controller is invalid when player joined");
                return;
            }

            CCSPlayerPawn? pawn = controller.PlayerPawn.Value;
            if (pawn == null)
            {
                Globals.Log($"[TBAC] WARNING: Pawn is invalid when player ({controller.PlayerName}) joined");
                return;
            }

            int playerIndex = controller.Slot;
            PlayerData player = new PlayerData()
            {
                Controller = controller,
                Pawn = pawn,

                Index = playerIndex,
                IsBot = controller.IsBot
            };

            Globals.Players[playerIndex] = player;
            BaseCaller.OnPlayerJoin(player);
            TelemetryManager.OnPlayerJoined(player);

            //Globals.Log($"[TBAC] Player joined -> {playerIndex} | {controller.PlayerName}");
        }

        private static void OnPlayerLeft(CCSPlayerController? controller)
        {
            if (controller == null || controller.IsValid == false)
            {
                Globals.Log($"[TBAC] WARNING: Controller is invalid when player left");
                return;
            }

            int playerIndex = controller.Slot;
            if (Globals.Players[playerIndex] == null)
            {
                return;
            }

            CCSPlayerPawn? pawn = controller.PlayerPawn.Value;
            if (pawn == null)
            {
                Globals.Log($"[TBAC] WARNING: Pawn is invalid when player ({controller.PlayerName}) left");
            }

            PlayerData player = Globals.Players[playerIndex];

            BaseCaller.OnPlayerLeave(player);
            TelemetryManager.OnPlayerLeft(player);
            Globals.Players[playerIndex] = null!;

            //Globals.Log($"[TBAC] Player left -> {playerIndex} | {controller.PlayerName}");
        }

        private static PlayerData? TryGetTrackedPlayer(CCSPlayerController? controller)
        {
            if (controller == null || controller.IsValid == false)
            {
                return null;
            }

            PlayerData player = Globals.Players[controller.Slot];
            if (player == null)
            {
                return null;
            }

            return player;
        }
    }
}

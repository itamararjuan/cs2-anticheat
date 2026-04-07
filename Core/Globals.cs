using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.ValveConstants.Protobuf;
using Microsoft.Extensions.Logging;
using TBAntiCheat.Detections;
using TBAntiCheat.Detections.Modules;

namespace TBAntiCheat.Core
{
    internal class PlayerData
    {
        internal required CCSPlayerController Controller;
        internal required CCSPlayerPawn Pawn;

        internal required int Index;
        internal required bool IsBot;

        internal string PlayerName => Controller.PlayerName;
        internal string SteamID => Controller.AuthorizedSteamID?.SteamId2 ?? "Invalid SteamID";
        internal int TeamNumber => Controller.IsValid ? Controller.TeamNum : 0;

        internal CCSWeaponBaseGun GetWeapon()
        {
            CCSPlayerPawn? pawn = GetCurrentPawn();
            if (pawn == null)
            {
                return null!;
            }

            CPlayer_WeaponServices? weaponServices = pawn.WeaponServices;
            if (weaponServices == null)
            {
                return null!;
            }

            CBasePlayerWeapon? weaponBase = weaponServices.ActiveWeapon.Value;
            if (weaponBase == null)
            {
                return null!;
            }

            return new CCSWeaponBaseGun(weaponBase.Handle);
        }

        internal CCSPlayerPawn? GetCurrentPawn()
        {
            try
            {
                if (Pawn != null && Pawn.IsValid)
                {
                    return Pawn;
                }

                CCSPlayerPawn? livePawn = Controller?.PlayerPawn.Value;
                if (livePawn != null && livePawn.IsValid)
                {
                    Pawn = livePawn;
                    return livePawn;
                }
            }
            catch
            {
            }

            return null;
        }

        internal CCSPlayerController_InGameMoneyServices? GetMoneyServices()
        {
            try
            {
                if (Controller == null || Controller.IsValid == false)
                {
                    return null;
                }

                return Controller.InGameMoneyServices;
            }
            catch
            {
                return null;
            }
        }

        internal List<string> GetInventoryItems()
        {
            List<string> items = [];

            try
            {
                CCSPlayerPawn? pawn = GetCurrentPawn();
                CPlayer_WeaponServices? weaponServices = pawn?.WeaponServices;
                if (weaponServices == null)
                {
                    return items;
                }

                foreach (CHandle<CBasePlayerWeapon> handle in weaponServices.MyWeapons)
                {
                    CBasePlayerWeapon? weapon = handle.Value;
                    if (weapon == null || weapon.IsValid == false || string.IsNullOrWhiteSpace(weapon.DesignerName))
                    {
                        continue;
                    }

                    items.Add(weapon.DesignerName);
                }
            }
            catch
            {
                return [];
            }

            return items;
        }

        internal void Disconnect(NetworkDisconnectionReason reason)
        {
            Controller.Disconnect(reason);
        }
    }

    internal static class Globals
    {
        private static bool initializedOnce = false;

        private static ACCore? pluginCore = null;
        private static ILogger? logger = null;

        internal static PlayerData[] Players = [];
        internal static BaseModule[] Modules = [];

        internal static void PreInit(ACCore core, ILogger log)
        {
            pluginCore = core;
            logger = log;
        }

        internal static void Initialize(bool forceReinitialize)
        {
            Players = new PlayerData[Server.MaxPlayers];
            if (initializedOnce == true && forceReinitialize == false)
            {
                return;
            }

            Log($"[TBAC] Globals Initializing (forced: {forceReinitialize})");

            Modules =
            [
                new Aimbot(),
                //new Backtrack(),
                new BunnyHop(),
                new RapidFire(),
                new UntrustedAngles()
            ];

            initializedOnce = true;

            Log($"[TBAC] Globals Initialized");
        }

        internal static string GetModuleDirectory()
        {
            if (pluginCore == null)
            {
                return string.Empty;
            }

            return pluginCore.ModuleDirectory;
        }

        internal static void Log(string message)
        {
            logger?.Log(LogLevel.Information, message);
        }
    }
}

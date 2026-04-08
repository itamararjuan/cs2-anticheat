using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.ValveConstants.Protobuf;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Reflection;
using TBAntiCheat.Detections;
using TBAntiCheat.Detections.Modules;
using TBAntiCheat.Telemetry;

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
        internal string? SteamID64 => TryGetSteamId64();
        internal int TeamNumber => Controller.IsValid ? Controller.TeamNum : 0;

        private string? TryGetSteamId64()
        {
            try
            {
                object? authorizedSteamId = Controller.AuthorizedSteamID;
                if (authorizedSteamId == null)
                {
                    return null;
                }

                PropertyInfo? steamId64Property =
                    authorizedSteamId.GetType().GetProperty("SteamId64") ??
                    authorizedSteamId.GetType().GetProperty("SteamID64");
                object? rawSteamId64 = steamId64Property?.GetValue(authorizedSteamId);

                return rawSteamId64 switch
                {
                    ulong steamId64 when steamId64 > 0 => steamId64.ToString(CultureInfo.InvariantCulture),
                    long steamId64 when steamId64 > 0 => steamId64.ToString(CultureInfo.InvariantCulture),
                    string steamId64 when string.IsNullOrWhiteSpace(steamId64) == false => steamId64,
                    _ => null
                };
            }
            catch
            {
                return null;
            }
        }

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

        internal static void OnOuRoRecordStartCommand(CCSPlayerController? player, CommandInfo command)
        {
            TelemetryManager.HandleOuRoRecordStartCommand(command);
        }

        internal static void OnOuRoRecordStopCommand(CCSPlayerController? player, CommandInfo command)
        {
            TelemetryManager.HandleOuRoRecordStopCommand(command);
        }
    }
}


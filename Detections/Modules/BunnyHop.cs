using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using TBAntiCheat.Core;
using TBAntiCheat.Handlers;

namespace TBAntiCheat.Detections.Modules
{
    public class BunnyHopSaveData
    {
        public bool DetectionEnabled { get; set; } = true;
    }

    internal class BunnyHopData
    {
        internal int perfectBhops;
    }

    /*
     * Module: Bunny Hop
     * Purpose: Detect players that does tick perfect bunny hops over and over again
     * NOTE: Not production ready. Needs testing
     */
    internal class BunnyHop : BaseModule
    {
        internal override string Name => "BunnyHop";

        private readonly BaseConfig<BunnyHopSaveData> config;
        private readonly BunnyHopData[] playerData;

        internal BunnyHop() : base()
        {
            config = new BaseConfig<BunnyHopSaveData>("BunnyHop");
            playerData = new BunnyHopData[Server.MaxPlayers];

            CommandHandler.RegisterCommand("tbac_bhop_enable", "Deactivates/Activates BunnyHop detections", OnEnableCommand);

            Globals.Log($"[TBAC] BunnyHop Initialized");
        }

        internal override void OnPlayerJoin(PlayerData player)
        {
            if (player.IsBot == true)
            {
                return;
            }

            playerData[player.Index] = new BunnyHopData()
            {
                perfectBhops = 0
            };
        }

        /*internal override void OnPlayerJump(PlayerData player)
        {
        }

        internal override void OnPlayerTick(PlayerData player)
        {
            BunnyHopData data = playerData[player.Index];

            PlayerButtons buttons = player.Controller.Buttons;
            if (buttons.HasFlag(PlayerButtons.Jump) == true)
            {
            }
        }*/

        // ----- Commands ----- \\

        [RequiresPermissions("@css/admin")]
        private void OnEnableCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (command.ArgCount != 2)
            {
                return;
            }

            string arg = command.ArgByIndex(1);
            if (bool.TryParse(arg, out bool state) == false)
            {
                return;
            }

            config.Config.DetectionEnabled = state;
            config.Save();
        }

    }
}

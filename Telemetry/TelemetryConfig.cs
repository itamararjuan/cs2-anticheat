using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using TBAntiCheat.Core;
using TBAntiCheat.Handlers;

namespace TBAntiCheat.Telemetry
{
    public class TelemetryConfigData
    {
        public bool CollectionEnabled { get; set; } = true;
        public bool UploadEnabled { get; set; } = false;
        public string ServerId { get; set; } = string.Empty;
        public string ServerLabel { get; set; } = string.Empty;
        public string ServerRegion { get; set; } = string.Empty;
        public string MatchSource { get; set; } = string.Empty;
        public string MatchId { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = "https://www.ouro.is/edge/";
        public string RelativePath { get; set; } = "/api/cs2/observations";
        public string BearerToken { get; set; } = string.Empty;
        public int ReportingIntervalSeconds { get; set; } = 120;
        public bool LogBatchSummaries { get; set; } = true;
        public bool LogObservationSummaries { get; set; } = false;
    }

    internal static class TelemetryConfig
    {
        private static BaseConfig<TelemetryConfigData> config = null!;

        internal static void Initialize()
        {
            Reload();

            CommandHandler.RegisterCommand("tbac_reload", "Reloads telemetry config", OnReloadCommand);
        }

        internal static BaseConfig<TelemetryConfigData> Get()
        {
            return config;
        }

        internal static void Reload()
        {
            config = new BaseConfig<TelemetryConfigData>("Telemetry");

            Globals.Log("[TBAC] Loaded telemetry config");
        }

        [RequiresPermissions("@css/admin")]
        private static void OnReloadCommand(CCSPlayerController? player, CommandInfo command)
        {
            Reload();
            TelemetryManager.OnConfigReloaded();
        }
    }
}

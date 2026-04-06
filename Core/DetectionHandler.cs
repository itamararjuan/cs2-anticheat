using TBAntiCheat.Detections;
using TBAntiCheat.Telemetry;

namespace TBAntiCheat.Core
{
    internal struct DetectionMetadata
    {
        internal BaseModule module;
        internal PlayerData player;
        internal DateTime time;
        internal string reason;
    }

    internal static class DetectionHandler
    {
        internal static void OnPlayerDetected(DetectionMetadata metadata)
        {
            Globals.Log($"[TBAC] Suspicious observation -> {metadata.player.Controller.PlayerName} | {metadata.module.Name} | {metadata.reason}");
            TelemetryManager.RecordModuleDetection(metadata);
        }
    }
}

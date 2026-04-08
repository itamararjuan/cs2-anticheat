using System.Net.Http.Headers;
using System.Text;

namespace TBAntiCheat.Telemetry
{
    public static class TelemetryUploadRoutes
    {
        public const string Observations = "/api/cs2/observations";
        public const string MatchEconomySummary = "/api/cs2/match-economy-summary";
    }

    public static class TelemetryRequestFactory
    {
        public static HttpRequestMessage CreateUploadRequest(TelemetryConfigData config, string jsonPayload)
        {
            return CreateUploadRequest(config, jsonPayload, relativePathOverride: null);
        }

        public static HttpRequestMessage CreateUploadRequest(
            TelemetryConfigData config,
            string jsonPayload,
            string? relativePathOverride
        )
        {
            Uri requestUri = BuildRequestUri(config, relativePathOverride);
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
            };

            if (string.IsNullOrWhiteSpace(config.BearerToken) == false)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.BearerToken);
            }

            return request;
        }

        public static Uri BuildRequestUri(TelemetryConfigData config)
        {
            return BuildRequestUri(config, relativePathOverride: null);
        }

        public static Uri BuildRequestUri(TelemetryConfigData config, string? relativePathOverride)
        {
            string path = string.IsNullOrWhiteSpace(relativePathOverride) ? config.RelativePath : relativePathOverride;
            Uri baseUri = new Uri(config.BaseUrl.TrimEnd('/') + "/");
            return new Uri(baseUri, path.TrimStart('/'));
        }
    }
}

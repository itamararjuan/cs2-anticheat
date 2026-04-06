using System.Net.Http.Headers;
using System.Text;

namespace TBAntiCheat.Telemetry
{
    public static class TelemetryRequestFactory
    {
        public static HttpRequestMessage CreateUploadRequest(TelemetryConfigData config, string jsonPayload)
        {
            Uri requestUri = BuildRequestUri(config);
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
            Uri baseUri = new Uri(config.BaseUrl.TrimEnd('/') + "/");
            return new Uri(baseUri, config.RelativePath.TrimStart('/'));
        }
    }
}

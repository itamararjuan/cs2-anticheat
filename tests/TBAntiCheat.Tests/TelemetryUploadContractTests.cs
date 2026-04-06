using System.Net.Http;
using TBAntiCheat.Telemetry;
using Xunit;

namespace TBAntiCheat.Tests;

public sealed class TelemetryUploadContractTests
{
    [Fact]
    public void TelemetryConfigDefaultsUseProductionEdgeRoute()
    {
        TelemetryConfigData config = new();

        Assert.Equal("https://www.ouro.is/edge/", config.BaseUrl);
        Assert.Equal("/api/cs2/observations", config.RelativePath);
        Assert.Equal(string.Empty, config.BearerToken);
    }

    [Fact]
    public void CreateUploadRequestUsesBearerAuthorization()
    {
        TelemetryConfigData config = new()
        {
            BaseUrl = "https://www.ouro.is/edge/",
            RelativePath = "/api/cs2/observations",
            BearerToken = "secret-token",
        };

        using HttpRequestMessage request = TelemetryRequestFactory.CreateUploadRequest(
            config,
            "{\"PluginName\":\"TB Anti-Cheat\"}"
        );

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(
            "https://www.ouro.is/edge/api/cs2/observations",
            request.RequestUri?.ToString()
        );
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("secret-token", request.Headers.Authorization?.Parameter);
        Assert.Equal("application/json", request.Content?.Headers.ContentType?.MediaType);
    }
}

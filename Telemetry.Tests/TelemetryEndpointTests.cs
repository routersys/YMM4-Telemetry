using Telemetry;
using Xunit;

namespace Telemetry.Tests;

public class TelemetryEndpointTests
{
    [Fact]
    public void 送信先はAccessの無いingestホストを指す()
    {
        Assert.Equal("ingest.routersys.com", TelemetryEndpoint.EnvelopeUri.Host);
    }

    [Fact]
    public void 送信先はエンベロープの受け口を指す()
    {
        Assert.Equal("https://ingest.routersys.com/api/1/envelope/", TelemetryEndpoint.EnvelopeUri.ToString());
    }

    [Fact]
    public void 認証ヘッダーはSentryの形式になる()
    {
        Assert.Equal(
            "Sentry sentry_version=7, sentry_key=41c831f58032402692bcae2a3756906e, sentry_client=telemetry.ymm4/1.0.0",
            TelemetryEndpoint.AuthorizationHeaderValue);
    }

    [Fact]
    public void 利用者エージェントを必ず持つ()
    {
        Assert.False(string.IsNullOrWhiteSpace(TelemetryEndpoint.UserAgent));
    }

    [Fact]
    public void 内容種別はエンベロープを表す()
    {
        Assert.Equal("application/x-sentry-envelope", TelemetryEndpoint.ContentType);
    }
}

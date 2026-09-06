using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telemetry;
using Xunit;

namespace Telemetry.Tests;

public class HttpTelemetryChannelTests
{
    private static readonly byte[] Envelope = Encoding.UTF8.GetBytes("envelope");
    private static readonly TimeSpan FallbackRetryAfter = TimeSpan.FromSeconds(10d);

    private static Task<DeliveryResult> SendAsync(StubHttpMessageHandler handler) =>
        new HttpTelemetryChannel(handler).SendAsync(Envelope, CancellationToken.None);

    [Fact]
    public void 経路がnullなら拒否する()
    {
        Assert.Throws<ArgumentNullException>(() => new HttpTelemetryChannel(null!));
    }

    [Fact]
    public async Task 中身がnullなら拒否する()
    {
        var channel = new HttpTelemetryChannel(new StubHttpMessageHandler(HttpStatusCode.OK));

        await Assert.ThrowsAsync<ArgumentNullException>(() => channel.SendAsync(null!, CancellationToken.None));
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.Accepted)]
    [InlineData(HttpStatusCode.NoContent)]
    public async Task 成功なら受理とみなす(HttpStatusCode status)
    {
        var result = await SendAsync(new StubHttpMessageHandler(status));

        Assert.Equal(DeliveryOutcome.Delivered, result.Outcome);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task 四百番台は再送しても無駄なので捨てる(HttpStatusCode status)
    {
        var result = await SendAsync(new StubHttpMessageHandler(status));

        Assert.Equal(DeliveryOutcome.Rejected, result.Outcome);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task 五百番台は一時的な失敗として残す(HttpStatusCode status)
    {
        var result = await SendAsync(new StubHttpMessageHandler(status));

        Assert.Equal(DeliveryOutcome.Deferred, result.Outcome);
    }

    [Fact]
    public async Task 制限を受けたら待ち時間を読み取る()
    {
        var expected = TimeSpan.FromSeconds(30d);

        var result = await SendAsync(new StubHttpMessageHandler(HttpStatusCode.TooManyRequests, expected));

        Assert.Equal(DeliveryOutcome.RateLimited, result.Outcome);
        Assert.Equal(expected, result.RetryAfter);
    }

    [Fact]
    public async Task 待ち時間が無ければ既定値を使う()
    {
        var result = await SendAsync(new StubHttpMessageHandler(HttpStatusCode.TooManyRequests));

        Assert.Equal(DeliveryOutcome.RateLimited, result.Outcome);
        Assert.Equal(FallbackRetryAfter, result.RetryAfter);
    }

    [Fact]
    public async Task 通信が失敗しても例外を外へ出さない()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("切断"));

        var result = await SendAsync(handler);

        Assert.Equal(DeliveryOutcome.Deferred, result.Outcome);
    }

    [Fact]
    public async Task 認証ヘッダーと内容種別を付ける()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK);

        await SendAsync(handler);

        var request = handler.Requests[0];
        Assert.Equal(
            TelemetryEndpoint.AuthorizationHeaderValue,
            request.Headers.GetValues(TelemetryEndpoint.AuthorizationHeaderName).Single());
        Assert.Equal(TelemetryEndpoint.ContentType, request.Content!.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task 利用者エージェントを付ける()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK);

        await SendAsync(handler);

        Assert.Equal(TelemetryEndpoint.UserAgent, handler.Requests[0].Headers.UserAgent.ToString());
    }

    [Fact]
    public async Task 送信先と中身をそのまま渡す()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK);

        await SendAsync(handler);

        Assert.Equal(TelemetryEndpoint.EnvelopeUri, handler.Requests[0].RequestUri);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal(Envelope, handler.Bodies[0]);
    }
}

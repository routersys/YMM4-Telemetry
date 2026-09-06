using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Telemetry;
using Xunit;

namespace Telemetry.Tests;

public class TelemetryReporterTests : IDisposable
{
    private static readonly TimeSpan RetryAfter = TimeSpan.FromMinutes(5d);

    private readonly string _directory;
    private readonly TelemetrySpool _spool;

    public TelemetryReporterTests()
    {
        ProcessStateReset.Clear();
        _directory = Path.Combine(Path.GetTempPath(), "telemetry-reporter-tests", Guid.NewGuid().ToString("N"));
        _spool = new TelemetrySpool(_directory);
    }

    public void Dispose()
    {
        ProcessStateReset.Clear();

        try
        {
            Directory.Delete(_directory, true);
        }
        catch (DirectoryNotFoundException)
        {
        }
    }

    [Fact]
    public void 例外がnullなら何もしない()
    {
        Assert.Equal(ReportDecision.NoException, TelemetryReporter.Prepare(null, _spool).Decision);
        Assert.Empty(_spool.List());
    }

    [Fact]
    public void 退避先がnullなら拒否する()
    {
        Assert.Throws<ArgumentNullException>(() => TelemetryReporter.Prepare(Failure(), null!));
    }

    [Fact]
    public void 例外を受け取ったら作って退避する()
    {
        var prepared = TelemetryReporter.Prepare(Failure(), _spool);

        Assert.Equal(ReportDecision.Prepared, prepared.Decision);
        Assert.NotNull(prepared.Envelope);
        Assert.Single(_spool.List());
    }

    [Fact]
    public void 同じ不具合は上限までしか作らない()
    {
        for (var index = 0; index < TelemetryBudget.MaxEventsPerFingerprint; index++)
        {
            Assert.Equal(ReportDecision.Prepared, TelemetryReporter.Prepare(Failure(), _spool).Decision);
        }

        Assert.Equal(ReportDecision.DuplicateLimitReached, TelemetryReporter.Prepare(Failure(), _spool).Decision);
    }

    [Fact]
    public void 背圧の期間中は作らない()
    {
        TelemetryBackoff.Extend(DateTimeOffset.UtcNow + RetryAfter);

        Assert.Equal(ReportDecision.RateLimited, TelemetryReporter.Prepare(Failure(), _spool).Decision);
    }

    [Fact]
    public async Task 送信できたら退避を消す()
    {
        var prepared = TelemetryReporter.Prepare(Failure(), _spool);
        var channel = new FakeTelemetryChannel(DeliveryOutcome.Delivered);

        var outcome = await TelemetryReporter.DeliverAsync(prepared, channel, _spool, CancellationToken.None);

        Assert.Equal(DeliveryOutcome.Delivered, outcome);
        Assert.Single(channel.Sent);
        Assert.Empty(_spool.List());
    }

    [Fact]
    public async Task 受け取りを拒まれたら退避を消す()
    {
        var prepared = TelemetryReporter.Prepare(Failure(), _spool);

        await TelemetryReporter.DeliverAsync(prepared, new FakeTelemetryChannel(DeliveryOutcome.Rejected), _spool, CancellationToken.None);

        Assert.Empty(_spool.List());
    }

    [Fact]
    public async Task 一時的な失敗なら退避を残す()
    {
        var prepared = TelemetryReporter.Prepare(Failure(), _spool);

        await TelemetryReporter.DeliverAsync(prepared, new FakeTelemetryChannel(DeliveryOutcome.Deferred), _spool, CancellationToken.None);

        Assert.Single(_spool.List());
    }

    [Fact]
    public async Task 制限を受けたら背圧を記録し退避を残す()
    {
        var prepared = TelemetryReporter.Prepare(Failure(), _spool);

        await TelemetryReporter.DeliverAsync(
            prepared,
            new FakeTelemetryChannel(DeliveryOutcome.RateLimited, RetryAfter),
            _spool,
            CancellationToken.None);

        Assert.True(TelemetryBackoff.IsActive);
        Assert.Single(_spool.List());
    }

    [Fact]
    public async Task 中身が無ければ送らない()
    {
        var outcome = await TelemetryReporter.DeliverAsync(
            new PreparationResult(ReportDecision.NoException, null, null),
            new FakeTelemetryChannel(DeliveryOutcome.Delivered),
            _spool,
            CancellationToken.None);

        Assert.Equal(DeliveryOutcome.Rejected, outcome);
    }

    [Fact]
    public async Task 退避した分を再送して消す()
    {
        TelemetryReporter.Prepare(Failure(), _spool);
        var channel = new FakeTelemetryChannel(DeliveryOutcome.Delivered);

        var delivered = await TelemetryReporter.DrainAsync(channel, _spool, CancellationToken.None);

        Assert.Equal(1, delivered);
        Assert.Empty(_spool.List());
    }

    [Fact]
    public async Task 再送中に失敗したら打ち切る()
    {
        for (var index = 0; index < TelemetryBudget.MaxEventsPerFingerprint; index++)
        {
            TelemetryReporter.Prepare(Failure(), _spool);
        }

        var channel = new FakeTelemetryChannel(DeliveryOutcome.Delivered);
        channel.Plan(DeliveryOutcome.Deferred);

        var delivered = await TelemetryReporter.DrainAsync(channel, _spool, CancellationToken.None);

        Assert.Equal(0, delivered);
        Assert.Equal(TelemetryBudget.MaxEventsPerFingerprint, _spool.List().Count);
    }

    [Fact]
    public async Task 背圧の期間中は再送しない()
    {
        TelemetryReporter.Prepare(Failure(), _spool);
        TelemetryBackoff.Extend(DateTimeOffset.UtcNow + RetryAfter);

        var channel = new FakeTelemetryChannel(DeliveryOutcome.Delivered);

        Assert.Equal(0, await TelemetryReporter.DrainAsync(channel, _spool, CancellationToken.None));
        Assert.Empty(channel.Sent);
    }

    [Fact]
    public async Task 経路がnullなら拒否する()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            TelemetryReporter.DrainAsync(null!, _spool, CancellationToken.None));
    }

    private static Exception Failure()
    {
        try
        {
            throw new InvalidOperationException("描画に失敗しました。");
        }
        catch (Exception exception)
        {
            return exception;
        }
    }
}

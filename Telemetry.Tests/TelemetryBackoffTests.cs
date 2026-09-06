using Telemetry;
using Xunit;

namespace Telemetry.Tests;

public class TelemetryBackoffTests : IDisposable
{
    private static readonly TimeSpan Ahead = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan FurtherAhead = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan Behind = TimeSpan.FromMinutes(-5);

    public TelemetryBackoffTests() => ProcessStateReset.Clear();

    public void Dispose() => ProcessStateReset.Clear();

    [Fact]
    public void 未設定なら止めない()
    {
        Assert.False(TelemetryBackoff.IsActive);
    }

    [Fact]
    public void 期限が先なら止める()
    {
        TelemetryBackoff.Extend(DateTimeOffset.UtcNow + Ahead);

        Assert.True(TelemetryBackoff.IsActive);
    }

    [Fact]
    public void 期限が過ぎていれば止めない()
    {
        TelemetryBackoff.Extend(DateTimeOffset.UtcNow + Behind);

        Assert.False(TelemetryBackoff.IsActive);
    }

    [Fact]
    public void より遠い期限で延長できる()
    {
        var far = DateTimeOffset.UtcNow + FurtherAhead;

        TelemetryBackoff.Extend(DateTimeOffset.UtcNow + Ahead);
        TelemetryBackoff.Extend(far);

        Assert.Equal(far, ProcessState.Read("RetryAfter"));
    }

    [Fact]
    public void より近い期限では短縮されない()
    {
        var far = DateTimeOffset.UtcNow + FurtherAhead;

        TelemetryBackoff.Extend(far);
        TelemetryBackoff.Extend(DateTimeOffset.UtcNow + Ahead);

        Assert.Equal(far, ProcessState.Read("RetryAfter"));
    }
}

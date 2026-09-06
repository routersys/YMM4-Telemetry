using System.Globalization;
using Telemetry;
using Xunit;

namespace Telemetry.Tests;

public class TelemetryBudgetTests : IDisposable
{
    private const string Fingerprint = "0123456789abcdef";
    private const string OtherFingerprint = "fedcba9876543210";

    public TelemetryBudgetTests() => ProcessStateReset.Clear();

    public void Dispose() => ProcessStateReset.Clear();

    [Fact]
    public void 最初の一件は許可される()
    {
        Assert.Equal(BudgetDecision.Allowed, TelemetryBudget.TryReserve(Fingerprint));
    }

    [Fact]
    public void 同じ指紋は上限までしか許可しない()
    {
        for (var index = 0; index < TelemetryBudget.MaxEventsPerFingerprint; index++)
        {
            Assert.Equal(BudgetDecision.Allowed, TelemetryBudget.TryReserve(Fingerprint));
        }

        Assert.Equal(BudgetDecision.DuplicateLimitReached, TelemetryBudget.TryReserve(Fingerprint));
    }

    [Fact]
    public void 別の指紋は独立して数える()
    {
        for (var index = 0; index < TelemetryBudget.MaxEventsPerFingerprint; index++)
        {
            TelemetryBudget.TryReserve(Fingerprint);
        }

        Assert.Equal(BudgetDecision.Allowed, TelemetryBudget.TryReserve(OtherFingerprint));
    }

    [Fact]
    public void セッション全体の上限を超えると許可しない()
    {
        for (var index = 0; index < TelemetryBudget.MaxEventsPerSession; index++)
        {
            Assert.Equal(BudgetDecision.Allowed, TelemetryBudget.TryReserve(Unique(index)));
        }

        Assert.Equal(BudgetDecision.SessionLimitReached, TelemetryBudget.TryReserve(Unique(TelemetryBudget.MaxEventsPerSession)));
    }

    [Fact]
    public void 背圧の期間中は許可しない()
    {
        TelemetryBackoff.Extend(DateTimeOffset.UtcNow.AddMinutes(5d));

        Assert.Equal(BudgetDecision.RateLimited, TelemetryBudget.TryReserve(Fingerprint));
    }

    [Fact]
    public void 拒否したときは枠を消費しない()
    {
        for (var index = 0; index < TelemetryBudget.MaxEventsPerFingerprint; index++)
        {
            TelemetryBudget.TryReserve(Fingerprint);
        }

        TelemetryBudget.TryReserve(Fingerprint);

        Assert.Equal(TelemetryBudget.MaxEventsPerFingerprint, ProcessState.Read("SentCount"));
    }

    [Fact]
    public void 指紋が空なら拒否する()
    {
        Assert.Throws<ArgumentException>(() => TelemetryBudget.TryReserve(string.Empty));
    }

    private static string Unique(int index) => index.ToString("x16", CultureInfo.InvariantCulture);
}

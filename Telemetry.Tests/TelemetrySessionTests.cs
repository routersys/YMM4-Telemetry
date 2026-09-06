using Telemetry;
using Xunit;

namespace Telemetry.Tests;

public class TelemetrySessionTests
{
    private const int IdentifierLength = 32;
    private const string HexadecimalDigits = "0123456789abcdef";
    private const string StartTime = "638000000000000000";
    private const int ProcessId = 4242;

    [Fact]
    public void 三十二桁の十六進数を返す()
    {
        var identifier = TelemetrySession.Derive(ProcessId, StartTime);

        Assert.Equal(IdentifierLength, identifier.Length);
        Assert.All(identifier, character => Assert.Contains(character, HexadecimalDigits));
    }

    [Fact]
    public void 同じ起動なら同じ値になる()
    {
        Assert.Equal(
            TelemetrySession.Derive(ProcessId, StartTime),
            TelemetrySession.Derive(ProcessId, StartTime));
    }

    [Fact]
    public void プロセスが違えば別の値になる()
    {
        Assert.NotEqual(
            TelemetrySession.Derive(ProcessId, StartTime),
            TelemetrySession.Derive(ProcessId + 1, StartTime));
    }

    [Fact]
    public void 開始時刻が違えば別の値になる()
    {
        Assert.NotEqual(
            TelemetrySession.Derive(ProcessId, StartTime),
            TelemetrySession.Derive(ProcessId, StartTime + "1"));
    }

    [Fact]
    public void 開始時刻がnullなら拒否する()
    {
        Assert.Throws<ArgumentNullException>(() => TelemetrySession.Derive(ProcessId, null!));
    }

    [Fact]
    public void このプロセスの値は導出結果と一致する()
    {
        Assert.Equal(IdentifierLength, TelemetrySession.Id.Length);
        Assert.All(TelemetrySession.Id, character => Assert.Contains(character, HexadecimalDigits));
    }
}

using Telemetry;
using Xunit;

namespace Telemetry.Tests;

public class EventFingerprintTests
{
    private const int IdentifierLength = 16;
    private const string HexadecimalDigits = "0123456789abcdef";

    [Fact]
    public void 入力がnullなら拒否する()
    {
        Assert.Throws<ArgumentNullException>(() => EventFingerprint.Compute(null!));
    }

    [Fact]
    public void 十六桁の十六進数を返す()
    {
        var fingerprint = EventFingerprint.Compute([Single("System.Exception", "./a.cs", 1)]);

        Assert.Equal(IdentifierLength, fingerprint.Length);
        Assert.All(fingerprint, character => Assert.Contains(character, HexadecimalDigits));
    }

    [Fact]
    public void パスと行番号は結果に影響しない()
    {
        var left = EventFingerprint.Compute([Single("System.Exception", "./a.cs", 10)]);
        var right = EventFingerprint.Compute([Single("System.Exception", @"M:\other\a.cs", 999)]);

        Assert.Equal(left, right);
    }

    [Fact]
    public void 例外の型が変われば結果も変わる()
    {
        var left = EventFingerprint.Compute([Single("System.Exception", "./a.cs", 1)]);
        var right = EventFingerprint.Compute([Single("System.InvalidOperationException", "./a.cs", 1)]);

        Assert.NotEqual(left, right);
    }

    [Fact]
    public void 関数が変われば結果も変わる()
    {
        var left = EventFingerprint.Compute([WithFrame(new CapturedFrame("A.B", "Render", null, 0, true))]);
        var right = EventFingerprint.Compute([WithFrame(new CapturedFrame("A.B", "Update", null, 0, true))]);

        Assert.NotEqual(left, right);
    }

    [Fact]
    public void 自分のフレームが無ければ全フレームで区別する()
    {
        var left = EventFingerprint.Compute([WithFrame(new CapturedFrame("External", "Deep", null, 0, false))]);
        var right = EventFingerprint.Compute([WithFrame(new CapturedFrame("External", "Shallow", null, 0, false))]);

        Assert.NotEqual(left, right);
    }

    [Fact]
    public void 自分のフレームがあれば外部フレームは無視する()
    {
        var withExternal = new CapturedException(
            "System.Exception",
            string.Empty,
            [new CapturedFrame("A.B", "Render", null, 0, true), new CapturedFrame("External", "Deep", null, 0, false)]);

        var withoutExternal = WithFrame(new CapturedFrame("A.B", "Render", null, 0, true));

        Assert.Equal(EventFingerprint.Compute([withExternal]), EventFingerprint.Compute([withoutExternal]));
    }

    [Fact]
    public void メッセージは結果に影響しない()
    {
        var left = new CapturedException("System.Exception", "一つ目", [new CapturedFrame("A.B", "Render", null, 0, true)]);
        var right = new CapturedException("System.Exception", "二つ目", [new CapturedFrame("A.B", "Render", null, 0, true)]);

        Assert.Equal(EventFingerprint.Compute([left]), EventFingerprint.Compute([right]));
    }

    private static CapturedException Single(string type, string fileName, int lineNumber) =>
        new(type, "メッセージ", [new CapturedFrame("Plugin.Effect", "Render", fileName, lineNumber, true)]);

    private static CapturedException WithFrame(CapturedFrame frame) =>
        new("System.Exception", string.Empty, [frame]);
}

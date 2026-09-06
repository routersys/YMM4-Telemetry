using System.Reflection;
using Telemetry;
using Xunit;

namespace Telemetry.Tests;

public class ExceptionCaptureTests
{
    private static Assembly Own => typeof(ExceptionCaptureTests).Assembly;

    [Fact]
    public void 例外がnullなら拒否する()
    {
        Assert.Throws<ArgumentNullException>(() => ExceptionCapture.Capture(null!, Own));
    }

    [Fact]
    public void フレームは呼び出し元が先で送出元が最後になる()
    {
        var captured = ExceptionCapture.Capture(Catch(static () => Outer()), Own);
        var frames = captured[^1].Frames;

        Assert.True(frames.Count >= 3);
        Assert.Equal(nameof(Innermost), frames[^1].Function);
        Assert.Equal(nameof(Middle), frames[^2].Function);
        Assert.Equal(nameof(Outer), frames[^3].Function);
    }

    [Fact]
    public void 例外の連なりは内側が先で送出された例外が最後になる()
    {
        var captured = ExceptionCapture.Capture(Catch(static () =>
        {
            try
            {
                throw new ArgumentNullException("原因");
            }
            catch (Exception inner)
            {
                throw new InvalidOperationException("結果", inner);
            }
        }), Own);

        Assert.Equal(2, captured.Count);
        Assert.Equal(typeof(ArgumentNullException).FullName, captured[0].Type);
        Assert.Equal(typeof(InvalidOperationException).FullName, captured[^1].Type);
    }

    [Fact]
    public void 連なりの長さを上限で打ち切る()
    {
        Exception exception = new InvalidOperationException("底");

        for (var depth = 0; depth < TelemetryBudget.MaxEventsPerSession; depth++)
        {
            exception = new InvalidOperationException("包み", exception);
        }

        Assert.Equal(ExceptionCapture.MaxChainLength, ExceptionCapture.Capture(exception, Own).Count);
    }

    [Fact]
    public void 自分のアセンブリのフレームだけをinAppにする()
    {
        var captured = ExceptionCapture.Capture(Catch(static () => "x".Substring(5)), Own);
        var frames = captured[^1].Frames;

        Assert.Contains(frames, frame => frame.InApp && frame.Module is not null
            && frame.Module.StartsWith(nameof(Telemetry) + "." + nameof(Tests), StringComparison.Ordinal));
        Assert.Contains(frames, frame => !frame.InApp && frame.Module == typeof(string).FullName);
    }

    [Fact]
    public void 判定用アセンブリがnullならinAppは立たない()
    {
        var captured = ExceptionCapture.Capture(Catch(static () => Outer()), null);

        Assert.All(captured[^1].Frames, frame => Assert.False(frame.InApp));
    }

    [Fact]
    public void 例外メッセージから個人情報を除く()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var captured = ExceptionCapture.Capture(new InvalidOperationException($"失敗: {profile}\\a.ymmp"), Own);

        Assert.DoesNotContain(profile, captured[0].Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[userprofile]", captured[0].Value, StringComparison.Ordinal);
    }

    [Fact]
    public void 送出していない例外でも変換できる()
    {
        var captured = ExceptionCapture.Capture(new InvalidOperationException("未送出"), Own);

        Assert.Single(captured);
        Assert.Empty(captured[0].Frames);
    }

    [Fact]
    public void 集約例外は最初の内部例外を辿る()
    {
        var first = new InvalidOperationException("一つ目");
        var second = new ArgumentException("二つ目");
        var captured = ExceptionCapture.Capture(new AggregateException(first, second), Own);

        Assert.Equal(2, captured.Count);
        Assert.Equal(typeof(InvalidOperationException).FullName, captured[0].Type);
        Assert.Equal(typeof(AggregateException).FullName, captured[^1].Type);
    }

    [Fact]
    public void 行番号を取得できる()
    {
        var captured = ExceptionCapture.Capture(Catch(static () => Outer()), Own);

        Assert.Contains(captured[^1].Frames, frame => frame.LineNumber > 0);
    }

    private static Exception Catch(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            return exception;
        }

        throw new InvalidOperationException("例外が発生しませんでした。");
    }

    private static void Outer() => Middle();

    private static void Middle() => Innermost();

    private static void Innermost() => throw new InvalidOperationException("最下層");
}

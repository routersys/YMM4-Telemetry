using System.Text;
using System.Text.Json;
using Telemetry;
using Xunit;

namespace Telemetry.Tests;

public class SentryEnvelopeTests
{
    private const string Identifier = "0123456789abcdef0123456789abcdef";
    private const string Level = "error";
    private const int EnvelopeLineCount = 3;
    private const int EnvelopeHeaderLine = 0;
    private const int ItemHeaderLine = 1;
    private const int PayloadLine = 2;

    private static readonly CapturedException Inner = new(
        "System.ArgumentNullException",
        "内側",
        [new CapturedFrame("Plugin.Effect", "Prepare", "./Effect.cs", 10, true)]);

    private static readonly CapturedException Outer = new(
        "System.InvalidOperationException",
        "外側",
        [
            new CapturedFrame("Plugin.Effect", "Render", "./Effect.cs", 20, true),
            new CapturedFrame("System.String", "Substring", null, 0, false),
        ]);

    private static string[] Build() => Encoding.UTF8
        .GetString(SentryEnvelope.Build(Identifier, [Inner, Outer], Level, DateTimeOffset.UtcNow))
        .Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private static JsonElement Payload() => JsonDocument.Parse(Build()[PayloadLine]).RootElement;

    [Fact]
    public void 識別子が空なら拒否する()
    {
        Assert.Throws<ArgumentException>(() => SentryEnvelope.Build(string.Empty, [Outer], Level, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void 捕捉結果がnullなら拒否する()
    {
        Assert.Throws<ArgumentNullException>(() => SentryEnvelope.Build(Identifier, null!, Level, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void 改行区切りの三行になる()
    {
        Assert.Equal(EnvelopeLineCount, Build().Length);
    }

    [Fact]
    public void 各行が単独で解析できるJSONになる()
    {
        foreach (var line in Build())
        {
            using var document = JsonDocument.Parse(line);
            Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
        }
    }

    [Fact]
    public void アイテムヘッダーはeventを宣言する()
    {
        using var item = JsonDocument.Parse(Build()[ItemHeaderLine]);

        Assert.Equal("event", item.RootElement.GetProperty("type").GetString());
        Assert.Equal("application/json", item.RootElement.GetProperty("content_type").GetString());
    }

    [Fact]
    public void ヘッダーと本体の識別子が一致する()
    {
        var lines = Build();
        using var header = JsonDocument.Parse(lines[EnvelopeHeaderLine]);
        using var payload = JsonDocument.Parse(lines[PayloadLine]);

        Assert.Equal(Identifier, header.RootElement.GetProperty("event_id").GetString());
        Assert.Equal(Identifier, payload.RootElement.GetProperty("event_id").GetString());
    }

    [Fact]
    public void 指紋にプラグイン名を含める()
    {
        var fingerprint = Payload().GetProperty("fingerprint").EnumerateArray().Select(static value => value.GetString()).ToArray();

        Assert.Equal("{{ default }}", fingerprint[0]);
        Assert.Equal(PluginIdentity.Name, fingerprint[^1]);
    }

    [Fact]
    public void 識別子はセッションのみで永続的な値を含めない()
    {
        var user = Payload().GetProperty("user");

        Assert.Equal(TelemetrySession.Id, user.GetProperty("id").GetString());
        Assert.False(user.TryGetProperty("ip_address", out _));
        Assert.False(user.TryGetProperty("username", out _));
        Assert.False(user.TryGetProperty("email", out _));
    }

    [Fact]
    public void プラグインの名前と版をタグに入れる()
    {
        var tags = Payload().GetProperty("tags");

        Assert.Equal(PluginIdentity.Name, tags.GetProperty("plugin").GetString());
        Assert.Equal(PluginIdentity.Version, tags.GetProperty("plugin_version").GetString());
        Assert.Equal(TelemetrySession.Id, tags.GetProperty("session").GetString());
    }

    [Fact]
    public void 計算機を特定しうる値を文脈に入れない()
    {
        var device = Payload().GetProperty("contexts").GetProperty("device");

        Assert.False(device.TryGetProperty("name", out _));
        Assert.False(device.TryGetProperty("model", out _));
    }

    [Fact]
    public void 例外は与えた順序のまま並ぶ()
    {
        var values = Payload().GetProperty("exception").GetProperty("values").EnumerateArray().ToArray();

        Assert.Equal(Inner.Type, values[0].GetProperty("type").GetString());
        Assert.Equal(Outer.Type, values[^1].GetProperty("type").GetString());
    }

    [Fact]
    public void フレームの属性を書き出す()
    {
        var frames = Payload().GetProperty("exception").GetProperty("values")
            .EnumerateArray().Last()
            .GetProperty("stacktrace").GetProperty("frames").EnumerateArray().ToArray();

        Assert.Equal("Plugin.Effect", frames[0].GetProperty("module").GetString());
        Assert.Equal("Render", frames[0].GetProperty("function").GetString());
        Assert.Equal("./Effect.cs", frames[0].GetProperty("filename").GetString());
        Assert.Equal(20, frames[0].GetProperty("lineno").GetInt32());
        Assert.True(frames[0].GetProperty("in_app").GetBoolean());
    }

    [Fact]
    public void 行番号が無いフレームには行番号を書かない()
    {
        var frames = Payload().GetProperty("exception").GetProperty("values")
            .EnumerateArray().Last()
            .GetProperty("stacktrace").GetProperty("frames").EnumerateArray().ToArray();

        Assert.False(frames[^1].TryGetProperty("lineno", out _));
        Assert.False(frames[^1].GetProperty("in_app").GetBoolean());
    }

    [Fact]
    public void 版と環境を書き出す()
    {
        var payload = Payload();

        Assert.Equal(PluginIdentity.Release, payload.GetProperty("release").GetString());
        Assert.Equal("production", payload.GetProperty("environment").GetString());
        Assert.Equal("csharp", payload.GetProperty("platform").GetString());
        Assert.Equal(Level, payload.GetProperty("level").GetString());
    }
}

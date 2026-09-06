using Telemetry;
using Xunit;

namespace Telemetry.Tests;

public class TextScrubberTests
{
    [Fact]
    public void Windowsの利用者ディレクトリを伏せる()
    {
        Assert.Equal(@"C:\Users\[user]\Videos\a.ymmp", TextScrubber.Scrub(@"C:\Users\tanaka\Videos\a.ymmp"));
    }

    [Fact]
    public void ドライブ名は残す()
    {
        Assert.StartsWith(@"D:\Users\[user]", TextScrubber.Scrub(@"D:\Users\tanaka\a.txt"), StringComparison.Ordinal);
    }

    [Fact]
    public void Unixのホームディレクトリを伏せる()
    {
        Assert.Equal("/home/[user]/videos/a.ymmp", TextScrubber.Scrub("/home/tanaka/videos/a.ymmp"));
    }

    [Fact]
    public void メールアドレスを伏せる()
    {
        Assert.Equal("連絡先は [mail] です", TextScrubber.Scrub("連絡先は taro.yamada@example.com です"));
    }

    [Fact]
    public void IPv4アドレスを伏せる()
    {
        Assert.Equal("接続先 [address] に到達できません", TextScrubber.Scrub("接続先 192.168.0.80 に到達できません"));
    }

    [Fact]
    public void 実際の利用者プロファイルを伏せる()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var scrubbed = TextScrubber.Scrub($"読み込み失敗: {profile}\\Videos\\a.ymmp");

        Assert.DoesNotContain(profile, scrubbed, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void 実際の計算機名を伏せる()
    {
        var machine = Environment.MachineName;

        var scrubbed = TextScrubber.Scrub($"ホスト {machine} で失敗しました");

        Assert.DoesNotContain(machine, scrubbed, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void 版数はアドレスとして潰さない()
    {
        const string message = "アセンブリを読み込めません。Version=10.0.26200.0";

        Assert.Equal(message, TextScrubber.Scrub(message));
    }

    [Fact]
    public void 範囲外の値を含む組はアドレスとみなさない()
    {
        const string message = "999.1.1.1 は住所ではない";

        Assert.Equal(message, TextScrubber.Scrub(message));
    }

    [Fact]
    public void リポジトリ相対のパスは触らない()
    {
        const string path = "./Effect/Renderer.cs";

        Assert.Equal(path, TextScrubber.Scrub(path));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void 空の入力はそのまま返す(string? value)
    {
        Assert.Equal(value, TextScrubber.Scrub(value));
    }

    [Fact]
    public void 短すぎる候補は誤爆を避けるため採用しない()
    {
        var selected = TextScrubber.SelectReplacements([new KeyValuePair<string?, string>("ab", "[user]")]);

        Assert.Empty(selected);
    }

    [Fact]
    public void 三文字以上の候補は採用する()
    {
        var selected = TextScrubber.SelectReplacements([new KeyValuePair<string?, string>("abc", "[user]")]);

        Assert.Single(selected);
    }

    [Fact]
    public void nullの候補は採用しない()
    {
        var selected = TextScrubber.SelectReplacements([new KeyValuePair<string?, string>(null, "[user]")]);

        Assert.Empty(selected);
    }

    [Fact]
    public void 重複する候補は一度だけ採用する()
    {
        var selected = TextScrubber.SelectReplacements(
        [
            new KeyValuePair<string?, string>("tanaka", "[user]"),
            new KeyValuePair<string?, string>("TANAKA", "[userprofile]"),
        ]);

        Assert.Single(selected);
    }

    [Fact]
    public void 長い候補から先に置換する()
    {
        var selected = TextScrubber.SelectReplacements(
        [
            new KeyValuePair<string?, string>("abc", "[user]"),
            new KeyValuePair<string?, string>("abcdef", "[userprofile]"),
        ]);

        Assert.Equal("abcdef", selected[0].Key);
    }
}

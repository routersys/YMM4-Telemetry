using System.Globalization;
using System.IO;
using System.Text;
using Telemetry;
using Xunit;

namespace Telemetry.Tests;

public class TelemetrySpoolTests : IDisposable
{
    private const int OverflowCount = TelemetrySpool.MaxFiles + 10;
    private const string Identifier = "0123456789abcdef0123456789abcdef";

    private readonly string _directory;
    private readonly TelemetrySpool _spool;

    public TelemetrySpoolTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "telemetry-spool-tests", Guid.NewGuid().ToString("N"));
        _spool = new TelemetrySpool(_directory);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, true);
        }
        catch (DirectoryNotFoundException)
        {
        }
    }

    [Fact]
    public void 保存先が空なら拒否する()
    {
        Assert.Throws<ArgumentException>(() => new TelemetrySpool(string.Empty));
    }

    [Fact]
    public void 書いた内容をそのまま読み出せる()
    {
        var payload = Encoding.UTF8.GetBytes("envelope");

        var path = _spool.TryWrite(Identifier, payload);

        Assert.NotNull(path);
        Assert.Equal(payload, _spool.TryRead(path));
    }

    [Fact]
    public void 書いたものが一覧に載る()
    {
        var path = _spool.TryWrite(Identifier, Encoding.UTF8.GetBytes("envelope"));

        Assert.Contains(path, _spool.List());
    }

    [Fact]
    public void 削除すると一覧から消える()
    {
        var path = _spool.TryWrite(Identifier, Encoding.UTF8.GetBytes("envelope"))!;

        _spool.TryDelete(path);

        Assert.DoesNotContain(path, _spool.List());
    }

    [Fact]
    public void 一時ファイルを残さない()
    {
        _spool.TryWrite(Identifier, Encoding.UTF8.GetBytes("envelope"));

        Assert.Empty(Directory.EnumerateFiles(_directory, "*.tmp"));
    }

    [Fact]
    public void 存在しない保存先なら空の一覧を返す()
    {
        Assert.Empty(new TelemetrySpool(Path.Combine(_directory, "missing")).List());
    }

    [Fact]
    public void 読めないファイルはnullを返す()
    {
        Assert.Null(_spool.TryRead(Path.Combine(_directory, "missing.envelope")));
    }

    [Fact]
    public void 上限を超えた分は消える()
    {
        for (var index = 0; index < OverflowCount; index++)
        {
            _spool.TryWrite(index.ToString("x32", CultureInfo.InvariantCulture), Encoding.UTF8.GetBytes("envelope"));
        }

        Assert.True(_spool.List().Count <= TelemetrySpool.MaxFiles);
    }

    [Fact]
    public void 全消去で空になる()
    {
        for (var index = 0; index < 3; index++)
        {
            _spool.TryWrite(index.ToString("x32", CultureInfo.InvariantCulture), Encoding.UTF8.GetBytes("envelope"));
        }

        _spool.Clear();

        Assert.Empty(_spool.List());
    }

    [Fact]
    public void 同じ識別子で書き直しても重複しない()
    {
        _spool.TryWrite(Identifier, Encoding.UTF8.GetBytes("一回目"));
        _spool.TryWrite(Identifier, Encoding.UTF8.GetBytes("二回目"));

        Assert.Single(_spool.List());
    }
}

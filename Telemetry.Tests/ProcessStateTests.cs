using System.Threading;
using Telemetry;
using Xunit;

namespace Telemetry.Tests;

public class ProcessStateTests : IDisposable
{
    private const string Name = "ProcessStateTests";

    public ProcessStateTests() => ProcessState.Remove(Name);

    public void Dispose() => ProcessState.Remove(Name);

    [Fact]
    public void 未設定の名前を読むとnullになる()
    {
        Assert.Null(ProcessState.Read(Name));
    }

    [Fact]
    public void 書いた値をそのまま読み出せる()
    {
        ProcessState.Write(Name, 42);

        Assert.Equal(42, ProcessState.Read(Name));
    }

    [Fact]
    public void 除去すると未設定に戻る()
    {
        ProcessState.Write(Name, true);
        ProcessState.Remove(Name);

        Assert.Null(ProcessState.Read(Name));
    }

    [Fact]
    public void 名前が空なら書き込みを拒否する()
    {
        Assert.Throws<ArgumentException>(() => ProcessState.Write(string.Empty, true));
    }

    [Fact]
    public void 値がnullなら書き込みを拒否する()
    {
        Assert.Throws<ArgumentNullException>(() => ProcessState.Write(Name, null!));
    }

    [Fact]
    public void 処理を実行して成功を返す()
    {
        var executed = false;

        var result = ProcessState.TryExecute(() => executed = true);

        Assert.True(result);
        Assert.True(executed);
    }

    [Fact]
    public void 同じスレッドから入れ子にしても停止しない()
    {
        var inner = false;

        var outer = ProcessState.TryExecute(() => ProcessState.TryExecute(() => inner = true));

        Assert.True(outer);
        Assert.True(inner);
    }

    [Fact]
    public void 施錠を取れなければ処理を実行しない()
    {
        using var held = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();

        var holder = new Thread(() => ProcessState.TryExecute(() =>
        {
            held.Set();
            release.Wait();
        }));

        holder.Start();
        held.Wait();

        var executed = false;
        var result = ProcessState.TryExecute(() => executed = true, TimeSpan.FromMilliseconds(50d));

        release.Set();
        holder.Join();

        Assert.False(result);
        Assert.False(executed);
    }

    [Fact]
    public void 複数スレッドから同時に更新しても数え落としが起きない()
    {
        const int ThreadCount = 8;
        const int IncrementsPerThread = 50;

        ProcessState.Write(Name, 0);

        var threads = new List<Thread>(ThreadCount);

        for (var index = 0; index < ThreadCount; index++)
        {
            var thread = new Thread(() =>
            {
                for (var step = 0; step < IncrementsPerThread; step++)
                {
                    ProcessState.TryExecute(() =>
                    {
                        var current = ProcessState.Read(Name) is int value ? value : 0;
                        ProcessState.Write(Name, current + 1);
                    });
                }
            });

            threads.Add(thread);
            thread.Start();
        }

        foreach (var thread in threads)
        {
            thread.Join();
        }

        Assert.Equal(ThreadCount * IncrementsPerThread, ProcessState.Read(Name));
    }
}

using System.Globalization;
using System.Threading;

namespace Telemetry;

internal static class ProcessState
{
    private const string DataKeyPrefix = "Telemetry.";
    private const string LockNamePrefix = @"Local\Telemetry.ProcessState.";

    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(5);

    private static readonly string LockName = string.Create(
        CultureInfo.InvariantCulture,
        $"{LockNamePrefix}{Environment.ProcessId}");

    public static object? Read(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return AppDomain.CurrentDomain.GetData(DataKeyPrefix + name);
    }

    public static void Write(string name, object value)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(value);
        AppDomain.CurrentDomain.SetData(DataKeyPrefix + name, value);
    }

    public static void Remove(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        AppDomain.CurrentDomain.SetData(DataKeyPrefix + name, null);
    }

    public static bool TryExecute(Action action, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(action);

        using var handle = new Mutex(false, LockName);
        var acquired = false;

        try
        {
            try
            {
                acquired = handle.WaitOne(timeout ?? LockTimeout, false);
            }
            catch (AbandonedMutexException)
            {
                acquired = true;
            }

            if (!acquired)
            {
                return false;
            }

            action();
            return true;
        }
        finally
        {
            if (acquired)
            {
                handle.ReleaseMutex();
            }
        }
    }
}

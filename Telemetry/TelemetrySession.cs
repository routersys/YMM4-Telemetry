using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Telemetry;

internal static class TelemetrySession
{
    private const string DerivationPrefix = "telemetry.session";
    private const string UnknownStartTime = "unknown";
    private const int IdentifierLength = 32;

    public static string Id { get; } = Derive(Environment.ProcessId, ReadStartTime());

    internal static string Derive(int processId, string startTime)
    {
        ArgumentNullException.ThrowIfNull(startTime);

        var seed = string.Create(
            CultureInfo.InvariantCulture,
            $"{DerivationPrefix} {processId} {startTime}");

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        return Convert.ToHexStringLower(hash)[..IdentifierLength];
    }

    private static string ReadStartTime()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            return process.StartTime.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            return UnknownStartTime;
        }
    }
}

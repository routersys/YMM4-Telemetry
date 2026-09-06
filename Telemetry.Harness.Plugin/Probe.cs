using System.Globalization;
using Telemetry;

namespace Telemetry.Harness.Plugin;

public static class Probe
{
    public static string AssemblyName => typeof(Probe).Assembly.GetName().Name ?? "unknown";

    public static string PluginName => PluginIdentity.Name;

    public static string SessionId => TelemetrySession.Id;

    public static string ReserveBudget(string fingerprint) =>
        TelemetryBudget.TryReserve(fingerprint).ToString();

    public static string PrepareReport()
    {
        try
        {
            throw new InvalidOperationException("検証台からの報告です。");
        }
        catch (Exception exception)
        {
            return TelemetryReporter.Prepare(exception, TelemetrySpool.Default).Decision.ToString();
        }
    }

    public static string BuildEnvelope()
    {
        try
        {
            throw new InvalidOperationException("検証台からの報告です。");
        }
        catch (Exception exception)
        {
            var captured = ExceptionCapture.Capture(exception, PluginIdentity.Assembly);
            var envelope = SentryEnvelope.Build(
                Guid.NewGuid().ToString("N"),
                captured,
                "error",
                DateTimeOffset.UtcNow);

            return System.Text.Encoding.UTF8.GetString(envelope);
        }
    }

    public static string Describe() => string.Create(
        CultureInfo.InvariantCulture,
        $"{AssemblyName} plugin={PluginName} session={SessionId}");
}

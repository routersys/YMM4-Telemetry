using System.Globalization;

namespace Telemetry;

internal static class TelemetryEndpoint
{
    private const string Scheme = "https";
    private const string Host = "ingest.routersys.com";
    private const string PublicKey = "41c831f58032402692bcae2a3756906e";
    private const string ProjectId = "1";
    private const string SentryProtocolVersion = "7";

    public const string ClientName = "telemetry.ymm4";
    public const string ClientVersion = "1.0.0";
    public const string ContentType = "application/x-sentry-envelope";
    public const string AuthorizationHeaderName = "X-Sentry-Auth";

    public static Uri EnvelopeUri { get; } = new(string.Create(
        CultureInfo.InvariantCulture,
        $"{Scheme}://{Host}/api/{ProjectId}/envelope/"));

    public static string AuthorizationHeaderValue { get; } = string.Create(
        CultureInfo.InvariantCulture,
        $"Sentry sentry_version={SentryProtocolVersion}, sentry_key={PublicKey}, sentry_client={ClientName}/{ClientVersion}");

    public static string UserAgent { get; } = string.Create(
        CultureInfo.InvariantCulture,
        $"{ClientName}/{ClientVersion}");
}

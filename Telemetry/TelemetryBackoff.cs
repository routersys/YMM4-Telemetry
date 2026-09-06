namespace Telemetry;

internal static class TelemetryBackoff
{
    private const string StateName = "RetryAfter";

    public static bool IsActive =>
        ProcessState.Read(StateName) is DateTimeOffset until && until > DateTimeOffset.UtcNow;

    public static bool Extend(DateTimeOffset until) => ProcessState.TryExecute(() =>
    {
        if (ProcessState.Read(StateName) is DateTimeOffset existing && existing >= until)
        {
            return;
        }

        ProcessState.Write(StateName, until);
    });
}

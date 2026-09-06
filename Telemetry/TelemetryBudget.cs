namespace Telemetry;

internal enum BudgetDecision
{
    Allowed,
    SessionLimitReached,
    DuplicateLimitReached,
    RateLimited,
    Unavailable,
}

internal static class TelemetryBudget
{
    public const int MaxEventsPerSession = 20;
    public const int MaxEventsPerFingerprint = 3;

    private const string SentCountName = "SentCount";
    private const string FingerprintCountsName = "FingerprintCounts";
    private const int NoEvents = 0;

    public static BudgetDecision TryReserve(string fingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);

        var decision = BudgetDecision.Unavailable;

        var executed = ProcessState.TryExecute(() =>
        {
            if (TelemetryBackoff.IsActive)
            {
                decision = BudgetDecision.RateLimited;
                return;
            }

            var sent = ProcessState.Read(SentCountName) is int stored ? stored : NoEvents;
            if (sent >= MaxEventsPerSession)
            {
                decision = BudgetDecision.SessionLimitReached;
                return;
            }

            var counts = ReadFingerprintCounts();
            counts.TryGetValue(fingerprint, out var duplicates);
            if (duplicates >= MaxEventsPerFingerprint)
            {
                decision = BudgetDecision.DuplicateLimitReached;
                return;
            }

            counts[fingerprint] = duplicates + 1;
            ProcessState.Write(SentCountName, sent + 1);
            ProcessState.Write(FingerprintCountsName, counts);
            decision = BudgetDecision.Allowed;
        });

        return executed ? decision : BudgetDecision.Unavailable;
    }

    private static Dictionary<string, int> ReadFingerprintCounts() =>
        ProcessState.Read(FingerprintCountsName) as Dictionary<string, int>
        ?? new Dictionary<string, int>(StringComparer.Ordinal);
}

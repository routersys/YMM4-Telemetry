using Telemetry;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Telemetry.Tests;

internal static class ProcessStateReset
{
    private static readonly string[] Names =
    [
        "RetryAfter",
        "SentCount",
        "FingerprintCounts",
        "DrainClaimed",
    ];

    public static void Clear()
    {
        foreach (var name in Names)
        {
            ProcessState.Remove(name);
        }
    }
}

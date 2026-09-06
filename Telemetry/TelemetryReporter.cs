using System.Threading;
using System.Threading.Tasks;

namespace Telemetry;

internal enum ReportDecision
{
    Prepared,
    NoException,
    SessionLimitReached,
    DuplicateLimitReached,
    RateLimited,
    Unavailable,
}

internal readonly record struct PreparationResult(ReportDecision Decision, byte[]? Envelope, string? SpoolPath);

public static class TelemetryReporter
{
    private const string ErrorLevel = "error";
    private const string DrainStateName = "DrainClaimed";
    private const int MaxDrainBatch = 10;
    private const string IdentifierFormat = "N";

    public static void Report(Exception? exception)
    {
        try
        {
            var prepared = Prepare(exception, TelemetrySpool.Default);
            if (prepared.Decision != ReportDecision.Prepared || prepared.Envelope is null)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await DeliverAsync(prepared, HttpTelemetryChannel.Default, TelemetrySpool.Default, CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (Exception)
                {
                }
            });
        }
        catch (Exception)
        {
        }
    }

    public static void Start()
    {
        try
        {
            if (!TryClaimDrain())
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await DrainAsync(HttpTelemetryChannel.Default, TelemetrySpool.Default, CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (Exception)
                {
                }
            });
        }
        catch (Exception)
        {
        }
    }

    internal static PreparationResult Prepare(Exception? exception, TelemetrySpool spool)
    {
        ArgumentNullException.ThrowIfNull(spool);

        if (exception is null)
        {
            return new PreparationResult(ReportDecision.NoException, null, null);
        }

        var captured = ExceptionCapture.Capture(exception, PluginIdentity.Assembly);

        var decision = TelemetryBudget.TryReserve(EventFingerprint.Compute(captured));
        if (decision != BudgetDecision.Allowed)
        {
            return new PreparationResult(Translate(decision), null, null);
        }

        var identifier = Guid.NewGuid().ToString(IdentifierFormat);
        var envelope = SentryEnvelope.Build(identifier, captured, ErrorLevel, DateTimeOffset.UtcNow);

        return new PreparationResult(ReportDecision.Prepared, envelope, spool.TryWrite(identifier, envelope));
    }

    internal static async Task<DeliveryOutcome> DeliverAsync(
        PreparationResult prepared,
        ITelemetryChannel channel,
        TelemetrySpool spool,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(spool);

        if (prepared.Envelope is null)
        {
            return DeliveryOutcome.Rejected;
        }

        var result = await channel.SendAsync(prepared.Envelope, cancellationToken).ConfigureAwait(false);

        if (result.Outcome == DeliveryOutcome.RateLimited)
        {
            TelemetryBackoff.Extend(DateTimeOffset.UtcNow + result.RetryAfter);
        }

        if (prepared.SpoolPath is not null
            && result.Outcome is DeliveryOutcome.Delivered or DeliveryOutcome.Rejected)
        {
            spool.TryDelete(prepared.SpoolPath);
        }

        return result.Outcome;
    }

    internal static async Task<int> DrainAsync(ITelemetryChannel channel, TelemetrySpool spool, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(spool);

        var delivered = 0;

        foreach (var path in spool.List())
        {
            if (delivered >= MaxDrainBatch || TelemetryBackoff.IsActive)
            {
                break;
            }

            var envelope = spool.TryRead(path);
            if (envelope is null)
            {
                spool.TryDelete(path);
                continue;
            }

            var outcome = await DeliverAsync(
                new PreparationResult(ReportDecision.Prepared, envelope, path),
                channel,
                spool,
                cancellationToken).ConfigureAwait(false);

            if (outcome is not (DeliveryOutcome.Delivered or DeliveryOutcome.Rejected))
            {
                break;
            }

            delivered++;
        }

        return delivered;
    }

    private static bool TryClaimDrain()
    {
        var claimed = false;

        ProcessState.TryExecute(() =>
        {
            if (ProcessState.Read(DrainStateName) is null)
            {
                ProcessState.Write(DrainStateName, true);
                claimed = true;
            }
        });

        return claimed;
    }

    private static ReportDecision Translate(BudgetDecision decision) => decision switch
    {
        BudgetDecision.SessionLimitReached => ReportDecision.SessionLimitReached,
        BudgetDecision.DuplicateLimitReached => ReportDecision.DuplicateLimitReached,
        BudgetDecision.RateLimited => ReportDecision.RateLimited,
        _ => ReportDecision.Unavailable,
    };
}

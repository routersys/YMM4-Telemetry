using System.Threading;
using System.Threading.Tasks;

namespace Telemetry;

internal enum DeliveryOutcome
{
    Delivered,
    RateLimited,
    Rejected,
    Deferred,
}

internal readonly record struct DeliveryResult(DeliveryOutcome Outcome, TimeSpan RetryAfter);

internal interface ITelemetryChannel
{
    Task<DeliveryResult> SendAsync(byte[] envelope, CancellationToken cancellationToken);
}

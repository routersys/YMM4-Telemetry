using System.Threading;
using System.Threading.Tasks;
using Telemetry;

namespace Telemetry.Tests;

internal sealed class FakeTelemetryChannel : ITelemetryChannel
{
    private readonly Queue<DeliveryResult> _planned = new();

    public FakeTelemetryChannel(DeliveryOutcome outcome, TimeSpan retryAfter = default) =>
        Fallback = new DeliveryResult(outcome, retryAfter);

    public DeliveryResult Fallback { get; }

    public List<byte[]> Sent { get; } = [];

    public void Plan(DeliveryOutcome outcome, TimeSpan retryAfter = default) =>
        _planned.Enqueue(new DeliveryResult(outcome, retryAfter));

    public Task<DeliveryResult> SendAsync(byte[] envelope, CancellationToken cancellationToken)
    {
        Sent.Add(envelope);
        return Task.FromResult(_planned.Count > 0 ? _planned.Dequeue() : Fallback);
    }
}

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Telemetry;

internal sealed class HttpTelemetryChannel : ITelemetryChannel
{
    private const int ClientErrorLowerBound = 400;
    private const int ClientErrorUpperBound = 500;

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan FallbackRetryAfter = TimeSpan.FromSeconds(10);
    private static readonly Lazy<HttpClient> SharedClient = new(static () => CreateClient(null), true);

    private readonly HttpClient? _client;

    private HttpTelemetryChannel()
    {
    }

    public HttpTelemetryChannel(HttpMessageHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _client = CreateClient(handler);
    }

    public static HttpTelemetryChannel Default { get; } = new();

    private HttpClient Client => _client ?? SharedClient.Value;

    public async Task<DeliveryResult> SendAsync(byte[] envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, TelemetryEndpoint.EnvelopeUri);
            request.Headers.TryAddWithoutValidation(
                TelemetryEndpoint.AuthorizationHeaderName,
                TelemetryEndpoint.AuthorizationHeaderValue);

            request.Content = new ByteArrayContent(envelope);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(TelemetryEndpoint.ContentType);

            using var response = await Client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                return new DeliveryResult(DeliveryOutcome.Delivered, TimeSpan.Zero);
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return new DeliveryResult(DeliveryOutcome.RateLimited, ReadRetryAfter(response));
            }

            var status = (int)response.StatusCode;
            return status >= ClientErrorLowerBound && status < ClientErrorUpperBound
                ? new DeliveryResult(DeliveryOutcome.Rejected, TimeSpan.Zero)
                : new DeliveryResult(DeliveryOutcome.Deferred, TimeSpan.Zero);
        }
        catch (Exception)
        {
            return new DeliveryResult(DeliveryOutcome.Deferred, TimeSpan.Zero);
        }
    }

    private static TimeSpan ReadRetryAfter(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;

        if (retryAfter?.Delta is { } delta && delta > TimeSpan.Zero)
        {
            return delta;
        }

        if (retryAfter?.Date is { } date)
        {
            var remaining = date - DateTimeOffset.UtcNow;
            if (remaining > TimeSpan.Zero)
            {
                return remaining;
            }
        }

        return FallbackRetryAfter;
    }

    private static HttpClient CreateClient(HttpMessageHandler? handler)
    {
        var client = handler is null
            ? new HttpClient { Timeout = RequestTimeout }
            : new HttpClient(handler) { Timeout = RequestTimeout };

        client.DefaultRequestHeaders.UserAgent.ParseAdd(TelemetryEndpoint.UserAgent);
        return client;
    }
}

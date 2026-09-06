using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Telemetry.Tests;

internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

    public StubHttpMessageHandler(HttpStatusCode status, TimeSpan? retryAfter = null)
        : this(_ => Build(status, retryAfter))
    {
    }

    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

    public List<HttpRequestMessage> Requests { get; } = [];

    public List<byte[]> Bodies { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);

        if (request.Content is not null)
        {
            Bodies.Add(await request.Content.ReadAsByteArrayAsync(cancellationToken));
        }

        return _respond(request);
    }

    private static HttpResponseMessage Build(HttpStatusCode status, TimeSpan? retryAfter)
    {
        var response = new HttpResponseMessage(status);

        if (retryAfter is { } delay)
        {
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(delay);
        }

        return response;
    }
}

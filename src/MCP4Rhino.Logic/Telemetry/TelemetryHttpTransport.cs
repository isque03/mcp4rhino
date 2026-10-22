using System.Net.Http.Headers;
using System.Text;

namespace MCP4Rhino.Logic.Telemetry;

public sealed class TelemetryHttpTransport : ITelemetryTransport
{
    public const int SuccessStatus = 204;

    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);

    private readonly HttpClient _http;
    private readonly Uri _endpoint;

    public TelemetryHttpTransport(string endpoint, string pluginVersion, HttpMessageHandler? handler = null)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
            throw new ArgumentException("Telemetry endpoint must be an absolute URI.", nameof(endpoint));

        _endpoint = uri;
        _http = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: true);
        _http.Timeout = DefaultTimeout;
        _http.DefaultRequestHeaders.UserAgent.Clear();
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MCP4Rhino", pluginVersion));
    }

    public TimeSpan Timeout => _http.Timeout;

    public async Task<bool> SendAsync(IReadOnlyList<TelemetryEvent> events, CancellationToken cancellationToken)
    {
        var json = TelemetryEvent.SerializeBatch(events);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync(_endpoint, content, cancellationToken).ConfigureAwait(false);
        return (int)response.StatusCode == SuccessStatus;
    }

    public void Dispose() => _http.Dispose();
}

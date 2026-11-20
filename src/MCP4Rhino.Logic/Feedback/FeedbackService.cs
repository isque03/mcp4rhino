using System.Net.Http.Headers;
using System.Text;
using MCP4Rhino.Logic.Telemetry;

namespace MCP4Rhino.Logic.Feedback;

/// <summary>Delivers one feedback body. Return <c>false</c> for a non-success response; throw on network failure.</summary>
public interface IFeedbackTransport : IDisposable
{
    Task<bool> SendAsync(string json, CancellationToken cancellationToken);
}

public readonly record struct FeedbackResult(bool Sent, string Message);

/// <summary>
/// Sends feedback the user explicitly asked for. This is deliberately separate from usage data:
/// it does not read the usage-data choice, never attaches the installation id, and does not
/// queue, retry or persist anything.
/// </summary>
public sealed class FeedbackService(
    IFeedbackTransport transport,
    string pluginVersion,
    string rhinoVersion,
    string os,
    Func<DateTimeOffset>? utcNow = null,
    TimeSpan? sendDeadline = null)
{
    public const string SuccessMessage = "Feedback sent. Thank you.";

    public static readonly TimeSpan SendDeadline = TimeSpan.FromSeconds(5);

    private readonly Func<DateTimeOffset> _clock = utcNow ?? (() => DateTimeOffset.UtcNow);

    public FeedbackPayload BuildPayload(FeedbackRequest request) => new()
    {
        Message = request.Message,
        Category = request.Category,
        Tool = request.Tool,
        PluginVersion = pluginVersion,
        RhinoVersion = rhinoVersion,
        Os = os,
        Timestamp = FeedbackPayload.FormatTimestamp(_clock()),
    };

    /// <summary>Never throws. Failure carries a short message that is safe to show the user.</summary>
    public FeedbackResult Send(FeedbackRequest request)
    {
        try
        {
            var json = BuildPayload(request).ToJson();
            using var cts = new CancellationTokenSource(sendDeadline ?? SendDeadline);
            var send = Task.Run(() => transport.SendAsync(json, cts.Token));
            // The transport honors the token; the extra second guards a transport that does not.
            if (Task.WaitAny([send], (sendDeadline ?? SendDeadline) + TimeSpan.FromSeconds(1)) < 0)
                return new FeedbackResult(false, "Sending feedback timed out. Nothing was sent. Try again later.");
            var ok = send.GetAwaiter().GetResult();
            return ok
                ? new FeedbackResult(true, SuccessMessage)
                : new FeedbackResult(false, "The feedback service did not accept the message. Nothing was sent.");
        }
        catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
        {
            return new FeedbackResult(false, "Sending feedback timed out. Nothing was sent. Try again later.");
        }
        catch
        {
            return new FeedbackResult(false, "Could not reach the feedback service. Nothing was sent.");
        }
    }
}

public sealed class FeedbackHttpTransport : IFeedbackTransport
{
    public const int SuccessStatus = 204;

    private readonly HttpClient _http;
    private readonly Uri _endpoint;

    public FeedbackHttpTransport(
        string endpoint,
        string pluginVersion,
        string apiKey,
        HttpMessageHandler? handler = null)
    {
        // `endpoint` is the final URL, usually from TelemetryEndpoint.ResolveFeedback().
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new ArgumentException("Feedback endpoint must be an absolute http or https URI.", nameof(endpoint));
        apiKey = apiKey?.Trim() ?? "";
        if (apiKey.Length == 0 || apiKey.IndexOfAny(['\r', '\n']) >= 0)
            throw new ArgumentException("Feedback API key must be a single header value.", nameof(apiKey));
        if (uri.AbsoluteUri.Contains(apiKey, StringComparison.Ordinal))
            throw new ArgumentException("Feedback endpoint must not contain the API key.", nameof(endpoint));

        _endpoint = uri;
        _http = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: true);
        _http.Timeout = FeedbackService.SendDeadline;
        _http.DefaultRequestHeaders.UserAgent.Clear();
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MCP4Rhino", pluginVersion));
        if (!_http.DefaultRequestHeaders.TryAddWithoutValidation(TelemetryEndpoint.ApiKeyHeader, apiKey))
            throw new InvalidOperationException("Feedback API key header was not accepted.");
    }

    public TimeSpan Timeout => _http.Timeout;

    public Uri Endpoint => _endpoint;

    public async Task<bool> SendAsync(string json, CancellationToken cancellationToken)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync(_endpoint, content, cancellationToken).ConfigureAwait(false);
        return (int)response.StatusCode == SuccessStatus &&
            (await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false)).Length == 0;
    }

    public void Dispose() => _http.Dispose();
}

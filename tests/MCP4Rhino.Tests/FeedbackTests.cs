using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using MCP4Rhino.Logic;
using MCP4Rhino.Logic.Feedback;
using MCP4Rhino.Logic.Telemetry;
using Xunit;

namespace MCP4Rhino.Tests;

public class FeedbackTests : IDisposable
{
    private const string InstallationId = "11111111-1111-1111-1111-111111111111";
    private const string ApiKey = "install-token-value";
    private static readonly DateTimeOffset FixedClock = new(2026, 10, 20, 14, 30, 0, TimeSpan.Zero);
    private static readonly IReadOnlySet<string> Registered = new HashSet<string> { "create_box", "send_feedback" };

    public void Dispose()
    {
        Telemetry.Configure(new TelemetryOptions { ConsentGranted = false, UseTimer = false });
        Telemetry.Shutdown();
    }

    private sealed class FakeTools : IMcpToolProvider
    {
        public object[] ListTools() =>
        [
            new { name = "create_box", description = "box", inputSchema = new { type = "object" } },
        ];

        public object CallTool(JsonNode? @params) => throw new InvalidOperationException("not used");
    }

    private sealed class RecordingTransport : IFeedbackTransport
    {
        public Func<string, CancellationToken, Task<bool>> Handler { get; init; } = (_, _) => Task.FromResult(true);

        public List<string> Bodies { get; } = [];

        public Task<bool> SendAsync(string json, CancellationToken cancellationToken)
        {
            Bodies.Add(json);
            return Handler(json, cancellationToken);
        }

        public void Dispose() { }
    }

    private static FeedbackService Service(RecordingTransport transport) =>
        new(transport, "0.7.3", "8", "macOS", () => FixedClock);

    private static JsonNode? Call(JsonObject arguments, FeedbackService? service)
    {
        var request = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = 1,
            ["method"] = "tools/call",
            ["params"] = new JsonObject { ["name"] = "send_feedback", ["arguments"] = arguments },
        };
        return McpJsonRpc.Dispatch(request, new FakeTools(), feedback: service);
    }

    private static JsonObject Args(string message = "Please add a mirror tool.") => new() { ["message"] = message };

    [Fact]
    public void Tool_Is_Listed_With_Strict_Schema_And_Cautious_Description()
    {
        var list = McpJsonRpc.Dispatch(
            JsonNode.Parse("""{"jsonrpc":"2.0","id":3,"method":"tools/list","params":{}}"""), new FakeTools());
        var tool = list!["result"]!["tools"]!.AsArray().Single(t => t!["name"]!.GetValue<string>() == "send_feedback")!;
        var description = tool["description"]!.GetValue<string>();
        Assert.Contains("ONLY when the user explicitly asks", description);
        Assert.Contains("Never call it on your own initiative", description);
        Assert.Contains("show the user the exact text", description);

        var schema = tool["inputSchema"]!;
        Assert.Equal("message", Assert.Single(schema["required"]!.AsArray())!.GetValue<string>());
        Assert.Equal(1, schema["properties"]!["message"]!["minLength"]!.GetValue<int>());
        Assert.Equal(4000, schema["properties"]!["message"]!["maxLength"]!.GetValue<int>());
        Assert.Equal(
            new[] { "bug", "feature_request", "praise", "other" },
            schema["properties"]!["category"]!["enum"]!.AsArray().Select(n => n!.GetValue<string>()).ToArray());
        Assert.Equal("other", schema["properties"]!["category"]!["default"]!.GetValue<string>());
        Assert.False(schema["additionalProperties"]!.GetValue<bool>());
        Assert.Equal(
            new[] { "message", "category", "tool" },
            schema["properties"]!.AsObject().Select(p => p.Key).ToArray());
    }

    [Fact]
    public void Parse_Applies_Defaults_And_Trims()
    {
        var request = FeedbackRequest.Parse(Args("  hello  "), Registered);
        Assert.Equal("hello", request.Message);
        Assert.Equal("other", request.Category);
        Assert.Null(request.Tool);

        var full = FeedbackRequest.Parse(
            new JsonObject { ["message"] = "x", ["category"] = "feature_request", ["tool"] = "create_box" }, Registered);
        Assert.Equal("feature_request", full.Category);
        Assert.Equal("create_box", full.Tool);

        var blanks = FeedbackRequest.Parse(
            new JsonObject { ["message"] = "x", ["category"] = null, ["tool"] = "  " }, Registered);
        Assert.Equal("other", blanks.Category);
        Assert.Null(blanks.Tool);
    }

    [Theory]
    [InlineData("bug")]
    [InlineData("feature_request")]
    [InlineData("praise")]
    [InlineData("other")]
    public void Parse_Accepts_Every_Category(string category)
    {
        var args = Args();
        args["category"] = category;
        Assert.Equal(category, FeedbackRequest.Parse(args, Registered).Category);
    }

    [Fact]
    public void Parse_Enforces_Message_Length_In_Characters()
    {
        Assert.Throws<ArgumentException>(() => FeedbackRequest.Parse(new JsonObject(), Registered));
        Assert.Throws<ArgumentException>(() => FeedbackRequest.Parse(Args(""), Registered));
        Assert.Throws<ArgumentException>(() => FeedbackRequest.Parse(Args(" \n\t "), Registered));
        Assert.Throws<ArgumentException>(() => FeedbackRequest.Parse(Args(new string('a', 4001)), Registered));

        Assert.Equal(1, FeedbackRequest.Parse(Args("a"), Registered).Message.Length);
        Assert.Equal(4000, FeedbackRequest.Parse(Args(new string('a', 4000)), Registered).Message.Length);
        // 4000 characters outside the BMP are 8000 UTF-16 units but 4000 characters.
        Assert.Equal(8000, FeedbackRequest.Parse(Args(string.Concat(Enumerable.Repeat("\U0001F600", 4000))), Registered).Message.Length);
        Assert.Throws<ArgumentException>(() =>
            FeedbackRequest.Parse(Args(string.Concat(Enumerable.Repeat("\U0001F600", 4001))), Registered));
    }

    [Fact]
    public void Parse_Rejects_Bad_Types_Values_And_Unknown_Arguments()
    {
        Assert.Throws<ArgumentException>(() => FeedbackRequest.Parse(new JsonObject { ["message"] = 5 }, Registered));
        Assert.Throws<ArgumentException>(() => FeedbackRequest.Parse(Args("\ud800 broken"), Registered));
        foreach (var category in new[] { "Bug", "complaint", "bugs" })
        {
            var args = Args();
            args["category"] = category;
            Assert.Throws<ArgumentException>(() => FeedbackRequest.Parse(args, Registered));
        }

        var badType = Args();
        badType["category"] = 3;
        Assert.Throws<ArgumentException>(() => FeedbackRequest.Parse(badType, Registered));

        var unknownTool = Args();
        unknownTool["tool"] = "not_a_tool";
        var ex = Assert.Throws<ArgumentException>(() => FeedbackRequest.Parse(unknownTool, Registered));
        Assert.Contains("not_a_tool", ex.Message);

        foreach (var extra in new[] { "installationId", "path", "document", "geometry" })
        {
            var args = Args();
            args[extra] = "value";
            Assert.Throws<ArgumentException>(() => FeedbackRequest.Parse(args, Registered));
        }
    }

    [Fact]
    public void Payload_Has_Exactly_The_Documented_Fields_And_No_Install_Id()
    {
        var service = Service(new RecordingTransport());
        var payload = service.BuildPayload(new FeedbackRequest("It works.", "praise", "create_box"));
        var json = JsonNode.Parse(payload.ToJson())!.AsObject();
        Assert.Equal(
            new[] { "category", "message", "os", "pluginVersion", "rhinoVersion", "timestamp", "tool" },
            json.Select(p => p.Key).OrderBy(k => k, StringComparer.Ordinal).ToArray());
        Assert.Equal("It works.", json["message"]!.GetValue<string>());
        Assert.Equal("praise", json["category"]!.GetValue<string>());
        Assert.Equal("create_box", json["tool"]!.GetValue<string>());
        Assert.Equal("0.7.3", json["pluginVersion"]!.GetValue<string>());
        Assert.Equal("8", json["rhinoVersion"]!.GetValue<string>());
        Assert.Equal("macOS", json["os"]!.GetValue<string>());
        Assert.Equal("2026-10-20T14:30:00Z", json["timestamp"]!.GetValue<string>());

        var withoutTool = JsonNode.Parse(service.BuildPayload(new FeedbackRequest("x", "other", null)).ToJson())!.AsObject();
        Assert.False(withoutTool.ContainsKey("tool"));
        Assert.DoesNotContain(
            typeof(FeedbackPayload).GetProperties(),
            p => p.Name.Contains("install", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Tool_Call_Sends_And_Confirms_Without_A_Tracking_Id()
    {
        var transport = new RecordingTransport();
        var response = Call(
            new JsonObject { ["message"] = "Add mirror", ["category"] = "feature_request", ["tool"] = "create_box" },
            Service(transport));

        Assert.Null(response!["error"]);
        var result = response["result"]!;
        Assert.False(result["isError"]!.GetValue<bool>());
        Assert.Equal("Feedback sent. Thank you.", result["content"]![0]!["text"]!.GetValue<string>());
        var body = Assert.Single(transport.Bodies);
        Assert.Contains("\"category\":\"feature_request\"", body);
        Assert.Contains("\"tool\":\"create_box\"", body);
    }

    [Fact]
    public void Sends_When_Usage_Data_Is_Off_And_Never_Attaches_The_Installation_Id()
    {
        Telemetry.Configure(new TelemetryOptions
        {
            ConsentGranted = false,
            InstallationId = InstallationId,
            UseTimer = false,
        });
        var transport = new RecordingTransport();
        var response = Call(Args(), Service(transport));
        Assert.False(response!["result"]!["isError"]!.GetValue<bool>());
        var body = Assert.Single(transport.Bodies);
        Assert.DoesNotContain(InstallationId, body);
        Assert.DoesNotContain("installation", body, StringComparison.OrdinalIgnoreCase);

        Telemetry.Configure(new TelemetryOptions
        {
            ConsentGranted = true,
            InstallationId = InstallationId,
            UseTimer = false,
        });
        Call(Args(), Service(transport));
        Assert.Equal(2, transport.Bodies.Count);
        Assert.DoesNotContain(InstallationId, transport.Bodies[1]);
    }

    [Fact]
    public void Invalid_Arguments_Return_An_Error_And_Send_Nothing()
    {
        var transport = new RecordingTransport();
        foreach (var args in new[]
        {
            new JsonObject(),
            Args(new string('a', 4001)),
            new JsonObject { ["message"] = "x", ["category"] = "nope" },
            new JsonObject { ["message"] = "x", ["tool"] = "not_a_tool" },
            new JsonObject { ["message"] = "x", ["installationId"] = InstallationId },
        })
        {
            var response = Call(args, Service(transport));
            Assert.NotNull(response!["error"]);
        }

        Assert.Empty(transport.Bodies);
    }

    [Fact]
    public void Delivery_Failures_Return_A_Tool_Error_And_Never_Throw()
    {
        var failures = new Func<string, CancellationToken, Task<bool>>[]
        {
            (_, _) => Task.FromResult(false),
            (_, _) => throw new HttpRequestException("secret detail 10.0.0.1"),
            (_, _) => Task.FromException<bool>(new TaskCanceledException()),
            (_, _) => Task.FromException<bool>(new TimeoutException()),
        };
        foreach (var handler in failures)
        {
            var response = Call(Args(), Service(new RecordingTransport { Handler = handler }));
            Assert.Null(response!["error"]);
            var result = response["result"]!;
            Assert.True(result["isError"]!.GetValue<bool>());
            var text = result["content"]![0]!["text"]!.GetValue<string>();
            Assert.Contains("Nothing was sent", text);
            Assert.DoesNotContain("10.0.0.1", text);
        }
    }

    [Fact]
    public void Missing_Service_Is_A_Clear_Tool_Error()
    {
        var response = Call(Args(), null);
        var result = response!["result"]!;
        Assert.True(result["isError"]!.GetValue<bool>());
        Assert.Contains(SupportInfo.NewIssueUrl, result["content"]![0]!["text"]!.GetValue<string>());
    }

    [Fact]
    public void Slow_Delivery_Is_Cancelled_At_The_Deadline()
    {
        Assert.Equal(TimeSpan.FromSeconds(5), FeedbackService.SendDeadline);
        var observed = new TaskCompletionSource<bool>();
        var transport = new RecordingTransport
        {
            Handler = async (_, token) =>
            {
                token.Register(() => observed.TrySetResult(true));
                await Task.Delay(Timeout.Infinite, token);
                return true;
            },
        };
        var result = new FeedbackService(transport, "0.7.3", "8", "macOS", sendDeadline: TimeSpan.FromMilliseconds(100))
            .Send(new FeedbackRequest("x", "other", null));
        Assert.False(result.Sent);
        Assert.Contains("timed out", result.Message);
        Assert.True(observed.Task.IsCompleted);
    }

    [Fact]
    public void Feedback_Url_Follows_The_Telemetry_Url_Override()
    {
        Assert.Equal("https://events.agenttoolkit.ai/feedback", TelemetryEndpoint.ResolveFeedback());
        Assert.Equal("http://127.0.0.1:8080/feedback", TelemetryEndpoint.ResolveFeedback("http://127.0.0.1:8080"));
        Assert.Equal("https://example.test/feedback", TelemetryEndpoint.ResolveFeedback("https://example.test/telemetry"));
        Assert.Equal("https://example.test/v1/feedback?x=1", TelemetryEndpoint.ResolveFeedback("https://example.test/v1?k=secret&x=1"));
        Assert.Null(TelemetryEndpoint.ResolveFeedback("ftp://example.test"));
        Assert.Null(TelemetryEndpoint.ResolveFeedback("not a url"));
    }

    [Fact]
    public async Task Http_Transport_Posts_Json_With_One_Api_Key_Header()
    {
        var handler = new StubHandler();
        using var transport = new FeedbackHttpTransport("https://example.test/feedback", "0.7.3", ApiKey, handler);
        Assert.Equal(TimeSpan.FromSeconds(5), transport.Timeout);
        Assert.True(await transport.SendAsync("""{"message":"x"}""", CancellationToken.None));
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("https://example.test/feedback", handler.Uri!.AbsoluteUri);
        Assert.Equal("MCP4Rhino/0.7.3", handler.UserAgent);
        Assert.StartsWith("application/json", handler.ContentType);
        Assert.Equal(new[] { ApiKey }, handler.ApiKeyValues);
        Assert.Equal("""{"message":"x"}""", handler.Body);
        Assert.DoesNotContain(ApiKey, handler.Uri.AbsoluteUri);
    }

    [Fact]
    public async Task Http_Transport_Treats_Anything_But_An_Empty_204_As_Failure()
    {
        using var rejected = new FeedbackHttpTransport(
            "https://example.test/feedback", "0.7.3", ApiKey, new StubHandler { Status = HttpStatusCode.ServiceUnavailable });
        Assert.False(await rejected.SendAsync("{}", CancellationToken.None));

        using var ok = new FeedbackHttpTransport(
            "https://example.test/feedback", "0.7.3", ApiKey, new StubHandler { Status = HttpStatusCode.OK });
        Assert.False(await ok.SendAsync("{}", CancellationToken.None));

        using var content = new FeedbackHttpTransport(
            "https://example.test/feedback", "0.7.3", ApiKey,
            new StubHandler { Status = HttpStatusCode.NoContent, ResponseBody = "body" });
        Assert.False(await content.SendAsync("{}", CancellationToken.None));

        var service = new FeedbackService(
            new FeedbackHttpTransport("https://example.test/feedback", "0.7.3", ApiKey, new StubHandler { Throw = true }),
            "0.7.3", "8", "macOS");
        var result = service.Send(new FeedbackRequest("x", "other", null));
        Assert.False(result.Sent);
    }

    [Fact]
    public void Http_Transport_Validates_Endpoint_And_Key()
    {
        Assert.Throws<ArgumentException>(() => new FeedbackHttpTransport("ftp://example.test/feedback", "0.7.3", ApiKey));
        Assert.Throws<ArgumentException>(() => new FeedbackHttpTransport("not a url", "0.7.3", ApiKey));
        Assert.Throws<ArgumentException>(() => new FeedbackHttpTransport("https://example.test/feedback", "0.7.3", " "));
        Assert.Throws<ArgumentException>(() => new FeedbackHttpTransport("https://example.test/feedback", "0.7.3", "a\nb"));
        Assert.Throws<ArgumentException>(() =>
            new FeedbackHttpTransport($"https://example.test/feedback/{ApiKey}", "0.7.3", ApiKey));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public HttpStatusCode Status { get; init; } = HttpStatusCode.NoContent;

        public string ResponseBody { get; init; } = "";

        public bool Throw { get; init; }

        public HttpMethod? Method { get; private set; }

        public Uri? Uri { get; private set; }

        public string? UserAgent { get; private set; }

        public string? ContentType { get; private set; }

        public string? Body { get; private set; }

        public IReadOnlyList<string> ApiKeyValues { get; private set; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Throw)
                throw new HttpRequestException("network down");
            Method = request.Method;
            Uri = request.RequestUri;
            UserAgent = request.Headers.UserAgent.ToString();
            ContentType = request.Content?.Headers.ContentType?.ToString();
            ApiKeyValues = request.Headers.TryGetValues(TelemetryEndpoint.ApiKeyHeader, out var values)
                ? values.ToArray()
                : [];
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(Status) { Content = new StringContent(ResponseBody) };
        }
    }
}

using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using MCP4Rhino.Contracts;
using MCP4Rhino.Logic;
using MCP4Rhino.Logic.Protocol;
using MCP4Rhino.Logic.Telemetry;
using Xunit;

namespace MCP4Rhino.Tests;

public class TelemetryDiagnosticsTests
{
    public static TheoryData<Exception, string, string> Exceptions => new()
    {
        { new ArgumentException("private"), "INVALID_ARGUMENT", "ArgumentException" },
        { new ArgumentNullException("private"), "INVALID_ARGUMENT", "ArgumentNullException" },
        { new ArgumentOutOfRangeException("private"), "INVALID_ARGUMENT", "ArgumentOutOfRangeException" },
        { new InvalidOperationException("private"), "INVALID_STATE", "InvalidOperationException" },
        { new NotSupportedException("private"), "NOT_SUPPORTED", "NotSupportedException" },
        { new TimeoutException("private"), "TIMEOUT", "TimeoutException" },
        { new OperationCanceledException("private"), "CANCELLED", "OperationCanceledException" },
        { new TaskCanceledException("private"), "CANCELLED", "TaskCanceledException" },
        { new IOException("private", new Exception("inner-private")), "UNHANDLED_EXCEPTION", "Other" },
        { new CustomArgumentException(), "INVALID_ARGUMENT", "Other" },
        { new CustomTimeoutException(), "TIMEOUT", "Other" },
    };

    [Theory]
    [MemberData(nameof(Exceptions))]
    public async Task Exceptions_Are_Classified_Without_Exception_Text(Exception exception, string code, string type)
    {
        using var capture = new Capture();
        var response = McpJsonRpc.Dispatch(Request(), new Tools(() => throw exception), telemetry: capture.Service);
        Assert.Contains("private", response!["error"]!["message"]!.GetValue<string>());
        var evt = await capture.Flush();
        Assert.False(evt.Success);
        Assert.Equal(code, evt.ErrorCode);
        Assert.Equal(type, evt.ErrorType);
        Assert.Null(evt.ErrorStage);
        Assert.DoesNotContain("private", TelemetryEvent.SerializeBatch([evt]));
    }

    [Fact]
    public async Task Known_Dispatch_Failure_Is_Distinguished_From_Returned_Tool_Error()
    {
        using var capture = new Capture();
        McpJsonRpc.Dispatch(Request("mcp4rhino_reload"), new Tools(() => throw new Exception()), telemetry: capture.Service);
        var dispatch = await capture.Flush();
        Assert.Equal("INVALID_STATE", dispatch.ErrorCode);
        Assert.Equal("DISPATCH", dispatch.ErrorStage);
        McpJsonRpc.Dispatch(Request("get_support_info"), new Tools(() => throw new Exception()),
            supportContext: () => throw new TimeoutException(), telemetry: capture.Service);
        Assert.Equal("EXECUTION", (await capture.Flush()).ErrorStage);
        McpJsonRpc.Dispatch(Request(), new Tools(() => new { isError = true }), telemetry: capture.Service);
        var result = await capture.Flush();
        Assert.Equal("TOOL_ERROR", result.ErrorCode);
        Assert.Null(result.ErrorType);
        Assert.Null(result.ErrorStage);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Generic_Command_Uses_Ok_Without_Changing_Mcp_Response(bool ok)
    {
        using var capture = new Capture();
        var result = McpContent.TextOnly(JsonSerializer.Serialize(new { ok, script = "private-script", object_count_before = 1, object_count_after = 1 }));
        var tools = new Tools(() => new ToolCallResult(result, new(RhinoCommandName: "Box")));
        var response = McpJsonRpc.Dispatch(Request("run_rhino_command"), tools, telemetry: capture.Service);
        Assert.Equal(JsonSerializer.SerializeToNode(result)!.ToJsonString(), response!["result"]!.ToJsonString());
        var evt = await capture.Flush();
        Assert.Equal(ok, evt.Success);
        Assert.Equal("Box", evt.RhinoCommandName);
        Assert.Equal(ok ? null : "RHINO_COMMAND_FAILED", evt.ErrorCode);
        Assert.Equal(ok ? null : "RHINO_COMMAND", evt.ErrorStage);
        Assert.Null(evt.ErrorType);
        Assert.DoesNotContain("private", TelemetryEvent.SerializeBatch([evt]));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"ok\":\"false\"}")]
    [InlineData("{\"ok\":null}")]
    [InlineData("private-invalid-json")]
    [InlineData("[]")]
    public async Task Missing_Or_Malformed_Ok_Does_Not_Infer_Failure(string text)
    {
        using var capture = new Capture();
        McpJsonRpc.Dispatch(Request("run_rhino_command"), new Tools(() => McpContent.TextOnly(text)), telemetry: capture.Service);
        var evt = await capture.Flush();
        Assert.True(evt.Success);
        Assert.Null(evt.ErrorCode);
        Assert.Null(evt.ErrorStage);
    }

    [Theory]
    [InlineData("private-invalid-json")]
    [InlineData("{}")]
    public async Task Malformed_Command_Content_Preserves_Existing_Failure(string text)
    {
        using var capture = new Capture();
        var result = new { isError = true, content = new[] { new { type = "text", text } } };
        var response = McpJsonRpc.Dispatch(Request("run_rhino_command"), new Tools(() => result), telemetry: capture.Service);
        Assert.Null(response!["error"]);
        Assert.False((await capture.Flush()).Success);
    }

    [Fact]
    public async Task Other_Tools_Do_Not_Interpret_Ok_Or_Copy_Command_Names()
    {
        using var capture = new Capture();
        var result = new ToolCallResult(McpContent.TextOnly("{\"ok\":false}"), new(RhinoCommandName: "Box"));
        McpJsonRpc.Dispatch(Request(), new Tools(() => result), telemetry: capture.Service);
        var evt = await capture.Flush();
        Assert.True(evt.Success);
        Assert.Null(evt.RhinoCommandName);
        Assert.Null(evt.ErrorCode);
    }

    [Fact]
    public async Task JsonObject_Results_And_Modern_Protocol_Keep_Correct_Outcomes()
    {
        using var capture = new Capture();
        var content = JsonNode.Parse("""{"isError":false,"content":[{"type":"text","text":"{\"ok\":false}"}]}""")!;
        var services = new McpServerServices(new Tools(() => new ToolCallResult(content, new(RhinoCommandName: "Box"))))
            { Telemetry = capture.Service };
        var response = McpDispatcher.Dispatch(Request("run_rhino_command"), services, new("2026-07-28", new()));
        Assert.False(response!["result"]!["isError"]!.GetValue<bool>());
        Assert.False((await capture.Flush()).Success);
    }

    [Fact]
    public async Task Legacy_Callers_Omit_Diagnostics_And_Lifecycle_Events_Stay_Unchanged()
    {
        using var capture = new Capture();
        capture.Service.TrackTool("create_box", 1, false);
        var json = JsonNode.Parse(TelemetryEvent.SerializeBatch([await capture.Flush()]))!["events"]![0]!.AsObject();
        Assert.Equal(9, json.Count);
        Assert.DoesNotContain(json, pair => pair.Value is null);
        capture.Service.Track(TelemetryEvents.PluginStarted);
        json = JsonNode.Parse(TelemetryEvent.SerializeBatch([await capture.Flush()]))!["events"]![0]!.AsObject();
        Assert.Equal(6, json.Count);
    }

    [Fact]
    public async Task Applicability_And_Invalid_Enums_Cannot_Poison_The_Batch()
    {
        using var capture = new Capture();
        capture.Service.TrackTool("run_rhino_command", 3, true,
            new(ToolErrorCode.Timeout, ToolErrorType.TimeoutException, ToolErrorStage.Execution, "Box"));
        var success = await capture.Flush();
        Assert.Null(success.ErrorCode);
        Assert.Null(success.ErrorType);
        Assert.Null(success.ErrorStage);
        Assert.Equal("Box", success.RhinoCommandName);
        foreach (var invalid in new ToolDiagnostics[]
        {
            new((ToolErrorCode)999), new(ToolErrorCode.Timeout, (ToolErrorType)999),
            new(ToolErrorCode.Timeout, ErrorStage: (ToolErrorStage)999),
            new(ErrorType: ToolErrorType.Other), new(ErrorStage: ToolErrorStage.Execution),
        })
        {
            capture.Service.TrackTool("run_rhino_command", 3, false, invalid);
            var evt = await capture.Flush();
            Assert.False(evt.Success);
            Assert.Null(evt.ErrorCode);
            Assert.Null(evt.ErrorType);
            Assert.Null(evt.ErrorStage);
        }
        capture.Service.TrackTool("run_rhino_command", 1, false, new(ToolErrorCode.ToolError, RhinoCommandName: "private/path.3dm"));
        var badName = await capture.Flush();
        Assert.Null(badName.RhinoCommandName);
        Assert.Equal("TOOL_ERROR", badName.ErrorCode);
    }

    [Theory]
    [InlineData(ToolErrorCode.NoActiveDocument, "NO_ACTIVE_DOCUMENT")]
    [InlineData(ToolErrorCode.ViewNotFound, "VIEW_NOT_FOUND")]
    [InlineData(ToolErrorCode.ObjectNotFound, "OBJECT_NOT_FOUND")]
    [InlineData(ToolErrorCode.InvalidGeometry, "INVALID_GEOMETRY")]
    public async Task Explicit_Structured_Diagnostics_Use_Approved_Wire_Values(ToolErrorCode code, string wire)
    {
        using var capture = new Capture();
        var result = new ToolCallResult(new { isError = true }, new(code));
        McpJsonRpc.Dispatch(Request(), new Tools(() => result), telemetry: capture.Service);
        var evt = await capture.Flush();
        Assert.Equal(wire, evt.ErrorCode);
        Assert.Null(evt.ErrorType);
        Assert.Null(evt.ErrorStage);
    }

    [Fact]
    public void Throwing_Diagnostics_Getter_And_Legacy_Telemetry_Do_Not_Change_Response()
    {
        var telemetry = new LegacyTelemetry();
        var result = McpContent.TextOnly("private-invalid-json");
        var response = McpJsonRpc.Dispatch(Request("run_rhino_command"), new Tools(() => result), telemetry: telemetry);
        Assert.Equal(JsonSerializer.SerializeToNode(result)!.ToJsonString(), response!["result"]!.ToJsonString());
        Assert.True(telemetry.Success);
        response = McpJsonRpc.Dispatch(Request("run_rhino_command"), new Tools(() => new ThrowingContent()), telemetry: telemetry);
        Assert.Null(response!["error"]);
        Assert.False(telemetry.Success);
    }

    [Fact]
    public async Task Transport_Is_Background_And_Contains_Only_Controlled_Diagnostics()
    {
        var handler = new Handler();
        using var capture = new Capture(new TelemetryHttpTransport("http://localhost/telemetry", "0.7.1", handler, "test-token"));
        var result = new ToolCallResult(McpContent.TextOnly("""{"ok":false,"script":"private-file.3dm"}"""), new(RhinoCommandName: "Export"));
        McpJsonRpc.Dispatch(Request("run_rhino_command"), new Tools(() => result), telemetry: capture.Service);
        Assert.Null(handler.Body);
        capture.Service.NotifyFlushInterval();
        await capture.Service.WaitForFlushAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Contains("RHINO_COMMAND_FAILED", handler.Body);
        Assert.Contains("Export", handler.Body);
        Assert.DoesNotContain("private", handler.Body);
        Assert.DoesNotContain("script", handler.Body);
        Assert.Equal(["test-token"], handler.Keys);
    }

    [Theory]
    [InlineData(204, "", true)]
    [InlineData(204, "unexpected", false)]
    [InlineData(200, "", false)]
    [InlineData(500, "", false)]
    public async Task Transport_Requires_Empty_204(int status, string body, bool expected)
    {
        using var transport = new TelemetryHttpTransport("http://localhost/telemetry", "0.7.1", new Handler(status, body));
        Assert.Equal(expected, await transport.SendAsync([], CancellationToken.None));
    }

    private static JsonObject Request(string name = "create_box") => new()
    {
        ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = "tools/call",
        ["params"] = new JsonObject { ["name"] = name, ["arguments"] = new JsonObject { ["script"] = "private-arguments" } },
    };

    private sealed class Tools(Func<object> call) : IMcpToolProvider
    {
        public object[] ListTools() => [];
        public object CallTool(JsonNode? args) => call();
    }

    private sealed class Capture : ITelemetryTransport
    {
        public TelemetryService Service { get; } = new();
        private readonly List<TelemetryEvent> _events = [];
        public Capture(ITelemetryTransport? transport = null) => Service.Configure(new()
        {
            ConsentGranted = true, InstallationId = "11111111-1111-1111-1111-111111111111",
            UseTimer = false, Transport = transport ?? this,
        });
        public async Task<TelemetryEvent> Flush()
        {
            Service.NotifyFlushInterval();
            await Service.WaitForFlushAsync().WaitAsync(TimeSpan.FromSeconds(5));
            var evt = Assert.Single(_events);
            _events.Clear();
            return evt;
        }
        public Task<bool> SendAsync(IReadOnlyList<TelemetryEvent> events, CancellationToken token)
        {
            _events.AddRange(events);
            return Task.FromResult(true);
        }
        public void Dispose() => Service.Dispose();
        void IDisposable.Dispose() { }
    }

    private sealed class Handler(int status = 204, string responseBody = "") : HttpMessageHandler
    {
        public string? Body { get; private set; }
        public string[] Keys { get; private set; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Body = await request.Content!.ReadAsStringAsync(token);
            Keys = request.Headers.TryGetValues("X-Api-Key", out var keys) ? keys.ToArray() : [];
            return new((HttpStatusCode)status) { Content = new StringContent(responseBody) };
        }
    }

    private sealed class LegacyTelemetry : ITelemetry
    {
        public bool Success { get; private set; }
        public void Track(string name) { }
        public void TrackTool(string name, long duration, bool success) => Success = success;
    }
    private sealed class ThrowingContent
    {
        public bool isError => true;
        [System.Text.Json.Serialization.JsonIgnore]
        public object content => throw new InvalidOperationException("private-classifier");
    }
    private sealed class CustomArgumentException() : ArgumentException("private");
    private sealed class CustomTimeoutException() : TimeoutException("private");
}

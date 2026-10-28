using System.Net;
using System.Text.Json.Nodes;
using MCP4Rhino.Logic;
using MCP4Rhino.Logic.Telemetry;
using Xunit;

namespace MCP4Rhino.Tests;

public class TelemetryTests : IDisposable
{
    private const string InstallationId = "11111111-1111-1111-1111-111111111111";
    private static readonly DateTimeOffset FixedClock = new(2026, 10, 20, 14, 30, 0, TimeSpan.Zero);

    public void Dispose()
    {
        Telemetry.Configure(new TelemetryOptions { ConsentGranted = false, UseTimer = false });
        Telemetry.Shutdown();
    }

    [Fact]
    public async Task No_Events_Before_Consent_Or_Without_Url()
    {
        var transport = new RecordingTransport();
        using var denied = CreateService(transport, consent: false);
        denied.Track(TelemetryEvents.PluginStarted);
        denied.TrackTool("create_box", 5, true);
        denied.NotifyFlushInterval();
        await Wait(denied);
        Assert.Equal(0, transport.Calls);
        Assert.Equal(0, denied.QueuedCount);

        using var noUrl = new TelemetryService();
        noUrl.Configure(Options(consent: true, transport: null));
        noUrl.Track(TelemetryEvents.PluginStarted);
        noUrl.TrackTool("create_box", 5, true);
        noUrl.NotifyFlushInterval();
        await Wait(noUrl);
        Assert.Equal(0, transport.Calls);
        Assert.Equal(0, noUrl.QueuedCount);

        var fresh = TelemetryConsent.Load(null, null);
        Assert.True(fresh.Enabled);
        using var sending = CreateService(transport, fresh.Enabled);
        sending.Track(TelemetryEvents.PluginStarted);
        sending.NotifyFlushInterval();
        await Wait(sending);
        Assert.Equal(1, transport.Calls);
    }

    [Fact]
    public void Installation_Id_Is_Stable_Across_Reload_And_Revoke()
    {
        var fresh = TelemetryConsent.Load(null, null);
        Assert.False(fresh.HasChoice);
        Assert.True(fresh.Enabled);
        Assert.Null(fresh.InstallationId);
        Assert.Null(fresh.ConsentValue);

        fresh.Enable();
        Assert.True(fresh.Enabled);
        Assert.Equal(TelemetryConsent.EnabledValue, fresh.ConsentValue);
        var id = fresh.InstallationId;
        Assert.NotNull(id);
        Assert.Equal(id, id!.ToLowerInvariant());
        Assert.Equal(id, Guid.Parse(id).ToString("D"));

        var reloaded = TelemetryConsent.Load(fresh.ConsentValue, fresh.InstallationId);
        Assert.Equal(id, reloaded.InstallationId);
        Assert.True(reloaded.Enabled);

        reloaded.Disable();
        Assert.False(reloaded.Enabled);
        Assert.Equal(TelemetryConsent.DisabledValue, reloaded.ConsentValue);
        Assert.Equal(id, reloaded.InstallationId);

        var again = TelemetryConsent.Load(reloaded.ConsentValue, reloaded.InstallationId);
        again.Enable();
        Assert.Equal(id, again.InstallationId);
        Assert.True(again.Enabled);

        var garbage = TelemetryConsent.Load(TelemetryConsent.EnabledValue, "not-a-uuid");
        Assert.Null(garbage.InstallationId);
        garbage.Enable();
        Assert.NotEqual("not-a-uuid", garbage.InstallationId);
        Assert.True(Guid.TryParse(garbage.InstallationId, out _));

        Assert.Equal("TelemetryConsent", TelemetryConsent.ConsentKey);
        Assert.Equal("TelemetryInstallationId", TelemetryConsent.InstallationIdKey);
    }

    [Fact]
    public async Task Events_Match_Schema_And_Omit_Tool_Fields_Until_Tool_Executed()
    {
        var transport = new RecordingTransport();
        using var service = CreateService(transport);
        service.Track(TelemetryEvents.PluginStarted);
        service.Track(TelemetryEvents.McpConnected);
        service.TrackTool("create_box", 42, true);
        service.TrackTool("create_sphere", 7, false);
        service.NotifyFlushInterval();
        await Wait(service);

        var batch = transport.Batches.Single();
        Assert.Equal(4, batch.Count);

        var started = Json(batch[0]);
        Assert.Equal(InstallationId, started["installationId"]!.GetValue<string>());
        Assert.Equal("plugin_started", started["event"]!.GetValue<string>());
        Assert.Equal("2026-10-20T14:30:00Z", started["timestamp"]!.GetValue<string>());
        Assert.Equal(McpJsonRpc.ServerVersion, started["pluginVersion"]!.GetValue<string>());
        Assert.Equal("8", started["rhinoVersion"]!.GetValue<string>());
        Assert.Equal("macOS", started["os"]!.GetValue<string>());
        AssertKeys(started, CommonEventKeys);

        var connected = Json(batch[1]);
        Assert.Equal("mcp_connected", connected["event"]!.GetValue<string>());
        AssertKeys(connected, CommonEventKeys);

        var ok = Json(batch[2]);
        Assert.Equal("tool_executed", ok["event"]!.GetValue<string>());
        Assert.Equal("create_box", ok["tool"]!.GetValue<string>());
        Assert.Equal(42, ok["durationMs"]!.GetValue<long>());
        Assert.True(ok["success"]!.GetValue<bool>());
        AssertKeys(ok, ToolEventKeys);

        var failed = Json(batch[3]);
        Assert.Equal("create_sphere", failed["tool"]!.GetValue<string>());
        Assert.Equal(7, failed["durationMs"]!.GetValue<long>());
        Assert.False(failed["success"]!.GetValue<bool>());
        AssertKeys(failed, ToolEventKeys);
    }

    [Fact]
    public async Task Queue_Flushes_At_20_And_Timer_Flushes_Remainder()
    {
        var transport = new RecordingTransport();
        using var service = CreateService(transport);

        for (var i = 0; i < 19; i++)
            service.Track(TelemetryEvents.PluginStarted);
        Assert.Equal(0, transport.Calls);
        Assert.Equal(19, service.QueuedCount);

        service.NotifyFlushInterval();
        await Wait(service);
        Assert.Equal(19, transport.Batches.Single().Count);
        Assert.Equal(0, service.QueuedCount);

        for (var i = 0; i < 19; i++)
            service.Track(TelemetryEvents.McpConnected);
        Assert.Equal(1, transport.Calls);
        service.Track(TelemetryEvents.McpConnected);
        await Wait(service);
        Assert.Equal(2, transport.Batches.Count);
        Assert.Equal(20, transport.Batches[1].Count);
        Assert.Equal(0, service.QueuedCount);
    }

    [Fact]
    public async Task Batch_Never_Exceeds_100_And_Drops_When_Full()
    {
        var transport = new RecordingTransport();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var call = 0;
        transport.Handler = async (events, ct) =>
        {
            var n = Interlocked.Increment(ref call);
            lock (transport.Batches)
                transport.Batches.Add(events.ToArray());
            if (n == 1)
            {
                entered.TrySetResult();
                await release.Task.WaitAsync(ct);
            }

            return true;
        };

        using var service = CreateService(transport);
        for (var i = 0; i < 20; i++)
            service.Track(TelemetryEvents.PluginStarted);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        for (var i = 0; i < 105; i++)
            service.TrackTool("create_box", 1, true);

        Assert.Equal(100, service.QueuedCount);
        Assert.Equal(1, transport.Logs.Count(m => m.Contains("queue full", StringComparison.Ordinal)));
        release.TrySetResult();
        await Wait(service);

        Assert.Equal(2, transport.Batches.Count);
        Assert.Equal(20, transport.Batches[0].Count);
        Assert.Equal(100, transport.Batches[1].Count);
        Assert.All(transport.Batches, batch => Assert.True(batch.Count <= TelemetryService.MaxBatchSize));
        Assert.Equal(0, service.QueuedCount);
    }

    [Fact]
    public async Task Delivery_Failure_Discards_Batch_And_Later_Batch_Can_Send()
    {
        var transport = new RecordingTransport();
        var fail = true;
        transport.Handler = (events, _) =>
        {
            lock (transport.Batches)
                transport.Batches.Add(events.ToArray());
            if (fail)
                throw new HttpRequestException("offline");
            return Task.FromResult(true);
        };

        using var service = CreateService(transport);
        for (var i = 0; i < 20; i++)
            service.TrackTool("lost", 1, false);
        await Wait(service);
        Assert.Equal(0, service.QueuedCount);
        Assert.Contains(transport.Logs, m => m.Contains("discarding batch", StringComparison.Ordinal));
        Assert.All(transport.Batches[0], e => Assert.Equal("lost", e.Tool));

        fail = false;
        transport.Handler = (events, _) =>
        {
            lock (transport.Batches)
                transport.Batches.Add(events.ToArray());
            return Task.FromResult(false);
        };
        for (var i = 0; i < 20; i++)
            service.TrackTool("rejected", 2, true);
        await Wait(service);
        Assert.Equal(0, service.QueuedCount);
        Assert.All(transport.Batches[1], e => Assert.Equal("rejected", e.Tool));

        transport.Handler = (events, _) =>
        {
            lock (transport.Batches)
                transport.Batches.Add(events.ToArray());
            return Task.FromResult(true);
        };
        for (var i = 0; i < 20; i++)
            service.TrackTool("kept", 3, true);
        await Wait(service);
        Assert.Equal(20, transport.Batches[2].Count);
        Assert.All(transport.Batches[2], e => Assert.Equal("kept", e.Tool));
        Assert.DoesNotContain(transport.Batches[2], e => e.Tool is "lost" or "rejected");
    }

    [Fact]
    public async Task Revoke_Clears_Queue_Cancels_Send_And_Ignores_Later_Events()
    {
        var transport = new RecordingTransport();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        transport.Handler = async (_, ct) =>
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.Infinite, ct);
            return true;
        };

        using var service = CreateService(transport);
        for (var i = 0; i < 5; i++)
            service.Track(TelemetryEvents.PluginStarted);
        Assert.Equal(5, service.QueuedCount);
        service.Revoke();
        Assert.Equal(0, service.QueuedCount);
        service.Track(TelemetryEvents.PluginStarted);
        Assert.Equal(0, service.QueuedCount);
        Assert.Equal(0, transport.Calls);

        service.Configure(Options(consent: true, transport));
        for (var i = 0; i < 20; i++)
            service.Track(TelemetryEvents.McpConnected);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        service.Revoke();
        await Wait(service);

        Assert.Equal(0, service.QueuedCount);
        service.TrackTool("create_box", 9, true);
        service.NotifyFlushInterval();
        await Wait(service);
        Assert.Equal(0, service.QueuedCount);
        Assert.Equal(1, transport.Calls);
        Assert.Contains(transport.Logs, m => m.Contains("canceled", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Tool_Results_Do_Not_Copy_Arguments_Or_Sensitive_Text()
    {
        var transport = new RecordingTransport();
        using var service = CreateService(transport);
        var tools = new ScriptedTools(
            ("create_box", new
            {
                content = new[] { new { type = "text", text = "result-secret-geometry" } },
                isError = false,
            }),
            ("create_sphere", new
            {
                content = new[] { new { type = "text", text = "result-secret-failure" } },
                isError = true,
            }));

        var ok = McpJsonRpc.Dispatch(Call("create_box", """{"path":"C:/secret-model.3dm","prompt":"secret-prompt-do-not-collect"}"""), tools, telemetry: service);
        var failed = McpJsonRpc.Dispatch(Call("create_sphere", """{"path":"D:/other-secret.3dm","prompt":"another-secret-prompt"}"""), tools, telemetry: service);
        var thrown = McpJsonRpc.Dispatch(Call("explode_objects", """{"path":"E:/throw-secret.3dm"}"""), tools, telemetry: service);
        var support = McpJsonRpc.Dispatch(Call("get_support_info", "{}"), tools, telemetry: service);

        Assert.Null(ok?["error"]);
        Assert.Contains("result-secret-geometry", ok!.ToJsonString());
        Assert.Null(failed?["error"]);
        Assert.Contains("secret-exception-do-not-collect", thrown!["error"]!.ToJsonString());
        Assert.Null(support?["error"]);

        service.NotifyFlushInterval();
        await Wait(service);
        var json = TelemetryEvent.SerializeBatch(transport.Batches.Single());

        Assert.DoesNotContain("secret-model", json);
        Assert.DoesNotContain("secret-prompt", json);
        Assert.DoesNotContain("other-secret", json);
        Assert.DoesNotContain("another-secret-prompt", json);
        Assert.DoesNotContain("throw-secret", json);
        Assert.DoesNotContain("secret-exception-do-not-collect", json);
        Assert.DoesNotContain("result-secret", json);
        Assert.DoesNotContain("mcp4rhino.log", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"path\"", json);
        Assert.DoesNotContain("\"prompt\"", json);
        Assert.Contains("create_box", json);
        Assert.Contains("create_sphere", json);
        Assert.Contains("explode_objects", json);
        Assert.Contains("get_support_info", json);

        var events = transport.Batches.Single();
        Assert.Equal(4, events.Count);
        Assert.True(events[0].Success);
        Assert.False(events[1].Success);
        Assert.False(events[2].Success);
        Assert.True(events[3].Success);
        Assert.All(events, e =>
        {
            Assert.Equal(TelemetryEvents.ToolExecuted, e.EventName);
            var diagnostics = e == events[1] ? new[] { "errorCode" }
                : e == events[2] ? new[] { "errorCode", "errorType" } : [];
            AssertKeys(Json(e), [.. ToolEventKeys, .. diagnostics]);
        });
    }

    [Fact]
    public void Dialog_Dismiss_Leaves_The_Choice_And_Revoke_Keeps_The_Installation_Id()
    {
        var fresh = TelemetryConsent.Load(null, null);
        fresh.Enable();
        var id = fresh.InstallationId;
        Assert.False(string.IsNullOrWhiteSpace(id));

        var dismissed = TelemetryConsentDecision.Apply(fresh, dialogResult: null);
        Assert.False(dismissed.Persist);
        Assert.False(dismissed.RevokeSending);
        Assert.False(dismissed.RecordPluginStarted);
        Assert.True(dismissed.Consent.Enabled);
        Assert.Equal(id, dismissed.Consent.InstallationId);

        var revoked = TelemetryConsentDecision.Apply(fresh, dialogResult: false);
        Assert.True(revoked.Persist);
        Assert.True(revoked.RevokeSending);
        Assert.False(revoked.RecordPluginStarted);
        Assert.False(revoked.Consent.Enabled);
        Assert.Equal(TelemetryConsent.DisabledValue, revoked.Consent.ConsentValue);
        Assert.Equal(id, revoked.Consent.InstallationId);

        var again = TelemetryConsentDecision.Apply(revoked.Consent, dialogResult: true);
        Assert.True(again.Consent.Enabled);
        Assert.False(again.RevokeSending);
        Assert.False(again.RecordPluginStarted);
        Assert.Equal(id, again.Consent.InstallationId);
    }

    [Fact]
    public void Initialize_Does_Not_Mention_Usage_And_Tools_Still_Run()
    {
        Assert.False(TelemetryUsePrompt.IncludeNotice(enabled: true, endpointConfigured: true, noticeShown: false));
        Assert.False(TelemetryUsePrompt.IncludeNotice(enabled: false, endpointConfigured: true, noticeShown: false));
        Assert.False(TelemetryUsePrompt.IncludeNotice(enabled: true, endpointConfigured: false, noticeShown: false));
        Assert.False(TelemetryUsePrompt.IncludeNotice(enabled: true, endpointConfigured: true, noticeShown: true));

        var marked = 0;
        string Instructions() => TelemetryUsePrompt.Instructions(SupportInfo.ServerInstructions, includeNotice: true);

        var first = McpJsonRpc.Dispatch(
            JsonNode.Parse("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}"""),
            new ScriptedTools(),
            instructions: Instructions,
            onInitialized: () => marked++);
        var instructions = first!["result"]!["instructions"]!.GetValue<string>();
        Assert.Equal(SupportInfo.ServerInstructions, instructions);
        Assert.DoesNotContain("usage", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("consent", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("github.com/isque03/mcp4rhino/issues", instructions);
        Assert.Equal(1, marked);

        var second = McpJsonRpc.Dispatch(
            JsonNode.Parse("""{"jsonrpc":"2.0","id":2,"method":"initialize","params":{}}"""),
            new ScriptedTools(),
            instructions: Instructions,
            onInitialized: () => marked++);
        Assert.Equal(SupportInfo.ServerInstructions, second!["result"]!["instructions"]!.GetValue<string>());
        Assert.DoesNotContain("usage", second["result"]!["instructions"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);

        var recorded = new RecordingTelemetry();
        var call = McpJsonRpc.Dispatch(
            Call("create_box", """{"path":"C:/secret-model.3dm","prompt":"secret-prompt-do-not-collect"}"""),
            new ScriptedTools(("create_box", new { content = new[] { new { type = "text", text = "made" } }, isError = false })),
            telemetry: recorded,
            instructions: Instructions);
        Assert.Contains("made", call!.ToJsonString());
        Assert.DoesNotContain("usage", call.ToJsonString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("consent", call.ToJsonString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret-model", call.ToJsonString());
        Assert.DoesNotContain("secret-prompt", call.ToJsonString());
        Assert.Equal(TelemetryEvents.ToolExecuted, Assert.Single(recorded.Events).Event);

        var broken = McpJsonRpc.Dispatch(
            JsonNode.Parse("""{"jsonrpc":"2.0","id":3,"method":"initialize","params":{}}"""),
            new ScriptedTools(),
            instructions: () => throw new InvalidOperationException("notice-failed"));
        Assert.Equal(SupportInfo.ServerInstructions, broken!["result"]!["instructions"]!.GetValue<string>());
        Assert.DoesNotContain("notice-failed", broken.ToJsonString());
    }

    [Fact]
    public void Dispatch_Tracks_Session_And_Swallow_Telemetry_Failures()
    {
        var recorded = new RecordingTelemetry();
        var init = McpJsonRpc.Dispatch(
            JsonNode.Parse("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}"""),
            new ScriptedTools(),
            telemetry: recorded);
        Assert.NotNull(init?["result"]);
        Assert.Equal(TelemetryEvents.McpConnected, Assert.Single(recorded.Events).Event);

        recorded.Events.Clear();
        var missing = McpJsonRpc.Dispatch(
            JsonNode.Parse("""{"jsonrpc":"2.0","id":2,"method":"nope"}"""),
            new ScriptedTools(),
            telemetry: recorded);
        Assert.NotNull(missing?["error"]);
        Assert.Empty(recorded.Events);

        var boom = new ThrowingTelemetry();
        var still = McpJsonRpc.Dispatch(
            JsonNode.Parse("""{"jsonrpc":"2.0","id":3,"method":"initialize","params":{}}"""),
            new ScriptedTools(),
            telemetry: boom);
        Assert.Equal(McpJsonRpc.ProtocolVersion, still!["result"]!["protocolVersion"]!.GetValue<string>());

        var tool = McpJsonRpc.Dispatch(
            Call("create_box", """{"prompt":"secret-prompt-do-not-collect"}"""),
            new ScriptedTools(("create_box", new { content = Array.Empty<object>(), isError = false })),
            telemetry: boom);
        Assert.Null(tool?["error"]);
        Assert.DoesNotContain("secret-prompt", tool!.ToJsonString());
    }

    [Fact]
    public async Task Static_Facade_Respects_Consent_And_Revoke()
    {
        var service = (TelemetryService)Telemetry.Gateway;
        Telemetry.Track(TelemetryEvents.PluginStarted);
        Telemetry.TrackTool("create_box", 1, true);
        Assert.Equal(0, service.QueuedCount);

        var transport = new RecordingTransport();
        Telemetry.Configure(Options(consent: true, transport));
        Telemetry.Track(TelemetryEvents.PluginStarted);
        Telemetry.TrackTool("create_box", 4, true);
        Telemetry.TrackTool("create_box", 2, false, new(MCP4Rhino.Contracts.ToolErrorCode.Timeout,
            MCP4Rhino.Contracts.ToolErrorType.TimeoutException, MCP4Rhino.Contracts.ToolErrorStage.Execution));
        Assert.Equal(3, service.QueuedCount);
        service.NotifyFlushInterval();
        await Wait(service);
        Assert.Equal(3, transport.Batches.Single().Count);
        var failed = transport.Batches.Single()[2];
        Assert.False(failed.Success);
        Assert.Equal("TIMEOUT", failed.ErrorCode);
        Assert.Equal("TimeoutException", failed.ErrorType);
        Assert.Equal("EXECUTION", failed.ErrorStage);

        Telemetry.Revoke();
        Assert.Equal(0, service.QueuedCount);
        Telemetry.Track(TelemetryEvents.McpConnected);
        Assert.Equal(0, service.QueuedCount);
        Assert.Equal(1, transport.Calls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a url")]
    [InlineData("/telemetry")]
    [InlineData("ftp://example.com/telemetry")]
    public void Endpoint_Rejects_Empty_Or_Non_Http(string raw) =>
        Assert.Null(TelemetryEndpoint.Resolve(raw));

    [Theory]
    [InlineData("http://127.0.0.1:8080", "http://127.0.0.1:8080/telemetry")]
    [InlineData("http://127.0.0.1:8080/", "http://127.0.0.1:8080/telemetry")]
    [InlineData("https://example.com/v1", "https://example.com/v1/telemetry")]
    [InlineData("http://127.0.0.1:8080/telemetry", "http://127.0.0.1:8080/telemetry")]
    [InlineData("http://127.0.0.1:8080/telemetry/", "http://127.0.0.1:8080/telemetry")]
    [InlineData("https://example.com/Telemetry", "https://example.com/Telemetry")]
    [InlineData("http://127.0.0.1:8080/telemetry?token=abc", "http://127.0.0.1:8080/telemetry?token=abc")]
    [InlineData("http://127.0.0.1:8080?token=abc", "http://127.0.0.1:8080/telemetry?token=abc")]
    [InlineData("https://example.com/v1?x=1&y=2", "https://example.com/v1/telemetry?x=1&y=2")]
    [InlineData("https://example.com/telemetry?k=test-api-key", "https://example.com/telemetry")]
    [InlineData("https://example.com/telemetry?k=test-api-key&x=1", "https://example.com/telemetry?x=1")]
    [InlineData("https://example.com/v1?x=1&k=test-api-key&y=2", "https://example.com/v1/telemetry?x=1&y=2")]
    public void Endpoint_Appends_Telemetry_Path_Once(string raw, string expected)
    {
        var resolved = TelemetryEndpoint.Resolve(raw);
        Assert.Equal(expected, resolved);
        Assert.DoesNotContain("test-api-key", resolved, StringComparison.Ordinal);
    }

    [Fact]
    public void Endpoint_Reads_Environment_When_Argument_Omitted()
    {
        var previous = Environment.GetEnvironmentVariable(TelemetryEndpoint.EnvVar);
        try
        {
            Environment.SetEnvironmentVariable(TelemetryEndpoint.EnvVar, "http://127.0.0.1:8099/telemetry");
            Assert.Equal("http://127.0.0.1:8099/telemetry", TelemetryEndpoint.Resolve());
            Assert.Equal("MCP4RHINO_TELEMETRY_URL", TelemetryEndpoint.EnvVar);
        }
        finally
        {
            Environment.SetEnvironmentVariable(TelemetryEndpoint.EnvVar, previous);
        }
    }

    [Fact]
    public void Endpoint_Uses_Production_Url_When_Override_Is_Unset()
    {
        var previous = Environment.GetEnvironmentVariable(TelemetryEndpoint.EnvVar);
        try
        {
            Environment.SetEnvironmentVariable(TelemetryEndpoint.EnvVar, null);
            Assert.Equal(
                "https://events.agenttoolkit.ai/telemetry",
                TelemetryEndpoint.DefaultUrl);
            Assert.Equal(TelemetryEndpoint.DefaultUrl, TelemetryEndpoint.Resolve());
            Assert.DoesNotContain("k=", TelemetryEndpoint.DefaultUrl, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(TelemetryEndpoint.EnvVar, previous);
        }
    }

    [Fact]
    public void Endpoint_Invalid_Override_Does_Not_Use_Default()
    {
        var previous = Environment.GetEnvironmentVariable(TelemetryEndpoint.EnvVar);
        try
        {
            Environment.SetEnvironmentVariable(TelemetryEndpoint.EnvVar, "not a url");
            Assert.Null(TelemetryEndpoint.Resolve());
        }
        finally
        {
            Environment.SetEnvironmentVariable(TelemetryEndpoint.EnvVar, previous);
        }
    }

    [Fact]
    public void ApiKey_Comes_From_Environment_Not_From_Url()
    {
        var previous = Environment.GetEnvironmentVariable(TelemetryEndpoint.ApiKeyEnvVar);
        try
        {
            Environment.SetEnvironmentVariable(TelemetryEndpoint.ApiKeyEnvVar, "  test-api-key  ");
            Assert.Equal("MCP4RHINO_TELEMETRY_API_KEY", TelemetryEndpoint.ApiKeyEnvVar);
            Assert.Equal("X-Api-Key", TelemetryEndpoint.ApiKeyHeader);
            Assert.Equal("test-api-key", TelemetryEndpoint.ApiKey());
            var resolved = TelemetryEndpoint.Resolve("https://example.com/telemetry?k=query-secret-do-not-send");
            Assert.Equal("https://example.com/telemetry", resolved);
            Assert.DoesNotContain("test-api-key", resolved, StringComparison.Ordinal);
            Assert.DoesNotContain("query-secret-do-not-send", resolved, StringComparison.Ordinal);
            Assert.Null(TelemetryEndpoint.ApiKey(" \r\n "));
        }
        finally
        {
            Environment.SetEnvironmentVariable(TelemetryEndpoint.ApiKeyEnvVar, previous);
        }
    }

    [Fact]
    public void ApiKey_Uses_Packaged_Token_Unless_Environment_Is_Set()
    {
        var previous = Environment.GetEnvironmentVariable(TelemetryEndpoint.ApiKeyEnvVar);
        var directory = Path.Combine(Path.GetTempPath(), "mcp4rhino-token-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, TelemetryEndpoint.InstallTokenFileName), "  packaged-token\n");
            Assert.Equal("telemetry-install-token", TelemetryEndpoint.InstallTokenFileName);
            var packaged = TelemetryEndpoint.ReadInstallToken(directory);
            Environment.SetEnvironmentVariable(TelemetryEndpoint.ApiKeyEnvVar, null);
            Assert.Equal("packaged-token", TelemetryEndpoint.ApiKey(packaged: packaged));
            Assert.Null(TelemetryEndpoint.ApiKey(packaged: "pack\ntoken"));
            Assert.Null(TelemetryEndpoint.ReadInstallToken(Path.Combine(directory, "missing")));
            Environment.SetEnvironmentVariable(TelemetryEndpoint.ApiKeyEnvVar, "env-token");
            Assert.Equal("env-token", TelemetryEndpoint.ApiKey(packaged: packaged));
        }
        finally
        {
            Environment.SetEnvironmentVariable(TelemetryEndpoint.ApiKeyEnvVar, previous);
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Http_Transport_Posts_Batch_And_Treats_Only_204_As_Success()
    {
        Assert.Equal(TimeSpan.FromSeconds(3), TelemetryHttpTransport.DefaultTimeout);
        Assert.Equal(TimeSpan.FromSeconds(30), TelemetryService.FlushInterval);
        Assert.Equal(20, TelemetryService.FlushThreshold);
        Assert.Equal(100, TelemetryService.QueueCapacity);

        var events = new[]
        {
            new TelemetryEvent
            {
                InstallationId = InstallationId,
                EventName = TelemetryEvents.PluginStarted,
                Timestamp = "2026-10-20T14:30:00Z",
                PluginVersion = McpJsonRpc.ServerVersion,
                RhinoVersion = "8",
                Os = "macOS",
            },
        };

        const string apiKey = "test-api-key";
        const string leaked = "query-secret-do-not-send";
        var ok = new StubHandler { Status = HttpStatusCode.NoContent };
        using (var transport = new TelemetryHttpTransport(
            "http://127.0.0.1:8080/telemetry?k=" + leaked,
            McpJsonRpc.ServerVersion,
            ok,
            apiKey))
        {
            Assert.Equal(TimeSpan.FromSeconds(3), transport.Timeout);
            Assert.True(await transport.SendAsync(events, CancellationToken.None));
        }

        Assert.Equal(HttpMethod.Post, ok.Method);
        Assert.Equal("http://127.0.0.1:8080/telemetry", ok.Uri?.ToString());
        Assert.DoesNotContain(apiKey, ok.Uri?.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(leaked, ok.Uri?.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("k=", ok.Uri?.Query, StringComparison.OrdinalIgnoreCase);
        Assert.Equal([apiKey], ok.ApiKeyValues);
        Assert.Empty(ok.ContentApiKeyValues);
        Assert.Contains($"MCP4Rhino/{McpJsonRpc.ServerVersion}", ok.UserAgent, StringComparison.Ordinal);
        Assert.Contains("application/json", ok.ContentType, StringComparison.Ordinal);
        Assert.Contains("\"events\"", ok.Body);
        Assert.DoesNotContain("tool", ok.Body);
        Assert.DoesNotContain(apiKey, ok.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(leaked, ok.Body, StringComparison.Ordinal);

        var denied = new StubHandler { Status = HttpStatusCode.InternalServerError };
        using var failing = new TelemetryHttpTransport("http://127.0.0.1:8080/telemetry", McpJsonRpc.ServerVersion, denied);
        Assert.False(await failing.SendAsync(events, CancellationToken.None));
        Assert.Empty(denied.ApiKeyValues);

        using var canceled = new TelemetryHttpTransport("http://127.0.0.1:8080/telemetry", McpJsonRpc.ServerVersion, new StubHandler());
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceled.SendAsync(events, cts.Token));

        Assert.Throws<ArgumentException>(() => new TelemetryHttpTransport("not-a-uri", "0.6.0-alpha"));
        var leakedInPath = Assert.Throws<ArgumentException>(() => new TelemetryHttpTransport(
            "http://127.0.0.1:8080/" + apiKey + "/telemetry",
            McpJsonRpc.ServerVersion,
            apiKey: apiKey));
        Assert.DoesNotContain(apiKey, leakedInPath.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Current_Os_Is_A_Coarse_Family_Name()
    {
        var os = TelemetryEnvironment.CurrentOs();
        Assert.Contains(os, new[] { "macOS", "Windows", "Linux" });
        if (OperatingSystem.IsMacOS()) Assert.Equal("macOS", os);
        else if (OperatingSystem.IsWindows()) Assert.Equal("Windows", os);
        else Assert.Equal("Linux", os);
    }

    [Fact]
    public void Clock_Failure_Does_Not_Escape_Track()
    {
        var transport = new RecordingTransport();
        using var service = CreateService(transport);
        service.Configure(Options(consent: true, transport, clock: () => throw new InvalidOperationException("clock")));
        var ex = Record.Exception(() => service.Track(TelemetryEvents.PluginStarted));
        Assert.Null(ex);
        Assert.Equal(0, service.QueuedCount);
        Assert.Contains(transport.Logs, m => m.Contains("track failed", StringComparison.Ordinal));
    }

    private static TelemetryService CreateService(RecordingTransport transport, bool consent = true)
    {
        var service = new TelemetryService();
        service.Configure(Options(consent, transport));
        return service;
    }

    private static TelemetryOptions Options(
        bool consent,
        ITelemetryTransport? transport,
        Func<DateTimeOffset>? clock = null) => new()
    {
        ConsentGranted = consent,
        InstallationId = InstallationId,
        PluginVersion = McpJsonRpc.ServerVersion,
        RhinoVersion = "8",
        Os = "macOS",
        Transport = transport,
        UtcNow = clock ?? (() => FixedClock),
        Log = message =>
        {
            if (transport is RecordingTransport recording)
                recording.Log(message);
        },
        UseTimer = false,
    };

    private static async Task Wait(TelemetryService service) =>
        await service.WaitForFlushAsync().WaitAsync(TimeSpan.FromSeconds(5));

    private static readonly string[] CommonEventKeys =
        ["installationId", "event", "timestamp", "pluginVersion", "rhinoVersion", "os"];

    private static readonly string[] ToolEventKeys =
        ["installationId", "event", "timestamp", "pluginVersion", "rhinoVersion", "os", "tool", "durationMs", "success"];

    private static void AssertKeys(JsonObject obj, string[] keys) =>
        Assert.Equal(new HashSet<string>(keys), obj.Select(p => p.Key).ToHashSet());

    private static JsonObject Json(TelemetryEvent evt)
    {
        var batch = JsonNode.Parse(TelemetryEvent.SerializeBatch([evt]))!;
        return batch["events"]![0]!.AsObject();
    }

    private static JsonObject Call(string name, string arguments)
    {
        var json = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/call\",\"params\":{\"name\":\""
            + name + "\",\"arguments\":" + arguments + "}}";
        return JsonNode.Parse(json)!.AsObject();
    }

    private sealed class RecordingTransport : ITelemetryTransport
    {
        private readonly object _gate = new();
        private int _calls;

        public List<IReadOnlyList<TelemetryEvent>> Batches { get; } = [];

        public List<string> Logs { get; } = [];

        public Func<IReadOnlyList<TelemetryEvent>, CancellationToken, Task<bool>>? Handler { get; set; }

        public int Calls => _calls;

        public void Log(string message)
        {
            lock (_gate)
                Logs.Add(message);
        }

        public Task<bool> SendAsync(IReadOnlyList<TelemetryEvent> events, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            if (Handler is not null)
                return Handler(events, cancellationToken);
            lock (_gate)
                Batches.Add(events.ToArray());
            return Task.FromResult(true);
        }

        public void Dispose()
        {
        }
    }

    private sealed class RecordingTelemetry : ITelemetry
    {
        public List<(string Event, string? Tool, long? Duration, bool? Success)> Events { get; } = [];

        public void Track(string eventName) => Events.Add((eventName, null, null, null));

        public void TrackTool(string toolName, long durationMs, bool success) =>
            Events.Add((TelemetryEvents.ToolExecuted, toolName, durationMs, success));
    }

    private sealed class ThrowingTelemetry : ITelemetry
    {
        public void Track(string eventName) => throw new InvalidOperationException("telemetry down");

        public void TrackTool(string toolName, long durationMs, bool success) =>
            throw new InvalidOperationException("telemetry down");
    }

    private sealed class ScriptedTools : IMcpToolProvider
    {
        private readonly Dictionary<string, object> _results;

        public ScriptedTools(params (string Name, object Result)[] results)
        {
            _results = results.ToDictionary(r => r.Name, r => r.Result);
        }

        public object[] ListTools() => [];

        public object CallTool(JsonNode? @params)
        {
            var name = @params?["name"]?.GetValue<string>() ?? "";
            if (_results.TryGetValue(name, out var result))
                return result;
            var args = @params?["arguments"]?.ToJsonString() ?? "";
            throw new InvalidOperationException($"unknown tool {name} secret-exception-do-not-collect {args}");
        }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public HttpStatusCode Status { get; init; } = HttpStatusCode.NoContent;

        public HttpMethod? Method { get; private set; }

        public Uri? Uri { get; private set; }

        public string? UserAgent { get; private set; }

        public string? ContentType { get; private set; }

        public string? Body { get; private set; }

        public IReadOnlyList<string> ApiKeyValues { get; private set; } = [];

        public IReadOnlyList<string> ContentApiKeyValues { get; private set; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Method = request.Method;
            Uri = request.RequestUri;
            UserAgent = request.Headers.UserAgent.ToString();
            ContentType = request.Content?.Headers.ContentType?.ToString();
            ApiKeyValues = request.Headers.TryGetValues(TelemetryEndpoint.ApiKeyHeader, out var values)
                ? values.ToArray()
                : [];
            ContentApiKeyValues = request.Content?.Headers.TryGetValues(TelemetryEndpoint.ApiKeyHeader, out var contentValues) == true
                ? contentValues.ToArray()
                : [];
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(Status);
        }
    }
}

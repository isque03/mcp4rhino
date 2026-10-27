namespace MCP4Rhino.Logic;

/// <summary>Shared URLs and payloads for GitHub Issues / MCP support tooling.</summary>
public static class SupportInfo
{
    /// <summary>Design rule: default listen port when <c>MCP4RHINO_PORT</c> is unset or invalid (matches host).</summary>
    public const int DefaultPort = 4010;

    public const string RepositoryUrl = "https://github.com/isque03/mcp4rhino";
    public const string IssuesUrl = "https://github.com/isque03/mcp4rhino/issues";
    public const string NewIssueUrl = "https://github.com/isque03/mcp4rhino/issues/new";
    public const string NewBugUrl = "https://github.com/isque03/mcp4rhino/issues/new?labels=bug";
    public const string NewEnhancementUrl = "https://github.com/isque03/mcp4rhino/issues/new?labels=enhancement";

    public static string ServerInstructions { get; } =
        "MCP4Rhino drives the active Rhino 8 document for AI assistants. Prefer the smallest lane that matches the ask. " +
        "New users: use getting_started or mcp4rhino://server/getting-started for a first Rhino session; ask before creating geometry. " +
        "Name Grasshopper as a lane in early onboarding: editable parametric graphs via grasshopper_getting_started or mcp4rhino://server/grasshopper-getting-started—not for ordinary one-off Rhino edits. " +
        "Track completed tutorials in the workspace file .mcp4rhino/tutorial-memory.md; read it before re-offering a walkthrough and update it when a tutorial finishes. " +
        "Orient with mcp4rhino://rhino/documents/active or get_document_info / get_document_units; lengths use document model units—call convert_length before create_* when the user speaks other units. " +
        "Direct modeling covers geometry, architecture (levels/walls/spaces/…), drawings/docs, views/capture, clash/QTO/export, and code-check findings—discover via tools/list or mcp4rhino://server/capabilities lanes. " +
        "Parametric work: after the Grasshopper tutorial or gh_help(topic: index) / mcp4rhino://grasshopper/guides/index, discover components and ports before patches. " +
        "Optional workflows: prompts/list. Inspect-only: inspect_model. Reading resources or prompts never mutates the model or initializes Grasshopper. " +
        "When writing to the user in English, use short clear sentences and one term per idea (ASD-STE100 style, not full compliance); see mcp4rhino://server/writing-style. In other languages, keep the same clarity: short sentences and consistent terms. " +
        "File bugs at https://github.com/isque03/mcp4rhino/issues. Call get_support_info first. " +
        "Include MCP4Rhino/Rhino/OS versions, repro, expected vs actual, and relevant log lines. Do not paste secrets or full license keys.";

    public static int ResolvePort()
    {
        var env = Environment.GetEnvironmentVariable("MCP4RHINO_PORT");
        if (!string.IsNullOrWhiteSpace(env) && int.TryParse(env, out var parsed) && parsed is > 0 and < 65536)
            return parsed;
        return DefaultPort;
    }

    /// <summary>Configured endpoint from env/default port (not necessarily listening).</summary>
    public static string ConfiguredEndpoint() => $"http://127.0.0.1:{ResolvePort()}/mcp";

    public static string DefaultLogPath()
    {
        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Logs", "MCP4Rhino", "mcp4rhino.log");
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MCP4Rhino", "mcp4rhino.log");
    }

    public static object BuildPayload(
        string? endpoint = null,
        string? logPath = null,
        bool? serverRunning = null) => new
    {
        package = McpJsonRpc.ServerName,
        server_version = McpJsonRpc.ServerVersion,
        repository = RepositoryUrl,
        endpoint = endpoint ?? ConfiguredEndpoint(),
        server_running = serverRunning,
        issues_url = IssuesUrl,
        new_issue_url = NewIssueUrl,
        new_bug_url = NewBugUrl,
        new_enhancement_url = NewEnhancementUrl,
        log_path = logPath ?? DefaultLogPath(),
        filing_tips = new[]
        {
            "Call get_support_info and paste its JSON into the issue (or attach key fields).",
            "State MCP4Rhino version, Rhino version, and OS.",
            "Describe steps to reproduce, expected result, and actual result.",
            "Attach relevant lines from the MCP4Rhino log path when helpful.",
            "Do not paste secrets, tokens, API keys, or full license keys.",
        },
    };

    public static object ToolContentResult(
        string? endpoint = null,
        string? logPath = null,
        bool? serverRunning = null) => new
    {
        content = new object[]
        {
            new
            {
                type = "text",
                text = System.Text.Json.JsonSerializer.Serialize(BuildPayload(endpoint, logPath, serverRunning)),
            },
        },
        isError = false,
    };
}

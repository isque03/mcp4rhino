using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using MCP4Rhino.Host;
using MCP4Rhino.Logic;

namespace MCP4Rhino.Tools;

[ExcludeFromCodeCoverage]
internal static class CodeCheckTools
{
    public static object[] ListTools() =>
    [
        T("set_code_context", "Set IRC/IBC code context for pre-checks (not a legal determination).", new
        {
            type = "object",
            properties = new
            {
                family = new { type = "string", description = "IRC|IBC" },
                edition = new { type = "string", description = "e.g. 2021 or 2024" },
                occupancy = new { type = "string" },
                sprinklered = new { type = "boolean" },
                jurisdiction_notes = new { type = "string" },
            },
        }),
        T("run_code_checks", "Run architectural pre-check suite. Returns findings; never claims legal compliance.", new
        {
            type = "object",
            properties = new
            {
                suite = new { type = "string", description = "residential_planning|commercial_egress|accessibility|all" },
            },
        }),
        T("get_code_report", "Get last code pre-check report path + findings.", new { type = "object", properties = new { } }),
    ];

    private static object T(string n, string d, object s) => new { name = n, description = d, inputSchema = s };

    public static string? Dispatch(string name, JsonObject args) => name switch
    {
        "set_code_context" => SetCodeContext(args),
        "run_code_checks" => RunCodeChecks(args),
        "get_code_report" => GetCodeReport(),
        _ => null,
    };

    private static string SetCodeContext(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = ToolHelpers.RequireDoc();
        var ctx = ToolHelpers.GetDocJson<Dictionary<string, string>>(doc, ToolHelpers.CodeKey) ?? new();
        foreach (var kv in args)
            ctx[kv.Key] = kv.Value?.ToString() ?? "";
        if (!ctx.ContainsKey("family")) ctx["family"] = "IRC";
        ToolHelpers.SetDocJson(doc, ToolHelpers.CodeKey, ctx);
        return ToolHelpers.Json(new
        {
            ok = true,
            context = ctx,
            disclaimer = "Code context for automated pre-checks only. Does not constitute a code opinion or replace a licensed architect.",
        });
    });

    private static string RunCodeChecks(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = ToolHelpers.RequireDoc();
        var suite = ToolHelpers.Str(args, "suite") ?? "all";
        var ctx = ToolHelpers.GetDocJson<Dictionary<string, string>>(doc, ToolHelpers.CodeKey) ?? new()
        {
            ["family"] = "IRC",
            ["edition"] = "2021",
        };
        var elements = ElementMapper.TaggedInDoc(doc);
        var report = CodeCheckEngine.Run(suite, ctx, elements);
        var findings = report.Findings.Select(f => new
        {
            rule_id = f.RuleId,
            code_ref = f.CodeRef,
            severity = f.Severity,
            status = f.Status,
            evidence = f.Evidence,
            element_ids = f.ElementIds,
            message = f.Message,
        }).ToArray();

        var payload = new
        {
            generated_at = report.GeneratedAt,
            suite = report.Suite,
            context = report.Context,
            disclaimer = report.Disclaimer,
            summary = new
            {
                total = report.Summary.Total,
                fail = report.Summary.Fail,
                warn = report.Summary.Warn,
                pass = report.Summary.Pass,
                info = report.Summary.Info,
            },
            findings,
        };

        var path = Path.Combine(ToolHelpers.LogsDir(), $"code-report-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
        var mdPath = Path.ChangeExtension(path, ".md");
        File.WriteAllText(mdPath, CodeCheckEngine.ToMarkdown(report));
        ToolHelpers.SetDocString(doc, ToolHelpers.LastCodeReportKey, path);
        return ToolHelpers.Json(new { path, markdown_path = mdPath, summary = payload.summary, findings, disclaimer = report.Disclaimer });
    });

    private static string GetCodeReport() => UiThread.Invoke(() =>
    {
        var doc = ToolHelpers.RequireDoc();
        var path = ToolHelpers.GetDocString(doc, ToolHelpers.LastCodeReportKey);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return ToolHelpers.Json(new { ok = false, error = "No report yet. Call run_code_checks." });
        var json = File.ReadAllText(path);
        return ToolHelpers.Json(new { ok = true, path, markdown_path = Path.ChangeExtension(path, ".md"), report = JsonSerializer.Deserialize<JsonElement>(json) });
    });
}

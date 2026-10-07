using System.Globalization;
using System.Text;
using System.Text.Json;
using MCP4Rhino.Logic.Models;

namespace MCP4Rhino.Logic;

public sealed record CodeFinding(
    string RuleId,
    string CodeRef,
    string Severity,
    string Status,
    string Evidence,
    string[] ElementIds,
    string Message);

public sealed class CodeCheckReport
{
    public required string GeneratedAt { get; init; }
    public required string Suite { get; init; }
    public required Dictionary<string, string> Context { get; init; }
    public required string Disclaimer { get; init; }
    public required CodeCheckSummary Summary { get; init; }
    public required IReadOnlyList<CodeFinding> Findings { get; init; }
}

public sealed class CodeCheckSummary
{
    public int Total { get; init; }
    public int Fail { get; init; }
    public int Warn { get; init; }
    public int Pass { get; init; }
    public int Info { get; init; }
}

/// <summary>IRC/IBC/ADA architectural pre-check heuristics (not a legal determination).</summary>
public static class CodeCheckEngine
{
    public const string Disclaimer =
        "PRE-CHECK ONLY. Findings are informational heuristics based on model tags/geometry. They are NOT a determination of code compliance. A licensed design professional must review before permit submission.";

    public static CodeCheckReport Run(string suite, Dictionary<string, string> context, IReadOnlyList<TaggedElement> elements)
    {
        suite = (suite ?? "all").ToLowerInvariant();
        var ctx = new Dictionary<string, string>(context, StringComparer.OrdinalIgnoreCase);
        if (!ctx.ContainsKey("family")) ctx["family"] = "IRC";
        if (!ctx.ContainsKey("edition")) ctx["edition"] = ctx.GetValueOrDefault("family") == "IBC" ? "2024" : "2021";

        var findings = new List<CodeFinding>();
        if (suite is "residential_planning" or "all")
            findings.AddRange(RunResidential(elements, ctx));
        if (suite is "commercial_egress" or "all")
            findings.AddRange(RunCommercial(elements, ctx));
        if (suite is "accessibility" or "all")
            findings.AddRange(RunAccessibility(elements, ctx));

        return new CodeCheckReport
        {
            GeneratedAt = DateTime.UtcNow.ToString("o"),
            Suite = suite,
            Context = ctx,
            Disclaimer = Disclaimer,
            Summary = new CodeCheckSummary
            {
                Total = findings.Count,
                Fail = findings.Count(f => f.Status == "fail"),
                Warn = findings.Count(f => f.Status == "warn"),
                Pass = findings.Count(f => f.Status == "pass"),
                Info = findings.Count(f => f.Status == "info"),
            },
            Findings = findings,
        };
    }

    public static string ToMarkdown(CodeCheckReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# MCP4Rhino code pre-check report");
        sb.AppendLine();
        sb.AppendLine($"> {report.Disclaimer}");
        sb.AppendLine();
        sb.AppendLine($"Suite: `{report.Suite}`  ");
        sb.AppendLine($"Context: `{JsonSerializer.Serialize(report.Context)}`");
        sb.AppendLine();
        sb.AppendLine("| Status | Severity | Rule | Code | Message |");
        sb.AppendLine("|--------|----------|------|------|---------|");
        foreach (var f in report.Findings)
            sb.AppendLine($"| {f.Status} | {f.Severity} | {f.RuleId} | {f.CodeRef} | {f.Message.Replace("|", "/")} |");
        return sb.ToString();
    }

    private static IEnumerable<CodeFinding> RunResidential(IReadOnlyList<TaggedElement> elements, Dictionary<string, string> ctx)
    {
        var edition = ctx.GetValueOrDefault("edition") ?? "2021";
        foreach (var obj in Kind(elements, "space"))
        {
            var name = obj.Name ?? Tag(obj, "mcp4:program") ?? "space";
            var area = ParseD(obj, "mcp4:area");
            var height = ParseD(obj, "mcp4:height");
            var program = (Tag(obj, "mcp4:program") ?? name).ToLowerInvariant();
            var ids = new[] { obj.Id };

            if (program.Contains("bed"))
            {
                yield return area >= 70
                    ? Pass("IRC-R304-area", $"IRC {edition} R304", $"Bedroom '{name}' area {area:0.##} ≥ 70 sf heuristic", ids, area)
                    : Fail("IRC-R304-area", $"IRC {edition} R304", "error", $"Bedroom '{name}' area {area:0.##} below 70 sf heuristic (verify jurisdiction)", ids, $"area={area}");
            }

            if (height > 0)
            {
                yield return height >= 7
                    ? Pass("IRC-R305-ceiling", $"IRC {edition} R305", $"Space '{name}' height {height:0.##} ≥ 7 ft heuristic", ids, height)
                    : Fail("IRC-R305-ceiling", $"IRC {edition} R305", "error", $"Space '{name}' height {height:0.##} below 7 ft heuristic", ids, $"height={height}");
            }
            else
                yield return Info("IRC-R305-ceiling", $"IRC {edition} R305", $"Space '{name}' missing mcp4:height tag", ids, "no height");
        }

        foreach (var obj in Kind(elements, "stair"))
        {
            var riser = ParseD(obj, "mcp4:riser");
            var tread = ParseD(obj, "mcp4:tread");
            var ids = new[] { obj.Id };
            if (riser > 0)
            {
                var ok = riser <= 7.75 && riser >= 4;
                yield return ok
                    ? Pass("IRC-R311-riser", $"IRC {edition} R311.7", $"Stair riser {riser:0.###} within 4–7.75 in heuristic (confirm units)", ids, riser)
                    : Fail("IRC-R311-riser", $"IRC {edition} R311.7", "error", $"Stair riser {riser:0.###} outside 4–7.75 heuristic (confirm units are inches)", ids, $"riser={riser}");
            }
            if (tread > 0)
            {
                yield return tread >= 10
                    ? Pass("IRC-R311-tread", $"IRC {edition} R311.7", $"Tread {tread:0.###} ≥ 10 heuristic", ids, tread)
                    : Warn("IRC-R311-tread", $"IRC {edition} R311.7", $"Tread {tread:0.###} may be below 10 (confirm units)", ids, $"tread={tread}");
            }
        }

        var doors = Kind(elements, "door").ToList();
        if (doors.Count == 0)
            yield return Warn("IRC-R311-egress-door", $"IRC {edition} R311.2", "No door elements tagged; cannot verify egress door", [], "no doors");
        else
            yield return Pass("IRC-R311-egress-door", $"IRC {edition} R311.2", $"Found {doors.Count} door element(s) — verify one qualifies as egress door", doors.Select(d => d.Id).ToArray(), doors.Count);
    }

    private static IEnumerable<CodeFinding> RunCommercial(IReadOnlyList<TaggedElement> elements, Dictionary<string, string> ctx)
    {
        var edition = ctx.GetValueOrDefault("edition") ?? "2024";
        var sprinklered = string.Equals(ctx.GetValueOrDefault("sprinklered"), "true", StringComparison.OrdinalIgnoreCase);
        double totalOl = 0;
        var spaceIds = new List<string>();
        foreach (var obj in Kind(elements, "space"))
        {
            var area = ParseD(obj, "mcp4:area");
            var factor = ParseD(obj, "mcp4:occupancy_factor");
            if (factor <= 0) factor = 100;
            if (area > 0)
            {
                totalOl += area / factor;
                spaceIds.Add(obj.Id);
            }
        }
        yield return Info("IBC-1004-OL", $"IBC {edition} 1004", $"Estimated occupant load ≈ {totalOl:0.#} from space areas/factors (heuristic)", spaceIds.ToArray(), $"OL={totalOl}");

        var exitDoors = Kind(elements, "door").Where(d =>
            Tag(d, "mcp4:egress") == "true"
            || (d.Name?.Contains("exit", StringComparison.OrdinalIgnoreCase) ?? false)
            || Tag(d, "mcp4:program")?.Contains("exit", StringComparison.OrdinalIgnoreCase) == true).ToList();
        if (exitDoors.Count == 0) exitDoors = Kind(elements, "door").ToList();

        var requiredExits = totalOl > 500 ? 3 : totalOl > 49 ? 2 : 1;
        yield return exitDoors.Count >= requiredExits
            ? Pass("IBC-1006-exits", $"IBC {edition} 1006", $"Exit door count {exitDoors.Count} ≥ required heuristic {requiredExits} for OL {totalOl:0.#}", exitDoors.Select(d => d.Id).ToArray(), exitDoors.Count)
            : Fail("IBC-1006-exits", $"IBC {edition} 1006", "error", $"Exit door count {exitDoors.Count} < required heuristic {requiredExits} for OL {totalOl:0.#}", exitDoors.Select(d => d.Id).ToArray(), $"doors={exitDoors.Count}");

        var travelLimit = sprinklered ? 250 : 200;
        yield return Info("IBC-1017-travel", $"IBC {edition} 1017", $"Travel distance limit heuristic {travelLimit} (sprinklered={sprinklered}). Model lacks path graph — verify manually.", [], $"limit={travelLimit}");

        var waterClosets = Math.Max(1, (int)Math.Ceiling(totalOl / 50));
        yield return Info("IBC-2902-fixtures", $"IBC {edition} 2902", $"Stub fixture estimate: ~{waterClosets} water closet(s) for OL {totalOl:0.#} (confirm Table 2902.1 + occupancy)", [], $"wc={waterClosets}");
    }

    private static IEnumerable<CodeFinding> RunAccessibility(IReadOnlyList<TaggedElement> elements, Dictionary<string, string> ctx)
    {
        foreach (var obj in Kind(elements, "door"))
        {
            var clear = ParseD(obj, "mcp4:clear_width");
            if (clear <= 0)
            {
                var w = ParseD(obj, "mcp4:width");
                if (w > 0) clear = Math.Max(0, w - 0.15);
            }
            var ids = new[] { obj.Id };
            if (clear <= 0)
            {
                yield return Info("ADA-door-clear", "ICC A117.1 / ADA", $"Door '{obj.Name}' missing clear width tag", ids, "no clear_width");
                continue;
            }
            var threshold = clear > 10 ? 32 : 2.67;
            yield return clear >= threshold
                ? Pass("ADA-door-clear", "ICC A117.1 / ADA", $"Door clear width {clear:0.##} meets {threshold} heuristic", ids, clear)
                : Fail("ADA-door-clear", "ICC A117.1 / ADA", "error", $"Door clear width {clear:0.##} below {threshold} heuristic (confirm units)", ids, $"clear={clear}");
        }

        foreach (var obj in Kind(elements, "ramp"))
        {
            var slope = Math.Abs(ParseD(obj, "mcp4:slope"));
            var ids = new[] { obj.Id };
            if (slope <= 0)
            {
                yield return Info("ADA-ramp-slope", "ICC A117.1 / ADA", "Ramp missing mcp4:slope", ids, "no slope");
                continue;
            }
            yield return slope <= (1.0 / 12.0) + 1e-6
                ? Pass("ADA-ramp-slope", "ICC A117.1 / ADA", $"Ramp slope {slope:0.####} ≤ 1:12", ids, slope)
                : Fail("ADA-ramp-slope", "ICC A117.1 / ADA", "error", $"Ramp slope {slope:0.####} steeper than 1:12", ids, $"slope={slope}");
        }

        yield return Info("ADA-route", "IBC Ch.11 / ADA", "Accessible route continuity requires a spaces/doors graph — not fully modeled in v1. Verify accessible path manually.", [], "manual");
    }

    private static IEnumerable<TaggedElement> Kind(IReadOnlyList<TaggedElement> elements, string kind) =>
        elements.Where(e => string.Equals(e.Kind, kind, StringComparison.OrdinalIgnoreCase));

    private static string? Tag(TaggedElement e, string key) =>
        e.Tags.TryGetValue(key, out var v) ? v : null;

    private static double ParseD(TaggedElement e, string key) =>
        e.Tags.TryGetValue(key, out var s) && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;

    private static CodeFinding Pass(string id, string code, string msg, string[] els, double evidence) =>
        new(id, code, "info", "pass", evidence.ToString("G", CultureInfo.InvariantCulture), els, msg);
    private static CodeFinding Fail(string id, string code, string sev, string msg, string[] els, string evidence) =>
        new(id, code, sev, "fail", evidence, els, msg);
    private static CodeFinding Warn(string id, string code, string msg, string[] els, string evidence) =>
        new(id, code, "warning", "warn", evidence, els, msg);
    private static CodeFinding Info(string id, string code, string msg, string[] els, string evidence) =>
        new(id, code, "info", "info", evidence, els, msg);
}

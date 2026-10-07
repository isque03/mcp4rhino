using MCP4Rhino.Logic;
using MCP4Rhino.Logic.Models;
using Xunit;

namespace MCP4Rhino.Tests;

public class CodeCheckEngineTests
{
    private static TaggedElement El(string id, string kind, string? name = null, params (string k, string v)[] tags) =>
        new()
        {
            Id = id,
            Name = name,
            Kind = kind,
            Tags = tags.ToDictionary(t => t.k, t => t.v, StringComparer.OrdinalIgnoreCase),
        };

    [Fact]
    public void Bedroom_Area_PassAndFail()
    {
        var pass = CodeCheckEngine.Run("residential_planning", new() { ["edition"] = "2021" },
        [
            El("1", "space", "Bed", ("mcp4:program", "bedroom"), ("mcp4:area", "120"), ("mcp4:height", "8")),
        ]);
        Assert.Contains(pass.Findings, f => f.RuleId == "IRC-R304-area" && f.Status == "pass");

        var fail = CodeCheckEngine.Run("residential_planning", new(),
        [
            El("1", "space", "Bed", ("mcp4:program", "bedroom"), ("mcp4:area", "50"), ("mcp4:height", "8")),
        ]);
        Assert.Contains(fail.Findings, f => f.RuleId == "IRC-R304-area" && f.Status == "fail");
    }

    [Fact]
    public void Ceiling_Height_And_Missing()
    {
        var low = CodeCheckEngine.Run("residential_planning", new(),
        [
            El("1", "space", "Living", ("mcp4:height", "6")),
        ]);
        Assert.Contains(low.Findings, f => f.RuleId == "IRC-R305-ceiling" && f.Status == "fail");

        var missing = CodeCheckEngine.Run("residential_planning", new(),
        [
            El("1", "space", "Living"),
        ]);
        Assert.Contains(missing.Findings, f => f.RuleId == "IRC-R305-ceiling" && f.Status == "info");
    }

    [Fact]
    public void Stair_Riser_Tread()
    {
        var ok = CodeCheckEngine.Run("residential_planning", new(),
        [
            El("s", "stair", null, ("mcp4:riser", "7"), ("mcp4:tread", "11")),
            El("d", "door"),
        ]);
        Assert.Contains(ok.Findings, f => f.RuleId == "IRC-R311-riser" && f.Status == "pass");
        Assert.Contains(ok.Findings, f => f.RuleId == "IRC-R311-tread" && f.Status == "pass");

        var bad = CodeCheckEngine.Run("residential_planning", new(),
        [
            El("s", "stair", null, ("mcp4:riser", "9"), ("mcp4:tread", "8")),
        ]);
        Assert.Contains(bad.Findings, f => f.RuleId == "IRC-R311-riser" && f.Status == "fail");
        Assert.Contains(bad.Findings, f => f.RuleId == "IRC-R311-tread" && f.Status == "warn");
        Assert.Contains(bad.Findings, f => f.RuleId == "IRC-R311-egress-door" && f.Status == "warn");
    }

    [Fact]
    public void Commercial_Ol_And_Exits()
    {
        var report = CodeCheckEngine.Run("commercial_egress", new() { ["sprinklered"] = "true", ["edition"] = "2024" },
        [
            El("sp", "space", null, ("mcp4:area", "10000"), ("mcp4:occupancy_factor", "100")),
            El("d1", "door", "exit A", ("mcp4:egress", "true")),
        ]);
        Assert.Contains(report.Findings, f => f.RuleId == "IBC-1004-OL");
        Assert.Contains(report.Findings, f => f.RuleId == "IBC-1006-exits" && f.Status == "fail"); // OL=100 needs 2
        Assert.Contains(report.Findings, f => f.RuleId == "IBC-1017-travel" && f.Evidence.Contains("250"));
    }

    [Fact]
    public void Accessibility_Door_And_Ramp()
    {
        var report = CodeCheckEngine.Run("accessibility", new(),
        [
            El("d", "door", "D1", ("mcp4:clear_width", "36")),
            El("r", "ramp", null, ("mcp4:slope", "0.12")),
            El("d2", "door", "D2", ("mcp4:width", "3")),
            El("r2", "ramp"),
        ]);
        Assert.Contains(report.Findings, f => f.RuleId == "ADA-door-clear" && f.Status == "pass");
        Assert.Contains(report.Findings, f => f.RuleId == "ADA-ramp-slope" && f.Status == "fail");
        Assert.Contains(report.Findings, f => f.RuleId == "ADA-ramp-slope" && f.Status == "info");
    }

    [Fact]
    public void Markdown_Contains_Disclaimer()
    {
        var report = CodeCheckEngine.Run("all", new(), []);
        var md = CodeCheckEngine.ToMarkdown(report);
        Assert.Contains("PRE-CHECK ONLY", md);
        Assert.Contains(CodeCheckEngine.Disclaimer, md);
        Assert.True(report.Summary.Total >= 1);
    }
}

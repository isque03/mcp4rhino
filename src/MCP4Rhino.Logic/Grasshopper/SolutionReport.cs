using System.Text.Json.Nodes;

namespace MCP4Rhino.Logic.Grasshopper;

public sealed record ResultIdentity(string SessionId, string DocumentId, long Revision, string? ResultId)
{
    public void Require(JsonObject arguments, bool requireResult = false)
    {
        if (arguments.ContainsKey("expected_revision")) GhJson.ExpectRevision(Revision, arguments);
        if (requireResult || arguments.ContainsKey("result_id"))
            if (ResultId is null || ResultId != GhJson.Text(arguments, "result_id"))
                throw new GhException("stale_result", "Outputs expired. Inspect the definition and solve again.");
    }
}

public sealed record NamedOutput(string Name, string Node, string Port)
{
    public static NamedOutput[] Parse(JsonObject args)
    {
        var outputs = GhJson.Array(args["outputs"]).Select(n =>
        {
            var item = GhJson.Object(n);
            return new NamedOutput(GhJson.Text(item, "name"), GhJson.Text(item, "node"), GhJson.Text(item, "port"));
        }).ToArray();
        if (outputs.Length is < 1 or > 20 || outputs.Select(o => o.Name).Distinct().Count() != outputs.Length)
            throw new GhException("invalid_arguments", "Select 1–20 outputs with unique non-empty names.");
        return outputs;
    }
}

public static class SolutionReport
{
    public static object Page(IEnumerable<object> rows, JsonObject args)
    {
        var offset = GhJson.Offset(args); var limit = GhJson.PageSize(args);
        var page = rows.Skip(offset).Take(limit + 1).ToArray();
        return new { offset, rows = page.Take(limit).ToArray(),
            next_offset = page.Length > limit ? offset + limit : (int?)null };
    }
}

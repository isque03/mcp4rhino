using System.Text.Json.Nodes;

namespace MCP4Rhino.Logic.Resources;

public static class ResourceCatalog
{
    public static JsonObject List(JsonObject args) => ResourcePaging.Catalog("resources", new object[]
    {
        Item("server/capabilities", "Server capabilities", "Server versions, subsystem state, lanes and limits", "application/json"),
        Item("server/getting-started", "Getting started", "First Rhino session walkthrough for new users", "text/markdown"),
        Item("server/writing-style", "Writing style", "Clear English (STE-inspired) rules for agent replies", "text/markdown"),
        Item("rhino/documents/active", "Active Rhino document", "Live active-document context without paths or geometry", "application/json"),
        Item("grasshopper/sessions", "Grasshopper sessions", "Definitions and sessions scoped to the active Rhino document", "application/json"),
        Item("grasshopper/guides/index", "Grasshopper guides", "Packaged guides and versioned recipes", "text/markdown"),
    }, args);

    private static object Item(string path, string name, string description, string mimeType) =>
        new { uri = "mcp4rhino://" + path, name, description, mimeType };

    public static JsonObject Templates(JsonObject args) => ResourcePaging.Catalog("resourceTemplates",
        ResourceAddress.Routes.Where(r => r.Pattern.Contains('{') || r.Kind == "sessions").Select(route => (object)new
        {
            name = route.Kind, description = $"Read {route.Kind}; live data requires the active document. No initialization or mutation.",
            uriTemplate = $"mcp4rhino://{route.Area}/{route.Pattern}" + route.QueryTemplate,
            mimeType = route.Kind == "guide" ? "text/markdown" : "application/json",
        }), args);
}

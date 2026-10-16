namespace MCP4Rhino.Logic.Grasshopper;

public sealed record GraphEdge(string Source, string Target);
public sealed record GraphPosition(float X, float Y);

public static class GraphRules
{
    public static IReadOnlyDictionary<string, GraphPosition> Layout(IEnumerable<string> nodes, IEnumerable<GraphEdge> connections)
    {
        var ids = nodes.ToArray(); var edges = connections.Distinct().ToArray();
        RequireAcyclic(ids, edges);
        var incoming = ids.ToDictionary(id => id, id => edges.Count(e => e.Target == id));
        var levels = ids.ToDictionary(id => id, _ => 0);
        var next = new Queue<string>(ids.Where(id => incoming[id] == 0));
        var outgoing = edges.ToLookup(e => e.Source);
        while (next.TryDequeue(out var id))
        {
            foreach (var edge in outgoing[id])
            {
                levels[edge.Target] = Math.Max(levels[edge.Target], levels[id] + 1);
                if (--incoming[edge.Target] == 0) next.Enqueue(edge.Target);
            }
        }
        return ids.GroupBy(id => levels[id]).SelectMany(group => group.Select((id, row) =>
            (id, position: new GraphPosition(80 + group.Key * 200, 100 + row * 140))))
            .ToDictionary(p => p.id, p => p.position);
    }

    public static void RequireAcyclic(IEnumerable<string> nodes, IEnumerable<GraphEdge> connections)
    {
        var edges = connections.Distinct().ToArray();
        var remaining = nodes.ToDictionary(n => n, _ => 0);
        foreach (var edge in edges)
        {
            if (!remaining.ContainsKey(edge.Source) || !remaining.ContainsKey(edge.Target))
                throw new GhException("invalid_connection", "Connection refers to a missing node.");
            remaining[edge.Target]++;
        }
        var next = new Queue<string>(remaining.Where(p => p.Value == 0).Select(p => p.Key));
        var visited = 0;
        var outgoing = edges.ToLookup(e => e.Source);
        while (next.TryDequeue(out var node))
        {
            visited++;
            foreach (var edge in outgoing[node])
                if (--remaining[edge.Target] == 0) next.Enqueue(edge.Target);
        }
        if (visited != remaining.Count)
            throw new GhException("cyclic_graph", "Feedback cycles are outside v1 support.", "debugging");
    }
}

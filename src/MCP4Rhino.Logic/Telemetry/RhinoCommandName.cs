namespace MCP4Rhino.Logic.Telemetry;

/// <summary>Examines only the leading token. Lookup delegates must run on Rhino's UI thread.</summary>
public static class RhinoCommandName
{
    public static string? Resolve(string script, Func<string, bool, Guid> lookupId, Func<Guid, bool, string?> lookupName)
    {
        try
        {
            var input = script.AsSpan().TrimStart();
            while (!input.IsEmpty && input[0] == '!') input = input[1..].TrimStart();
            var english = false;
            while (!input.IsEmpty && input[0] is '_' or '-' or '\'')
            {
                english |= input[0] == '_';
                input = input[1..];
            }
            var end = 0;
            while (end < input.Length && !char.IsWhiteSpace(input[end])) end++;
            if (end == 0 || end > 64 || !char.IsLetter(input[0])) return null;
            var token = input[..end];
            foreach (var c in token)
                if (!char.IsLetterOrDigit(c) && c != '_') return null;
            var name = token.ToString();
            var id = lookupId(name, english);
            if (id == Guid.Empty && !english) id = lookupId(name, true);
            if (id == Guid.Empty) return null;
            var canonical = lookupName(id, true);
            return ToolDiagnosticsWire.IsCommandName(canonical) ? canonical : null;
        }
        catch { return null; /* diagnostics must never prevent command execution */ }
    }
}

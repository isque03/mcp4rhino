namespace MCP4Rhino.Logic.Telemetry;

public static class TelemetryEnvironment
{
    public static string CurrentOs()
    {
        if (OperatingSystem.IsMacOS()) return "macOS";
        if (OperatingSystem.IsWindows()) return "Windows";
        return "Linux";
    }
}

/// <summary>Resolves <c>MCP4RHINO_TELEMETRY_URL</c>. No production URL is compiled in.</summary>
public static class TelemetryEndpoint
{
    public const string EnvVar = "MCP4RHINO_TELEMETRY_URL";

    public static string? Resolve(string? raw = null)
    {
        raw ??= Environment.GetEnvironmentVariable(EnvVar);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        raw = raw.Trim();
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
            return null;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return null;

        var path = uri.AbsolutePath.TrimEnd('/');
        var nextPath = path.EndsWith("/telemetry", StringComparison.OrdinalIgnoreCase)
            ? path
            : string.IsNullOrEmpty(path) ? "/telemetry" : path + "/telemetry";
        if (string.IsNullOrEmpty(nextPath))
            nextPath = "/telemetry";

        // Keep the query. A token or other parameter is part of the configured URL.
        var built = new UriBuilder(uri) { Path = nextPath };
        return built.Uri.AbsoluteUri;
    }
}

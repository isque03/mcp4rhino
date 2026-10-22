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

/// <summary>
/// Resolves the telemetry URL and the API key. The key is never part of the URL.
/// </summary>
public static class TelemetryEndpoint
{
    public const string EnvVar = "MCP4RHINO_TELEMETRY_URL";

    public const string ApiKeyEnvVar = "MCP4RHINO_TELEMETRY_API_KEY";

    public const string ApiKeyHeader = "X-Api-Key";

    public const string InstallTokenFileName = "telemetry-install-token";

    public const string DefaultUrl = "https://events.agenttoolkit.ai/telemetry";

    public static string? Resolve(string? raw = null)
    {
        if (raw is null)
        {
            raw = Environment.GetEnvironmentVariable(EnvVar);
            if (string.IsNullOrWhiteSpace(raw))
                raw = DefaultUrl;
        }

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

        var built = new UriBuilder(uri)
        {
            Path = nextPath,
            Query = QueryWithoutSecret(uri.Query),
        };
        return built.Uri.AbsoluteUri;
    }

    /// <summary>
    /// Resolves the install token. A non-empty <c>MCP4RHINO_TELEMETRY_API_KEY</c> wins;
    /// otherwise <paramref name="packaged"/>, usually the yak file, is used.
    /// </summary>
    public static string? ApiKey(string? raw = null, string? packaged = null)
    {
        if (raw is null)
            raw = Environment.GetEnvironmentVariable(ApiKeyEnvVar);
        if (string.IsNullOrWhiteSpace(raw))
            raw = packaged;
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        raw = raw.Trim();
        if (raw.IndexOfAny(['\r', '\n']) >= 0)
            return null;
        return raw;
    }

    public static string? ReadInstallToken(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return null;
        var path = Path.Combine(directory, InstallTokenFileName);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    private static string QueryWithoutSecret(string query)
    {
        if (string.IsNullOrEmpty(query) || query == "?")
            return "";

        var body = query[0] == '?' ? query[1..] : query;
        var kept = new List<string>();
        foreach (var part in body.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var name = part.Split('=', 2)[0];
            if (string.Equals(Uri.UnescapeDataString(name), "k", StringComparison.OrdinalIgnoreCase))
                continue;
            kept.Add(part);
        }

        return string.Join("&", kept);
    }
}

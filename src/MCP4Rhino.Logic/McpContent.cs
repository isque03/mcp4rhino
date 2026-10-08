namespace MCP4Rhino.Logic;

/// <summary>Builders for MCP tools/call content arrays (hand-rolled; no MCP SDK).</summary>
public static class McpContent
{
    /// <summary>
    /// Max raw PNG bytes embedded as an MCP image content block (~1.33 MiB base64).
    /// Larger files stay on disk; tool result is text-only with omit reason.
    /// </summary>
    public const int MaxEmbeddedPngBytes = 1_048_576;

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47];

    public static object TextOnly(string text) => new
    {
        content = new[] { new { type = "text", text } },
        isError = false,
    };

    /// <summary>
    /// Text metadata first (clients that parse content[0].text), then PNG as an image block.
    /// </summary>
    public static object TextAndPng(string text, byte[] pngBytes)
    {
        RequireValidPng(pngBytes);
        return new
        {
            content = new object[]
            {
                new { type = "text", text },
                new
                {
                    type = "image",
                    data = Convert.ToBase64String(pngBytes),
                    mimeType = "image/png",
                },
            },
            isError = false,
        };
    }

    public static void RequireValidPng(byte[]? pngBytes)
    {
        if (pngBytes is null || pngBytes.Length < 33)
            throw new ArgumentException("PNG bytes are empty or too short for a valid PNG.");
        if (!pngBytes.AsSpan(0, 4).SequenceEqual(PngSignature))
            throw new ArgumentException("Bytes are not a PNG (missing PNG signature).");
    }

    public static bool IsValidPng(byte[]? pngBytes)
    {
        if (pngBytes is null || pngBytes.Length < 33) return false;
        return pngBytes.AsSpan(0, 4).SequenceEqual(PngSignature);
    }
}

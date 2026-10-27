using System.Text.Json;
using System.Text.Json.Nodes;

namespace MCP4Rhino.Logic.Grasshopper;

public static class CaptureContent
{
    public static JsonObject ContextSchema => JsonNode.Parse("""
        {"type":"object","properties":{"session_id":{"type":"string"},
         "expected_revision":{"type":"integer","minimum":0},"result_id":{"type":"string"}},
         "required":["session_id","expected_revision","result_id"],"additionalProperties":false}
        """)!.AsObject();

    public static object FromPng(JsonObject metadata, byte[] bytes)
    {
        McpContent.RequireValidPng(bytes);
        var embedded = bytes.Length <= McpContent.MaxEmbeddedPngBytes;
        metadata = (JsonObject)metadata.DeepClone();
        metadata["byte_length"] = bytes.Length; metadata["image_embedded"] = embedded;
        if (!embedded)
        {
            metadata["image_omitted_reason"] = "exceeds_max_bytes";
            metadata["max_embedded_png_bytes"] = McpContent.MaxEmbeddedPngBytes;
        }
        return embedded ? McpContent.TextAndPng(metadata.ToJsonString(), bytes) : McpContent.TextOnly(metadata.ToJsonString());
    }

    public static object Associate(object result, object identity)
    {
        var content = JsonSerializer.SerializeToNode(result)!.AsObject();
        if (content["isError"]?.GetValue<bool>() == true) return result;
        var text = content["content"]![0]!;
        var metadata = JsonNode.Parse(text["text"]!.GetValue<string>())!.AsObject();
        metadata["grasshopper"] = JsonSerializer.SerializeToNode(identity, new JsonSerializerOptions
            { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        text["text"] = metadata.ToJsonString();
        return content;
    }
}

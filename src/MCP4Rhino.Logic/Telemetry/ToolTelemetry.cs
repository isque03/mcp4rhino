using System.Collections;
using System.Text.Json;
using System.Text.Json.Nodes;
using MCP4Rhino.Contracts;

namespace MCP4Rhino.Logic.Telemetry;

internal readonly record struct ToolOutcome(bool Success, ToolDiagnostics? Diagnostics = null);

internal static class ToolTelemetry
{
    public static ToolOutcome FromResult(string name, object result, ToolDiagnostics? diagnostics)
    {
        var success = true;
        try
        {
            success = Property(result, "isError") is not true;
            if (name == "run_rhino_command" && CommandOk(result) is false)
                return new(false, new(ToolErrorCode.RhinoCommandFailed, ErrorStage: ToolErrorStage.RhinoCommand,
                    RhinoCommandName: diagnostics?.RhinoCommandName));
            if (!success && diagnostics?.ErrorCode is null)
                diagnostics = new(ToolErrorCode.ToolError, RhinoCommandName: diagnostics?.RhinoCommandName);
            return new(success, diagnostics);
        }
        catch { return new(success); }
    }

    public static ToolOutcome FromException(Exception exception, ToolErrorStage? stage) =>
        new(false, new(Code(exception), Type(exception), stage));

    private static ToolErrorCode Code(Exception exception) => exception switch
    {
        ArgumentException => ToolErrorCode.InvalidArgument,
        InvalidOperationException => ToolErrorCode.InvalidState,
        NotSupportedException => ToolErrorCode.NotSupported,
        TimeoutException => ToolErrorCode.Timeout,
        OperationCanceledException => ToolErrorCode.Cancelled,
        _ => ToolErrorCode.UnhandledException,
    };

    private static ToolErrorType Type(Exception exception)
    {
        var type = exception.GetType();
        if (type == typeof(ArgumentException)) return ToolErrorType.ArgumentException;
        if (type == typeof(ArgumentNullException)) return ToolErrorType.ArgumentNullException;
        if (type == typeof(ArgumentOutOfRangeException)) return ToolErrorType.ArgumentOutOfRangeException;
        if (type == typeof(InvalidOperationException)) return ToolErrorType.InvalidOperationException;
        if (type == typeof(NotSupportedException)) return ToolErrorType.NotSupportedException;
        if (type == typeof(TimeoutException)) return ToolErrorType.TimeoutException;
        if (type == typeof(OperationCanceledException)) return ToolErrorType.OperationCanceledException;
        if (type == typeof(TaskCanceledException)) return ToolErrorType.TaskCanceledException;
        return ToolErrorType.Other;
    }

    private static bool? CommandOk(object result)
    {
        if (Property(result, "content") is not IEnumerable content) return null;
        foreach (var item in content)
        {
            if (item is null || Property(item, "type") as string != "text") continue;
            if (Property(item, "text") is not string text) return null;
            using var json = JsonDocument.Parse(text);
            if (json.RootElement.ValueKind != JsonValueKind.Object || !json.RootElement.TryGetProperty("ok", out var ok)) return null;
            return ok.ValueKind is JsonValueKind.True or JsonValueKind.False ? ok.GetBoolean() : null;
        }
        return null;
    }

    private static object? Property(object value, string name)
    {
        if (value is JsonObject obj) return Primitive(obj[name]);
        return value.GetType().GetProperty(name)?.GetValue(value);
    }

    private static object? Primitive(JsonNode? value)
    {
        if (value is JsonValue primitive)
        {
            if (primitive.TryGetValue<bool>(out var boolean)) return boolean;
            if (primitive.TryGetValue<string>(out var text)) return text;
        }
        return value;
    }
}

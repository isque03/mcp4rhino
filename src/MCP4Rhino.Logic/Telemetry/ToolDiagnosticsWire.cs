using MCP4Rhino.Contracts;

namespace MCP4Rhino.Logic.Telemetry;

/// <summary>Explicit backend catalog and applicability rules, independent of enum spelling.</summary>
internal readonly record struct ToolDiagnosticsWire(
    string? ErrorCode, string? ErrorType, string? ErrorStage, string? RhinoCommandName)
{
    public static ToolDiagnosticsWire Create(string tool, bool success, ToolDiagnostics? diagnostics)
    {
        if (diagnostics is not { } value) return default;
        var code = Code(value.ErrorCode);
        var type = Type(value.ErrorType);
        var stage = Stage(value.ErrorStage);
        if ((value.ErrorCode.HasValue && code is null) ||
            (value.ErrorType.HasValue && type is null) ||
            (value.ErrorStage.HasValue && stage is null) ||
            (code is null && (type is not null || stage is not null))) return default;
        var command = tool == "run_rhino_command" && IsCommandName(value.RhinoCommandName)
            ? value.RhinoCommandName : null;
        return new(success ? null : code, success ? null : type, success ? null : stage, command);
    }

    public static bool IsCommandName(string? name) => name is { Length: >= 1 and <= 64 } &&
        char.IsAsciiLetter(name[0]) && name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');

    private static string? Code(ToolErrorCode? value) => value switch
    {
        ToolErrorCode.InvalidArgument => "INVALID_ARGUMENT", ToolErrorCode.InvalidState => "INVALID_STATE",
        ToolErrorCode.NotSupported => "NOT_SUPPORTED", ToolErrorCode.Timeout => "TIMEOUT",
        ToolErrorCode.Cancelled => "CANCELLED", ToolErrorCode.NoActiveDocument => "NO_ACTIVE_DOCUMENT",
        ToolErrorCode.ViewNotFound => "VIEW_NOT_FOUND", ToolErrorCode.ObjectNotFound => "OBJECT_NOT_FOUND",
        ToolErrorCode.InvalidGeometry => "INVALID_GEOMETRY", ToolErrorCode.RhinoCommandFailed => "RHINO_COMMAND_FAILED",
        ToolErrorCode.ToolError => "TOOL_ERROR", ToolErrorCode.UnhandledException => "UNHANDLED_EXCEPTION", _ => null,
    };

    private static string? Type(ToolErrorType? value) => value switch
    {
        ToolErrorType.ArgumentException => "ArgumentException", ToolErrorType.ArgumentNullException => "ArgumentNullException",
        ToolErrorType.ArgumentOutOfRangeException => "ArgumentOutOfRangeException",
        ToolErrorType.InvalidOperationException => "InvalidOperationException", ToolErrorType.NotSupportedException => "NotSupportedException",
        ToolErrorType.TimeoutException => "TimeoutException", ToolErrorType.OperationCanceledException => "OperationCanceledException",
        ToolErrorType.TaskCanceledException => "TaskCanceledException", ToolErrorType.Other => "Other", _ => null,
    };

    private static string? Stage(ToolErrorStage? value) => value switch
    {
        ToolErrorStage.Dispatch => "DISPATCH", ToolErrorStage.Execution => "EXECUTION",
        ToolErrorStage.RhinoCommand => "RHINO_COMMAND", _ => null,
    };
}

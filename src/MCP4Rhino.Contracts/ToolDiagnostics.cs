namespace MCP4Rhino.Contracts;

public enum ToolErrorCode
{
    InvalidArgument, InvalidState, NotSupported, Timeout, Cancelled,
    NoActiveDocument, ViewNotFound, ObjectNotFound, InvalidGeometry,
    RhinoCommandFailed, ToolError, UnhandledException,
}

public enum ToolErrorType
{
    ArgumentException, ArgumentNullException, ArgumentOutOfRangeException,
    InvalidOperationException, NotSupportedException, TimeoutException,
    OperationCanceledException, TaskCanceledException, Other,
}

public enum ToolErrorStage { Dispatch, Execution, RhinoCommand }

/// <summary>Controlled, optional diagnostics. Never holds exceptions, scripts or results.</summary>
public readonly record struct ToolDiagnostics(
    ToolErrorCode? ErrorCode = null,
    ToolErrorType? ErrorType = null,
    ToolErrorStage? ErrorStage = null,
    string? RhinoCommandName = null);

/// <summary>Internal bridge envelope; the dispatcher returns only Result to the MCP client.</summary>
public sealed record ToolCallResult(object Result, ToolDiagnostics Diagnostics);

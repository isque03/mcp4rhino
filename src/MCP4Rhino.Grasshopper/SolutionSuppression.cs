using System.Reflection;
using Grasshopper.Kernel;
using MCP4Rhino.Logic.Grasshopper;

namespace MCP4Rhino.Grasshopper;

/// <summary>UI-thread-only scope. Restoring the public setter would itself start a solution.</summary>
internal sealed class SolutionSuppression : IDisposable
{
    private readonly FieldInfo _gate;
    private readonly bool _enabled;

    public SolutionSuppression()
    {
        _gate = typeof(GH_Document).GetField("m_enableSolutions", BindingFlags.Static | BindingFlags.NonPublic) ??
            throw new GhException("solver_gate_unavailable", "Cannot suppress implicit solutions on this Grasshopper version.");
        if (_gate.FieldType != typeof(bool)) throw new GhException("solver_gate_unavailable", "Incompatible solver gate.");
        _enabled = (bool)_gate.GetValue(null)!;
        _gate.SetValue(null, false);
    }

    public void Dispose() => _gate.SetValue(null, _enabled);
}

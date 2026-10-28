using MCP4Rhino.Logic.Telemetry;
using Xunit;

namespace MCP4Rhino.Tests;

public class RhinoCommandNameTests
{
    [Theory]
    [InlineData("! _-Box 0,0,0 10,10,10 _Enter", "Box", true)]
    [InlineData(" \t! ! '_-Box 0,0,0", "Box", true)]
    [InlineData("_-Export \"C:\\private\\design.3dm\" _Enter", "Export", true)]
    [InlineData("_Box ... _Sphere ...", "Box", true)]
    [InlineData("Box", "Box", false)]
    [InlineData("'Box", "Box", false)]
    [InlineData("Würfel 0,0,0", "Würfel", false)]
    [InlineData("_RunPythonScript \"private.py\"", "RunPythonScript", true)]
    public void Only_Leading_Token_Reaches_Lookup(string script, string token, bool english)
    {
        var id = Guid.NewGuid();
        var calls = new List<(string, bool)>();
        var result = RhinoCommandName.Resolve(script, (name, mode) =>
        {
            calls.Add((name, mode));
            return id;
        }, (commandId, mode) =>
        {
            Assert.Equal(id, commandId);
            Assert.True(mode);
            return token == "Würfel" ? "Box" : token;
        });
        Assert.Equal([(token, english)], calls);
        Assert.Equal(token == "Würfel" ? "Box" : token, result);
    }

    [Fact]
    public void Localized_Lookup_Falls_Back_To_English_But_English_Prefix_Does_Not()
    {
        var modes = new List<bool>();
        Guid Lookup(string name, bool english) { modes.Add(english); return english ? Guid.NewGuid() : Guid.Empty; }
        Assert.Equal("Box", RhinoCommandName.Resolve("Box", Lookup, (_, _) => "Box"));
        Assert.Equal([false, true], modes);
        modes.Clear();
        Assert.Equal("Box", RhinoCommandName.Resolve("_Box", Lookup, (_, _) => "Box"));
        Assert.Equal([true], modes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("! _-")]
    [InlineData("_- Box")]
    [InlineData("\"private.3dm\"")]
    [InlineData("C:\\private\\design.3dm")]
    [InlineData("private.3dm")]
    [InlineData("_Box;_Sphere")]
    [InlineData("_Box(1)")]
    [InlineData("_123")]
    public void Ambiguous_Or_File_Tokens_Are_Omitted_Without_Lookup(string script)
    {
        var lookups = 0;
        Assert.Null(RhinoCommandName.Resolve(script, (_, _) => { lookups++; return Guid.Empty; }, (_, _) => throw new Exception()));
        Assert.Equal(0, lookups);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("private.3dm")]
    [InlineData("1Box")]
    [InlineData("Böx")]
    public void Invalid_Canonical_Names_Are_Omitted(string? canonical) =>
        Assert.Null(RhinoCommandName.Resolve("_Box", (_, _) => Guid.NewGuid(), (_, _) => canonical));

    [Fact]
    public void Unresolved_Aliases_Lookup_Failures_And_Length_Bounds_Are_Safe()
    {
        Assert.Null(RhinoCommandName.Resolve("alias _Box", (_, _) => Guid.Empty, (_, _) => throw new Exception()));
        Assert.Null(RhinoCommandName.Resolve("_Box", (_, _) => throw new Exception("private"), (_, _) => "Box"));
        Assert.Null(RhinoCommandName.Resolve("_Box", (_, _) => Guid.NewGuid(), (_, _) => throw new Exception("private")));
        Assert.Null(RhinoCommandName.Resolve(new string('B', 65), (_, _) => throw new Exception(), (_, _) => "Box"));
        Assert.Null(RhinoCommandName.Resolve("Box", (_, _) => Guid.NewGuid(), (_, _) => new string('B', 65)));
        Assert.Equal(new string('B', 64), RhinoCommandName.Resolve(new string('B', 64), (_, _) => Guid.NewGuid(), (_, _) => new string('B', 64)));
    }
}

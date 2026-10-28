using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Xunit;

namespace MCP4Rhino.Tests;

public class GrasshopperSdkContractTests
{
    [Fact]
    public void PinnedSdkSupportsNonSolvingCanvasBindingAndActivation()
    {
        var cache = Environment.GetEnvironmentVariable("NUGET_PACKAGES") ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
        using var file = File.OpenRead(Path.Combine(cache, "grasshopper", "8.21.25188.17001", "lib", "net7.0", "Grasshopper.dll"));
        using var pe = new PEReader(file);
        var metadata = pe.GetMetadataReader();
        var types = metadata.TypeDefinitions.Select(metadata.GetTypeDefinition).Where(type =>
            metadata.GetString(type.Name) is "GH_Canvas" or "GH_Document").ToDictionary(type =>
                metadata.GetString(type.Namespace) + "." + metadata.GetString(type.Name));
        var canvas = types["Grasshopper.GUI.Canvas.GH_Canvas"];
        var document = types["Grasshopper.Kernel.GH_Document"];
        var binding = Field(metadata, canvas, "_document");
        Assert.True(binding.Attributes.HasFlag(FieldAttributes.Private));
        Assert.False(binding.Attributes.HasFlag(FieldAttributes.Static));
        var gate = Field(metadata, document, "m_enableSolutions");
        Assert.True(gate.Attributes.HasFlag(FieldAttributes.Static));
        Assert.Equal(new byte[] { 0x06, 0x02 }, metadata.GetBlobBytes(gate.Signature)); // field: Boolean
        var render = canvas.GetMethods().Select(metadata.GetMethodDefinition).Single(method =>
            metadata.GetString(method.Name) == "GenerateHiResImageTile");
        Assert.True(render.Attributes.HasFlag(MethodAttributes.Public));
        Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), assembly => assembly.GetName().Name == "Grasshopper");
    }

    private static FieldDefinition Field(MetadataReader metadata, TypeDefinition type, string name) =>
        type.GetFields().Select(metadata.GetFieldDefinition).Single(field => metadata.GetString(field.Name) == name);

    [Fact]
    public void PinnedSdkSupportsDynamicMetadataRawSlidersAndArchiveDiagnostics()
    {
        var cache = Environment.GetEnvironmentVariable("NUGET_PACKAGES") ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
        var directory = Path.Combine(cache, "grasshopper", "8.21.25188.17001", "lib", "net7.0");
        AssertMembers(Path.Combine(directory, "Grasshopper.dll"), "Grasshopper.Kernel.GH_ComponentServer",
            ["get_ObjectProxies", "EmitObjectProxy", "EmitObject"]);
        AssertMembers(Path.Combine(directory, "Grasshopper.dll"), "Grasshopper.Kernel.IGH_ObjectProxy",
            ["get_Type", "get_SDKCompliant", "get_LibraryGuid", "get_Obsolete"]);
        AssertMembers(Path.Combine(directory, "Grasshopper.dll"), "Grasshopper.Kernel.GH_AssemblyInfo",
            ["get_Assembly", "get_Location", "get_LoadingMechanism"]);
        AssertMembers(Path.Combine(directory, "Grasshopper.dll"), "Grasshopper.GUI.GH_Slider", ["get_Value", "get_Type"]);
        AssertMembers(Path.Combine(directory, "GH_IO.dll"), "GH_IO.Serialization.GH_Archive",
            ["ReadFromFile", "ExtractObject", "MessageCount"]);
        Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), assembly => assembly.GetName().Name == "Grasshopper");
        Assert.NotEmpty(AssemblyName.GetAssemblyName(Path.Combine(directory, "Grasshopper.dll")).GetPublicKeyToken()!);
    }

    private static void AssertMembers(string assemblyPath, string typeName, string[] members)
    {
        using var file = File.OpenRead(assemblyPath);
        using var pe = new PEReader(file);
        var metadata = pe.GetMetadataReader();
        var type = metadata.TypeDefinitions.Select(metadata.GetTypeDefinition).Single(type =>
            metadata.GetString(type.Namespace) + "." + metadata.GetString(type.Name) == typeName);
        var methods = type.GetMethods().Select(metadata.GetMethodDefinition).Select(method => metadata.GetString(method.Name)).ToArray();
        foreach (var member in members) Assert.Contains(member, methods);
    }
}

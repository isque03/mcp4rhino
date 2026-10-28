using System.Diagnostics.CodeAnalysis;
using Eto.Drawing;
using Eto.Forms;
using MCP4Rhino.Contracts;
using MCP4Rhino.Logic.Grasshopper;
using Rhino.UI;

namespace MCP4Rhino.Host;

[ExcludeFromCodeCoverage]
internal static class GrasshopperPluginsDialog
{
    public static string[]? Show(GrasshopperComponentLibrary[] libraries, ComponentTrust trust, Control? owner)
    {
        string[]? result = null;
        using var dialog = new Dialog { Title = "Grasshopper plugins", ClientSize = new Size(560, 420), Padding = 16, Resizable = true };
        var rows = new StackLayout { Spacing = 8, HorizontalContentAlignment = HorizontalAlignment.Stretch };
        var choices = Choices(libraries, trust, rows);
        var apply = new Button { Text = "Apply" };
        var cancel = new Button { Text = "Cancel" };
        apply.Click += (_, _) => { result = choices.Where(pair => pair.Value.Checked == true).Select(pair => pair.Key).ToArray(); dialog.Close(); };
        cancel.Click += (_, _) => dialog.Close();
        dialog.AbortButton = cancel;
        dialog.Content = new StackLayout
        {
            Spacing = 12, HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Items =
            {
                Text("Rhino's built-in modeling components work automatically."),
                Text("Choose third-party plugins that MCP4Rhino may use. Enable only plugins you trust; their code runs inside Rhino. Scripts, timers and other restricted components stay blocked."),
                new StackLayoutItem(new Scrollable { Content = rows }, true),
                Text("Changes apply immediately and are saved for your next Rhino session."),
                new StackLayout { Orientation = Orientation.Horizontal, Spacing = 8, Items = { apply, cancel } },
            },
        };
        dialog.ShowModal(owner ?? RhinoEtoApp.MainWindow);
        return result;
    }

    private static Dictionary<string, CheckBox> Choices(GrasshopperComponentLibrary[] libraries, ComponentTrust trust, StackLayout rows)
    {
        var choices = new Dictionary<string, CheckBox>(StringComparer.Ordinal);
        foreach (var library in libraries)
        {
            if (library.AvailableComponents == 0)
            {
                rows.Items.Add(Text($"{library.Name} {library.Version} — restricted components only"));
                continue;
            }
            var checkbox = new CheckBox { Text = $"{library.Name} {library.Version}", ToolTip = library.AssemblyIdentity,
                Checked = trust.Allows(library.AssemblyIdentity) };
            choices.Add(library.AssemblyIdentity, checkbox);
            rows.Items.Add(checkbox);
        }
        if (libraries.Length == 0) rows.Items.Add(Text("No third-party Grasshopper plugins were found."));
        return choices;
    }

    private static Label Text(string text) => new() { Text = text, Wrap = WrapMode.Word };
}

using System.Diagnostics.CodeAnalysis;
using Eto.Drawing;
using Eto.Forms;
using Rhino.UI;

namespace MCP4Rhino.Host;

/// <summary>Later usage-data choice. Neither button is the default.</summary>
[ExcludeFromCodeCoverage]
public static class TelemetryConsentDialog
{
    public const string Title = "Help improve MCP4Rhino";

    /// <returns><c>true</c> to turn on, <c>false</c> to turn off, or <c>null</c> when closed without a button.</returns>
    public static bool? ShowPreferences(bool currentlyEnabled, Control? owner = null)
    {
        bool? result = null;
        using var dlg = new Dialog
        {
            Title = Title,
            ClientSize = new Size(440, 220),
            Resizable = false,
            Padding = 16,
        };

        var question = new Label
        {
            Text = currentlyEnabled
                ? "Anonymous usage statistics are on."
                : "Anonymous usage statistics are off.",
            Wrap = WrapMode.Word,
            Width = 408,
            TextAlignment = TextAlignment.Left,
            Font = new Font(SystemFont.Bold, 12),
        };
        var keep = new Button { Text = currentlyEnabled ? "Keep on" : "Keep off" };
        var change = new Button { Text = currentlyEnabled ? "Turn off" : "Turn on" };
        keep.Click += (_, _) =>
        {
            result = currentlyEnabled;
            dlg.Close();
        };
        change.Click += (_, _) =>
        {
            result = !currentlyEnabled;
            dlg.Close();
        };

        dlg.Content = new StackLayout
        {
            Spacing = 8,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalContentAlignment = VerticalAlignment.Top,
            Items =
            {
                question,
                Paragraph("We collect your plugin version, operating system, and which MCP tools you use. This helps us understand usage and improve the plugin."),
                Paragraph("We never collect your models, prompts, files, or personal information."),
                Paragraph("You can change this at any time."),
                new StackLayout
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Items = { keep, change },
                },
            },
        };

        dlg.ShowModal(owner ?? RhinoEtoApp.MainWindow);
        return result;
    }

    private static Label Paragraph(string text) => new()
    {
        Text = text,
        Wrap = WrapMode.Word,
        Width = 408,
        TextAlignment = TextAlignment.Left,
    };
}

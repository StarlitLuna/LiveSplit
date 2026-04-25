using System.Drawing;
using System.IO;
using System.Reflection;

using LiveSplit.Model;

namespace LiveSplit.UI.LayoutFactories;

public class StandardLayoutFactory : ILayoutFactory
{
    private const string DefaultLayoutResourceName = "LiveSplit.Resources.DefaultLayout.lsl";

    public ILayout Create(LiveSplitState state)
    {
        Assembly assembly = typeof(StandardLayoutFactory).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(DefaultLayoutResourceName)
            ?? throw new FileNotFoundException(
                $"Embedded resource '{DefaultLayoutResourceName}' was not found in {assembly.FullName}.");
        ILayout layout = new XMLLayoutFactory(stream).Create(state);

        layout.X = layout.Y = 100;
        CenturyGothicFix(layout);

        return layout;
    }

    public static void CenturyGothicFix(ILayout layout)
    {
        if (layout.Settings.TimerFont.Name != "Century Gothic")
        {
            layout.Settings.TimerFont = new FontDescriptor("Calibri", layout.Settings.TimerFont.Size, FontStyle.Bold, GraphicsUnit.Pixel);
        }
    }
}

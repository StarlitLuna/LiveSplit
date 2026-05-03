using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Xml;

using LiveSplit.Avalonia.Dialogs;
using LiveSplit.Model;
using LiveSplit.Options;
using LiveSplit.Options.SettingsFactories;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using LiveSplit.UI.Drawing;

using Xunit;

namespace LiveSplit.Tests.Avalonia;

public class LayoutEditorDialogMust
{
    [Fact]
    public void RestoreFullLayoutSnapshotOnCancel()
    {
        var originalComponent = new StubComponent("Original", "one");
        var layout = CreateLayout();
        var layoutComponent = new LayoutComponent("original.dll", originalComponent)
        {
            FontOverrides = new FontOverrides
            {
                OverrideTextFont = true,
                TextFont = new FontDescriptor("Arial", 14f),
            },
        };
        layout.LayoutComponents.Add(layoutComponent);

        var snapshot = LayoutEditorDialog.LayoutSnapshot.Capture(layout);

        layout.LayoutComponents.Clear();
        layout.LayoutComponents.Add(new LayoutComponent("added.dll", new StubComponent("Added", "two")));
        layout.Mode = LayoutMode.Horizontal;
        layout.VerticalWidth = 301;
        layout.VerticalHeight = 401;
        layout.HorizontalWidth = 501;
        layout.HorizontalHeight = 601;
        layout.X = 7;
        layout.Y = 9;
        layout.HasChanged = false;
        layout.Settings.TextColor = Color.Red;
        layoutComponent.FontOverrides.OverrideTextFont = false;
        layoutComponent.FontOverrides.TextFont = new FontDescriptor("Consolas", 18f);
        originalComponent.Value = "mutated";

        snapshot.Apply(layout);

        var restored = Assert.IsType<LayoutComponent>(Assert.Single(layout.LayoutComponents));
        Assert.Equal("original.dll", restored.Path);
        Assert.Same(originalComponent, restored.Component);
        Assert.Equal(LayoutMode.Vertical, layout.Mode);
        Assert.Equal(101, layout.VerticalWidth);
        Assert.Equal(201, layout.VerticalHeight);
        Assert.Equal(11, layout.HorizontalWidth);
        Assert.Equal(21, layout.HorizontalHeight);
        Assert.Equal(3, layout.X);
        Assert.Equal(4, layout.Y);
        Assert.True(layout.HasChanged);
        Assert.Equal(Color.Blue.ToArgb(), layout.Settings.TextColor.ToArgb());
        Assert.True(restored.FontOverrides.OverrideTextFont);
        Assert.Equal("Arial", restored.FontOverrides.TextFont.FamilyName);
        Assert.Equal("one", originalComponent.Value);
    }

    [Fact]
    public void AcceptMarksLayoutDirtyWithoutSavingOrClearingHasChanged()
    {
        string path = Path.Combine(Path.GetTempPath(), $"livesplit-layout-editor-{Guid.NewGuid():N}.lsl");
        File.WriteAllText(path, "original file");

        try
        {
            var layout = CreateLayout();
            layout.FilePath = path;
            layout.HasChanged = false;

            LayoutEditorDialog.AcceptLayout(layout);

            Assert.True(layout.HasChanged);
            Assert.Equal("original file", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ExposeOrientationAndSetSizeControls()
    {
        string source = File.ReadAllText(FindRepoFile("src/LiveSplit/Avalonia/Dialogs/LayoutEditorDialog.cs"));

        Assert.Contains("\"Vertical\"", source, StringComparison.Ordinal);
        Assert.Contains("\"Horizontal\"", source, StringComparison.Ordinal);
        Assert.Contains("\"Set Size", source, StringComparison.Ordinal);
    }

    private static Layout CreateLayout()
    {
        LayoutSettings settings = new StandardLayoutSettingsFactory().Create();
        settings.TextColor = Color.Blue;

        return new Layout
        {
            Settings = settings,
            Mode = LayoutMode.Vertical,
            VerticalWidth = 101,
            VerticalHeight = 201,
            HorizontalWidth = 11,
            HorizontalHeight = 21,
            X = 3,
            Y = 4,
            HasChanged = true,
        };
    }

    private static string FindRepoFile(string relativePath)
    {
        DirectoryInfo directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            string candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(relativePath);
    }

    private sealed class StubComponent : IComponent
    {
        public StubComponent(string name, string value)
        {
            ComponentName = name;
            Value = value;
        }

        public string ComponentName { get; }
        public string Value { get; set; }
        public float HorizontalWidth => 0;
        public float MinimumHeight => 0;
        public float VerticalHeight => 0;
        public float MinimumWidth => 0;
        public float PaddingTop => 0;
        public float PaddingBottom => 0;
        public float PaddingLeft => 0;
        public float PaddingRight => 0;
        public IDictionary<string, Action> ContextMenuControls => null;

        public void DrawHorizontal(IDrawingContext ctx, LiveSplitState state, float height) { }
        public void DrawVertical(IDrawingContext ctx, LiveSplitState state, float width) { }
        public XmlNode GetSettings(XmlDocument document)
        {
            XmlElement settings = document.CreateElement("Settings");
            XmlElement value = document.CreateElement("Value");
            value.InnerText = Value;
            settings.AppendChild(value);
            return settings;
        }

        public void SetSettings(XmlNode settings)
        {
            Value = settings["Value"]?.InnerText;
        }

        public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode) { }
        public void Dispose() { }
    }
}

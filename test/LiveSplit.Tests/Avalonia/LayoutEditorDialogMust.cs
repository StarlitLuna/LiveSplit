using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
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
    public void UseMasterGridButtonStructure()
    {
        object spec = LayoutSpec();

        Assert.Equal(new[] { 48, 94, 81, 78, 74, -1, 88 }, IntList(spec, "ColumnWidths"));
        Assert.Equal(new[] { 48, 41, 41, 41, 41, -1, 36 }, IntList(spec, "RowHeights"));
        Assert.Equal(544, Int(spec, "InitialClientWidth"));
        Assert.Equal(320, Int(spec, "InitialClientHeight"));
        Assert.Equal(560, Int(spec, "MinimumWindowWidth"));
        Assert.Equal(306, Int(spec, "MinimumWindowHeight"));
        Assert.Equal(35, Int(spec, "IconButtonSize"));
        Assert.Equal(75, Int(spec, "FooterButtonWidth"));
        Assert.Equal(23, Int(spec, "FooterButtonHeight"));
        Assert.Equal(
            new[]
            {
                "AddIconButton",
                "RemoveIconButton",
                "MoveUpIconButton",
                "MoveDownIconButton",
                "ComponentList",
                "LayoutSettingsButton",
                "SetSizeButton",
                "HorizontalRadio",
                "VerticalRadio",
                "OK",
                "Cancel",
            },
            StringList(spec, "StructuralOrder"));

        string source = File.ReadAllText(FindRepoFile("src/LiveSplit/Avalonia/Dialogs/LayoutEditorDialog.cs"));
        Assert.DoesNotContain("var sideBar", source, StringComparison.Ordinal);
    }

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
    public void AcceptWithoutChangesDoesNotDirtyOrSaveLayout()
    {
        string path = Path.Combine(Path.GetTempPath(), $"livesplit-layout-editor-{Guid.NewGuid():N}.lsl");
        File.WriteAllText(path, "original file");

        try
        {
            var layout = CreateLayout();
            layout.FilePath = path;
            layout.HasChanged = false;

            LayoutEditorDialog.AcceptLayout(layout);

            Assert.False(layout.HasChanged);
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

    [Fact]
    public void OrientationSwitchPreservesCurrentModeSizeAndSeedsMissingTargetSizeLikeMaster()
    {
        var layout = CreateLayout();
        layout.Mode = LayoutMode.Vertical;
        layout.VerticalWidth = 250;
        layout.VerticalHeight = 700;
        layout.HorizontalWidth = LiveSplit.UI.Layout.InvalidSize;
        layout.HorizontalHeight = LiveSplit.UI.Layout.InvalidSize;

        LayoutEditorDialog.ApplyOrientationSwitch(layout, LayoutMode.Horizontal, overallSize: 480.2f);

        Assert.Equal(250, layout.VerticalWidth);
        Assert.Equal(700, layout.VerticalHeight);
        Assert.Equal(480, layout.HorizontalWidth);
        Assert.Equal(45, layout.HorizontalHeight);
        Assert.Equal(LayoutMode.Horizontal, layout.Mode);
        Assert.True(layout.HasChanged);
    }

    [Fact]
    public void SetSizeTargetsOwnerTimerWindowWhenAvailable()
    {
        string source = File.ReadAllText(FindRepoFile("src/LiveSplit/Avalonia/Dialogs/LayoutEditorDialog.cs"));

        int methodStart = source.IndexOf("private async Task SetSize()", StringComparison.Ordinal);
        Assert.True(methodStart >= 0);
        int methodEnd = source.IndexOf("internal static void AcceptLayout", methodStart, StringComparison.Ordinal);
        Assert.True(methodEnd > methodStart);
        string method = source[methodStart..methodEnd];

        Assert.Contains("Window target = Owner as Window ?? CreateSetSizeFallbackWindow", method, StringComparison.Ordinal);
        Assert.Contains("new SetSizeForm(target)", method, StringComparison.Ordinal);
    }

    [Fact]
    public void RefuseToRemoveTheLastLayoutComponentLikeMaster()
    {
        var layout = CreateLayout();
        layout.LayoutComponents.Add(new LayoutComponent("timer.dll", new StubComponent("Timer", "one")));

        Assert.False(LayoutEditorDialog.CanRemoveComponent(layout));

        layout.LayoutComponents.Add(new LayoutComponent("splits.dll", new StubComponent("Splits", "two")));

        Assert.True(LayoutEditorDialog.CanRemoveComponent(layout));
    }

    [Fact]
    public void DeactivateRemovedComponentsImmediatelyAndDisposeThemOnlyOnOk()
    {
        var removed = new TrackingComponent("Removed") { Activated = true };
        var retained = new TrackingComponent("Retained") { Activated = true };
        var layout = CreateLayout();
        layout.LayoutComponents.Add(new LayoutComponent("removed.dll", removed));
        layout.LayoutComponents.Add(new LayoutComponent("retained.dll", retained));
        var snapshot = LayoutEditorDialog.LayoutSnapshot.Capture(layout);

        Assert.True(LayoutEditorDialog.RemoveComponentAt(layout, 0));

        Assert.False(removed.Activated);
        Assert.Equal(0, removed.DisposeCount);
        Assert.Single(layout.LayoutComponents);

        snapshot.DisposeComponentsRemovedFrom(layout);

        Assert.Equal(1, removed.DisposeCount);
        Assert.Equal(0, retained.DisposeCount);
    }

    [Fact]
    public void ReorderLayoutComponentsForDragDropMoves()
    {
        var first = new TrackingComponent("First");
        var second = new TrackingComponent("Second");
        var third = new TrackingComponent("Third");
        var layout = CreateLayout();
        layout.LayoutComponents.Add(new LayoutComponent("first.dll", first));
        layout.LayoutComponents.Add(new LayoutComponent("second.dll", second));
        layout.LayoutComponents.Add(new LayoutComponent("third.dll", third));
        layout.HasChanged = false;

        Assert.True(LayoutEditorDialog.MoveComponent(layout, 0, 2));

        Assert.Collection(
            layout.LayoutComponents,
            component => Assert.Same(second, component.Component),
            component => Assert.Same(third, component.Component),
            component => Assert.Same(first, component.Component));
        Assert.True(layout.HasChanged);
    }

    [Fact]
    public void IgnoreInvalidDragDropReorderIndexes()
    {
        var only = new TrackingComponent("Only");
        var layout = CreateLayout();
        layout.LayoutComponents.Add(new LayoutComponent("only.dll", only));
        layout.HasChanged = false;

        Assert.False(LayoutEditorDialog.MoveComponent(layout, -1, 0));
        Assert.False(LayoutEditorDialog.MoveComponent(layout, 0, 2));
        Assert.False(LayoutEditorDialog.MoveComponent(layout, 0, 0));

        Assert.Same(only, Assert.Single(layout.LayoutComponents).Component);
        Assert.False(layout.HasChanged);
    }

    [Fact]
    public void ExposeComponentLoadFailureDialogText()
    {
        Assert.Equal("Error", LayoutEditorDialog.ComponentLoadFailureTitle);
        Assert.Equal("The Component could not be loaded.", LayoutEditorDialog.ComponentLoadFailureMessage);
    }

    [Fact]
    public void CancelRestoresRemovedComponentsAndDisposesNewTransientComponents()
    {
        var original = new TrackingComponent("Original") { Activated = true };
        var retained = new TrackingComponent("Retained") { Activated = true };
        var added = new TrackingComponent("Added") { Activated = true };
        var layout = CreateLayout();
        layout.LayoutComponents.Add(new LayoutComponent("original.dll", original));
        layout.LayoutComponents.Add(new LayoutComponent("retained.dll", retained));
        var snapshot = LayoutEditorDialog.LayoutSnapshot.Capture(layout);

        Assert.True(LayoutEditorDialog.RemoveComponentAt(layout, 0));
        layout.LayoutComponents.Add(new LayoutComponent("added.dll", added));

        snapshot.RestoreAfterCancel(layout);

        Assert.Collection(
            layout.LayoutComponents,
            component => Assert.Same(original, component.Component),
            component => Assert.Same(retained, component.Component));
        Assert.True(original.Activated);
        Assert.Equal(0, original.DisposeCount);
        Assert.Equal(1, added.DisposeCount);
    }

    [Fact]
    public void KeepUserFacingButtonTextReadable()
    {
        string source = File.ReadAllText(FindRepoFile("src/LiveSplit/Avalonia/Dialogs/LayoutEditorDialog.cs"));

        Assert.DoesNotContain("Ã", source, StringComparison.Ordinal);
        Assert.DoesNotContain("â", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ComponentSettingsUseCurrentLayoutMode()
    {
        var component = new ModeTrackingComponent();

        ComponentSettingsDialog.CreateSettingsControl(component, LayoutMode.Horizontal);

        Assert.Equal(LayoutMode.Horizontal, component.LastMode);
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

    private static object LayoutSpec()
    {
        Type type = Type.GetType("LiveSplit.Avalonia.Dialogs.LayoutEditorDialogLayoutSpec, LiveSplit");
        Assert.NotNull(type);
        object value = type.GetProperty("Master", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.GetValue(null);
        Assert.NotNull(value);
        return value;
    }

    private static IReadOnlyList<int> IntList(object instance, string propertyName)
        => Assert.IsAssignableFrom<IEnumerable<int>>(
            instance.GetType().GetProperty(propertyName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.GetValue(instance)).ToList();

    private static IReadOnlyList<string> StringList(object instance, string propertyName)
        => Assert.IsAssignableFrom<IEnumerable<string>>(
            instance.GetType().GetProperty(propertyName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.GetValue(instance)).ToList();

    private static int Int(object instance, string propertyName)
        => Assert.IsType<int>(
            instance.GetType().GetProperty(propertyName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.GetValue(instance));

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

    private sealed class ModeTrackingComponent : IComponent
    {
        public LayoutMode? LastMode { get; private set; }

        public string ComponentName => "Tracking";
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
        public global::Avalonia.Controls.Control GetSettingsControl(LayoutMode mode)
        {
            LastMode = mode;
            return new global::Avalonia.Controls.Panel();
        }

        public XmlNode GetSettings(XmlDocument document) => document.CreateElement("Settings");
        public void SetSettings(XmlNode settings) { }
        public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode) { }
        public void Dispose() { }
    }

    private sealed class TrackingComponent : IDeactivatableComponent
    {
        public TrackingComponent(string name)
        {
            ComponentName = name;
        }

        public int DisposeCount { get; private set; }
        public bool Activated { get; set; }
        public string ComponentName { get; }
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
        public XmlNode GetSettings(XmlDocument document) => document.CreateElement("Settings");
        public void SetSettings(XmlNode settings) { }
        public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode) { }
        public void Dispose() => DisposeCount++;
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;

using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Model.RunFactories;
using LiveSplit.Options.SettingsFactories;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using LiveSplit.UI.Drawing;
using LiveSplit.UI.Drawing.Skia;

using SkiaSharp;

using Xunit;

namespace LiveSplit.Tests.UI.Components;

[Collection("DrawingApi")]
public class ComponentRendererMust
{
    [Fact]
    public void RenderAllStackedVerticalComponentsOnSkia()
    {
        DrawingApi.Register(new SkiaDrawingFactory());
        var components = new[]
        {
            new SolidComponent(Color.FromArgb(255, 255, 0, 0), 20f),
            new SolidComponent(Color.FromArgb(255, 0, 255, 0), 200f),
            new SolidComponent(Color.FromArgb(255, 0, 0, 255), 40f),
            new SolidComponent(Color.FromArgb(255, 255, 255, 255), 30f),
        };
        var renderer = new ComponentRenderer { VisibleComponents = components };
        var settings = new StandardLayoutSettingsFactory().Create();
        var layout = new Layout
        {
            Settings = settings,
            LayoutComponents =
            [
                new LayoutComponent("red", components[0]),
                new LayoutComponent("green", components[1]),
                new LayoutComponent("blue", components[2]),
                new LayoutComponent("white", components[3]),
            ],
            Mode = LayoutMode.Vertical,
        };
        var run = new StandardRunFactory().Create(new StandardComparisonGeneratorsFactory());
        var state = new LiveSplitState(run, null, layout, settings, new StandardSettingsFactory().Create());

        renderer.CalculateOverallSize(LayoutMode.Vertical);
        using SKSurface surface = SKSurface.Create(new SKImageInfo(100, 290, SKColorType.Bgra8888, SKAlphaType.Premul));
        surface.Canvas.Clear(SKColors.Black);
        var ctx = new SkiaDrawingContext(surface.Canvas);

        renderer.Render(ctx, state, 100f, 290f, LayoutMode.Vertical);

        using SKImage image = surface.Snapshot();
        using SKBitmap bitmap = SKBitmap.FromImage(image);
        AssertPixel(bitmap, 10, 10, new SKColor(255, 0, 0));
        AssertPixel(bitmap, 10, 30, new SKColor(0, 255, 0));
        AssertPixel(bitmap, 10, 230, new SKColor(0, 0, 255));
        AssertPixel(bitmap, 10, 275, new SKColor(255, 255, 255));
    }

    [Fact]
    public void RenderGraphSeparatorFullDevicePixelWhenScaled()
    {
        DrawingApi.Register(new SkiaDrawingFactory());
        var separator = new GraphSeparatorComponent(new GraphSettings())
        {
            LockToBottom = true,
        };
        var components = new IComponent[]
        {
            new SolidComponent(Color.Black, 1f),
            separator,
        };
        var renderer = new ComponentRenderer { VisibleComponents = components };
        var settings = new StandardLayoutSettingsFactory().Create();
        var layout = new Layout
        {
            Settings = settings,
            LayoutComponents =
            [
                new LayoutComponent("black", components[0]),
                new LayoutComponent("separator", components[1]),
            ],
            Mode = LayoutMode.Vertical,
        };
        var run = new StandardRunFactory().Create(new StandardComparisonGeneratorsFactory());
        var state = new LiveSplitState(run, null, layout, settings, new StandardSettingsFactory().Create());

        renderer.CalculateOverallSize(LayoutMode.Vertical);
        using SKSurface surface = SKSurface.Create(new SKImageInfo(15, 3, SKColorType.Bgra8888, SKAlphaType.Premul));
        surface.Canvas.Clear(SKColors.Transparent);
        var ctx = new SkiaDrawingContext(surface.Canvas);
        ctx.ScaleTransform(1.5f, 1.5f);

        renderer.Render(ctx, state, 10f, 2f, LayoutMode.Vertical);

        using SKImage image = surface.Snapshot();
        using SKBitmap bitmap = SKBitmap.FromImage(image);
        AssertPixel(bitmap, 7, 1, SKColors.White);
        AssertPixel(bitmap, 7, 2, SKColors.White);
    }

    private static void AssertPixel(SKBitmap bitmap, int x, int y, SKColor expected)
        => Assert.Equal(expected, bitmap.GetPixel(x, y));

    private sealed class SolidComponent(Color color, float height) : IComponent
    {
        public string ComponentName => "Solid";
        public float PaddingTop => 0f;
        public float PaddingLeft => 0f;
        public float PaddingBottom => 0f;
        public float PaddingRight => 0f;
        public float VerticalHeight => height;
        public float MinimumWidth => 1f;
        public float HorizontalWidth => 1f;
        public float MinimumHeight => 1f;
        public IDictionary<string, Action> ContextMenuControls => null;

        public void DrawVertical(IDrawingContext ctx, LiveSplitState state, float width)
        {
            using ISolidBrush brush = DrawingApi.Factory.CreateSolidBrush(color);
            ctx.FillRectangle(brush, 0f, 0f, width, height);
        }

        public void DrawHorizontal(IDrawingContext ctx, LiveSplitState state, float height)
        {
        }

        public global::Avalonia.Controls.Control GetSettingsControl(LayoutMode mode) => null;
        public void SetSettings(System.Xml.XmlNode settings) { }
        public System.Xml.XmlNode GetSettings(System.Xml.XmlDocument document) => document.CreateElement("Settings");
        public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode) { }
        public void Dispose() { }
        public int GetSettingsHashCode() => 0;
    }
}

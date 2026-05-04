using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Drawing.Text;
using System.Numerics;
using System.Xml;

using global::Avalonia.Controls;

using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Model.RunFactories;
using LiveSplit.Options.SettingsFactories;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using LiveSplit.UI.Drawing;

using SkiaSharp;

using Xunit;

namespace LiveSplit.Tests.UI.Components;

public class VideoComponentMust
{
    [Fact]
    public void RoundTripSettings()
    {
        var settings = new VideoSettings
        {
            VideoPath = @"C:\runs\pb.mp4",
            Offset = TimeSpan.FromSeconds(12.34),
            Height = 240,
            Width = 320
        };

        var document = new XmlDocument();
        var loaded = new VideoSettings();

        loaded.SetSettings(settings.GetSettings(document));

        Assert.Equal(settings.VideoPath, loaded.VideoPath);
        Assert.Equal(settings.Offset, loaded.Offset);
        Assert.Equal(settings.Height, loaded.Height);
        Assert.Equal(settings.Width, loaded.Width);
    }

    [Fact]
    public void LoadPlayAndSynchronizeWhenTimerStarts()
    {
        LiveSplitState state = CreateState();
        var player = new FakeVideoPlayer();
        var component = new VideoComponent(state, player);
        var document = new XmlDocument();
        XmlElement settings = document.CreateElement("Settings");
        SettingsHelper.CreateSetting(document, settings, "VideoPath", @"C:\runs\pb.mp4");
        SettingsHelper.CreateSetting(document, settings, "Offset", "0:00:03.000");
        SettingsHelper.CreateSetting(document, settings, "Height", 240f);
        SettingsHelper.CreateSetting(document, settings, "Width", 320f);
        component.SetSettings(settings);

        var model = new TimerModel { CurrentState = state };
        model.Start();

        Assert.Equal(@"C:\runs\pb.mp4", player.LoadedPath);
        Assert.True(player.Played);
        Assert.True(
            Math.Abs((player.LastTime.Value - TimeSpan.FromSeconds(3)).TotalMilliseconds) < 50,
            $"Expected synchronization near 3 seconds, got {player.LastTime}.");
    }

    [Fact]
    public void PauseResumeAndStopWithTimerState()
    {
        LiveSplitState state = CreateState();
        var player = new FakeVideoPlayer();
        var component = new VideoComponent(state, player);
        var model = new TimerModel { CurrentState = state };

        model.Start();
        model.Pause();
        model.Pause();
        model.Reset(false);

        Assert.True(player.Paused);
        Assert.True(player.Played);
        Assert.True(player.Stopped);
        component.Dispose();
        Assert.True(player.Disposed);
    }

    [Fact]
    public void DoesNotContinuouslySeekWhenVideoTimeIsWithinTolerance()
    {
        LiveSplitState state = CreateState();
        var player = new FakeVideoPlayer();
        var component = new VideoComponent(state, player);
        var document = new XmlDocument();
        XmlElement settings = document.CreateElement("Settings");
        SettingsHelper.CreateSetting(document, settings, "VideoPath", @"C:\runs\pb.mp4");
        SettingsHelper.CreateSetting(document, settings, "Offset", "0:00:00.000");
        component.SetSettings(settings);

        var model = new TimerModel { CurrentState = state };
        model.Start();
        state.AdjustedStartTime = TimeStamp.Now - TimeSpan.FromSeconds(3);
        player.CurrentTime = TimeSpan.FromSeconds(3.1);

        component.Update(null, state, 320, 240, LayoutMode.Vertical);

        Assert.Equal(1, player.SetTimeCount);
    }

    [Fact]
    public void DrawCurrentVideoFrameWhenPlayerHasDecodedFrame()
    {
        LiveSplitState state = CreateState();
        var frame = new FakeImage(16, 9);
        var player = new FakeVideoPlayer { CurrentFrame = frame };
        var component = new VideoComponent(state, player);
        var model = new TimerModel { CurrentState = state };
        var ctx = new RecordingDrawingContext();

        model.Start();
        component.DrawHorizontal(ctx, state, 90);

        Assert.Same(frame, ctx.DrawnImage);
        Assert.Equal(new RectangleF(0, 0, component.HorizontalWidth, 90), ctx.DrawnDestination);
        Assert.Equal(0, ctx.FilledRectangles);
    }

    [Fact]
    public void SettingsControlUsesMasterVideoPickerAndVerticalHeightSlider()
    {
        LiveSplitState state = CreateState();
        var component = new VideoComponent(state, new FakeVideoPlayer());

        Control control = component.GetSettingsControl(LayoutMode.Vertical);

        Assert.NotNull(FindNamed<TextBox>(control, "VideoPathTextBox"));
        Assert.NotNull(FindNamed<Button>(control, "BrowseVideoButton"));
        Assert.NotNull(FindNamed<TextBox>(control, "OffsetTextBox"));
        Assert.NotNull(FindNamed<Slider>(control, "HeightSlider"));
        Assert.Null(FindNamed<Slider>(control, "WidthSlider"));
    }

    [Fact]
    public void SettingsControlUsesMasterVideoPickerAndHorizontalWidthSlider()
    {
        LiveSplitState state = CreateState();
        var component = new VideoComponent(state, new FakeVideoPlayer());

        Control control = component.GetSettingsControl(LayoutMode.Horizontal);

        Assert.NotNull(FindNamed<TextBox>(control, "VideoPathTextBox"));
        Assert.NotNull(FindNamed<Button>(control, "BrowseVideoButton"));
        Assert.NotNull(FindNamed<TextBox>(control, "OffsetTextBox"));
        Assert.NotNull(FindNamed<Slider>(control, "WidthSlider"));
        Assert.Null(FindNamed<Slider>(control, "HeightSlider"));
    }

    [Fact]
    public void SettingsControlUpdatesPathOffsetAndModeSpecificSize()
    {
        LiveSplitState state = CreateState();
        var component = new VideoComponent(state, new FakeVideoPlayer());
        Control control = component.GetSettingsControl(LayoutMode.Vertical);

        FindNamed<TextBox>(control, "VideoPathTextBox")!.Text = @"C:\runs\video.mp4";
        FindNamed<TextBox>(control, "OffsetTextBox")!.Text = "0:00:05.000";
        FindNamed<Slider>(control, "HeightSlider")!.Value = 240;

        Assert.Equal(@"C:\runs\video.mp4", component.Settings.VideoPath);
        Assert.Equal(TimeSpan.FromSeconds(5), component.Settings.Offset);
        Assert.Equal(240, component.Settings.Height);
    }

    [Fact]
    public void ConvertReceivedLibVlcFrameIntoSharedDrawingImage()
    {
        IDrawingFactory previousFactory = null;
        try
        {
            previousFactory = DrawingApi.Factory;
        }
        catch (InvalidOperationException)
        {
        }

        var factory = new RecordingDrawingFactory();
        DrawingApi.Register(factory);
        using var buffer = new LibVlcVideoFrameBuffer();
        IntPtr chroma = Marshal.AllocHGlobal(4);
        IntPtr planes = Marshal.AllocHGlobal(IntPtr.Size);

        try
        {
            IntPtr opaque = IntPtr.Zero;
            uint width = 2;
            uint height = 1;
            uint pitch = 0;
            uint lines = 0;

            Assert.Equal(1u, buffer.Format(ref opaque, chroma, ref width, ref height, ref pitch, ref lines));
            IntPtr picture = buffer.Lock(IntPtr.Zero, planes);
            var pixels = new byte[pitch * lines];
            pixels[0] = 0x10;
            pixels[1] = 0x20;
            pixels[2] = 0x30;
            pixels[3] = 0xFF;
            pixels[4] = 0x40;
            pixels[5] = 0x50;
            pixels[6] = 0x60;
            pixels[7] = 0xFF;
            Marshal.Copy(pixels, 0, picture, pixels.Length);

            buffer.Display(IntPtr.Zero, picture);

            Assert.Same(factory.LoadedImage, buffer.CurrentFrame);
            Assert.True(
                factory.LoadedBytes.Take(4).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47 }),
                "Expected the received video frame to be encoded through the PNG image path.");
            using SKBitmap decoded = SKBitmap.Decode(factory.LoadedBytes);
            Assert.Equal(new SKColor(0x30, 0x20, 0x10, 0xFF), decoded.GetPixel(0, 0));
            Assert.Equal(new SKColor(0x60, 0x50, 0x40, 0xFF), decoded.GetPixel(1, 0));
        }
        finally
        {
            Marshal.FreeHGlobal(chroma);
            Marshal.FreeHGlobal(planes);
            if (previousFactory != null)
            {
                DrawingApi.Register(previousFactory);
            }
        }
    }

    private static LiveSplitState CreateState()
    {
        IRun run = new StandardRunFactory().Create(new StandardComparisonGeneratorsFactory());
        return new LiveSplitState(
            run,
            null,
            new Layout(),
            new StandardLayoutSettingsFactory().Create(),
            new StandardSettingsFactory().Create())
        {
            CurrentComparison = Run.PersonalBestComparisonName,
            CurrentTimingMethod = TimingMethod.RealTime
        };
    }

    private static T FindNamed<T>(Control root, string name)
        where T : Control
    {
        if (root is T typed && root.Name == name)
        {
            return typed;
        }

        if (root is Decorator decorator && decorator.Child is Control decoratorChild)
        {
            T found = FindNamed<T>(decoratorChild, name);
            if (found is not null)
            {
                return found;
            }
        }

        if (root is ContentControl contentControl && contentControl.Content is Control content)
        {
            T found = FindNamed<T>(content, name);
            if (found is not null)
            {
                return found;
            }
        }

        if (root is Panel panel)
        {
            foreach (Control child in panel.Children.OfType<Control>())
            {
                T found = FindNamed<T>(child, name);
                if (found is not null)
                {
                    return found;
                }
            }
        }

        if (root is ItemsControl itemsControl)
        {
            foreach (object item in itemsControl.Items)
            {
                if (item is Control itemControl)
                {
                    T found = FindNamed<T>(itemControl, name);
                    if (found is not null)
                    {
                        return found;
                    }
                }
            }
        }

        return null;
    }

    private sealed class FakeVideoPlayer : IVideoPlayer, IVideoFrameSource
    {
        public string LoadedPath { get; private set; }
        public TimeSpan? LastTime { get; private set; }
        public TimeSpan CurrentTime { get; set; }
        public int SetTimeCount { get; private set; }
        public bool Played { get; private set; }
        public bool Paused { get; private set; }
        public bool Stopped { get; private set; }
        public bool Disposed { get; private set; }
        public IImage CurrentFrame { get; set; }

        public void Load(string path)
        {
            LoadedPath = path;
        }

        public void Play()
        {
            Played = true;
        }

        public void Pause()
        {
            Paused = true;
        }

        public void Stop()
        {
            Stopped = true;
        }

        public void SetTime(TimeSpan time)
        {
            LastTime = time;
            CurrentTime = time;
            SetTimeCount++;
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }

    private sealed class FakeImage(int width, int height) : IImage
    {
        public int Width { get; } = width;
        public int Height { get; } = height;

        public void Dispose()
        {
        }
    }

    private sealed class RecordingDrawingFactory : IDrawingFactory
    {
        public FakeImage LoadedImage { get; } = new(2, 1);
        public byte[] LoadedBytes { get; private set; }

        public ISolidBrush CreateSolidBrush(Color color) => throw new NotSupportedException();
        public ILinearGradientBrush CreateLinearGradientBrush(PointF start, PointF end, Color startColor, Color endColor)
            => throw new NotSupportedException();
        public IPen CreatePen(Color color, float width) => throw new NotSupportedException();
        public IFont CreateFont(string familyName, float size, FontStyle style, GraphicsUnit unit)
            => throw new NotSupportedException();
        public IImage CreateImage(int width, int height) => throw new NotSupportedException();

        public IImage LoadImage(Stream stream)
        {
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            LoadedBytes = memory.ToArray();
            return LoadedImage;
        }

        public IGraphicsPath CreateGraphicsPath() => throw new NotSupportedException();
        public ITextFormat CreateTextFormat() => throw new NotSupportedException();
    }

    private sealed class RecordingDrawingContext : IDrawingContext
    {
        public IImage DrawnImage { get; private set; }
        public RectangleF DrawnDestination { get; private set; }
        public int FilledRectangles { get; private set; }

        public SmoothingMode SmoothingMode { get; set; }
        public TextRenderingHint TextRenderingHint { get; set; }
        public InterpolationMode InterpolationMode { get; set; }
        public CompositingQuality CompositingQuality { get; set; }
        public CompositingMode CompositingMode { get; set; }
        public PixelOffsetMode PixelOffsetMode { get; set; }
        public float DpiX => 96;
        public float DpiY => 96;

        public void FillRectangle(IBrush brush, RectangleF rect) => FilledRectangles++;
        public void FillRectangle(IBrush brush, float x, float y, float width, float height) => FilledRectangles++;
        public void DrawRectangle(IPen pen, RectangleF rect) => throw new NotSupportedException();
        public void DrawLine(IPen pen, PointF p1, PointF p2) => throw new NotSupportedException();
        public void FillPolygon(IBrush brush, PointF[] points) => throw new NotSupportedException();
        public void FillEllipse(IBrush brush, float x, float y, float width, float height) => throw new NotSupportedException();

        public void DrawImage(IImage image, RectangleF destRect)
        {
            DrawnImage = image;
            DrawnDestination = destRect;
        }

        public void DrawImage(IImage image, Rectangle destRect, Rectangle srcRect)
            => DrawImage(image, new RectangleF(destRect.X, destRect.Y, destRect.Width, destRect.Height));

        public void DrawImageWithOpacity(IImage image, Rectangle destRect, Rectangle srcRect, float opacity, float blurSigma = 0f)
            => DrawImage(image, destRect, srcRect);

        public void DrawImageWithOpacity(IImage image, RectangleF destRect, RectangleF srcRect, float opacity, float blurSigma = 0f)
            => DrawImage(image, destRect);

        public void DrawString(string text, IFont font, IBrush brush, RectangleF bounds, ITextFormat format)
            => throw new NotSupportedException();

        public SizeF MeasureString(string text, IFont font, int maxWidth, ITextFormat format)
            => throw new NotSupportedException();

        public void FillPath(IBrush brush, IGraphicsPath path) => throw new NotSupportedException();
        public void DrawPath(IPen pen, IGraphicsPath path) => throw new NotSupportedException();
        public void TranslateTransform(float dx, float dy) => throw new NotSupportedException();
        public void ScaleTransform(float sx, float sy) => throw new NotSupportedException();
        public void ResetTransform() => throw new NotSupportedException();
        public Matrix3x2 GetTransform() => Matrix3x2.Identity;
        public void ClearClip() => throw new NotSupportedException();
        public void SetClip(RectangleF rect) => throw new NotSupportedException();
        public void IntersectClip(RectangleF rect) => throw new NotSupportedException();
        public bool IsVisible(RectangleF rect) => true;
        public IDrawingState Save() => throw new NotSupportedException();
    }
}

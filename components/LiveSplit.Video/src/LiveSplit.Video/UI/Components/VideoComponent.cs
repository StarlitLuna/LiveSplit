using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Xml;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;
using global::Avalonia.Platform.Storage;

using LibVLCSharp.Shared;

using LiveSplit.Model;
using LiveSplit.Options;
using LiveSplit.UI.Drawing;

using SkiaSharp;

namespace LiveSplit.UI.Components;

public interface IVideoPlayer : IDisposable
{
    TimeSpan CurrentTime { get; }
    void Load(string path);
    void Play();
    void Pause();
    void Stop();
    void SetTime(TimeSpan time);
}

public interface IVideoFrameSource
{
    IImage CurrentFrame { get; }
}

public class VideoComponent : IComponent, IDeactivatableComponent
{
    private const int SyncToleranceMilliseconds = 500;

    private readonly LiveSplitState _state;
    private readonly IVideoPlayer _player;
    private string _loadedPath;
    private bool _isVisible;

    public VideoSettings Settings { get; set; }
    public bool Activated { get; set; }

    public string ComponentName => "Video";
    public float HorizontalWidth => Settings.Width;
    public float MinimumHeight => 10;
    public float VerticalHeight => Settings.Height;
    public float MinimumWidth => 10;
    public float PaddingTop => 0;
    public float PaddingLeft => 0;
    public float PaddingBottom => 0;
    public float PaddingRight => 0;
    public System.Collections.Generic.IDictionary<string, Action> ContextMenuControls => null;

    public VideoComponent(LiveSplitState state)
        : this(state, new LibVlcVideoPlayer())
    {
    }

    public VideoComponent(LiveSplitState state, IVideoPlayer player)
    {
        _state = state;
        _player = player ?? throw new ArgumentNullException(nameof(player));
        Settings = new VideoSettings();
        Activated = true;

        _state.OnReset += State_OnReset;
        _state.OnStart += State_OnStart;
        _state.OnPause += State_OnPause;
        _state.OnResume += State_OnResume;
    }

    public Avalonia.Controls.Control GetSettingsControl(LayoutMode mode)
    {
        Settings.Mode = mode;
        return VideoSettingsControl.Build(Settings);
    }

    public XmlNode GetSettings(XmlDocument document)
        => Settings.GetSettings(document);

    public void SetSettings(XmlNode settings)
        => Settings.SetSettings(settings);

    public void DrawVertical(IDrawingContext ctx, LiveSplitState state, float width)
        => Draw(ctx, width, VerticalHeight);

    public void DrawHorizontal(IDrawingContext ctx, LiveSplitState state, float height)
        => Draw(ctx, HorizontalWidth, height);

    public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
    {
        if (!Activated || string.IsNullOrWhiteSpace(Settings.VideoPath))
        {
            return;
        }

        EnsureLoaded();
        if (state.CurrentPhase == TimerPhase.Running || state.CurrentPhase == TimerPhase.Paused)
        {
            Synchronize();
        }
    }

    public void Dispose()
    {
        _state.OnReset -= State_OnReset;
        _state.OnStart -= State_OnStart;
        _state.OnPause -= State_OnPause;
        _state.OnResume -= State_OnResume;
        _player.Dispose();
    }

    public int GetSettingsHashCode()
        => Settings.GetSettingsHashCode();

    private void State_OnStart(object sender, EventArgs e)
    {
        EnsureLoaded();
        _player.Play();
        _isVisible = true;
        Synchronize(force: true);
    }

    private void State_OnPause(object sender, EventArgs e)
        => _player.Pause();

    private void State_OnResume(object sender, EventArgs e)
    {
        _player.Play();
        Synchronize();
    }

    private void State_OnReset(object sender, TimerPhase e)
    {
        _player.Stop();
        _isVisible = false;
    }

    private void EnsureLoaded()
    {
        if (_loadedPath == Settings.VideoPath || string.IsNullOrWhiteSpace(Settings.VideoPath))
        {
            return;
        }

        _player.Load(Settings.VideoPath);
        _loadedPath = Settings.VideoPath;
    }

    private void Synchronize(bool force = false)
    {
        TimeSpan target = (_state.CurrentTime[TimingMethod.RealTime] ?? TimeSpan.Zero) + Settings.Offset;
        TimeSpan drift = target - _player.CurrentTime;
        if (force || Math.Abs(drift.TotalMilliseconds) > SyncToleranceMilliseconds)
        {
            _player.SetTime(target);
        }
    }

    private void Draw(IDrawingContext ctx, float width, float height)
    {
        if (!_isVisible || !Activated)
        {
            return;
        }

        if (_player is IVideoFrameSource frameSource && frameSource.CurrentFrame is IImage frame)
        {
            ctx.DrawImage(frame, new RectangleF(0, 0, width, height));
            return;
        }

        using ISolidBrush brush = DrawingApi.Factory.CreateSolidBrush(Color.Black);
        ctx.FillRectangle(brush, 0, 0, width, height);
    }
}

internal static class VideoSettingsControl
{
    public static Control Build(VideoSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var videoPathBox = new TextBox
        {
            Name = "VideoPathTextBox",
            Text = settings.VideoPath ?? string.Empty,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        videoPathBox.PropertyChanged += (_, args) =>
        {
            if (args.Property == TextBox.TextProperty)
            {
                settings.VideoPath = videoPathBox.Text ?? string.Empty;
            }
        };

        var browseButton = new Button
        {
            Name = "BrowseVideoButton",
            Content = "Browse...",
            Width = 75,
        };
        browseButton.Click += async (_, _) => await BrowseVideo(settings, videoPathBox, browseButton);

        var offsetBox = new TextBox
        {
            Name = "OffsetTextBox",
            Text = settings.OffsetString,
            TextAlignment = global::Avalonia.Media.TextAlignment.Right,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        offsetBox.PropertyChanged += (_, args) =>
        {
            if (args.Property == TextBox.TextProperty)
            {
                settings.OffsetString = offsetBox.Text ?? string.Empty;
            }
        };
        offsetBox.LostFocus += (_, _) => offsetBox.Text = settings.OffsetString;

        Slider sizeSlider = BuildSizeSlider(settings);

        var grid = new Grid
        {
            Margin = new Thickness(7),
            ColumnDefinitions = new ColumnDefinitions("82,*,81"),
            RowDefinitions = new RowDefinitions("29,29,36"),
        };

        AddLabel(grid, "Video Path:", row: 0);
        Grid.SetColumn(videoPathBox, 1);
        Grid.SetRow(videoPathBox, 0);
        Grid.SetColumn(browseButton, 2);
        Grid.SetRow(browseButton, 0);
        grid.Children.Add(videoPathBox);
        grid.Children.Add(browseButton);

        AddLabel(grid, "Run Starts At:", row: 1);
        Grid.SetColumn(offsetBox, 1);
        Grid.SetColumnSpan(offsetBox, 2);
        Grid.SetRow(offsetBox, 1);
        grid.Children.Add(offsetBox);

        AddLabel(grid, settings.Mode == LayoutMode.Horizontal ? "Width:" : "Height:", row: 2);
        Grid.SetColumn(sizeSlider, 1);
        Grid.SetColumnSpan(sizeSlider, 2);
        Grid.SetRow(sizeSlider, 2);
        grid.Children.Add(sizeSlider);

        return new ScrollViewer
        {
            HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = grid,
        };
    }

    private static Slider BuildSizeSlider(VideoSettings settings)
    {
        bool horizontal = settings.Mode == LayoutMode.Horizontal;
        var slider = new Slider
        {
            Name = horizontal ? "WidthSlider" : "HeightSlider",
            Minimum = 100,
            Maximum = horizontal ? 400 : 300,
            Value = horizontal ? settings.Width : settings.Height,
            TickFrequency = 1,
            IsSnapToTickEnabled = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        slider.PropertyChanged += (_, args) =>
        {
            if (args.Property != global::Avalonia.Controls.Primitives.RangeBase.ValueProperty)
            {
                return;
            }

            if (horizontal)
            {
                settings.Width = (float)slider.Value;
            }
            else
            {
                settings.Height = (float)slider.Value;
            }
        };

        return slider;
    }

    private static void AddLabel(Grid grid, string text, int row)
    {
        var label = new TextBlock
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(label, 0);
        Grid.SetRow(label, row);
        grid.Children.Add(label);
    }

    private static async Task BrowseVideo(VideoSettings settings, TextBox videoPathBox, Control ownerControl)
    {
        TopLevel top = TopLevel.GetTopLevel(ownerControl);
        if (top is null)
        {
            return;
        }

        IReadOnlyList<IStorageFile> files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Video",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Video Files")
                {
                    Patterns = ["*.avi", "*.mpeg", "*.mpg", "*.mp4", "*.mov", "*.wmv", "*.m4v", "*.flv", "*.mkv", "*.ogg"],
                },
                FilePickerFileTypes.All,
            ],
        });

        IStorageFile picked = files?.FirstOrDefault();
        if (picked?.Path?.LocalPath is not { Length: > 0 } path)
        {
            return;
        }

        settings.VideoPath = path;
        videoPathBox.Text = path;
    }
}

internal sealed class LibVlcVideoPlayer : IVideoPlayer, IVideoFrameSource
{
    private readonly object _sync = new();
    private readonly LibVlcVideoFrameBuffer _frames = new();
    private LibVLC _libVlc;
    private MediaPlayer _player;
    private Media _media;

    public TimeSpan CurrentTime
    {
        get
        {
            lock (_sync)
            {
                return TimeSpan.FromMilliseconds(Math.Max(0, _player?.Time ?? 0));
            }
        }
    }

    public IImage CurrentFrame => _frames.CurrentFrame;

    public void Load(string path)
    {
        try
        {
            lock (_sync)
            {
                EnsurePlayer();
                _media?.Dispose();
                Uri uri = File.Exists(path) ? new Uri(Path.GetFullPath(path)) : new Uri(path, UriKind.RelativeOrAbsolute);
                _media = new Media(_libVlc, uri);
                _player.Media = _media;
                _player.Mute = true;
            }
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
    }

    public void Play()
    {
        try
        {
            lock (_sync)
            {
                EnsurePlayer();
                _player.Play();
            }
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
    }

    public void Pause()
    {
        try
        {
            lock (_sync)
            {
                _player?.Pause();
            }
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
    }

    public void Stop()
    {
        try
        {
            lock (_sync)
            {
                _player?.Stop();
            }
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
    }

    public void SetTime(TimeSpan time)
    {
        try
        {
            lock (_sync)
            {
                EnsurePlayer();
                _player.Time = Math.Max(0, (long)time.TotalMilliseconds);
            }
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _media?.Dispose();
            _media = null;
            _player?.Dispose();
            _player = null;
            _libVlc?.Dispose();
            _libVlc = null;
            _frames.Dispose();
        }
    }

    private void EnsurePlayer()
    {
        if (_player != null)
        {
            return;
        }

        string libVlcDirectory = LibVlcRuntime.FindWindowsLibVlcDirectory();
        if (libVlcDirectory is null)
        {
            Core.Initialize();
        }
        else
        {
            Core.Initialize(libVlcDirectory);
        }
        _libVlc = new LibVLC();
        _player = new MediaPlayer(_libVlc)
        {
            Mute = true
        };
        _player.SetVideoFormatCallbacks(_frames.Format, _frames.Cleanup);
        _player.SetVideoCallbacks(_frames.Lock, _frames.Unlock, _frames.Display);
    }
}

internal sealed class LibVlcVideoFrameBuffer : IDisposable
{
    private readonly object _sync = new();
    private byte[] _pixels;
    private GCHandle _pixelsHandle;
    private uint _width;
    private uint _height;
    private uint _pitch;
    private IImage _currentFrame;

    public IImage CurrentFrame
    {
        get
        {
            lock (_sync)
            {
                return _currentFrame;
            }
        }
    }

    public uint Format(ref IntPtr opaque, IntPtr chroma, ref uint width, ref uint height, ref uint pitches, ref uint lines)
    {
        byte[] rv32 = [(byte)'R', (byte)'V', (byte)'3', (byte)'2'];
        Marshal.Copy(rv32, 0, chroma, rv32.Length);

        uint pitch = Align(width * 4, 32);
        uint lineCount = Align(height, 32);

        pitches = pitch;
        lines = lineCount;

        lock (_sync)
        {
            Allocate(width, height, pitch, lineCount);
        }

        return 1;
    }

    public void Cleanup(ref IntPtr opaque)
    {
    }

    public IntPtr Lock(IntPtr opaque, IntPtr planes)
    {
        lock (_sync)
        {
            if (!_pixelsHandle.IsAllocated)
            {
                return IntPtr.Zero;
            }

            IntPtr buffer = _pixelsHandle.AddrOfPinnedObject();
            Marshal.WriteIntPtr(planes, buffer);
            return buffer;
        }
    }

    public void Unlock(IntPtr opaque, IntPtr picture, IntPtr planes)
    {
    }

    public void Display(IntPtr opaque, IntPtr picture)
    {
        byte[] snapshot;
        uint width;
        uint height;
        uint pitch;

        lock (_sync)
        {
            if (_pixels is null || _width == 0 || _height == 0)
            {
                return;
            }

            snapshot = (byte[])_pixels.Clone();
            width = _width;
            height = _height;
            pitch = _pitch;
        }

        IImage image = CreateImage(snapshot, width, height, pitch);
        lock (_sync)
        {
            IImage previous = _currentFrame;
            _currentFrame = image;
            previous?.Dispose();
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _currentFrame?.Dispose();
            _currentFrame = null;
            if (_pixelsHandle.IsAllocated)
            {
                _pixelsHandle.Free();
            }

            _pixels = null;
            _width = 0;
            _height = 0;
            _pitch = 0;
        }
    }

    private void Allocate(uint width, uint height, uint pitch, uint lines)
    {
        int byteCount = checked((int)(pitch * lines));
        if (_pixels != null && _pixels.Length == byteCount && _width == width && _height == height && _pitch == pitch)
        {
            return;
        }

        if (_pixelsHandle.IsAllocated)
        {
            _pixelsHandle.Free();
        }

        _pixels = new byte[byteCount];
        _pixelsHandle = GCHandle.Alloc(_pixels, GCHandleType.Pinned);
        _width = width;
        _height = height;
        _pitch = pitch;
    }

    private static IImage CreateImage(byte[] pixels, uint width, uint height, uint pitch)
    {
        var info = new SKImageInfo(checked((int)width), checked((int)height), SKColorType.Bgra8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);

        unsafe
        {
            fixed (byte* sourceBase = pixels)
            {
                for (int y = 0; y < info.Height; y++)
                {
                    byte* source = sourceBase + checked((int)(y * pitch));
                    void* dest = bitmap.GetAddress(0, y).ToPointer();
                    Buffer.MemoryCopy(source, dest, info.RowBytes, info.RowBytes);
                }
            }
        }

        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream(data.ToArray());
        return DrawingApi.Factory.LoadImage(stream);
    }

    private static uint Align(uint value, uint alignment)
        => ((value + alignment - 1) / alignment) * alignment;
}

internal static class LibVlcRuntime
{
    public static string FindWindowsLibVlcDirectory()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return null;
        }

        string rid = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X86 => "win-x86",
            Architecture.X64 => "win-x64",
            Architecture.Arm64 => "win-arm64",
            _ => null
        };

        if (rid is null)
        {
            return null;
        }

        string baseDirectory = AppContext.BaseDirectory;
        string[] candidates =
        [
            Path.Combine(baseDirectory, "Components", "runtimes", rid, "libvlc", rid),
            Path.Combine(baseDirectory, "Components", "libvlc", rid),
            Path.Combine(baseDirectory, "libvlc", rid)
        ];

        foreach (string candidate in candidates)
        {
            if (File.Exists(Path.Combine(candidate, "libvlc.dll"))
                && File.Exists(Path.Combine(candidate, "libvlccore.dll")))
            {
                return candidate;
            }
        }

        return null;
    }
}

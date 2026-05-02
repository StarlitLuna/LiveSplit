using System;
using System.Drawing;
using System.IO;
using System.Xml;

using LibVLCSharp.Shared;

using LiveSplit.Model;
using LiveSplit.Options;
using LiveSplit.UI.Drawing;

namespace LiveSplit.UI.Components;

public interface IVideoPlayer : IDisposable
{
    void Load(string path);
    void Play();
    void Pause();
    void Stop();
    void SetTime(TimeSpan time);
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
        return AvaloniaSettingsBuilder.Build(Settings, ComponentName);
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
        if (force || Math.Abs(target.TotalMilliseconds) > SyncToleranceMilliseconds)
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

        using ISolidBrush brush = DrawingApi.Factory.CreateSolidBrush(Color.Black);
        ctx.FillRectangle(brush, 0, 0, width, height);
    }
}

internal sealed class LibVlcVideoPlayer : IVideoPlayer
{
    private readonly object _sync = new();
    private LibVLC _libVlc;
    private MediaPlayer _player;
    private Media _media;

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
        }
    }

    private void EnsurePlayer()
    {
        if (_player != null)
        {
            return;
        }

        Core.Initialize();
        _libVlc = new LibVLC();
        _player = new MediaPlayer(_libVlc)
        {
            Mute = true
        };
    }
}

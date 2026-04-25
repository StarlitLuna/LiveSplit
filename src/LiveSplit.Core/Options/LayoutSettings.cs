using System;
using System.Drawing;
using System.IO;

using LiveSplit.UI;
using LiveSplit.UI.Drawing;

namespace LiveSplit.Options;

public class LayoutSettings : ICloneable
{
    public Color TextColor { get; set; }
    public Color BackgroundColor { get; set; }
    public Color BackgroundColor2 { get; set; }
    public Color ThinSeparatorsColor { get; set; }
    public Color SeparatorsColor { get; set; }
    public Color PersonalBestColor { get; set; }
    public Color AheadGainingTimeColor { get; set; }
    public Color AheadLosingTimeColor { get; set; }
    public Color BehindGainingTimeColor { get; set; }
    public Color BehindLosingTimeColor { get; set; }
    public Color BestSegmentColor { get; set; }
    public Color NotRunningColor { get; set; }
    public Color PausedColor { get; set; }
    public Color TextOutlineColor { get; set; }
    public Color ShadowsColor { get; set; }

    public BackgroundType BackgroundType { get; set; }

    private byte[] _backgroundImage;
    private IImage _cachedBackgroundImage;

    /// <summary>Encoded image bytes (PNG/JPEG). Stored opaquely so the data type stays cross-platform;
    /// rendering decodes via SkiaSharp when the layout draws.</summary>
    public byte[] BackgroundImage
    {
        get => _backgroundImage;
        set
        {
            if (!ReferenceEquals(_backgroundImage, value))
            {
                _cachedBackgroundImage?.Dispose();
                _cachedBackgroundImage = null;
            }

            _backgroundImage = value;
        }
    }

    /// <summary>Lazily-decoded <see cref="IImage"/> for <see cref="BackgroundImage"/>. The
    /// renderer reads this on each frame; the decode happens once and caches until
    /// <see cref="BackgroundImage"/> is reassigned. Returns null if the bytes are missing or
    /// fail to decode (any IO/decoder error gets logged and the cache stays null so we don't
    /// hot-loop on a broken image).</summary>
    public IImage GetCachedBackgroundImage()
    {
        if (_cachedBackgroundImage != null)
        {
            return _cachedBackgroundImage;
        }

        if (_backgroundImage is null || _backgroundImage.Length == 0)
        {
            return null;
        }

        try
        {
            using var stream = new MemoryStream(_backgroundImage);
            _cachedBackgroundImage = DrawingApi.Factory.LoadImage(stream);
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            _cachedBackgroundImage = null;
        }

        return _cachedBackgroundImage;
    }

    public float ImageOpacity { get; set; }
    public float ImageBlur { get; set; }

    public FontDescriptor TimerFont { get; set; }
    public FontDescriptor TimesFont { get; set; }
    public FontDescriptor TextFont { get; set; }

    public bool ShowBestSegments { get; set; }
    public bool AlwaysOnTop { get; set; }
    public bool AntiAliasing { get; set; }
    public bool DropShadows { get; set; }
    public bool UseRainbowColor { get; set; }

    public float Opacity { get; set; }
    public bool MousePassThroughWhileRunning { get; set; }
    public bool AllowResizing { get; set; }
    public bool AllowMoving { get; set; }

    public object Clone()
    {
        var settings = new LayoutSettings();
        settings.Assign(this);
        return settings;
    }

    public void Assign(LayoutSettings settings)
    {
        TextColor = settings.TextColor;
        BackgroundColor = settings.BackgroundColor;
        BackgroundColor2 = settings.BackgroundColor2;
        ThinSeparatorsColor = settings.ThinSeparatorsColor;
        SeparatorsColor = settings.SeparatorsColor;
        PersonalBestColor = settings.PersonalBestColor;
        AheadGainingTimeColor = settings.AheadGainingTimeColor;
        AheadLosingTimeColor = settings.AheadLosingTimeColor;
        BehindGainingTimeColor = settings.BehindGainingTimeColor;
        BehindLosingTimeColor = settings.BehindLosingTimeColor;
        BestSegmentColor = settings.BestSegmentColor;
        UseRainbowColor = settings.UseRainbowColor;
        NotRunningColor = settings.NotRunningColor;
        PausedColor = settings.PausedColor;
        TextOutlineColor = settings.TextOutlineColor;
        ShadowsColor = settings.ShadowsColor;
        TimerFont = settings.TimerFont?.Clone();
        TimesFont = settings.TimesFont?.Clone();
        TextFont = settings.TextFont?.Clone();
        ShowBestSegments = settings.ShowBestSegments;
        AlwaysOnTop = settings.AlwaysOnTop;
        AntiAliasing = settings.AntiAliasing;
        DropShadows = settings.DropShadows;
        Opacity = settings.Opacity;
        MousePassThroughWhileRunning = settings.MousePassThroughWhileRunning;
        BackgroundType = settings.BackgroundType;
        BackgroundImage = settings.BackgroundImage;
        ImageOpacity = settings.ImageOpacity;
        ImageBlur = settings.ImageBlur;
        AllowResizing = settings.AllowResizing;
        AllowMoving = settings.AllowMoving;
    }
}

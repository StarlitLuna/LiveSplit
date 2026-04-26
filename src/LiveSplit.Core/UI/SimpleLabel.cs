using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;

using LiveSplit.Options;
using LiveSplit.UI.Drawing;

namespace LiveSplit.UI;

public class SimpleLabel
{
    public string Text { get; set; }
    public ICollection<string> AlternateText { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public FontDescriptor Font { get; set; }

    private IBrush _brush;

    // Lazy default + disposing setter. Lazy because component classes hold SimpleLabel field
    // initializers that run before DrawingApi.Register, so the constructor must not touch the
    // factory. Disposing because Timer/Subsplits reassign Brush per frame to a fresh
    // ILinearGradientBrush — the Skia backing wraps an SKShader native handle that would
    // otherwise leak at the frame rate.
    public IBrush Brush
    {
        get => _brush ??= DrawingApi.Factory.CreateSolidBrush(Color.Black);
        set
        {
            if (!ReferenceEquals(_brush, value))
            {
                _brush?.Dispose();
                _brush = value;
            }
        }
    }

    public StringAlignment HorizontalAlignment { get; set; }
    public StringAlignment VerticalAlignment { get; set; }
    public Color ShadowColor { get; set; }
    public Color OutlineColor { get; set; }

    public bool HasShadow { get; set; }
    public bool IsMonospaced { get; set; }

    private ITextFormat _format;
    private ITextFormat _centeredFormat;

    // Lazy for the same factory-deferral reason as Brush. The constant flags are set once on
    // first access; the mutable Alignment/LineAlignment bits are reapplied per Draw call by
    // the callers below (matches the StringFormat-mutation pattern this used to do).
    private ITextFormat Format
    {
        get
        {
            if (_format == null)
            {
                _format = DrawingApi.Factory.CreateTextFormat();
                _format.FormatFlags = StringFormatFlags.NoWrap;
                _format.Trimming = StringTrimming.EllipsisCharacter;
            }

            return _format;
        }
    }

    // Cached centered format used by the monospace digit-column layout. LineAlignment is
    // refreshed per call because callers may flip VerticalAlignment between draws.
    private ITextFormat CenteredFormat
    {
        get
        {
            if (_centeredFormat == null)
            {
                _centeredFormat = DrawingApi.Factory.CreateTextFormat();
                _centeredFormat.Alignment = StringAlignment.Center;
            }

            _centeredFormat.LineAlignment = VerticalAlignment;
            return _centeredFormat;
        }
    }

    public float ActualWidth { get; set; }

    public Color ForeColor
    {
        // Returns Color.Empty if the brush is currently a gradient. The Subsplits header round-
        // trip relies on this in steady state — DeltaLabel.ForeColor is captured before the
        // gradient assignment and reapplied afterward via the setter (which mutates in-place).
        get => (Brush as ISolidBrush)?.Color ?? Color.Empty;
        set
        {
            try
            {
                if (_brush is ISolidBrush sb)
                {
                    sb.Color = value;
                }
                else
                {
                    Brush = DrawingApi.Factory.CreateSolidBrush(value);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }
    }

    public SimpleLabel(
        string text = "",
        float x = 0.0f, float y = 0.0f,
        FontDescriptor font = null,
        float width = float.MaxValue, float height = float.MaxValue,
        StringAlignment horizontalAlignment = StringAlignment.Near,
        StringAlignment verticalAlignment = StringAlignment.Near,
        IEnumerable<string> alternateText = null)
    {
        Text = text;
        X = x;
        Y = y;
        Font = font ?? new FontDescriptor("Arial", 1.0f);
        Width = width;
        Height = height;
        HorizontalAlignment = horizontalAlignment;
        VerticalAlignment = verticalAlignment;
        IsMonospaced = false;
        HasShadow = true;
        ShadowColor = Color.FromArgb(128, 0, 0, 0);
        OutlineColor = Color.FromArgb(0, 0, 0, 0);
        ((List<string>)(AlternateText = [])).AddRange(alternateText ?? new string[0]);
    }

    public void Draw(IDrawingContext ctx)
    {
        ITextFormat fmt = Format;
        fmt.Alignment = HorizontalAlignment;
        fmt.LineAlignment = VerticalAlignment;

        using IFont iFont = WrapFont();
        IBrush iBrush = Brush;

        if (!IsMonospaced)
        {
            string actualText = CalculateAlternateText(ctx, Width, iFont, fmt);
            DrawText(actualText, ctx, X, Y, Width, Height, iFont, iBrush, fmt);
        }
        else
        {
            ITextFormat monoFormat = CenteredFormat;

            int measurement = (int)(ctx.MeasureString("0", iFont, 9999, fmt).Width + 0.5f);
            float offset;
            int charIndex = 0;
            SetActualWidth(ctx);
            string cutOffText = CutOff(ctx);

            offset = Width - MeasureActualWidth(cutOffText, ctx);
            if (HorizontalAlignment != StringAlignment.Far)
            {
                offset = 0f;
            }

            while (charIndex < cutOffText.Length)
            {
                float curOffset;
                char curChar = cutOffText[charIndex];

                if (char.IsDigit(curChar))
                {
                    curOffset = measurement;
                }
                else
                {
                    curOffset = (int)(ctx.MeasureString(curChar.ToString(), iFont, 9999, fmt).Width + 0.5f);
                }

                DrawText(curChar.ToString(), ctx, X + offset - (curOffset / 2f), Y, curOffset * 2f, Height, iFont, iBrush, monoFormat);

                charIndex++;
                offset += curOffset;
            }
        }
    }

    private void DrawText(string text, IDrawingContext ctx, float x, float y, float width, float height,
        IFont iFont, IBrush iBrush, ITextFormat iFormat)
    {
        if (text == null)
        {
            return;
        }

        if (ctx.TextRenderingHint == TextRenderingHint.AntiAlias && OutlineColor.A > 0)
        {
            float fontSize = GetFontSize(ctx);
            using IBrush shadowBrush = DrawingApi.Factory.CreateSolidBrush(ShadowColor);
            using IGraphicsPath gp = DrawingApi.Factory.CreateGraphicsPath();
            using IPen outline = DrawingApi.Factory.CreatePen(OutlineColor, GetOutlineSize(fontSize));
            outline.LineJoin = LineJoin.Round;

            // AddString on our IGraphicsPath uses the IFont's pixel size internally; GetFontSize()
            // returns the font's apparent pixel size so the outline thickness stays in sync.
            if (HasShadow)
            {
                gp.AddString(text, iFont, new RectangleF(x + 1f, y + 1f, width, height), iFormat);
                ctx.FillPath(shadowBrush, gp);
                gp.Reset();
                gp.AddString(text, iFont, new RectangleF(x + 2f, y + 2f, width, height), iFormat);
                ctx.FillPath(shadowBrush, gp);
                gp.Reset();
            }

            gp.AddString(text, iFont, new RectangleF(x, y, width, height), iFormat);
            ctx.DrawPath(outline, gp);
            ctx.FillPath(iBrush, gp);
        }
        else
        {
            if (HasShadow)
            {
                using IBrush shadowBrush = DrawingApi.Factory.CreateSolidBrush(ShadowColor);
                ctx.DrawString(text, iFont, shadowBrush, new RectangleF(x + 1f, y + 1f, width, height), iFormat);
                ctx.DrawString(text, iFont, shadowBrush, new RectangleF(x + 2f, y + 2f, width, height), iFormat);
            }

            ctx.DrawString(text, iFont, iBrush, new RectangleF(x, y, width, height), iFormat);
        }
    }

    private static float GetOutlineSize(float fontSize)
    {
        return 2.1f + (fontSize * 0.055f);
    }

    private float GetFontSize(IDrawingContext ctx)
    {
        if (Font.Unit == GraphicsUnit.Point)
        {
            return Font.Size * ctx.DpiY / 72;
        }

        return Font.Size;
    }

    public void SetActualWidth(IDrawingContext ctx)
    {
        ITextFormat fmt = Format;
        fmt.Alignment = HorizontalAlignment;
        fmt.LineAlignment = VerticalAlignment;

        using IFont iFont = WrapFont();

        if (!IsMonospaced)
        {
            ActualWidth = ctx.MeasureString(Text, iFont, 9999, fmt).Width;
        }
        else
        {
            ActualWidth = MeasureActualWidth(Text, ctx);
        }
    }

    public string CalculateAlternateText(IDrawingContext ctx, float width)
    {
        using IFont iFont = WrapFont();
        return CalculateAlternateText(ctx, width, iFont, Format);
    }

    // Internal overload that reuses already-built IFont / ITextFormat across a Draw tick.
    private string CalculateAlternateText(IDrawingContext ctx, float width, IFont iFont, ITextFormat iFormat)
    {
        string actualText = Text;
        ActualWidth = ctx.MeasureString(Text, iFont, 9999, iFormat).Width;
        foreach (string curText in AlternateText.OrderByDescending(x => x.Length))
        {
            if (width < ActualWidth)
            {
                actualText = curText;
                ActualWidth = ctx.MeasureString(actualText, iFont, 9999, iFormat).Width;
            }
            else
            {
                break;
            }
        }

        return actualText;
    }

    private float MeasureActualWidth(string text, IDrawingContext ctx)
    {
        using IFont iFont = WrapFont();
        ITextFormat iFormat = Format;

        int charIndex = 0;
        int measurement = (int)(ctx.MeasureString("0", iFont, 9999, iFormat).Width + 0.5f);
        int offset = 0;

        while (charIndex < text.Length)
        {
            char curChar = text[charIndex];

            if (char.IsDigit(curChar))
            {
                offset += measurement;
            }
            else
            {
                offset += (int)(ctx.MeasureString(curChar.ToString(), iFont, 9999, iFormat).Width + 0.5f);
            }

            charIndex++;
        }

        return offset;
    }

    private string CutOff(IDrawingContext ctx)
    {
        if (ActualWidth < Width)
        {
            return Text;
        }

        string cutOffText = Text;
        while (ActualWidth >= Width && !string.IsNullOrEmpty(cutOffText))
        {
            cutOffText = cutOffText.Remove(cutOffText.Length - 1, 1);
            ActualWidth = MeasureActualWidth(cutOffText + "...", ctx);
        }

        if (ActualWidth >= Width)
        {
            return "";
        }

        return cutOffText + "...";
    }

    private IFont WrapFont()
    {
        return DrawingApi.Factory.CreateFont(
            Font.FamilyName, Font.Size, Font.Style, Font.Unit);
    }
}

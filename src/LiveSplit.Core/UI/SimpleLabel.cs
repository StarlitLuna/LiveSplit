using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

using LiveSplit.Options;
using LiveSplit.UI.Drawing;

using static System.Windows.Forms.TextRenderer;

namespace LiveSplit.UI;

public class SimpleLabel
{
    public string Text { get; set; }
    public ICollection<string> AlternateText { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public Font Font { get; set; }
    public Brush Brush { get; set; }
    public StringAlignment HorizontalAlignment { get; set; }
    public StringAlignment VerticalAlignment { get; set; }
    public Color ShadowColor { get; set; }
    public Color OutlineColor { get; set; }

    public bool HasShadow { get; set; }
    public bool IsMonospaced { get; set; }

    private StringFormat Format { get; set; }

    public float ActualWidth { get; set; }

    public Color ForeColor
    {
        get => ((SolidBrush)Brush).Color;
        set
        {
            try
            {
                if (Brush is SolidBrush brush)
                {
                    brush.Color = value;
                }
                else
                {
                    Brush = new SolidBrush(value);
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
        Font font = null, Brush brush = null,
        float width = float.MaxValue, float height = float.MaxValue,
        StringAlignment horizontalAlignment = StringAlignment.Near,
        StringAlignment verticalAlignment = StringAlignment.Near,
        IEnumerable<string> alternateText = null)
    {
        Text = text;
        X = x;
        Y = y;
        Font = font ?? new Font("Arial", 1.0f);
        Brush = brush ?? new SolidBrush(Color.Black);
        Width = width;
        Height = height;
        HorizontalAlignment = horizontalAlignment;
        VerticalAlignment = verticalAlignment;
        IsMonospaced = false;
        HasShadow = true;
        ShadowColor = Color.FromArgb(128, 0, 0, 0);
        OutlineColor = Color.FromArgb(0, 0, 0, 0);
        ((List<string>)(AlternateText = [])).AddRange(alternateText ?? new string[0]);
        Format = new StringFormat
        {
            Alignment = HorizontalAlignment,
            LineAlignment = VerticalAlignment,
            FormatFlags = StringFormatFlags.NoWrap,
            Trimming = StringTrimming.EllipsisCharacter
        };
    }

    public void Draw(IDrawingContext ctx)
    {
        Format.Alignment = HorizontalAlignment;
        Format.LineAlignment = VerticalAlignment;

        using IFont iFont = WrapFont();
        using IBrush iBrush = WrapBrush();
        ITextFormat iFormat = WrapFormat(Format);

        if (!IsMonospaced)
        {
            string actualText = CalculateAlternateText(ctx, Width, iFont, iFormat);
            DrawText(actualText, ctx, X, Y, Width, Height, iFont, iBrush, iFormat);
        }
        else
        {
            ITextFormat monoFormat = WrapFormat(new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = VerticalAlignment,
            });

            int measurement = (int)(ctx.MeasureString("0", iFont, 9999, iFormat).Width + 0.5f);
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
                    curOffset = (int)(ctx.MeasureString(curChar.ToString(), iFont, 9999, iFormat).Width + 0.5f);
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
        Format.Alignment = HorizontalAlignment;
        Format.LineAlignment = VerticalAlignment;

        using IFont iFont = WrapFont();
        ITextFormat iFormat = WrapFormat(Format);

        if (!IsMonospaced)
        {
            ActualWidth = ctx.MeasureString(Text, iFont, 9999, iFormat).Width;
        }
        else
        {
            ActualWidth = MeasureActualWidth(Text, ctx);
        }
    }

    public string CalculateAlternateText(IDrawingContext ctx, float width)
    {
        using IFont iFont = WrapFont();
        ITextFormat iFormat = WrapFormat(Format);
        return CalculateAlternateText(ctx, width, iFont, iFormat);
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
        ITextFormat iFormat = WrapFormat(Format);

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

    // --- Factory bridges between the System.Drawing public API of SimpleLabel and the
    //     abstract IDrawingContext resource types. Each call allocates a fresh wrapper;
    //     Phase 5.4 (optimization) can introduce caching keyed on font family+size+style
    //     and color if this shows up in profiles.

    private IFont WrapFont()
    {
        return DrawingApi.Factory.CreateFont(
            Font.FontFamily.Name, Font.Size, Font.Style, Font.Unit);
    }

    private IBrush WrapBrush()
    {
        // Consumers (Timer.DrawUnscaled in particular) sometimes assign a
        // LinearGradientBrush to get gradient-filled text. Preserve that instead of
        // collapsing to ForeColor, which would only read SolidBrush.
        if (Brush is LinearGradientBrush lgb)
        {
            Color[] colors = lgb.LinearColors;
            RectangleF rect = lgb.Rectangle;
            // The LinearGradientBrush doesn't re-expose the constructor points; infer the
            // direction from the bounding rectangle's main axis. This matches the
            // start-and-end-point input pattern Timer uses (fixed X or fixed Y).
            var start = new PointF(rect.X, rect.Y);
            var end = new PointF(rect.X + rect.Width, rect.Y + rect.Height);
            return DrawingApi.Factory.CreateLinearGradientBrush(start, end, colors[0], colors[1]);
        }

        return DrawingApi.Factory.CreateSolidBrush(ForeColor);
    }

    private static ITextFormat WrapFormat(StringFormat src)
    {
        ITextFormat f = DrawingApi.Factory.CreateTextFormat();
        f.Alignment = src.Alignment;
        f.LineAlignment = src.LineAlignment;
        f.FormatFlags = src.FormatFlags;
        f.Trimming = src.Trimming;
        return f;
    }
}

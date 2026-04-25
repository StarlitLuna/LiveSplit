using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using LiveSplit.Model;
using LiveSplit.TimeFormatters;
using LiveSplit.UI.Drawing;

namespace LiveSplit.UI.Components;

[GlobalFontConsumer(GlobalFont.TimesFont | GlobalFont.TextFont)]
public class SplitComponent : IComponent
{
    public ISegment Split { get; set; }

    protected SimpleLabel NameLabel { get; set; }
    public SplitsSettings Settings { get; set; }


    public GraphicsCache Cache { get; set; }
    protected bool NeedUpdateAll { get; set; }
    protected bool IsActive { get; set; }

    protected TimeAccuracy CurrentAccuracy { get; set; }
    protected TimeAccuracy CurrentDeltaAccuracy { get; set; }
    protected bool CurrentDropDecimals { get; set; }

    protected ITimeFormatter TimeFormatter { get; set; }
    protected ITimeFormatter DeltaTimeFormatter { get; set; }

    protected int IconWidth => DisplayIcon ? (int)(Settings.IconSize + 7.5f) : 0;

    public bool DisplayIcon { get; set; }

    public IImage ShadowImage { get; set; }

    public float PaddingTop => 0f;
    public float PaddingLeft => 0f;
    public float PaddingBottom => 0f;
    public float PaddingRight => 0f;

    public IEnumerable<ColumnData> ColumnsList { get; set; }
    public IList<SimpleLabel> LabelsList { get; set; }
    protected List<(int exLength, float exWidth, float width)> ColumnWidths { get; }

    public float VerticalHeight { get; set; }

    public float MinimumWidth
        => CalculateLabelsWidth() + IconWidth + 10;

    public float HorizontalWidth
        => Settings.SplitWidth + CalculateLabelsWidth() + IconWidth;

    public float MinimumHeight { get; set; }

    public IDictionary<string, Action> ContextMenuControls => null;

    public SplitComponent(SplitsSettings settings, IEnumerable<ColumnData> columnsList, List<(int exLength, float exWidth, float width)> columnWidths)
    {
        NameLabel = new SimpleLabel()
        {
            HorizontalAlignment = StringAlignment.Near,
            X = 8,
        };
        Settings = settings;
        ColumnsList = columnsList;
        ColumnWidths = columnWidths;
        TimeFormatter = new SplitTimeFormatter(Settings.SplitTimesAccuracy);
        DeltaTimeFormatter = new DeltaSplitTimeFormatter(Settings.DeltasAccuracy, Settings.DropDecimals);
        MinimumHeight = 25;
        VerticalHeight = 31;

        NeedUpdateAll = true;
        IsActive = false;

        Cache = new GraphicsCache();
        LabelsList = [];
    }

    private void DrawGeneral(IDrawingContext ctx, LiveSplitState state, float width, float height, LayoutMode mode)
    {
        if (NeedUpdateAll)
        {
            UpdateAll(state);
        }

        if (Settings.BackgroundGradient == ExtendedGradientType.Alternating)
        {
            Color rowColor = (state.Run.IndexOf(Split) % 2) + (Settings.ShowColumnLabels ? 1 : 0) == 1
                ? Settings.BackgroundColor2
                : Settings.BackgroundColor;
            using ISolidBrush rowBrush = DrawingApi.Factory.CreateSolidBrush(rowColor);
            ctx.FillRectangle(rowBrush, 0, 0, width, height);
        }

        NameLabel.ShadowColor = state.LayoutSettings.ShadowsColor;
        NameLabel.OutlineColor = state.LayoutSettings.TextOutlineColor;
        foreach (SimpleLabel label in LabelsList)
        {
            label.ShadowColor = state.LayoutSettings.ShadowsColor;
            label.OutlineColor = state.LayoutSettings.TextOutlineColor;
        }

        if (Settings.SplitTimesAccuracy != CurrentAccuracy)
        {
            TimeFormatter = new SplitTimeFormatter(Settings.SplitTimesAccuracy);
            CurrentAccuracy = Settings.SplitTimesAccuracy;
        }

        if (Settings.DeltasAccuracy != CurrentDeltaAccuracy || Settings.DropDecimals != CurrentDropDecimals)
        {
            DeltaTimeFormatter = new DeltaSplitTimeFormatter(Settings.DeltasAccuracy, Settings.DropDecimals);
            CurrentDeltaAccuracy = Settings.DeltasAccuracy;
            CurrentDropDecimals = Settings.DropDecimals;
        }

        if (Split != null)
        {

            if (mode == LayoutMode.Vertical)
            {
                NameLabel.VerticalAlignment = StringAlignment.Center;
                NameLabel.Y = 0;
                NameLabel.Height = height;
                foreach (SimpleLabel label in LabelsList)
                {
                    label.VerticalAlignment = StringAlignment.Center;
                    label.Y = 0;
                    label.Height = height;
                }
            }
            else
            {
                NameLabel.VerticalAlignment = StringAlignment.Near;
                NameLabel.Y = 0;
                NameLabel.Height = 50;
                foreach (SimpleLabel label in LabelsList)
                {
                    label.VerticalAlignment = StringAlignment.Far;
                    label.Y = height - 50;
                    label.Height = 50;
                }
            }

            if (IsActive)
            {
                BackgroundHelper.DrawBackground(ctx,
                    Settings.CurrentSplitTopColor,
                    Settings.CurrentSplitGradient == GradientType.Plain
                        ? Settings.CurrentSplitTopColor
                        : Settings.CurrentSplitBottomColor,
                    width, height, Settings.CurrentSplitGradient);
            }

            IImage icon = Split.IconImage;
            if (DisplayIcon && icon != null)
            {
                IImage shadow = ShadowImage;

                float drawWidth = Settings.IconSize;
                float drawHeight = Settings.IconSize;
                float shadowWidth = Settings.IconSize * (5 / 4f);
                float shadowHeight = Settings.IconSize * (5 / 4f);
                if (icon.Width > icon.Height)
                {
                    float ratio = icon.Height / (float)icon.Width;
                    drawHeight *= ratio;
                    shadowHeight *= ratio;
                }
                else
                {
                    float ratio = icon.Width / (float)icon.Height;
                    drawWidth *= ratio;
                    shadowWidth *= ratio;
                }

                if (Settings.IconShadows && shadow != null)
                {
                    ctx.DrawImage(
                        shadow,
                        new RectangleF(
                            7 + (((Settings.IconSize * (5 / 4f)) - shadowWidth) / 2) - 0.7f,
                            ((height - Settings.IconSize) / 2.0f) + (((Settings.IconSize * (5 / 4f)) - shadowHeight) / 2) - 0.7f,
                            shadowWidth,
                            shadowHeight));
                }

                ctx.DrawImage(
                    icon,
                    new RectangleF(
                        7 + ((Settings.IconSize - drawWidth) / 2),
                        ((height - Settings.IconSize) / 2.0f) + ((Settings.IconSize - drawHeight) / 2),
                        drawWidth,
                        drawHeight));
            }

            NameLabel.Font = state.LayoutSettings.TextFont;
            NameLabel.X = 5 + IconWidth;
            NameLabel.HasShadow = state.LayoutSettings.DropShadows;

            if (ColumnsList.Count() == LabelsList.Count)
            {
                while (ColumnWidths.Count < LabelsList.Count)
                {
                    ColumnWidths.Add((0, 0f, 0f));
                }

                float curX = width - 7;
                float nameX = width - 7;
                foreach (SimpleLabel label in LabelsList.Reverse())
                {
                    int i = LabelsList.IndexOf(label);
                    float labelWidth = ColumnWidths[i].width;

                    label.Width = labelWidth + 20;
                    curX -= labelWidth + 5;
                    label.X = curX - 15;

                    label.Font = state.LayoutSettings.TimesFont;
                    label.HasShadow = state.LayoutSettings.DropShadows;
                    label.IsMonospaced = true;
                    label.Draw(ctx);

                    if (!string.IsNullOrEmpty(label.Text))
                    {
                        nameX = curX + labelWidth + 5 - label.ActualWidth;
                        if (ColumnWidths[i].exWidth < label.ActualWidth)
                        {
                            ColumnWidths[i] = (label.Text.Length, label.ActualWidth, labelWidth);
                        }
                    }
                }

                NameLabel.Width = (mode == LayoutMode.Horizontal ? width - 10 : nameX) - IconWidth;
                NameLabel.Draw(ctx);
            }
        }
        else
        {
            DisplayIcon = Settings.DisplayIcons;
        }
    }

    public void DrawVertical(IDrawingContext ctx, LiveSplitState state, float width)
    {
        if (Settings.Display2Rows)
        {
            VerticalHeight = Settings.SplitHeight + (0.85f * (MeasureFontCapHeight(ctx, state.LayoutSettings.TimesFont) + MeasureFontCapHeight(ctx, state.LayoutSettings.TextFont)));
            DrawGeneral(ctx, state, width, VerticalHeight, LayoutMode.Horizontal);
        }
        else
        {
            VerticalHeight = Settings.SplitHeight + 25;
            DrawGeneral(ctx, state, width, VerticalHeight, LayoutMode.Vertical);
        }
    }

    public void DrawHorizontal(IDrawingContext ctx, LiveSplitState state, float height)
    {
        MinimumHeight = 0.85f * (MeasureFontCapHeight(ctx, state.LayoutSettings.TimesFont) + MeasureFontCapHeight(ctx, state.LayoutSettings.TextFont));
        DrawGeneral(ctx, state, HorizontalWidth, height, LayoutMode.Horizontal);
    }

    private static float MeasureFontCapHeight(IDrawingContext ctx, FontDescriptor font)
    {
        using IFont iFont = DrawingApi.Factory.CreateFont(font.FamilyName, font.Size, font.Style, font.Unit);
        ITextFormat fmt = DrawingApi.Factory.CreateTextFormat();
        return ctx.MeasureString("A", iFont, 9999, fmt).Height;
    }

    public string ComponentName => "Split";

    public void SetSettings(System.Xml.XmlNode settings)
    {
        throw new NotSupportedException();
    }

    public System.Xml.XmlNode GetSettings(System.Xml.XmlDocument document)
    {
        throw new NotSupportedException();
    }

    public string UpdateName => throw new NotSupportedException();

    public string XMLURL => throw new NotSupportedException();

    public string UpdateURL => throw new NotSupportedException();

    public Version Version => throw new NotSupportedException();

    protected void UpdateAll(LiveSplitState state)
    {
        if (Split != null)
        {
            RecreateLabels();

            if (Settings.AutomaticAbbreviations)
            {
                if (NameLabel.Text != Split.Name || NameLabel.AlternateText == null || !NameLabel.AlternateText.Any())
                {
                    NameLabel.AlternateText = Split.Name.GetAbbreviations().ToList();
                }
            }
            else if (NameLabel.AlternateText != null && NameLabel.AlternateText.Any())
            {
                NameLabel.AlternateText.Clear();
            }

            NameLabel.Text = Split.Name;

            int splitIndex = state.Run.IndexOf(Split);
            if (splitIndex < state.CurrentSplitIndex)
            {
                NameLabel.ForeColor = Settings.OverrideTextColor ? Settings.BeforeNamesColor : state.LayoutSettings.TextColor;
            }
            else
            {
                if (Split == state.CurrentSplit)
                {
                    NameLabel.ForeColor = Settings.OverrideTextColor ? Settings.CurrentNamesColor : state.LayoutSettings.TextColor;
                }
                else
                {
                    NameLabel.ForeColor = Settings.OverrideTextColor ? Settings.AfterNamesColor : state.LayoutSettings.TextColor;
                }
            }

            foreach (SimpleLabel label in LabelsList)
            {
                ColumnData column = ColumnsList.ElementAt(LabelsList.IndexOf(label));
                UpdateColumn(state, label, column);
            }
        }
    }

    protected void UpdateColumn(LiveSplitState state, SimpleLabel label, ColumnData data)
    {
        string comparison = data.Comparison == "Current Comparison" ? state.CurrentComparison : data.Comparison;
        if (!state.Run.Comparisons.Contains(comparison))
        {
            comparison = state.CurrentComparison;
        }

        TimingMethod timingMethod = state.CurrentTimingMethod;
        if (data.TimingMethod == "Real Time")
        {
            timingMethod = TimingMethod.RealTime;
        }
        else if (data.TimingMethod == "Game Time")
        {
            timingMethod = TimingMethod.GameTime;
        }

        ColumnType type = data.Type;

        int splitIndex = state.Run.IndexOf(Split);
        if (splitIndex < state.CurrentSplitIndex)
        {
            if (type is ColumnType.SplitTime or ColumnType.SegmentTime or ColumnType.CustomVariable)
            {
                label.ForeColor = Settings.OverrideTimesColor ? Settings.BeforeTimesColor : state.LayoutSettings.TextColor;

                if (type == ColumnType.SplitTime)
                {
                    label.Text = TimeFormatter.Format(Split.SplitTime[timingMethod]);
                }
                else if (type == ColumnType.SegmentTime)
                {
                    TimeSpan? segmentTime = LiveSplitStateHelper.GetPreviousSegmentTime(state, splitIndex, timingMethod);
                    label.Text = TimeFormatter.Format(segmentTime);
                }
                else if (type == ColumnType.CustomVariable)
                {
                    Split.CustomVariableValues.TryGetValue(data.Name, out string text);
                    label.Text = text ?? "";
                }
            }

            if (type is ColumnType.DeltaorSplitTime or ColumnType.Delta)
            {
                TimeSpan? deltaTime = Split.SplitTime[timingMethod] - Split.Comparisons[comparison][timingMethod];
                Color? color = LiveSplitStateHelper.GetSplitColor(state, deltaTime, splitIndex, true, true, comparison, timingMethod);
                if (color == null)
                {
                    color = Settings.OverrideTimesColor ? Settings.BeforeTimesColor : state.LayoutSettings.TextColor;
                }

                label.ForeColor = color.Value;

                if (type == ColumnType.DeltaorSplitTime)
                {
                    if (deltaTime != null)
                    {
                        label.Text = DeltaTimeFormatter.Format(deltaTime);
                    }
                    else
                    {
                        label.Text = TimeFormatter.Format(Split.SplitTime[timingMethod]);
                    }
                }

                else if (type == ColumnType.Delta)
                {
                    label.Text = DeltaTimeFormatter.Format(deltaTime);
                }
            }

            else if (type is ColumnType.SegmentDeltaorSegmentTime or ColumnType.SegmentDelta)
            {
                TimeSpan? segmentDelta = LiveSplitStateHelper.GetPreviousSegmentDelta(state, splitIndex, comparison, timingMethod);
                Color? color = LiveSplitStateHelper.GetSplitColor(state, segmentDelta, splitIndex, false, true, comparison, timingMethod);
                if (color == null)
                {
                    color = Settings.OverrideTimesColor ? Settings.BeforeTimesColor : state.LayoutSettings.TextColor;
                }

                label.ForeColor = color.Value;

                if (type == ColumnType.SegmentDeltaorSegmentTime)
                {
                    if (segmentDelta != null)
                    {
                        label.Text = DeltaTimeFormatter.Format(segmentDelta);
                    }
                    else
                    {
                        label.Text = TimeFormatter.Format(LiveSplitStateHelper.GetPreviousSegmentTime(state, splitIndex, timingMethod));
                    }
                }
                else if (type == ColumnType.SegmentDelta)
                {
                    label.Text = DeltaTimeFormatter.Format(segmentDelta);
                }
            }
        }
        else
        {
            if (type is ColumnType.SplitTime or ColumnType.SegmentTime or ColumnType.DeltaorSplitTime or ColumnType.SegmentDeltaorSegmentTime or ColumnType.CustomVariable)
            {
                if (Split == state.CurrentSplit)
                {
                    label.ForeColor = Settings.OverrideTimesColor ? Settings.CurrentTimesColor : state.LayoutSettings.TextColor;
                }
                else
                {
                    label.ForeColor = Settings.OverrideTimesColor ? Settings.AfterTimesColor : state.LayoutSettings.TextColor;
                }

                if (type is ColumnType.SplitTime or ColumnType.DeltaorSplitTime)
                {
                    label.Text = TimeFormatter.Format(Split.Comparisons[comparison][timingMethod]);
                }
                else if (type is ColumnType.SegmentTime or ColumnType.SegmentDeltaorSegmentTime)
                {
                    TimeSpan previousTime = TimeSpan.Zero;
                    for (int index = splitIndex - 1; index >= 0; index--)
                    {
                        TimeSpan? comparisonTime = state.Run[index].Comparisons[comparison][timingMethod];
                        if (comparisonTime != null)
                        {
                            previousTime = comparisonTime.Value;
                            break;
                        }
                    }

                    label.Text = TimeFormatter.Format(Split.Comparisons[comparison][timingMethod] - previousTime);
                }
                else if (type is ColumnType.CustomVariable)
                {
                    if (splitIndex == state.CurrentSplitIndex)
                    {
                        label.Text = state.Run.Metadata.CustomVariableValue(data.Name) ?? "";
                    }
                    else if (splitIndex > state.CurrentSplitIndex)
                    {
                        label.Text = "";
                    }
                }
            }

            //Live Delta
            bool splitDelta = type is ColumnType.DeltaorSplitTime or ColumnType.Delta;
            TimeSpan? bestDelta = LiveSplitStateHelper.CheckLiveDelta(state, splitDelta, comparison, timingMethod);
            if (bestDelta != null && Split == state.CurrentSplit &&
                (type == ColumnType.DeltaorSplitTime || type == ColumnType.Delta || type == ColumnType.SegmentDeltaorSegmentTime || type == ColumnType.SegmentDelta))
            {
                label.Text = DeltaTimeFormatter.Format(bestDelta);
                label.ForeColor = Settings.OverrideDeltasColor ? Settings.DeltasColor : state.LayoutSettings.TextColor;
            }
            else if (type is ColumnType.Delta or ColumnType.SegmentDelta)
            {
                label.Text = "";
            }
        }
    }

    protected float CalculateLabelsWidth()
    {
        if (ColumnWidths != null)
        {
            return ColumnWidths.Sum(e => e.width) + (5 * ColumnWidths.Count());
        }

        return 0f;
    }

    protected void RecreateLabels()
    {
        if (ColumnsList != null && LabelsList.Count != ColumnsList.Count())
        {
            LabelsList.Clear();
            foreach (ColumnData column in ColumnsList)
            {
                LabelsList.Add(new SimpleLabel
                {
                    HorizontalAlignment = StringAlignment.Far
                });
            }
        }
    }

    public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
    {
        if (Split != null)
        {
            UpdateAll(state);
            NeedUpdateAll = false;

            IsActive = (state.CurrentPhase == TimerPhase.Running
                        || state.CurrentPhase == TimerPhase.Paused) &&
                                                state.CurrentSplit == Split;

            Cache.Restart();
            Cache["IconPng"] = Split.IconPng;

            Cache["DisplayIcon"] = DisplayIcon;
            Cache["SplitName"] = NameLabel.Text;
            Cache["IsActive"] = IsActive;
            Cache["NameColor"] = NameLabel.ForeColor.ToArgb();
            Cache["ColumnsCount"] = ColumnsList.Count();
            for (int index = 0; index < LabelsList.Count; index++)
            {
                SimpleLabel label = LabelsList[index];
                Cache["Columns" + index + "Text"] = label.Text;
                Cache["Columns" + index + "Color"] = label.ForeColor.ToArgb();
                if (index < ColumnWidths.Count)
                {
                    Cache["Columns" + index + "Width"] = ColumnWidths[index].width;
                }
            }

            if (invalidator != null && Cache.HasChanged)
            {
                invalidator.Invalidate(0, 0, width, height);
            }
        }
    }

    public void Dispose()
    {
    }
}

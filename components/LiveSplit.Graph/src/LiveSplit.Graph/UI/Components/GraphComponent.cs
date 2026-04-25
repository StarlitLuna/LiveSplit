using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

using LiveSplit.Model;
using LiveSplit.Options;
using LiveSplit.UI.Drawing;

namespace LiveSplit.UI.Components;

public class GraphComponent : IComponent
{
    public float PaddingTop => 0f;
    public float PaddingLeft => 0f;
    public float PaddingBottom => 0f;
    public float PaddingRight => 0f;

    public List<TimeSpan?> Deltas { get; set; }
    public TimeSpan? FinalSplit { get; set; }
    public TimeSpan MaxDelta { get; set; }
    public TimeSpan MinDelta { get; set; }

    public bool IsLiveDeltaActive { get; set; }

    public GraphicsCache Cache { get; set; }

    public float VerticalHeight => Settings.GraphHeight;

    public float MinimumWidth => 20;

    public float HorizontalWidth => Settings.GraphWidth;

    public float MinimumHeight => 20;

    public IDictionary<string, Action> ContextMenuControls => null;

    public TimeSpan GraphEdgeValue { get; set; }
    public float GraphEdgeMin { get; set; }
    public GraphSettings Settings { get; set; }

    public GraphComponent(GraphSettings settings)
    {
        GraphEdgeValue = new TimeSpan(0, 0, 0, 0, 200);
        GraphEdgeMin = 5;
        Settings = settings;
        Cache = new GraphicsCache();
        Deltas = [];
        FinalSplit = TimeSpan.Zero;
        MaxDelta = TimeSpan.Zero;
        MinDelta = TimeSpan.Zero;
    }

    private void DrawGeneral(IDrawingContext ctx, LiveSplitState state, float width, float height)
    {
        using IDrawingState savedState = ctx.Save();
        if (Settings.FlipGraph)
        {
            ctx.ScaleTransform(1, -1);
            ctx.TranslateTransform(0, -height);
        }

        DrawUnflipped(ctx, state, width, height);
    }

    private void DrawUnflipped(IDrawingContext ctx, LiveSplitState state, float width, float height)
    {
        string comparison = Settings.Comparison == "Current Comparison" ? state.CurrentComparison : Settings.Comparison;
        if (!state.Run.Comparisons.Contains(comparison))
        {
            comparison = state.CurrentComparison;
        }

        TimeSpan totalDelta = MinDelta - MaxDelta;

        CalculateMiddleAndGraphEdge(height, totalDelta, out float graphEdge, out float graphHeight, out float middle);

        using ISolidBrush brush = DrawingApi.Factory.CreateSolidBrush(Settings.GraphColor);
        DrawGreenAndRedGraphPortions(ctx, width, graphHeight, middle, brush);

        CalculateGridlines(state, width, totalDelta, graphEdge, graphHeight, out double gridValueX, out double gridValueY);

        using IPen pen = DrawingApi.Factory.CreatePen(Settings.GridlinesColor, 2.0f);
        DrawGridlines(ctx, width, graphHeight, middle, gridValueX, gridValueY, pen);

        try
        {
            DrawGraph(ctx, state, width, comparison, totalDelta, graphEdge, graphHeight, middle, brush, pen);
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
    }

    private void DrawGraph(IDrawingContext ctx, LiveSplitState state, float width, string comparison, TimeSpan TotalDelta, float graphEdge, float graphHeight, float middle, ISolidBrush brush, IPen pen)
    {
        pen.Width = 1.75f;
        pen.StartCap = LineCap.Round;
        pen.EndCap = LineCap.Round;
        var circleList = new List<PointF>();
        if (Deltas.Count > 0)
        {
            float heightOne = graphHeight;
            if (TotalDelta != TimeSpan.Zero)
            {
                heightOne = (float)(((-MaxDelta.TotalMilliseconds) / TotalDelta.TotalMilliseconds
                    * (graphHeight - graphEdge) * 2) + graphEdge);
            }

            float heightTwo = 0;
            float widthOne = 0;
            float widthTwo = 0;
            int y = 0;

            var pointArray = new List<PointF>
            {
                new(0, middle)
            };
            circleList.Add(new PointF(widthOne, heightOne));

            while (y < Deltas.Count)
            {
                while (Deltas[y] == null && y < Deltas.Count - 1)
                {
                    y++;
                }

                if (Deltas[y] != null)
                {
                    CalculateRightSideCoordinates(state, width, TotalDelta, graphEdge, graphHeight, ref heightTwo, ref widthTwo, y);
                    DrawFillBeneathGraph(ctx, TotalDelta, middle, brush, heightOne, heightTwo, widthOne, widthTwo, y, pointArray);
                    AddGraphNode(circleList, heightTwo, widthTwo);
                    CalculateLeftSideCoordinates(state, width, TotalDelta, graphEdge, graphHeight, ref heightOne, ref widthOne, y);
                }
                else
                {
                    DrawFinalPolygon(ctx, middle, brush, pointArray);
                }

                y++;
            }

            DrawCirclesAndLines(ctx, state, width, pen, brush, circleList);
        }
    }

    private void DrawCirclesAndLines(IDrawingContext ctx, LiveSplitState state, float width, IPen pen, ISolidBrush brush, List<PointF> circleList)
    {
        int i = Deltas.Count - 1;

        circleList.Reverse();
        PointF previousCircle = circleList.FirstOrDefault();
        if (circleList.Count > 0)
        {
            circleList.RemoveAt(0);
        }

        foreach (PointF circle in circleList)
        {
            while (Deltas[i] == null)
            {
                i--;
            }

            pen.Color = brush.Color = Settings.GraphColor;
            bool finalDelta = previousCircle.X == width && IsLiveDeltaActive;
            if (!finalDelta && CheckBestSegment(state, i, state.CurrentTimingMethod))
            {
                pen.Color = brush.Color = Settings.GraphGoldColor;
            }

            DrawLineShadowed(ctx, pen, previousCircle.X, previousCircle.Y, circle.X, circle.Y, Settings.FlipGraph);
            if (!finalDelta)
            {
                DrawEllipseShadowed(ctx, brush, previousCircle.X - 2.5f, previousCircle.Y - 2.5f, 5, 5, Settings.FlipGraph);
            }

            previousCircle = circle;
            i--;
        }
    }

    private static void AddGraphNode(List<PointF> circleList, float heightTwo, float widthTwo)
    {
        circleList.Add(new PointF(widthTwo, heightTwo));
    }

    private void CalculateLeftSideCoordinates(LiveSplitState state, float width, TimeSpan TotalDelta, float graphEdge, float GraphHeight, ref float heightOne, ref float widthOne, int y)
    {
        if (TotalDelta != TimeSpan.Zero)
        {
            heightOne = ((float)((Deltas[y].Value.TotalMilliseconds - MaxDelta.TotalMilliseconds) / TotalDelta.TotalMilliseconds)
                * (GraphHeight - graphEdge) * 2) + graphEdge;
        }
        else
        {
            heightOne = GraphHeight;
        }

        if (y != Deltas.Count - 1 && state.Run[y].SplitTime[state.CurrentTimingMethod] != null)
        {
            widthOne = (float)(state.Run[y].SplitTime[state.CurrentTimingMethod].Value.TotalMilliseconds / FinalSplit.Value.TotalMilliseconds * width);
        }
    }

    private void CalculateRightSideCoordinates(LiveSplitState state, float width, TimeSpan TotalDelta, float graphEdge, float GraphHeight, ref float heightTwo, ref float widthTwo, int y)
    {
        if (y == Deltas.Count - 1 && IsLiveDeltaActive)
        {
            widthTwo = width;
        }
        else if (state.Run[y].SplitTime[state.CurrentTimingMethod] != null)
        {
            widthTwo = (float)(state.Run[y].SplitTime[state.CurrentTimingMethod].Value.TotalMilliseconds / FinalSplit.Value.TotalMilliseconds * width);
        }

        if (TotalDelta != TimeSpan.Zero)
        {
            heightTwo = (float)(((Deltas[y].Value.TotalMilliseconds - MaxDelta.TotalMilliseconds) / TotalDelta.TotalMilliseconds
                * (GraphHeight - graphEdge) * 2) + graphEdge);
        }
        else
        {
            heightTwo = GraphHeight;
        }
    }

    private void DrawFillBeneathGraph(IDrawingContext ctx, TimeSpan TotalDelta, float Middle, ISolidBrush brush, float heightOne, float heightTwo, float widthOne, float widthTwo, int y, List<PointF> pointArray)
    {
        if ((heightTwo - Middle) / (heightOne - Middle) > 0)
        {
            AddFillOneSide(ctx, Middle, brush, heightOne, heightTwo, widthOne, widthTwo, y, pointArray);
        }
        else
        {
            float ratio = (heightOne - Middle) / (heightOne - heightTwo);
            if (float.IsNaN(ratio))
            {
                ratio = 0.0f;
            }

            AddFillFirstHalf(ctx, TotalDelta, Middle, brush, heightOne, widthOne, widthTwo, y, pointArray, ratio);
            AddFillSecondHalf(ctx, TotalDelta, Middle, brush, heightTwo, widthOne, widthTwo, y, pointArray, ratio);
        }

        if (y == Deltas.Count - 1)
        {
            DrawFinalPolygon(ctx, Middle, brush, pointArray);
        }
    }

    private void DrawFinalPolygon(IDrawingContext ctx, float Middle, ISolidBrush brush, List<PointF> pointArray)
    {
        pointArray.Add(new PointF(pointArray.Last().X, Middle));
        if (pointArray.Count > 1)
        {
            brush.Color = pointArray[^2].Y > Middle ? Settings.CompleteFillColorAhead : Settings.CompleteFillColorBehind;
            ctx.FillPolygon(brush, pointArray.ToArray());
        }
    }

    // Adds to the point array the second portion of the fill if the graph goes from ahead to behind or vice versa
    private void AddFillSecondHalf(IDrawingContext ctx, TimeSpan TotalDelta, float Middle, ISolidBrush brush, float heightTwo, float widthOne, float widthTwo, int y, List<PointF> pointArray, float ratio)
    {
        if (y == Deltas.Count - 1 && IsLiveDeltaActive)
        {
            brush.Color = heightTwo > Middle ? Settings.PartialFillColorAhead : Settings.PartialFillColorBehind;
            if (TotalDelta != TimeSpan.Zero)
            {
                ctx.FillPolygon(brush, new PointF[]
                {
                    new(widthOne+((widthTwo-widthOne)*ratio), Middle),
                    new(widthTwo, heightTwo),
                    new(widthTwo, Middle)
                });
            }
        }
        else
        {
            brush.Color = heightTwo > Middle ? Settings.CompleteFillColorAhead : Settings.CompleteFillColorBehind;
            pointArray.Clear();
            pointArray.Add(new PointF(widthOne + ((widthTwo - widthOne) * ratio), Middle));
            pointArray.Add(new PointF(widthTwo, heightTwo));
        }
    }

    // Adds to the point array the first portion of the fill if the graph goes from ahead to behind or vice versa
    private void AddFillFirstHalf(IDrawingContext ctx, TimeSpan TotalDelta, float Middle, ISolidBrush brush, float heightOne, float widthOne, float widthTwo, int y, List<PointF> pointArray, float ratio)
    {
        if (y == Deltas.Count - 1 && IsLiveDeltaActive)
        {
            brush.Color = heightOne > Middle ? Settings.PartialFillColorAhead : Settings.PartialFillColorBehind;
            if (TotalDelta != TimeSpan.Zero)
            {
                ctx.FillPolygon(brush, new PointF[]
                {
                    new(widthOne, Middle),
                    new(widthOne, heightOne),
                    new(widthOne+((widthTwo-widthOne)*ratio), Middle)
                });
            }
        }
        else
        {
            pointArray.Add(new PointF(widthOne + ((widthTwo - widthOne) * ratio), Middle));
            brush.Color = heightOne > Middle ? Settings.CompleteFillColorAhead : Settings.CompleteFillColorBehind;
            ctx.FillPolygon(brush, pointArray.ToArray());
            brush.Color = heightOne > Middle ? Settings.CompleteFillColorAhead : Settings.CompleteFillColorBehind;
        }
    }

    // Adds to the point array the fill under the graph if the current portion of the graph is either completely ahead or completely behind
    private void AddFillOneSide(IDrawingContext ctx, float Middle, ISolidBrush brush, float heightOne, float heightTwo, float widthOne, float widthTwo, int y, List<PointF> pointArray)
    {
        if (y == Deltas.Count - 1 && IsLiveDeltaActive)
        {
            brush.Color = heightTwo > Middle ? Settings.PartialFillColorAhead : Settings.PartialFillColorBehind;
            ctx.FillPolygon(brush, new PointF[]
            {
                 new(widthOne, Middle),
                 new(widthOne, heightOne),
                 new(widthTwo, heightTwo),
                 new(widthTwo, Middle)
            });
        }
        else
        {
            pointArray.Add(new PointF(widthTwo, heightTwo));
        }
    }

    private static void DrawGridlines(IDrawingContext ctx, float width, float GraphHeight, float Middle, double gridValueX, double gridValueY, IPen pen)
    {
        if (gridValueX > 0)
        {
            for (double x = gridValueX; x < width; x += gridValueX)
            {
                ctx.DrawLine(pen, new PointF((float)x, 0), new PointF((float)x, GraphHeight * 2));
            }
        }

        for (float y = Middle - 1; y > 0; y -= (float)gridValueY)
        {
            ctx.DrawLine(pen, new PointF(0, y), new PointF(width, y));
            if (gridValueY < 0)
            {
                break;
            }
        }

        for (float y = Middle; y < GraphHeight * 2; y += (float)gridValueY)
        {
            ctx.DrawLine(pen, new PointF(0, y), new PointF(width, y));
            if (gridValueY < 0)
            {
                break;
            }
        }
    }

    private void CalculateMiddleAndGraphEdge(float height, TimeSpan TotalDelta, out float graphEdge, out float graphHeight, out float middle)
    {
        graphEdge = 0;
        graphHeight = height / 2.0f;
        middle = graphHeight;
        if (TotalDelta != TimeSpan.Zero)
        {
            graphEdge = (float)(GraphEdgeValue.TotalMilliseconds / (-TotalDelta.TotalMilliseconds + (GraphEdgeValue.TotalMilliseconds * 2)) * ((graphHeight * 2) - (GraphEdgeMin * 2)));
            graphEdge += GraphEdgeMin;
            middle = (float)((-(MaxDelta.TotalMilliseconds / TotalDelta.TotalMilliseconds)
                    * (graphHeight - graphEdge) * 2) + graphEdge);
        }
    }

    private void CalculateGridlines(LiveSplitState state, float width, TimeSpan TotalDelta, float graphEdge, float graphHeight, out double gridValueX, out double gridValueY)
    {
        if (state.CurrentPhase != TimerPhase.NotRunning && FinalSplit > TimeSpan.Zero)
        {
            gridValueX = 1000;
            while (FinalSplit.Value.TotalMilliseconds / gridValueX > width / 20)
            {
                gridValueX *= 6;
            }

            gridValueX = gridValueX / FinalSplit.Value.TotalMilliseconds * width;
        }
        else
        {
            gridValueX = -1;
        }

        if (state.CurrentPhase != TimerPhase.NotRunning && TotalDelta < TimeSpan.Zero)
        {
            gridValueY = 1000;
            while ((-TotalDelta.TotalMilliseconds) / gridValueY > (graphHeight - graphEdge) * 2 / 20)
            {
                gridValueY *= 6;
            }

            gridValueY = gridValueY / (-TotalDelta.TotalMilliseconds) * (graphHeight - graphEdge) * 2;
        }
        else
        {
            gridValueY = -1;
        }
    }

    private void DrawGreenAndRedGraphPortions(IDrawingContext ctx, float width, float GraphHeight, float Middle, ISolidBrush brush)
    {
        brush.Color = Settings.BehindGraphColor;
        ctx.FillRectangle(brush, 0, 0, width, Middle);
        brush.Color = Settings.AheadGraphColor;
        ctx.FillRectangle(brush, 0, Middle, width, (GraphHeight * 2) - Middle);
    }

    public bool CheckBestSegment(LiveSplitState state, int splitNumber, TimingMethod method)
    {
        if (Settings.ShowBestSegments)
        {
            return LiveSplitStateHelper.CheckBestSegment(state, splitNumber, method);
        }

        return false;
    }

    public void DrawVertical(IDrawingContext ctx, LiveSplitState state, float width, Region clipRegion)
    {
        DrawGeneral(ctx, state, width, VerticalHeight);
    }

    public void DrawHorizontal(IDrawingContext ctx, LiveSplitState state, float height, Region clipRegion)
    {
        DrawGeneral(ctx, state, HorizontalWidth, height);
    }

    private void DrawLineShadowed(IDrawingContext ctx, IPen pen, float x1, float y1, float x2, float y2, bool flipShadow)
    {
        Color originalColor = pen.Color;
        pen.Color = Settings.ShadowsColor;
        if (!flipShadow)
        {
            ctx.DrawLine(pen, new PointF(x1 + 1, y1 + 1), new PointF(x2 + 1, y2 + 1));
            ctx.DrawLine(pen, new PointF(x1 + 1, y1 + 2), new PointF(x2 + 1, y2 + 2));
            ctx.DrawLine(pen, new PointF(x1 + 1, y1 + 3), new PointF(x2 + 1, y2 + 3));
        }
        else
        {
            ctx.DrawLine(pen, new PointF(x1 + 1, y1 - 1), new PointF(x2 + 1, y2 - 1));
            ctx.DrawLine(pen, new PointF(x1 + 1, y1 - 2), new PointF(x2 + 1, y2 - 2));
            ctx.DrawLine(pen, new PointF(x1 + 1, y1 - 3), new PointF(x2 + 1, y2 - 3));
        }

        pen.Color = originalColor;
        ctx.DrawLine(pen, new PointF(x1, y1), new PointF(x2, y2));
    }

    private void DrawEllipseShadowed(IDrawingContext ctx, ISolidBrush brush, float x, float y, float width, float height, bool flipShadow)
    {
        Color originalColor = brush.Color;
        using ISolidBrush shadowBrush = DrawingApi.Factory.CreateSolidBrush(Settings.ShadowsColor);

        if (!flipShadow)
        {
            ctx.FillEllipse(shadowBrush, x + 1, y + 1, width, height);
            ctx.FillEllipse(shadowBrush, x + 1, y + 2, width, height);
            ctx.FillEllipse(shadowBrush, x + 1, y + 3, width, height);
        }
        else
        {
            ctx.FillEllipse(shadowBrush, x + 1, y - 1, width, height);
            ctx.FillEllipse(shadowBrush, x + 1, y - 2, width, height);
            ctx.FillEllipse(shadowBrush, x + 1, y - 3, width, height);
        }

        brush.Color = originalColor;
        ctx.FillEllipse(brush, x, y, width, height);
    }

    public string ComponentName => throw new NotImplementedException();

    public Control GetSettingsControl(LayoutMode mode)
    {
        throw new NotSupportedException();
    }

    public System.Xml.XmlNode GetSettings(System.Xml.XmlDocument document)
    {
        throw new NotSupportedException();
    }

    public void SetSettings(System.Xml.XmlNode settings)
    {
        throw new NotSupportedException();
    }

    protected void Calculate(LiveSplitState state)
    {
        string comparison = Settings.Comparison == "Current Comparison" ? state.CurrentComparison : Settings.Comparison;
        if (!state.Run.Comparisons.Contains(comparison))
        {
            comparison = state.CurrentComparison;
        }

        CalculateFinalSplit(state);
        CalculateDeltas(state, comparison);
        CheckLiveSegmentDelta(state, comparison);
    }

    private void CalculateFinalSplit(LiveSplitState state)
    {
        FinalSplit = TimeSpan.Zero;
        if (Settings.IsLiveGraph)
        {
            if (state.CurrentPhase != TimerPhase.NotRunning)
            {
                FinalSplit = state.CurrentTime[state.CurrentTimingMethod] ?? state.CurrentTime.RealTime;
            }
        }
        else
        {
            foreach (ISegment segment in state.Run)
            {
                if (segment.SplitTime[state.CurrentTimingMethod] != null)
                {
                    FinalSplit = segment.SplitTime[state.CurrentTimingMethod];
                }
            }
        }
    }

    private void CalculateDeltas(LiveSplitState state, string comparison)
    {
        Deltas = [];
        MaxDelta = TimeSpan.Zero;
        MinDelta = TimeSpan.Zero;
        for (int x = 0; x < state.Run.Count; x++)
        {
            TimeSpan? time = state.Run[x].SplitTime[state.CurrentTimingMethod]
                    - state.Run[x].Comparisons[comparison][state.CurrentTimingMethod];
            if (time > MaxDelta)
            {
                MaxDelta = time.Value;
            }

            if (time < MinDelta)
            {
                MinDelta = time.Value;
            }

            Deltas.Add(time);
        }
    }

    private void CheckLiveSegmentDelta(LiveSplitState state, string comparison)
    {
        IsLiveDeltaActive = false;
        if (Settings.IsLiveGraph)
        {
            if (state.CurrentPhase is TimerPhase.Running or TimerPhase.Paused)
            {
                TimeSpan? bestSeg = LiveSplitStateHelper.CheckLiveDelta(state, true, comparison, state.CurrentTimingMethod);
                TimeSpan? curSplit = state.Run[state.CurrentSplitIndex].Comparisons[comparison][state.CurrentTimingMethod];
                TimeSpan? curTime = state.CurrentTime[state.CurrentTimingMethod];
                if (bestSeg == null && curSplit != null && curTime - curSplit > MinDelta)
                {
                    bestSeg = curTime - curSplit;
                }

                if (bestSeg != null)
                {
                    if (bestSeg > MaxDelta)
                    {
                        MaxDelta = bestSeg.Value;
                    }

                    if (bestSeg < MinDelta)
                    {
                        MinDelta = bestSeg.Value;
                    }

                    Deltas.Add(bestSeg);
                    IsLiveDeltaActive = true;
                }
            }
        }
    }

    public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
    {
        Calculate(state);

        Cache.Restart();
        Cache["FinalSplit"] = FinalSplit.ToString();
        Cache["IsLiveDeltaActive"] = IsLiveDeltaActive;
        Cache["DeltasCount"] = Deltas.Count;
        for (int ind = 0; ind < Deltas.Count; ind++)
        {
            Cache["Deltas" + ind] = Deltas[ind] == null ? "null" : Deltas[ind].ToString();
        }

        if (invalidator != null && Cache.HasChanged)
        {
            invalidator.Invalidate(0, 0, width, height);
        }
    }

    public void Dispose()
    {
    }
}

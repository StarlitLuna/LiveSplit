using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;

using LiveSplit.Model;
using LiveSplit.Options;
using LiveSplit.UI.Drawing;

namespace LiveSplit.UI.Components;

public class ComponentRenderer
{
    public IEnumerable<IComponent> VisibleComponents { get; set; }

    public float OverallSize = 10f;

    public float MinimumWidth
        => !VisibleComponents.Any() ? 0 : VisibleComponents.Max(x => x.MinimumWidth);

    public float MinimumHeight
        => !VisibleComponents.Any() ? 0 : VisibleComponents.Max(x => x.MinimumHeight);

    protected bool errorInComponent;

    private readonly Dictionary<IComponent, FontOverrides> _overrideLookup = [];

    // Per-component draw helpers. The cursor-advancing TranslateTransform is applied at the
    // CALLER's scope (outside the per-component Save in Render) so inter-component translation
    // stacks across iterations, matching the GDI+ behavior. The inner clip+draw happens inside
    // the caller's Save so each component's IntersectClip doesn't leak into the next.
    private void DrawVerticalComponent(int index, IDrawingContext ctx, LiveSplitState state, float width, float height)
    {
        IComponent component = VisibleComponents.ElementAt(index);
        float topPadding = Math.Min(GetPaddingAbove(index), component.PaddingTop) / 2f;
        float bottomPadding = Math.Min(GetPaddingBelow(index), component.PaddingBottom) / 2f;
        ctx.IntersectClip(new RectangleF(0, topPadding, width, component.VerticalHeight - topPadding - bottomPadding));

        Matrix3x2 t = ctx.GetTransform();
        float scale = t.M11;
        int separatorOffset = component.VerticalHeight * scale < 3 ? 1 : 0;

        if (ctx.IsVisible(new RectangleF(
            t.M31,
            -separatorOffset + t.M32 - (topPadding * scale),
            width,
            (separatorOffset * 2f) + (scale * (component.VerticalHeight + bottomPadding)))))
        {
            component.DrawVertical(ctx, state, width);
        }
    }

    private void DrawHorizontalComponent(int index, IDrawingContext ctx, LiveSplitState state, float width, float height)
    {
        IComponent component = VisibleComponents.ElementAt(index);
        float leftPadding = Math.Min(GetPaddingToLeft(index), component.PaddingLeft) / 2f;
        float rightPadding = Math.Min(GetPaddingToRight(index), component.PaddingRight) / 2f;
        ctx.IntersectClip(new RectangleF(leftPadding, 0, component.HorizontalWidth - leftPadding - rightPadding, height));

        Matrix3x2 t = ctx.GetTransform();
        float scale = t.M11;
        int separatorOffset = component.VerticalHeight * scale < 3 ? 1 : 0;

        if (ctx.IsVisible(new RectangleF(
            -separatorOffset + t.M31 - (leftPadding * scale),
            t.M32,
            (separatorOffset * 2f) + (scale * (component.HorizontalWidth + rightPadding)),
            height)))
        {
            component.DrawHorizontal(ctx, state, height);
        }
    }

    private float GetPaddingAbove(int index)
    {
        while (index > 0)
        {
            index--;
            IComponent component = VisibleComponents.ElementAt(index);
            if (component.VerticalHeight != 0)
            {
                return component.PaddingBottom;
            }
        }

        return 0f;
    }

    private float GetPaddingBelow(int index)
    {
        while (index < VisibleComponents.Count() - 1)
        {
            index++;
            IComponent component = VisibleComponents.ElementAt(index);
            if (component.VerticalHeight != 0)
            {
                return component.PaddingTop;
            }
        }

        return 0f;
    }

    private float GetPaddingToLeft(int index)
    {
        while (index > 0)
        {
            index--;
            IComponent component = VisibleComponents.ElementAt(index);
            if (component.HorizontalWidth != 0)
            {
                return component.PaddingLeft;
            }
        }

        return 0f;
    }

    private float GetPaddingToRight(int index)
    {
        while (index < VisibleComponents.Count() - 1)
        {
            index++;
            IComponent component = VisibleComponents.ElementAt(index);
            if (component.HorizontalWidth != 0)
            {
                return component.PaddingRight;
            }
        }

        return 0f;
    }

    protected float GetHeightVertical(int index)
    {
        IComponent component = VisibleComponents.ElementAt(index);
        float bottomPadding = Math.Min(GetPaddingBelow(index), component.PaddingBottom) / 2f;
        return component.VerticalHeight - (bottomPadding * 2f);
    }

    protected float GetWidthHorizontal(int index)
    {
        IComponent component = VisibleComponents.ElementAt(index);
        float rightPadding = Math.Min(GetPaddingToRight(index), component.PaddingRight) / 2f;
        return component.HorizontalWidth - (rightPadding * 2f);
    }

    public void CalculateOverallSize(LayoutMode mode)
    {
        float totalSize = 0f;
        int index = 0;
        foreach (IComponent component in VisibleComponents)
        {
            if (mode == LayoutMode.Vertical)
            {
                totalSize += GetHeightVertical(index);
            }
            else
            {
                totalSize += GetWidthHorizontal(index);
            }

            index++;
        }

        OverallSize = Math.Max(totalSize, 1f);
    }

    public void Render(IDrawingContext ctx, LiveSplitState state, float width, float height, LayoutMode mode)
    {
        if (!errorInComponent)
        {
            try
            {
                // Outer Save scope so the caller's transform/clip snap back after the per-component
                // TranslateTransform stacking finishes (Skia state isn't directly assignable, so
                // restore-via-Save is the only way back).
                using IDrawingState outerState = ctx.Save();

                var crashedComponents = new List<IComponent>();
                Dictionary<IComponent, FontOverrides> overrideLookup = BuildOverrideLookup(state);
                int index = 0;
                foreach (IComponent component in VisibleComponents)
                {
                    try
                    {
                        ApplyFontOverrides(overrideLookup, component, state.LayoutSettings, out FontDescriptor origTimer, out FontDescriptor origTimes, out FontDescriptor origText);
                        try
                        {
                            // Per-component Save so each component's IntersectClip is isolated
                            // from its neighbors — clip resets between components.
                            using (IDrawingState componentState = ctx.Save())
                            {
                                if (mode == LayoutMode.Vertical)
                                {
                                    DrawVerticalComponent(index, ctx, state, width, height);
                                }
                                else
                                {
                                    DrawHorizontalComponent(index, ctx, state, width, height);
                                }
                            }

                            // Stacking translate applied OUTSIDE the per-component Save so
                            // cursor advances persist across components — transform accumulates,
                            // clip resets.
                            IComponent drawn = VisibleComponents.ElementAt(index);
                            if (mode == LayoutMode.Vertical)
                            {
                                float bottomPadding = Math.Min(GetPaddingBelow(index), drawn.PaddingBottom) / 2f;
                                ctx.TranslateTransform(0.0f, drawn.VerticalHeight - (bottomPadding * 2f));
                            }
                            else
                            {
                                float rightPadding = Math.Min(GetPaddingToRight(index), drawn.PaddingRight) / 2f;
                                ctx.TranslateTransform(drawn.HorizontalWidth - (rightPadding * 2f), 0.0f);
                            }
                        }
                        finally
                        {
                            FontOverrides.Restore(state.LayoutSettings, origTimer, origTimes, origText);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error(e);
                        crashedComponents.Add(component);
                        errorInComponent = true;
                    }

                    index++;
                }

                if (crashedComponents.Count > 0)
                {
                    var remainingComponents = VisibleComponents.ToList();
                    crashedComponents.ForEach(x =>
                    {
                        remainingComponents.Remove(x);
                        state.Layout.LayoutComponents = state.Layout.LayoutComponents.Where(y => y.Component != x).ToList();
                    });
                    VisibleComponents = remainingComponents;
                }
            }
            finally
            {
                errorInComponent = false;
            }
        }
    }

    protected void InvalidateVerticalComponent(int index, LiveSplitState state, IInvalidator invalidator, float width, float height, float scaleFactor)
    {
        IComponent component = VisibleComponents.ElementAt(index);
        float topPadding = Math.Min(GetPaddingAbove(index), component.PaddingTop) / 2f;
        float bottomPadding = Math.Min(GetPaddingBelow(index), component.PaddingBottom) / 2f;
        float totalHeight = scaleFactor * (component.VerticalHeight - topPadding - bottomPadding);
        component.Update(invalidator, state, width, totalHeight, LayoutMode.Vertical);
        invalidator.Transform *= System.Numerics.Matrix3x2.CreateTranslation(0.0f, totalHeight);
    }

    protected void InvalidateHorizontalComponent(int index, LiveSplitState state, IInvalidator invalidator, float width, float height, float scaleFactor)
    {
        IComponent component = VisibleComponents.ElementAt(index);
        float leftPadding = Math.Min(GetPaddingToLeft(index), component.PaddingLeft) / 2f;
        float rightPadding = Math.Min(GetPaddingToRight(index), component.PaddingRight) / 2f;
        float totalWidth = scaleFactor * (component.HorizontalWidth - leftPadding - rightPadding);
        component.Update(invalidator, state, totalWidth, height, LayoutMode.Horizontal);
        invalidator.Transform *= System.Numerics.Matrix3x2.CreateTranslation(totalWidth, 0.0f);
    }

    public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
    {
        System.Numerics.Matrix3x2 oldTransform = invalidator.Transform;
        float scaleFactor = mode == LayoutMode.Vertical
                ? height / OverallSize
                : width / OverallSize;
        Dictionary<IComponent, FontOverrides> overrideLookup = BuildOverrideLookup(state);

        for (int ind = 0; ind < VisibleComponents.Count(); ind++)
        {
            IComponent component = VisibleComponents.ElementAt(ind);
            ApplyFontOverrides(overrideLookup, component, state.LayoutSettings, out FontDescriptor origTimer, out FontDescriptor origTimes, out FontDescriptor origText);
            try
            {
                if (mode == LayoutMode.Vertical)
                {
                    InvalidateVerticalComponent(ind, state, invalidator, width, height, scaleFactor);
                }
                else
                {
                    InvalidateHorizontalComponent(ind, state, invalidator, width, height, scaleFactor);
                }
            }
            finally
            {
                FontOverrides.Restore(state.LayoutSettings, origTimer, origTimes, origText);
            }
        }

        invalidator.Transform = oldTransform;
    }

    private Dictionary<IComponent, FontOverrides> BuildOverrideLookup(LiveSplitState state)
    {
        _overrideLookup.Clear();
        foreach (ILayoutComponent layoutComponent in state.Layout.LayoutComponents)
        {
            if (layoutComponent is LayoutComponent componentWithOverrides
                && componentWithOverrides.FontOverrides.HasOverrides)
            {
                _overrideLookup[componentWithOverrides.Component] = componentWithOverrides.FontOverrides;
            }
        }

        return _overrideLookup;
    }

    private static void ApplyFontOverrides(Dictionary<IComponent, FontOverrides> lookup, IComponent component, LayoutSettings settings, out FontDescriptor origTimer, out FontDescriptor origTimes, out FontDescriptor origText)
    {
        if (lookup.TryGetValue(component, out FontOverrides overrides))
        {
            overrides.ApplyTo(settings, out origTimer, out origTimes, out origText);
        }
        else
        {
            origTimer = settings.TimerFont;
            origTimes = settings.TimesFont;
            origText = settings.TextFont;
        }
    }
}

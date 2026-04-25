using System;
using System.Collections.Generic;
using System.Drawing;

using LiveSplit.Model.Comparisons;
using LiveSplit.UI.Drawing;

namespace LiveSplit.Model;

public interface ISegment : ICloneable
{
    Image Icon { get; set; }
    /// <summary>
    /// Raw PNG/JPG bytes for <see cref="Icon"/>. Populated by run factories alongside
    /// the System.Drawing.Image; the live render path consumes <see cref="IconImage"/>
    /// on Skia where Image.FromStream throws without libgdiplus.
    /// </summary>
    byte[] IconPng { get; set; }
    /// <summary>
    /// Lazy <see cref="IImage"/> decoded from <see cref="IconPng"/> via the active drawing
    /// factory; null when no icon bytes are present. Cache invalidates whenever IconPng is set.
    /// </summary>
    IImage IconImage { get; }
    string Name { get; set; }
    Time PersonalBestSplitTime { get; set; }
    IComparisons Comparisons { get; set; }
    Time BestSegmentTime { get; set; }
    Time SplitTime { get; set; }
    SegmentHistory SegmentHistory { get; set; }
    /// <summary>
    ///     A dictionary mapping custom variable names to values.
    /// </summary>
    Dictionary<string, string> CustomVariableValues { get; }
}

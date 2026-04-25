using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

using LiveSplit.Model.Comparisons;
using LiveSplit.UI.Drawing;

namespace LiveSplit.Model;

[Serializable]
public class Segment : ISegment
{
    public Image Icon { get; set; }

    [NonSerialized]
    private byte[] iconPng;
    [NonSerialized]
    private IImage iconImage;

    public byte[] IconPng
    {
        get => iconPng;
        set
        {
            iconPng = value;
            iconImage?.Dispose();
            iconImage = null;
        }
    }

    public IImage IconImage
    {
        get
        {
            if (iconImage != null)
            {
                return iconImage;
            }

            if (iconPng is not { Length: > 0 })
            {
                return null;
            }

            try
            {
                using var stream = new MemoryStream(iconPng, writable: false);
                iconImage = DrawingApi.Factory.LoadImage(stream);
            }
            catch
            {
                iconImage = null;
            }

            return iconImage;
        }
    }

    public string Name { get; set; }
    public Time PersonalBestSplitTime
    {
        get => Comparisons[Run.PersonalBestComparisonName];
        set => Comparisons[Run.PersonalBestComparisonName] = value;
    }
    public IComparisons Comparisons { get; set; }
    public Time BestSegmentTime { get; set; }
    public Time SplitTime { get; set; }
    public SegmentHistory SegmentHistory { get; set; }
    public Dictionary<string, string> CustomVariableValues { get; private set; }

    public Segment(
        string name, Time pbSplitTime = default,
        Time bestSegmentTime = default, Image icon = null,
        Time splitTime = default)
    {
        Comparisons = new CompositeComparisons();
        Name = name;
        PersonalBestSplitTime = pbSplitTime;
        BestSegmentTime = bestSegmentTime;
        SplitTime = splitTime;
        Icon = icon;
        SegmentHistory = [];
        CustomVariableValues = [];
    }

    public Segment Clone()
    {
        SegmentHistory newSegmentHistory = SegmentHistory.Clone();

        return new Segment(Name)
        {
            BestSegmentTime = BestSegmentTime,
            SplitTime = SplitTime,
            Icon = Icon,
            IconPng = iconPng?.ToArray(),
            SegmentHistory = newSegmentHistory,
            CustomVariableValues = CustomVariableValues.ToDictionary(x => x.Key, x => x.Value),
            Comparisons = (IComparisons)Comparisons.Clone()
        };
    }

    object ICloneable.Clone()
    {
        return Clone();
    }
}

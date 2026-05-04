using System;
using System.Collections.Generic;
using System.Linq;
using LiveSplit.Localization;
using LiveSplit.Model;
using LiveSplit.Model.Comparisons;

namespace LiveSplit.UI.Components;

public class ColumnSettings
{
    public static readonly string[] TypeNames =
    [
        "Delta",
        "Split Time",
        "Delta or Split Time",
        "Segment Delta",
        "Segment Time",
        "Segment Delta or Segment Time",
        "Custom Variable"
    ];

    public static readonly string[] TimingMethodNames =
    [
        "Current Timing Method",
        "Real Time",
        "Game Time"
    ];

    private static string T(string source) => UiLocalizer.Translate(source, LanguageResolver.ResolveCurrentCultureLanguage());

    public string ColumnName { get => Data.Name; set => Data.Name = value; }
    public string Type
    {
        get => GetColumnType(Data.Type);
        set => Data.Type = ParseColumnType(value);
    }
    public string Comparison { get => Data.Comparison; set => Data.Comparison = value; }
    public string TimingMethod { get => Data.TimingMethod; set => Data.TimingMethod = value; }

    public ColumnData Data { get; set; }
    protected LiveSplitState CurrentState { get; set; }
    protected IList<ColumnSettings> ColumnsList { get; set; }

    protected int ColumnIndex => ColumnsList.IndexOf(this);
    protected int TotalColumns => ColumnsList.Count;

    public ColumnSettings(LiveSplitState state, string columnName, IList<ColumnSettings> columnsList)
    {

        Data = new ColumnData(columnName, ColumnType.Delta, "Current Comparison", "Current Timing Method");

        CurrentState = state;
        ColumnsList = columnsList;
    }

    public static string GetColumnType(ColumnType type)
    {
        if (type == ColumnType.SplitTime)
        {
            return "Split Time";
        }
        else if (type == ColumnType.Delta)
        {
            return "Delta";
        }
        else if (type == ColumnType.DeltaorSplitTime)
        {
            return "Delta or Split Time";
        }
        else if (type == ColumnType.SegmentTime)
        {
            return "Segment Time";
        }
        else if (type == ColumnType.SegmentDelta)
        {
            return "Segment Delta";
        }
        else if (type == ColumnType.SegmentDeltaorSegmentTime)
        {
            return "Segment Delta or Segment Time";
        }
        else if (type == ColumnType.CustomVariable)
        {
            return "Custom Variable";
        }
        else
        {
            return "Unknown";
        }
    }

    public static ColumnType ParseColumnType(string columnType)
    {
        return (ColumnType)Enum.Parse(typeof(ColumnType), columnType.Replace(" ", string.Empty));
    }

    public static bool UsesSegmentComparison(ColumnType type)
        => type is ColumnType.SegmentDelta or ColumnType.SegmentTime or ColumnType.SegmentDeltaorSegmentTime or ColumnType.CustomVariable;

    public static string[] GetComparisons(LiveSplitState state, ColumnType type, string currentComparison)
    {
        List<string> comparisons = ["Current Comparison"];
        comparisons.AddRange(state.Run.Comparisons.Where(x => x != NoneComparisonGenerator.ComparisonName));

        if (UsesSegmentComparison(type))
        {
            comparisons.Remove(BestSplitTimesComparisonGenerator.ComparisonName);
        }

        if (!string.IsNullOrEmpty(currentComparison) && !comparisons.Contains(currentComparison))
        {
            comparisons.Add(currentComparison);
        }

        return comparisons.Distinct().ToArray();
    }
}

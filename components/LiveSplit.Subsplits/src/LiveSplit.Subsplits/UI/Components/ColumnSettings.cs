using System;
using System.Collections.Generic;
using System.Linq;
using LiveSplit.Model;
using LiveSplit.Model.Comparisons;

namespace LiveSplit.UI.Components;

public class ColumnSettings
{
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

    public event EventHandler ColumnRemoved;
    public event EventHandler MovedUp;
    public event EventHandler MovedDown;

    public ColumnSettings(LiveSplitState state, string columnName, IList<ColumnSettings> columnsList)
    {

        Data = new ColumnData(columnName, ColumnType.Delta, "Current Comparison", "Current Timing Method");

        CurrentState = state;
        ColumnsList = columnsList;
    }

    private static string GetColumnType(ColumnType type)
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

    private static ColumnType ParseColumnType(string columnType)
    {
        return (ColumnType)Enum.Parse(typeof(ColumnType), columnType.Replace(" ", string.Empty));
    }

}

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System;

using global::Avalonia.Controls;

using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Model.RunFactories;
using LiveSplit.Options.SettingsFactories;
using LiveSplit.UI;
using Xunit;

namespace LiveSplit.Tests.UI.Components;

public class SplitsSettingsControlMust
{
    [Fact]
    public void ExposeMasterColumnEditorControls()
    {
        dynamic component = CreateSplitsComponent(CreateState());
        Control control = component.GetSettingsControl(LayoutMode.Vertical);

        Assert.NotNull(FindNamed<Button>(control, "AddColumnButton"));
        Assert.NotNull(FindNamed<TextBox>(control, "Column0NameTextBox"));
        Assert.NotNull(FindNamed<ComboBox>(control, "Column0TypeComboBox"));
        Assert.NotNull(FindNamed<ComboBox>(control, "Column0ComparisonComboBox"));
        Assert.NotNull(FindNamed<ComboBox>(control, "Column0TimingMethodComboBox"));
        Assert.NotNull(FindNamed<Button>(control, "Column0MoveDownButton"));
        Assert.NotNull(FindNamed<Button>(control, "Column0RemoveButton"));
    }

    [Fact]
    public void MutateColumnListFromEditor()
    {
        dynamic component = CreateSplitsComponent(CreateState());
        Control control = component.GetSettingsControl(LayoutMode.Vertical);

        FindNamed<Button>(control, "AddColumnButton")!.Command.Execute(null);
        FindNamed<TextBox>(control, "Column2NameTextBox")!.Text = "Segment";
        FindNamed<ComboBox>(control, "Column2TypeComboBox")!.SelectedItem = "Segment Time";
        FindNamed<ComboBox>(control, "Column2ComparisonComboBox")!.SelectedItem = "Current Comparison";
        FindNamed<ComboBox>(control, "Column2TimingMethodComboBox")!.SelectedItem = "Game Time";
        FindNamed<Button>(control, "Column2MoveUpButton")!.Command.Execute(null);

        Assert.Equal(3, component.Settings.ColumnsList.Count);
        Assert.Equal("Segment", component.Settings.ColumnsList[1].ColumnName);
        Assert.Equal("SegmentTime", component.Settings.ColumnsList[1].Data.Type.ToString());
        Assert.Equal("Game Time", component.Settings.ColumnsList[1].TimingMethod);

        FindNamed<Button>(control, "Column1RemoveButton")!.Command.Execute(null);

        List<string> columnNames = [];
        foreach (dynamic column in component.Settings.ColumnsList)
        {
            columnNames.Add((string)column.ColumnName);
        }

        Assert.Equal(new[] { "+/-", "Time" }, columnNames.ToArray());
    }

    [Fact]
    public void FilterColumnComparisonsLikeMaster()
    {
        var run = CreateRun();
        run.CustomComparisons.Add("Practice");
        var state = CreateState(run);
        dynamic component = CreateSplitsComponent(state);
        Control control = component.GetSettingsControl(LayoutMode.Vertical);

        ComboBox comparisonBox = FindNamed<ComboBox>(control, "Column0ComparisonComboBox")!;
        string[] deltaComparisons = comparisonBox.Items.Cast<string>().ToArray();
        Assert.Contains("Best Split Times", deltaComparisons);
        Assert.DoesNotContain("None", deltaComparisons);

        FindNamed<ComboBox>(control, "Column0TypeComboBox")!.SelectedItem = "Segment Time";
        string[] segmentComparisons = comparisonBox.Items.Cast<string>().ToArray();

        Assert.DoesNotContain("Best Split Times", segmentComparisons);
        Assert.DoesNotContain("None", segmentComparisons);
        Assert.Equal("Current Comparison", component.Settings.ColumnsList[0].Comparison);
    }

    private static LiveSplitState CreateState(IRun run = null)
    {
        run ??= CreateRun();
        return new LiveSplitState(
            run,
            null,
            new Layout(),
            new StandardLayoutSettingsFactory().Create(),
            new StandardSettingsFactory().Create())
        {
            CurrentComparison = Run.PersonalBestComparisonName,
            CurrentTimingMethod = TimingMethod.RealTime
        };
    }

    private static IRun CreateRun()
    {
        IRun run = new StandardRunFactory().Create(new StandardComparisonGeneratorsFactory());
        run.ComparisonGenerators.Add(new BestSplitTimesComparisonGenerator(run));
        run.ComparisonGenerators.Add(new NoneComparisonGenerator(run));
        return run;
    }

    private static dynamic CreateSplitsComponent(LiveSplitState state)
    {
        Assembly assembly = Assembly.Load("LiveSplit.Splits");
        Type type = assembly.GetType("LiveSplit.UI.Components.SplitsComponent", throwOnError: true);
        return type.GetConstructor([typeof(LiveSplitState)])!.Invoke([state]);
    }

    private static T FindNamed<T>(Control root, string name)
        where T : Control
        => Descendants<T>(root).FirstOrDefault(x => x.Name == name);

    private static IEnumerable<T> Descendants<T>(Control root)
        where T : Control
    {
        if (root is T typed)
        {
            yield return typed;
        }

        if (root is Decorator decorator && decorator.Child is Control decoratorChild)
        {
            foreach (T child in Descendants<T>(decoratorChild))
            {
                yield return child;
            }
        }

        if (root is ContentControl contentControl && contentControl.Content is Control content)
        {
            foreach (T child in Descendants<T>(content))
            {
                yield return child;
            }
        }

        if (root is Panel panel)
        {
            foreach (Control child in panel.Children.OfType<Control>())
            {
                foreach (T grandchild in Descendants<T>(child))
                {
                    yield return grandchild;
                }
            }
        }

        if (root is ItemsControl itemsControl)
        {
            foreach (object item in itemsControl.Items)
            {
                if (item is Control itemControl)
                {
                    foreach (T child in Descendants<T>(itemControl))
                    {
                        yield return child;
                    }
                }
            }
        }
    }
}

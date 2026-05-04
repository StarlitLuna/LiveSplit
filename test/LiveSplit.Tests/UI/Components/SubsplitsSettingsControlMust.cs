using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using global::Avalonia.Controls;

using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Model.RunFactories;
using LiveSplit.Options.SettingsFactories;
using LiveSplit.UI;

using Xunit;

namespace LiveSplit.Tests.UI.Components;

public class SubsplitsSettingsControlMust
{
    [Fact]
    public void ExposeHeaderAndColumnEditorControls()
    {
        dynamic component = CreateSubsplitsComponent(CreateState());
        Control control = component.GetSettingsControl(LayoutMode.Vertical);

        Assert.NotNull(FindNamed<ComboBox>(control, "HeaderComparisonComboBox"));
        Assert.NotNull(FindNamed<ComboBox>(control, "HeaderTimingMethodComboBox"));
        Assert.NotNull(FindNamed<Button>(control, "AddColumnButton"));
        Assert.NotNull(FindNamed<TextBox>(control, "Column0NameTextBox"));
        Assert.NotNull(FindNamed<ComboBox>(control, "Column0TypeComboBox"));
        Assert.NotNull(FindNamed<ComboBox>(control, "Column0ComparisonComboBox"));
        Assert.NotNull(FindNamed<ComboBox>(control, "Column0TimingMethodComboBox"));
    }

    [Fact]
    public void MutateHeaderComparisonAndTiming()
    {
        var run = CreateRun();
        run.CustomComparisons.Add("Practice");
        dynamic component = CreateSubsplitsComponent(CreateState(run));
        Control control = component.GetSettingsControl(LayoutMode.Vertical);

        FindNamed<ComboBox>(control, "HeaderComparisonComboBox")!.SelectedItem = "Practice";
        FindNamed<ComboBox>(control, "HeaderTimingMethodComboBox")!.SelectedItem = "Game Time";

        Assert.Equal("Practice", component.Settings.HeaderComparison);
        Assert.Equal("Game Time", component.Settings.HeaderTimingMethod);
    }

    [Fact]
    public void MutateColumnListFromEditor()
    {
        dynamic component = CreateSubsplitsComponent(CreateState());
        Control control = component.GetSettingsControl(LayoutMode.Vertical);

        FindNamed<Button>(control, "AddColumnButton")!.Command.Execute(null);
        FindNamed<TextBox>(control, "Column2NameTextBox")!.Text = "Segment";
        FindNamed<ComboBox>(control, "Column2TypeComboBox")!.SelectedItem = "Segment Time";
        FindNamed<Button>(control, "Column2MoveUpButton")!.Command.Execute(null);

        Assert.Equal(3, component.Settings.ColumnsList.Count);
        Assert.Equal("Segment", component.Settings.ColumnsList[1].ColumnName);
        Assert.Equal("SegmentTime", component.Settings.ColumnsList[1].Data.Type.ToString());

        FindNamed<Button>(control, "Column1RemoveButton")!.Command.Execute(null);
        List<string> columnNames = [];
        foreach (dynamic column in component.Settings.ColumnsList)
        {
            columnNames.Add((string)column.ColumnName);
        }

        Assert.Equal(new[] { "+/-", "Time" }, columnNames.ToArray());
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

    private static dynamic CreateSubsplitsComponent(LiveSplitState state)
    {
        Assembly assembly = Assembly.Load("LiveSplit.Subsplits");
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

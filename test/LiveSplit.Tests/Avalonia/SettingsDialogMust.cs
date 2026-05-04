using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using global::Avalonia.Media;

using LiveSplit.Avalonia.Dialogs;
using LiveSplit.Options;
using LiveSplit.Options.SettingsFactories;

using Xunit;

namespace LiveSplit.Tests.Avalonia;

public class SettingsDialogMust
{
    [Fact]
    public void UseMasterGroupedLayoutMetrics()
    {
        object spec = LayoutSpec();

        Assert.Equal(
            new[] { "HotkeysGroup", "HotkeyProfilesGroup", "LiveSplitServerGroup", "RefreshRateTextBox" },
            StringList(spec, "StructuralOrder"));
        Assert.Equal(200, Int(spec, "LabelColumnWidth"));
        Assert.Equal(204, Int(spec, "HotkeyTextBoxWidth"));
        Assert.Equal(204, Int(spec, "ServerPortTextBoxWidth"));
        Assert.Equal(50, Int(spec, "HotkeyDelayTextBoxWidth"));
        Assert.Equal(51, Int(spec, "RefreshRateTextBoxWidth"));
        Assert.Equal(75, Int(spec, "ProfileButtonWidth"));
        Assert.Equal(442, Int(spec, "InitialWindowWidth"));
        Assert.Equal(734, Int(spec, "InitialWindowHeight"));
        Assert.Equal(20, Int(spec, "TextBoxHeight"));
        Assert.Equal(26, Int(spec, "ComboBoxHeight"));
        Assert.Equal(23, Int(spec, "ButtonHeight"));
        Assert.Equal(17, Int(spec, "CheckBoxHeight"));
        Assert.Equal(13, Int(spec, "CheckBoxGlyphSize"));
        Assert.Equal(3, Int(spec, "ControlHorizontalMargin"));
        Assert.Equal(3, Int(spec, "GroupContentHorizontalPadding"));
        Assert.Equal(8, Int(spec, "GroupContentTopPadding"));
        Assert.Equal(3, Int(spec, "GroupHorizontalMargin"));
        Assert.Equal(210, Int(spec, "InputCellWidth"));
        Assert.Equal(42, Int(spec, "HotkeyDelayTextBoxVisibleWidth"));
        Assert.Equal(48, Int(spec, "RefreshRateTextBoxVisibleWidth"));
        Assert.Equal(2, Int(spec, "ModernCheckBoxCornerRadius"));
        Assert.Empty(StringList(spec, "NumericSpinnerControlNames"));
    }

    [Fact]
    public void UseStaticDialogThemeInsteadOfLayoutSettings()
    {
        Type themeType = Type.GetType("LiveSplit.Avalonia.Dialogs.DialogTheme, LiveSplit");
        Assert.NotNull(themeType);
        object background = themeType.GetProperty("WindowBackgroundColor", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null);
        Assert.Equal(Color.Parse("#202020"), Assert.IsType<Color>(background));

        Type dialogType = typeof(SettingsDialog);
        Assert.DoesNotContain(
            dialogType.GetConstructors().SelectMany(x => x.GetParameters()),
            x => x.ParameterType.Name.Contains("LayoutSettings", StringComparison.Ordinal));
        Assert.DoesNotContain(
            dialogType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public),
            x => x.FieldType.Name.Contains("LayoutSettings", StringComparison.Ordinal));
    }

    [Fact]
    public void ApplyNumericTextValuesOnOkWithMasterClamps()
    {
        ISettings settings = new StandardSettingsFactory().Create();

        Assert.True(TryApplyNumericTextSettings(settings, "-2.5", "16835", "999"));
        Assert.Equal(0f, settings.HotkeyProfiles[HotkeyProfile.DefaultHotkeyProfileName].HotkeyDelay);
        Assert.Equal(16835, settings.ServerPort);
        Assert.Equal(300, settings.RefreshRate);
    }

    [Fact]
    public void RejectInvalidNumericTextValuesWithoutMutatingSettings()
    {
        ISettings settings = new StandardSettingsFactory().Create();
        settings.RefreshRate = 60;

        Assert.False(TryApplyNumericTextSettings(settings, "bad", "16835", "240"));
        Assert.Equal(0f, settings.HotkeyProfiles[HotkeyProfile.DefaultHotkeyProfileName].HotkeyDelay);
        Assert.Equal(16834, settings.ServerPort);
        Assert.Equal(60, settings.RefreshRate);
    }

    [Fact]
    public void DisplayLoadedRefreshRateInsteadOfFactoryDefault()
    {
        ISettings settings = new StandardSettingsFactory().Create();
        settings.RefreshRate = 60;

        Assert.Equal("60", FormatRefreshRate(settings));
    }

    [Fact]
    public void KeepGamepadHotkeySettingEditableLikeMaster()
    {
        string source = File.ReadAllText(FindRepoFile("src/LiveSplit/Avalonia/Dialogs/SettingsDialog.cs"));

        Assert.DoesNotContain("allowGamepads.IsEnabled = false", source, StringComparison.Ordinal);
        Assert.DoesNotContain("allowGamepads.SetTextBrush(DialogTheme.DisabledTextBrush)", source, StringComparison.Ordinal);
    }

    private static object LayoutSpec()
    {
        Type type = Type.GetType("LiveSplit.Avalonia.Dialogs.SettingsDialogLayoutSpec, LiveSplit");
        Assert.NotNull(type);
        object value = type.GetProperty("Master", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null);
        Assert.NotNull(value);
        return value;
    }

    private static IReadOnlyList<string> StringList(object instance, string propertyName)
    {
        return Assert.IsAssignableFrom<IEnumerable<string>>(
            instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(instance)).ToList();
    }

    private static int Int(object instance, string propertyName)
    {
        return Assert.IsType<int>(
            instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(instance));
    }

    private static bool TryApplyNumericTextSettings(ISettings settings, string hotkeyDelay, string serverPort, string refreshRate)
    {
        Type type = Type.GetType("LiveSplit.Avalonia.Dialogs.SettingsDialogModel, LiveSplit");
        Assert.NotNull(type);
        MethodInfo method = type.GetMethod("TryApplyNumericTextSettings", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (bool)method.Invoke(null, new object[] { settings, HotkeyProfile.DefaultHotkeyProfileName, hotkeyDelay, serverPort, refreshRate });
    }

    private static string FormatRefreshRate(ISettings settings)
    {
        Type type = Type.GetType("LiveSplit.Avalonia.Dialogs.SettingsDialogModel, LiveSplit");
        Assert.NotNull(type);
        MethodInfo method = type.GetMethod("FormatRefreshRate", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<string>(method.Invoke(null, new object[] { settings }));
    }

    private static string FindRepoFile(string relativePath)
    {
        DirectoryInfo directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            string candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(relativePath);
    }
}

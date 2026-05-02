using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using global::Avalonia.Controls;

using LiveSplit.Localization;
using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Options;
using LiveSplit.UI.Components;
using LiveSplit.Web.SRL;

namespace LiveSplit.Avalonia;

internal sealed class TimerContextMenuContext
{
    public LiveSplitState State { get; set; }
    public IEnumerable<RecentSplitsFile> RecentSplits { get; set; } = [];
    public IEnumerable<string> RecentLayouts { get; set; } = [];
    public IEnumerable<IComponent> LayoutComponents { get; set; } = [];
    public IEnumerable<MenuItem> RaceProviderItems { get; set; } = [];

    public Func<Task> EditSplits { get; set; } = CompletedTask;
    public Func<Task> OpenSplits { get; set; } = CompletedTask;
    public Func<Task> OpenSplitsFromUrl { get; set; } = CompletedTask;
    public Func<Task> EditSplitsHistory { get; set; } = CompletedTask;
    public Func<Task> SaveSplits { get; set; } = CompletedTask;
    public Func<Task> SaveSplitsAs { get; set; } = CompletedTask;
    public Func<Task> CloseSplits { get; set; } = CompletedTask;
    public Func<string, Task> OpenRecentSplits { get; set; } = _ => Task.CompletedTask;

    public Func<Task> EditLayout { get; set; } = CompletedTask;
    public Func<Task> OpenLayout { get; set; } = CompletedTask;
    public Func<Task> OpenLayoutFromUrl { get; set; } = CompletedTask;
    public Func<Task> LoadDefaultLayout { get; set; } = CompletedTask;
    public Func<Task> EditLayoutHistory { get; set; } = CompletedTask;
    public Func<Task> SaveLayout { get; set; } = CompletedTask;
    public Func<Task> SaveLayoutAs { get; set; } = CompletedTask;
    public Func<string, Task> OpenRecentLayout { get; set; } = _ => Task.CompletedTask;

    public Func<Task> Settings { get; set; } = CompletedTask;
    public Func<Task> Share { get; set; } = CompletedTask;
    public Func<Task> About { get; set; } = CompletedTask;
    public Action Exit { get; set; } = static () => { };

    public Action StartOrSplit { get; set; } = static () => { };
    public Func<Task> Reset { get; set; } = CompletedTask;
    public Action UndoSplit { get; set; } = static () => { };
    public Action SkipSplit { get; set; } = static () => { };
    public Action Pause { get; set; } = static () => { };
    public Action UndoAllPauses { get; set; } = static () => { };
    public Action ToggleGlobalHotkeys { get; set; } = static () => { };

    public Func<ServerStateType> GetServerState { get; set; } = static () => ServerStateType.Off;
    public Action ToggleTcpServer { get; set; } = static () => { };
    public Action ToggleWebSocketServer { get; set; } = static () => { };

    public Action<string> SwitchComparison { get; set; } = static _ => { };
    public Action<TimingMethod> SwitchTimingMethod { get; set; } = static _ => { };
    public Func<string, Task> ApplyLanguage { get; set; } = _ => Task.CompletedTask;

    private static Task CompletedTask() => Task.CompletedTask;
}

internal static class TimerContextMenuBuilder
{
    public static IReadOnlyList<object> BuildRootItems(TimerContextMenuContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var items = new List<object>
        {
            MenuItem(T("Edit Splits..."), context.EditSplits),
            OpenSplitsMenu(context),
            MenuItem(T("Save Splits"), context.SaveSplits),
            MenuItem(T("Save Splits As..."), context.SaveSplitsAs),
            MenuItem(T("Close Splits"), context.CloseSplits),
            new Separator(),
            ControlMenu(context),
            ComparisonMenu(context),
            new Separator(),
            MenuItem(T("Share..."), context.Share),
        };

        items.AddRange(context.RaceProviderItems);
        items.Add(new Separator());

        items.Add(MenuItem(T("Edit Layout..."), context.EditLayout));
        items.Add(OpenLayoutMenu(context));
        items.Add(MenuItem(T("Save Layout"), context.SaveLayout));
        items.Add(MenuItem(T("Save Layout As..."), context.SaveLayoutAs));
        items.Add(new Separator());
        items.Add(MenuItem(T("Settings"), context.Settings));

        if (ShouldShowLanguageMenu())
        {
            items.Add(LanguageMenu(context));
        }

        items.Add(new Separator());
        items.Add(MenuItem(T("About"), context.About));
        items.Add(MenuItem(T("Exit"), context.Exit));

        return items;
    }

    public static bool ShouldShowLanguageMenu()
        => UiTextCatalog.Languages.Any(x => !x.IsDefault && !x.IsAuto);

    private static MenuItem OpenSplitsMenu(TimerContextMenuContext context)
    {
        var parent = new MenuItem { Header = T("Open Splits") };
        foreach (object item in BuildOpenSplitsItems(context))
        {
            parent.Items.Add(item);
        }

        return parent;
    }

    private static IEnumerable<object> BuildOpenSplitsItems(TimerContextMenuContext context)
    {
        var items = new List<object>();
        foreach (IGrouping<string, RecentSplitsFile> game in context.RecentSplits
            .Reverse()
            .Where(x => !string.IsNullOrEmpty(x.Path))
            .GroupBy(x => x.GameName ?? string.Empty))
        {
            var gameMenuItem = new MenuItem();

            foreach (IGrouping<string, RecentSplitsFile> category in game.GroupBy(x => x.CategoryName ?? string.Empty))
            {
                var categoryMenuItem = new MenuItem { Tag = "Category" };

                foreach (RecentSplitsFile splitsFile in category)
                {
                    string capturedPath = splitsFile.Path;
                    var menuItem = MenuItem(Path.GetFileName(capturedPath), () => context.OpenRecentSplits(capturedPath));
                    menuItem.Tag = "FileName";
                    categoryMenuItem.Items.Add(menuItem);
                }

                if (categoryMenuItem.Items.Count == 1)
                {
                    categoryMenuItem = (MenuItem)categoryMenuItem.Items[0];
                    if (!string.IsNullOrEmpty(category.Key))
                    {
                        categoryMenuItem.Header = category.Key;
                        categoryMenuItem.Tag = "Category";
                    }
                }
                else
                {
                    categoryMenuItem.Header = string.IsNullOrEmpty(category.Key)
                        ? "Unknown Category"
                        : category.Key;
                }

                gameMenuItem.Items.Add(categoryMenuItem);
            }

            string gameName;
            if (string.IsNullOrEmpty(game.Key))
            {
                gameName = "Unknown Game";

                if (gameMenuItem.Items.Count == 1)
                {
                    gameMenuItem = (MenuItem)gameMenuItem.Items[0];
                    gameName = gameMenuItem.Header?.ToString();
                    if (gameName == "Unknown Category")
                    {
                        gameName = "Unknown";
                    }
                }
            }
            else
            {
                gameName = game.Key;

                if (gameMenuItem.Items.Count == 1)
                {
                    gameMenuItem = (MenuItem)gameMenuItem.Items[0];
                    if ((string)gameMenuItem.Tag == "Category")
                    {
                        string categoryName = gameMenuItem.Header?.ToString();
                        if (!string.IsNullOrEmpty(categoryName) && !categoryName.StartsWith("Unknown Category", StringComparison.Ordinal))
                        {
                            gameName += " - " + categoryName;
                        }
                    }
                    else
                    {
                        gameName += " (" + gameMenuItem.Header + ")";
                    }
                }
            }

            gameMenuItem.Header = gameName;
            items.Add(gameMenuItem);
        }

        if (items.Count > 0)
        {
            items.Add(new Separator());
        }

        items.Add(MenuItem(T("From File..."), context.OpenSplits));
        items.Add(MenuItem(T("From URL..."), context.OpenSplitsFromUrl));
        items.Add(new Separator());
        items.Add(MenuItem(T("Edit History"), context.EditSplitsHistory));
        return items;
    }

    private static MenuItem OpenLayoutMenu(TimerContextMenuContext context)
    {
        var parent = new MenuItem { Header = T("Open Layout") };
        foreach (object item in BuildOpenLayoutItems(context))
        {
            parent.Items.Add(item);
        }

        return parent;
    }

    private static IEnumerable<object> BuildOpenLayoutItems(TimerContextMenuContext context)
    {
        var items = new List<object>();
        foreach (string path in context.RecentLayouts.Reverse().Where(x => !string.IsNullOrEmpty(x)))
        {
            string capturedPath = path;
            items.Add(MenuItem(Path.GetFileNameWithoutExtension(capturedPath), () => context.OpenRecentLayout(capturedPath)));
        }

        if (items.Count > 0)
        {
            items.Add(new Separator());
        }

        items.Add(MenuItem(T("From File..."), context.OpenLayout));
        items.Add(MenuItem(T("From URL..."), context.OpenLayoutFromUrl));
        items.Add(MenuItem(T("Default"), context.LoadDefaultLayout));
        items.Add(new Separator());
        items.Add(MenuItem(T("Edit History"), context.EditLayoutHistory));
        return items;
    }

    private static MenuItem ControlMenu(TimerContextMenuContext context)
    {
        var parent = new MenuItem { Header = T("Control") };
        LiveSplitState state = context.State;
        TimerPhase phase = state.CurrentPhase;

        parent.Items.Add(MenuItem(
            phase == TimerPhase.Paused ? T("Resume") : phase == TimerPhase.NotRunning ? T("Start") : T("Split"),
            context.StartOrSplit,
            phase is TimerPhase.NotRunning or TimerPhase.Running or TimerPhase.Paused));
        parent.Items.Add(MenuItem(T("Reset"), context.Reset, phase != TimerPhase.NotRunning));
        parent.Items.Add(MenuItem(T("Undo Split"), context.UndoSplit, state.CurrentSplitIndex > 0));
        parent.Items.Add(MenuItem(
            T("Skip Split"),
            context.SkipSplit,
            (phase is TimerPhase.Running or TimerPhase.Paused)
                && state.CurrentSplitIndex < state.Run.Count - 1));
        parent.Items.Add(MenuItem(T("Pause"), context.Pause, phase == TimerPhase.Running));
        parent.Items.Add(MenuItem(T("Undo All Pauses"), context.UndoAllPauses, state.PauseTime.HasValue));
        parent.Items.Add(new Separator());

        HotkeyProfile profile = null;
        state.Settings.HotkeyProfiles?.TryGetValue(state.CurrentHotkeyProfile, out profile);
        var hotkeys = MenuItem(T("Global Hotkeys"), context.ToggleGlobalHotkeys, profile != null);
        hotkeys.Icon = profile?.GlobalHotkeysEnabled == true ? new TextBlock { Text = "*" } : null;
        parent.Items.Add(hotkeys);

        parent.Items.Add(new Separator());
        AddServerItems(parent, context);

        foreach (IComponent component in context.LayoutComponents)
        {
            if (component.ContextMenuControls is not { Count: > 0 } controls)
            {
                continue;
            }

            parent.Items.Add(new Separator());
            foreach (KeyValuePair<string, Action> control in controls)
            {
                parent.Items.Add(MenuItem(control.Key, control.Value));
            }
        }

        return parent;
    }

    private static void AddServerItems(MenuItem parent, TimerContextMenuContext context)
    {
        ServerStateType state = context.GetServerState();
        parent.Items.Add(MenuItem(
            state == ServerStateType.TCP ? T("Stop TCP Server") : T("Start TCP Server"),
            context.ToggleTcpServer,
            state != ServerStateType.Websocket));
        parent.Items.Add(MenuItem(
            state == ServerStateType.Websocket ? T("Stop WebSocket Server") : T("Start WebSocket Server"),
            context.ToggleWebSocketServer,
            state != ServerStateType.TCP));
    }

    private static MenuItem ComparisonMenu(TimerContextMenuContext context)
    {
        var parent = new MenuItem { Header = T("Compare Against") };
        LiveSplitState state = context.State;

        foreach (string customComparison in state.Run.CustomComparisons)
        {
            parent.Items.Add(ComparisonItem(customComparison, state.CurrentComparison, context.SwitchComparison));
        }

        if (state.Run.ComparisonGenerators.Count > 0)
        {
            parent.Items.Add(new Separator());
        }

        bool raceSeparatorAdded = false;
        foreach (IComparisonGenerator generator in state.Run.ComparisonGenerators)
        {
            if (!raceSeparatorAdded && generator is SRLComparisonGenerator)
            {
                parent.Items.Add(new Separator());
                raceSeparatorAdded = true;
            }

            parent.Items.Add(ComparisonItem(generator.Name, state.CurrentComparison, context.SwitchComparison));
        }

        parent.Items.Add(new Separator());
        parent.Items.Add(TimingMethodItem("Real Time", TimingMethod.RealTime, state.CurrentTimingMethod, context.SwitchTimingMethod));
        parent.Items.Add(TimingMethodItem("Game Time", TimingMethod.GameTime, state.CurrentTimingMethod, context.SwitchTimingMethod));
        return parent;
    }

    private static MenuItem ComparisonItem(string name, string current, Action<string> switchComparison)
    {
        var item = MenuItem(T(name), () => switchComparison(name));
        item.Icon = string.Equals(name, current, StringComparison.Ordinal)
            ? new TextBlock { Text = "\u2022" }
            : null;
        return item;
    }

    private static MenuItem TimingMethodItem(string header, TimingMethod method, TimingMethod current, Action<TimingMethod> switchTimingMethod)
    {
        var item = MenuItem(T(header), () => switchTimingMethod(method));
        item.Icon = method == current ? new TextBlock { Text = "\u2022" } : null;
        return item;
    }

    private static MenuItem LanguageMenu(TimerContextMenuContext context)
    {
        var parent = new MenuItem { Header = TK(LocalizationKeys.LanguageMenu, "Language") };
        string current = LanguageResolver.NormalizeSettingValue(context.State.Settings.UILanguage);
        bool isAuto = LanguageResolver.IsAuto(current);
        AppLanguage configuredLanguage = isAuto ? null : LanguageResolver.Resolve(current);

        var followSystem = MenuItem(TK(LocalizationKeys.LanguageFollowSystem, "Follow System"), () => context.ApplyLanguage(string.Empty));
        followSystem.Icon = isAuto ? new TextBlock { Text = "\u2022" } : null;
        parent.Items.Add(followSystem);
        parent.Items.Add(new Separator());

        foreach (AppLanguage language in UiTextCatalog.Languages)
        {
            string code = language.Code;
            var item = MenuItem(language.DisplayName, () => context.ApplyLanguage(code));
            item.Icon = !isAuto && language.Equals(configuredLanguage)
                ? new TextBlock { Text = "\u2022" }
                : null;
            parent.Items.Add(item);
        }

        return parent;
    }

    private static MenuItem MenuItem(string header, Func<Task> action, bool isEnabled = true)
    {
        var item = new MenuItem { Header = header, IsEnabled = isEnabled };
        item.Click += async (_, _) => await action();
        return item;
    }

    private static MenuItem MenuItem(string header, Action action, bool isEnabled = true)
    {
        var item = new MenuItem { Header = header, IsEnabled = isEnabled };
        item.Click += (_, _) => action();
        return item;
    }

    private static string T(string source)
        => UiLocalizer.Translate(source, LanguageResolver.ResolveCurrentCultureLanguage());

    private static string TK(string key, string fallback)
        => UiLocalizer.TranslateKey(key, fallback, LanguageResolver.ResolveCurrentCultureLanguage());
}

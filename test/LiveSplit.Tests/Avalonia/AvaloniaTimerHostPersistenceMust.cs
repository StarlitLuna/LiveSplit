using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

using LiveSplit.Avalonia;
using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Model.RunSavers;
using LiveSplit.Options;
using LiveSplit.Model.Input;
using LiveSplit.UI.Drawing;
using LiveSplit.UI.Drawing.Skia;
using LiveSplit.UI.LayoutFactories;
using LiveSplit.UI.LayoutSavers;

using Xunit;

namespace LiveSplit.Tests.Avalonia;

[Collection("DrawingApi")]
public class AvaloniaTimerHostPersistenceMust
{
    [Fact]
    public void SaveRunAsAssignsFilePathAndClearsDirtyFlag()
    {
        DrawingApi.Register(new SkiaDrawingFactory());
        EnsureComponentFolder();
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.lss");
        string settingsBackup = BackupSettingsFile();

        try
        {
            using var host = new AvaloniaTimerHost(
                static () => { },
                startBackgroundServices: false,
                persistOnDispose: false);
            host.State.Run.HasChanged = true;

            Assert.True(host.SaveRunAs(path));

            Assert.Equal(path, host.State.Run.FilePath);
            Assert.False(host.State.Run.HasChanged);
            Assert.True(File.Exists(path));
        }
        finally
        {
            RestoreSettingsFile(settingsBackup);
            TryDelete(path);
        }
    }

    [Fact]
    public void SaveLayoutAsAssignsFilePathPositionAndClearsDirtyFlag()
    {
        DrawingApi.Register(new SkiaDrawingFactory());
        EnsureComponentFolder();
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.lsl");
        string settingsBackup = BackupSettingsFile();

        try
        {
            using var host = new AvaloniaTimerHost(
                static () => { },
                startBackgroundServices: false,
                persistOnDispose: false);
            host.State.Layout.HasChanged = true;

            Assert.True(host.SaveLayoutAs(path, 12, 34));

            Assert.Equal(path, host.State.Layout.FilePath);
            Assert.Equal(12, host.State.Layout.X);
            Assert.Equal(34, host.State.Layout.Y);
            Assert.False(host.State.Layout.HasChanged);
            Assert.True(File.Exists(path));
        }
        finally
        {
            RestoreSettingsFile(settingsBackup);
            TryDelete(path);
        }
    }

    [Fact]
    public void LoadingRunFromTimerOnlyModeRestoresRunLayoutPath()
    {
        DrawingApi.Register(new SkiaDrawingFactory());
        EnsureComponentFolder();
        string settingsBackup = BackupSettingsFile();
        string layoutPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.lsl");
        string splitsPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.lss");

        try
        {
            var layoutSettings = new LiveSplit.Options.SettingsFactories.StandardLayoutSettingsFactory().Create();
            var layout = new LiveSplit.UI.Layout
            {
                Mode = LiveSplit.UI.LayoutMode.Vertical,
                VerticalWidth = 321,
                VerticalHeight = 123,
                Settings = layoutSettings,
            };
            using (FileStream stream = File.Open(layoutPath, FileMode.Create, FileAccess.Write))
            {
                new XMLLayoutSaver().Save(layout, stream);
            }

            var run = new Run(new StandardComparisonGeneratorsFactory());
            run.AddSegment("Finish");
            run.LayoutPath = layoutPath;
            using (FileStream stream = File.Open(splitsPath, FileMode.Create, FileAccess.Write))
            {
                new XMLRunSaver().Save(run, stream);
            }

            using var host = new AvaloniaTimerHost(
                static () => { },
                startBackgroundServices: false,
                persistOnDispose: false);
            host.CloseSplits();

            Assert.True(host.LoadRun(splitsPath));

            Assert.Equal(layoutPath, host.State.Layout.FilePath);
            Assert.Equal(321, host.State.Layout.VerticalWidth);
        }
        finally
        {
            RestoreSettingsFile(settingsBackup);
            TryDelete(layoutPath);
            TryDelete(splitsPath);
        }
    }

    [Fact]
    public void FirstRunTimerOnlyModeUsesEmbeddedDefaultLayoutSettings()
    {
        DrawingApi.Register(new SkiaDrawingFactory());
        EnsureComponentFolder();
        string settingsBackup = BackupSettingsFile();

        try
        {
            using var host = new AvaloniaTimerHost(
                static () => { },
                startBackgroundServices: false,
                persistOnDispose: false);
            LayoutSettings defaultSettings = StandardLayoutFactory.CreateDefaultSettings();

            Assert.True(host.InTimerOnlyMode);
            Assert.Equal(defaultSettings.NotRunningColor.ToArgb(), host.State.LayoutSettings.NotRunningColor.ToArgb());
            Assert.Equal(defaultSettings.BackgroundColor.ToArgb(), host.State.LayoutSettings.BackgroundColor.ToArgb());
            Assert.Equal(defaultSettings.BackgroundType, host.State.LayoutSettings.BackgroundType);
        }
        finally
        {
            RestoreSettingsFile(settingsBackup);
        }
    }

    [Fact]
    public void LoadingRunRestoresExistingRecentTimingAndHotkeyProfile()
    {
        DrawingApi.Register(new SkiaDrawingFactory());
        EnsureComponentFolder();
        string settingsBackup = BackupSettingsFile();
        string splitsPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.lss");

        try
        {
            var run = new Run(new StandardComparisonGeneratorsFactory());
            run.AddSegment("Finish");
            using (FileStream stream = File.Open(splitsPath, FileMode.Create, FileAccess.Write))
            {
                new XMLRunSaver().Save(run, stream);
            }

            using var host = new AvaloniaTimerHost(
                static () => { },
                startBackgroundServices: false,
                persistOnDispose: false);
            host.State.Settings.HotkeyProfiles["Alt"] = new HotkeyProfile
            {
                SplitKey = new KeyOrButton(Key.NumPad4),
                DoubleTapPrevention = false,
            };
            host.State.Settings.AddToRecentSplits(splitsPath, run, TimingMethod.GameTime, "Alt");
            host.State.CurrentTimingMethod = TimingMethod.RealTime;
            host.State.CurrentHotkeyProfile = HotkeyProfile.DefaultHotkeyProfileName;

            Assert.True(host.LoadRun(splitsPath));

            Assert.Equal(TimingMethod.GameTime, host.State.CurrentTimingMethod);
            Assert.Equal("Alt", host.State.CurrentHotkeyProfile);
        }
        finally
        {
            RestoreSettingsFile(settingsBackup);
            TryDelete(splitsPath);
        }
    }

    [Fact]
    public void ReplaceRecentHistoriesFromEditedContextMenuHistory()
    {
        DrawingApi.Register(new SkiaDrawingFactory());
        EnsureComponentFolder();
        string settingsBackup = BackupSettingsFile();

        try
        {
            using var host = new AvaloniaTimerHost(
                static () => { },
                startBackgroundServices: false,
                persistOnDispose: false);
            var run = new Run(new StandardComparisonGeneratorsFactory());
            host.State.Settings.AddToRecentSplits(@"C:\runs\keep.lss", run, TimingMethod.RealTime, HotkeyProfile.DefaultHotkeyProfileName);
            host.State.Settings.AddToRecentSplits(@"C:\runs\drop.lss", run, TimingMethod.RealTime, HotkeyProfile.DefaultHotkeyProfileName);
            host.State.Settings.AddToRecentLayouts(@"C:\layouts\keep.lsl");
            host.State.Settings.AddToRecentLayouts(@"C:\layouts\drop.lsl");

            host.SetRecentSplitsHistory([@"C:\runs\keep.lss"]);
            host.SetRecentLayoutsHistory([@"C:\layouts\keep.lsl"]);

            Assert.Equal(@"C:\runs\keep.lss", Assert.Single(host.State.Settings.RecentSplits).Path);
            Assert.Equal(@"C:\layouts\keep.lsl", Assert.Single(host.State.Settings.RecentLayouts));
        }
        finally
        {
            RestoreSettingsFile(settingsBackup);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private static void EnsureComponentFolder()
    {
        string baseDir = AppContext.BaseDirectory;
        string componentsDir = Path.Combine(baseDir, "Components");
        Directory.CreateDirectory(componentsDir);

        foreach (string dll in Directory.EnumerateFiles(baseDir, "LiveSplit.*.dll")
            .Where(path =>
            {
                string name = Path.GetFileName(path);
                return name != "LiveSplit.dll"
                    && name != "LiveSplit.Core.dll"
                    && name != "LiveSplit.Tests.dll";
            }))
        {
            string destination = Path.Combine(componentsDir, Path.GetFileName(dll));
            CopyComponentDll(dll, destination);
        }
    }

    private static void CopyComponentDll(string source, string destination)
    {
        try
        {
            File.Copy(source, destination, overwrite: true);
        }
        catch (IOException) when (File.Exists(destination) && AreFilesIdentical(source, destination))
        {
        }
        catch (UnauthorizedAccessException) when (File.Exists(destination) && AreFilesIdentical(source, destination))
        {
        }
    }

    private static bool AreFilesIdentical(string first, string second)
    {
        byte[] firstHash = SHA256.HashData(File.ReadAllBytes(first));
        byte[] secondHash = SHA256.HashData(File.ReadAllBytes(second));
        return firstHash.SequenceEqual(secondHash);
    }

    private static string BackupSettingsFile()
    {
        string path = UserDataPaths.SettingsFile;
        if (!File.Exists(path))
        {
            return null;
        }

        string contents = File.ReadAllText(path);
        File.Delete(path);
        return contents;
    }

    private static void RestoreSettingsFile(string backup)
    {
        string path = UserDataPaths.SettingsFile;
        if (backup is null)
        {
            TryDelete(path);
            return;
        }

        File.WriteAllText(path, backup);
    }
}

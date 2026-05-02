using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Xml;

using LiveSplit.Avalonia;
using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Model.RunFactories;
using LiveSplit.Model.RunSavers;
using LiveSplit.Options;
using LiveSplit.Options.SettingsFactories;
using LiveSplit.Options.SettingsSavers;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using LiveSplit.UI.Drawing;
using LiveSplit.UI.LayoutSavers;

using Xunit;

namespace LiveSplit.Tests.Avalonia;

[Collection("DrawingApi")]
public class SmokeTestRunnerMust
{
    [Fact]
    public void ParseSmokeTestStartupOptions()
    {
        StartupOptions.Parse(new[] { "--smoke-test", "-s", "fixture.lss", "-l", "fixture.lsl" });

        Assert.True(StartupOptions.SmokeTest);
        Assert.Equal("fixture.lss", StartupOptions.SplitsPath);
        Assert.Equal("fixture.lsl", StartupOptions.LayoutPath);
    }

    [Fact]
    public void RunDefaultTimerSmokeTest()
    {
        EnsureComponentFolder();

        int exitCode = SmokeTestRunner.Run(new SmokeTestOptions
        {
            Width = 320,
            Height = 120,
            StartBackgroundServices = false,
            PersistOnDispose = false
        });

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void DoNotActivateAutoSplittersDuringSmokeTest()
    {
        EnsureComponentFolder();

        const string gameName = "Smoke Test Auto Splitter";
        string settingsPath = UserDataPaths.SettingsFile;
        string settingsBackup = File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : null;
        string splitsPath = Path.Combine(AppContext.BaseDirectory, "smoke-autosplitter.lss");
        string fakeSplitterFileName = "SmokeAutoSplitter.dll";
        string fakeSplitterPath = Path.Combine(AppContext.BaseDirectory, "Components", fakeSplitterFileName);
        IDictionary<string, IComponentFactory> previousFactories = null;
        IDictionary<string, AutoSplitter> previousAutoSplitters = AutoSplitterFactory.Instance.AutoSplitters;
        var fakeFactory = new FakeComponentFactory();

        try
        {
            SaveSettingsWithActiveAutoSplitter(gameName, settingsPath);
            SaveRun(gameName, splitsPath);
            File.WriteAllText(fakeSplitterPath, string.Empty);

            ComponentManager.BasePath = AppContext.BaseDirectory;
            var factories = new Dictionary<string, IComponentFactory>(ComponentManager.LoadAllFactories<IComponentFactory>())
            {
                [fakeSplitterFileName] = fakeFactory
            };
            previousFactories = ReplaceComponentFactories(factories);
            AutoSplitterFactory.Instance.AutoSplitters = new Dictionary<string, AutoSplitter>
            {
                [gameName.ToLowerInvariant()] = new AutoSplitter
                {
                    Description = "Smoke Test Auto Splitter",
                    Games = new[] { gameName.ToLowerInvariant() },
                    Type = AutoSplitterType.Component,
                    URLs = new List<string> { "https://example.invalid/" + fakeSplitterFileName }
                }
            };

            int exitCode = SmokeTestRunner.Run(new SmokeTestOptions
            {
                SplitsPath = splitsPath,
                StartBackgroundServices = false,
                PersistOnDispose = false
            });

            Assert.Equal(0, exitCode);
            Assert.Equal(0, fakeFactory.CreateCalls);
        }
        finally
        {
            RestoreSettingsFile(settingsPath, settingsBackup);
            AutoSplitterFactory.Instance.AutoSplitters = previousAutoSplitters;
            ReplaceComponentFactories(previousFactories);
            TryDelete(splitsPath);
            TryDelete(fakeSplitterPath);
        }
    }

    [Fact]
    public void DoNotResolveAutoSplittersDuringSmokeTest()
    {
        EnsureComponentFolder();
        bool resolvedAutoSplitter = false;

        using var host = new AvaloniaTimerHost(
            static () => { },
            startBackgroundServices: false,
            persistOnDispose: false,
            autoSplitterResolver: _ =>
            {
                resolvedAutoSplitter = true;
                return null;
            });

        Assert.NotNull(host.State.Run);
        Assert.False(resolvedAutoSplitter);
    }

    [Fact]
    public void FailSmokeTestWhenFrameIsBlank()
    {
        EnsureComponentFolder();
        string layoutPath = Path.Combine(AppContext.BaseDirectory, "smoke-blank-layout.lsl");

        try
        {
            SaveBlankLayout(layoutPath);

            int exitCode = SmokeTestRunner.Run(new SmokeTestOptions
            {
                LayoutPath = layoutPath,
                StartBackgroundServices = false,
                PersistOnDispose = false
            });

            Assert.Equal(1, exitCode);
        }
        finally
        {
            TryDelete(layoutPath);
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

    private static void SaveSettingsWithActiveAutoSplitter(string gameName, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        ISettings settings = new StandardSettingsFactory().Create();
        settings.ActiveAutoSplitters.Add(gameName);

        using FileStream stream = File.Open(path, FileMode.Create, FileAccess.Write);
        new XMLSettingsSaver().Save(settings, stream);
    }

    private static void SaveRun(string gameName, string path)
    {
        IRun run = new StandardRunFactory().Create(new StandardComparisonGeneratorsFactory());
        run.GameName = gameName;

        using FileStream stream = File.Open(path, FileMode.Create, FileAccess.Write);
        new XMLRunSaver().Save(run, stream);
    }

    private static void SaveBlankLayout(string path)
    {
        var layout = new Layout
        {
            Mode = LayoutMode.Vertical,
            VerticalWidth = 320,
            VerticalHeight = 120,
            HorizontalWidth = 320,
            HorizontalHeight = 120,
            Settings = new StandardLayoutSettingsFactory().Create()
        };
        layout.LayoutComponents.Add(new LayoutComponent("LiveSplit.BlankSpace.dll", new BlankSpace()));

        using FileStream stream = File.Open(path, FileMode.Create, FileAccess.Write);
        new XMLLayoutSaver().Save(layout, stream);
    }

    private static IDictionary<string, IComponentFactory> ReplaceComponentFactories(
        IDictionary<string, IComponentFactory> factories)
    {
        PropertyInfo property = typeof(ComponentManager).GetProperty(
            nameof(ComponentManager.ComponentFactories),
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        var previous = (IDictionary<string, IComponentFactory>)property.GetValue(null);
        property.SetValue(null, factories);
        return previous;
    }

    private static void RestoreSettingsFile(string path, string backup)
    {
        if (backup is null)
        {
            TryDelete(path);
            return;
        }

        File.WriteAllText(path, backup);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private sealed class FakeComponentFactory : IComponentFactory
    {
        public int CreateCalls { get; private set; }

        public string ComponentName => "Smoke Auto Splitter";
        public string Description => "Smoke auto splitter test component.";
        public ComponentCategory Category => ComponentCategory.Control;
        public string UpdateName => ComponentName;
        public string XMLURL => string.Empty;
        public string UpdateURL => string.Empty;
        public Version Version => new(1, 0);

        public IComponent Create(LiveSplitState state)
        {
            CreateCalls++;
            return new FakeComponent();
        }
    }

    private sealed class FakeComponent : IComponent
    {
        public string ComponentName => "Smoke Auto Splitter";
        public float HorizontalWidth => 1;
        public float MinimumHeight => 1;
        public float VerticalHeight => 1;
        public float MinimumWidth => 1;
        public float PaddingTop => 0;
        public float PaddingBottom => 0;
        public float PaddingLeft => 0;
        public float PaddingRight => 0;
        public IDictionary<string, Action> ContextMenuControls => null;

        public void DrawHorizontal(IDrawingContext ctx, LiveSplitState state, float height)
        {
        }

        public void DrawVertical(IDrawingContext ctx, LiveSplitState state, float width)
        {
        }

        public XmlNode GetSettings(XmlDocument document) => document.CreateElement("Settings");

        public void SetSettings(XmlNode settings)
        {
        }

        public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
        {
        }

        public void Dispose()
        {
        }
    }
}

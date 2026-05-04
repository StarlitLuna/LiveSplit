using System;
using System.IO;

using Xunit;

namespace LiveSplit.Tests.Packaging;

public class MediaPackagingMust
{
    [Fact]
    public void PublishWindowsComponentLibVlcRuntimeAssetsIntoZipLayout()
    {
        string liveSplitProject = ReadRepoFile("src", "LiveSplit", "LiveSplit.csproj");
        string workflow = ReadRepoFile(".github", "workflows", "ci.yml");

        Assert.Contains("<_ComponentRuntimeFiles Include=\"$(BuildPath)\\Components\\runtimes\\$(RuntimeIdentifier)\\**\\*\"", liveSplitProject);
        Assert.Contains("<RelativePath>Components\\runtimes\\$(RuntimeIdentifier)\\%(RecursiveDir)%(Filename)%(Extension)</RelativePath>", liveSplitProject);
        Assert.Contains("--runtime win-x64", workflow);
        Assert.Contains("Compress-Archive -Path \"$PWD/dist/LiveSplit-win-x64/*\"", workflow);
    }

    [Theory]
    [InlineData("components", "LiveSplit.Sound", "src", "LiveSplit.Sound", "LiveSplit.Sound.csproj")]
    [InlineData("components", "LiveSplit.Video", "src", "LiveSplit.Video", "LiveSplit.Video.csproj")]
    public void StageVideoLanWindowsLibVlcAssetsForDynamicComponents(params string[] projectPath)
    {
        string project = ReadRepoFile(projectPath);

        Assert.Contains("<PackageReference Include=\"VideoLAN.LibVLC.Windows\"", project);
        Assert.Contains("<Target Name=\"StageLibVlcWindowsRuntimeAssets\" AfterTargets=\"Build\">", project);
        Assert.Contains("$(OutputPath)libvlc\\win-x64\\**\\*", project);
        Assert.Contains("$(OutputPath)runtimes\\win-x64\\libvlc\\win-x64\\%(RecursiveDir)%(Filename)%(Extension)", project);
    }

    [Fact]
    public void FlatpakManifestBundlesLibVlcBeforeLiveSplit()
    {
        string manifest = ReadRepoFile("org.livesplit.LiveSplit.yml");

        int libVlcModule = manifest.IndexOf("- name: libvlc", StringComparison.Ordinal);
        int appModule = manifest.IndexOf("- name: livesplit", StringComparison.Ordinal);
        Assert.True(libVlcModule >= 0, "Flatpak manifest must build or bundle LibVLC into /app.");
        Assert.True(appModule > libVlcModule, "LibVLC must be staged before the LiveSplit module runs dotnet publish.");
        Assert.Contains("download.videolan.org/pub/videolan/vlc/", manifest);
        Assert.Contains("--enable-libvlc", manifest);
        Assert.Contains("--env=VLC_PLUGIN_PATH=/app/lib/vlc/plugins", manifest);
    }

    [Fact]
    public void FedoraRpmBuildAndRuntimeDependenciesIncludeVlcLibraries()
    {
        string workflow = ReadRepoFile(".github", "workflows", "ci.yml");
        string spec = ReadRepoFile("packaging", "rpm", "livesplit.spec");
        string readme = ReadRepoFile("README.md");
        string packageScript = ReadRepoFile("scripts", "package-fedora-rpm.sh");

        Assert.Contains("vlc-devel", workflow);
        Assert.Contains("vlc-libs", workflow);
        Assert.Contains("vlc-plugin-ffmpeg", workflow);
        Assert.Contains("Requires:       vlc-libs", spec);
        Assert.Contains("Requires:       vlc-plugin-ffmpeg", spec);
        Assert.Contains("BuildRequires:  vlc-devel", spec);
        Assert.Contains("vlc-libs", readme);
        Assert.Contains("vlc-devel vlc-libs vlc-plugin-ffmpeg", packageScript);
    }

    [Fact]
    public void LinuxDesktopMetadataRegistersSplitsAndLayoutMimeTypes()
    {
        string desktop = ReadRepoFile("org.livesplit.LiveSplit.desktop");
        string mimeInfo = ReadRepoFile("org.livesplit.LiveSplit.xml");
        string manifest = ReadRepoFile("org.livesplit.LiveSplit.yml");
        string spec = ReadRepoFile("packaging", "rpm", "livesplit.spec");

        Assert.Contains("MimeType=application/x-livesplit-splits;application/x-livesplit-layout;", desktop);
        Assert.Contains("<mime-type type=\"application/x-livesplit-splits\">", mimeInfo);
        Assert.Contains("<glob pattern=\"*.lss\"/>", mimeInfo);
        Assert.Contains("<mime-type type=\"application/x-livesplit-layout\">", mimeInfo);
        Assert.Contains("<glob pattern=\"*.lsl\"/>", mimeInfo);
        Assert.Contains("/app/share/mime/packages/org.livesplit.LiveSplit.xml", manifest);
        Assert.Contains("%{_datadir}/mime/packages/org.livesplit.LiveSplit.xml", spec);
    }

    [Fact]
    public void WindowsAppRestoresFileAssociationRegistrationHook()
    {
        string program = ReadRepoFile("src", "LiveSplit", "Program.cs");
        string appProject = ReadRepoFile("src", "LiveSplit", "LiveSplit.csproj");
        string linuxNoOp = ReadRepoFile("src", "LiveSplit.Register", "LinuxNoOp.cs");

        Assert.Contains("RegisterWindowsFileFormatsIfNeeded();", program);
        Assert.Contains("FiletypeRegistryHelper.RegisterFileFormatsIfNotAlreadyRegistered();", program);
        Assert.Contains("ProjectReference Include=\"$(SrcPath)\\LiveSplit.Register\\LiveSplit.Register.csproj\"", appProject);
        Assert.Contains("RegisterFileFormatsIfNotAlreadyRegistered", linuxNoOp);
    }

    private static string ReadRepoFile(params string[] relativePath)
        => File.ReadAllText(Path.Combine(FindRepoRoot(), Path.Combine(relativePath)));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LiveSplit.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find LiveSplit.sln from the test output directory.");
    }
}

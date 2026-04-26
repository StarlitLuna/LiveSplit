using System;
using System.Linq;
using System.Reflection;

using SkiaSharp;

using Xunit;

namespace LiveSplit.Tests.UI.Drawing;

/// <summary>
/// Regression coverage for the Avalonia.Skia ↔ SkiaSharp version-skew bug that crashed the
/// Linux build at startup. Avalonia.Skia is compiled against a specific SkiaSharp managed
/// version and ships a matching native libSkiaSharp through SkiaSharp.NativeAssets.Linux;
/// when the project pulls in a different SkiaSharp major.minor (or NuGet ends up resolving
/// a managed/native pair that disagree), SkiaSharp's own .cctor throws an
/// InvalidOperationException about the native library being outside the supported range.
///
/// Both tests are version-agnostic by design: they reflect on what's actually loaded and
/// require the relationships to hold, so a coordinated upgrade (Avalonia + SkiaSharp bumped
/// together to a compatible pair) keeps passing without the test needing to be touched.
/// </summary>
public class SkiaVersionCompatibilityTests
{
    /// <summary>
    /// Fails if the SkiaSharp managed assembly resolved by NuGet doesn't match the SkiaSharp
    /// version Avalonia.Skia was compiled against. Catches the original bug at build time:
    /// Avalonia.Skia 11.2 was compiled against SkiaSharp 2.88.x, but a direct PackageReference
    /// promoted SkiaSharp to 3.116.x — the major mismatch made libSkiaSharp incompatible.
    /// </summary>
    [Fact]
    public void AvaloniaSkia_ReferencedSkiaSharpMatchesLoadedSkiaSharp()
    {
        Assembly avaloniaSkia = Assembly.Load("Avalonia.Skia");

        AssemblyName referencedSkia = avaloniaSkia
            .GetReferencedAssemblies()
            .FirstOrDefault(n => n.Name == "SkiaSharp")
            ?? throw new InvalidOperationException(
                "Avalonia.Skia no longer directly references SkiaSharp; this test needs to " +
                "track whatever Avalonia depends on for its Skia integration.");

        Version loaded = typeof(SKObject).Assembly.GetName().Version
            ?? throw new InvalidOperationException("Loaded SkiaSharp assembly has no Version.");

        Assert.True(
            referencedSkia.Version!.Major == loaded.Major
                && referencedSkia.Version.Minor == loaded.Minor,
            $"Avalonia.Skia ({avaloniaSkia.GetName().Version}) was compiled against SkiaSharp " +
            $"{referencedSkia.Version}, but {loaded} is loaded. Mismatched major.minor produces " +
            $"the libSkiaSharp version-range exception at startup. Either pin SkiaSharp to the " +
            $"same major.minor as Avalonia.Skia, or upgrade Avalonia.Skia to a version that " +
            $"targets the SkiaSharp you want.");
    }

    /// <summary>
    /// Fails if the bundled native libSkiaSharp is outside the range the managed
    /// SkiaSharp.dll declares it supports — i.e. SkiaSharp's own version check throws.
    /// Constructing any SKObject-derived type triggers SKObject's static initializer
    /// which runs <c>SkiaSharpVersion.CheckNativeLibraryCompatible</c>; if it throws,
    /// the publish layout has a managed/native skew (Avalonia.Skia + SkiaSharp from
    /// incompatible release lines staged side-by-side).
    /// </summary>
    [Fact]
    public void SkiaSharp_NativeLibraryIsCompatibleWithManagedAssembly()
    {
        using SKPath path = new();
        Assert.NotNull(path);
    }
}

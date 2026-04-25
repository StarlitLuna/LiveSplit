using System;

using LiveSplit.UI;

namespace LiveSplit.UI.Components;

/// <summary>
/// Therun.gg upload progress publisher. Routes "uploading / success / error" through the
/// <see cref="Notifications"/> pipe; the Avalonia timer window subscribes and renders a transient
/// overlay. The original Windows build owned a borderless WinForms <c>Form</c> in the corner of
/// the screen, which doesn't translate cleanly to Avalonia (no system tray helpers, and a
/// transparent always-on-top window has a different lifecycle on each Linux WM).
/// </summary>
public class UploadToast : IDisposable
{
    public bool IsDisposed { get; private set; }

    public UploadToast(object owner) { }

    public void ShowUploading() => Notifications.Info("Uploading run to therun.gg…");
    public void ShowSuccess() => Notifications.Success("Run uploaded to therun.gg.");
    public void ShowError() => Notifications.Error("Run upload to therun.gg failed.");

    public void Dispose() => IsDisposed = true;
}

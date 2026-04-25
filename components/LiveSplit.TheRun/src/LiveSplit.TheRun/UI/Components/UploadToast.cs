using System;

namespace LiveSplit.UI.Components;

/// <summary>
/// Stub for the WinForms popup toast that flashed an upload-status message in the bottom-right
/// corner. The Avalonia front-end doesn't surface that yet — calls into <see cref="ShowUploading"/>
/// / <see cref="ShowSuccess"/> / <see cref="ShowError"/> are recorded as <see cref="LastStatus"/>
/// so the host can decide later how to surface it.
/// </summary>
public class UploadToast : IDisposable
{
    public bool IsDisposed { get; private set; }
    public string LastStatus { get; private set; }

    public UploadToast(object owner) { }

    public void ShowUploading() => LastStatus = "Uploading…";
    public void ShowSuccess() => LastStatus = "Upload succeeded";
    public void ShowError() => LastStatus = "Upload failed";

    public void Dispose() => IsDisposed = true;
}

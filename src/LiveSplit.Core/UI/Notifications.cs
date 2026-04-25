using System;

namespace LiveSplit.UI;

public enum NotificationSeverity
{
    Info,
    Success,
    Error,
}

public sealed class NotificationEventArgs : EventArgs
{
    public NotificationSeverity Severity { get; }
    public string Message { get; }

    public NotificationEventArgs(NotificationSeverity severity, string message)
    {
        Severity = severity;
        Message = message;
    }
}

/// <summary>
/// One-way pipe for transient user-visible status messages from components (e.g. upload
/// success/failure from the therun.gg collector). The host UI subscribes to <see cref="Raised"/>
/// and decides how to surface each event — typically a small overlay toast in the timer window.
/// Components publish via <see cref="Show"/> without taking a UI dependency.
/// </summary>
public static class Notifications
{
    public static event EventHandler<NotificationEventArgs> Raised;

    public static void Show(NotificationSeverity severity, string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        Raised?.Invoke(null, new NotificationEventArgs(severity, message));
    }

    public static void Info(string message) => Show(NotificationSeverity.Info, message);
    public static void Success(string message) => Show(NotificationSeverity.Success, message);
    public static void Error(string message) => Show(NotificationSeverity.Error, message);
}

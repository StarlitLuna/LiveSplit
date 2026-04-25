using System;
using System.Diagnostics;

namespace LiveSplit.Options;

/// <summary>
/// Trace-listener-backed logger. The Windows build wired up an
/// <see cref="System.Diagnostics.EventLog"/> sink under the "LiveSplit" source so errors flowed
/// to the OS Application event log; that's a Windows-only API and is gone on the linux-port.
/// Trace messages still flow through <see cref="Trace.Listeners"/>, so a host can register a
/// console / file listener at startup if it wants persistent logs.
/// </summary>
public static class Log
{
    public static void Error(Exception ex)
    {
        try
        {
            Trace.TraceError("{0}\n\n{1}", ex.Message, ex.StackTrace);
        }
        catch { }
    }

    public static void Error(string message)
    {
        try
        {
            Trace.TraceError(message);
        }
        catch { }
    }

    public static void Info(string message)
    {
        try
        {
            Trace.TraceInformation(message);
        }
        catch { }
    }

    public static void Warning(string message)
    {
        try
        {
            Trace.TraceWarning(message);
        }
        catch { }
    }
}

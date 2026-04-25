using System.Diagnostics;
using System.Reflection;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;
using global::Avalonia.Media;

using LiveSplit.Updates;

namespace LiveSplit.Avalonia.Dialogs;

public sealed class AboutBox : Window
{
    public AboutBox()
    {
        Title = "About LiveSplit";
        Width = 480;
        Height = 360;
        CanResize = false;

        string product = AssemblyTitle;
        if (Git.Branch is not null and not "master" and not "HEAD")
        {
            product = $"{product} ({Git.Branch})";
        }

        var stack = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = product, FontSize = 22, FontWeight = FontWeight.Bold },
                new TextBlock { Text = $"Version {Git.Version ?? "Unknown"}" },
                new TextBlock { Text = AssemblyCopyright ?? string.Empty },
                new TextBlock { Text = AssemblyDescription ?? string.Empty, TextWrapping = TextWrapping.Wrap },
            },
        };

        var websiteBtn = new Button { Content = "livesplit.org" };
        websiteBtn.Click += (_, _) => OpenUrl("https://livesplit.org");
        stack.Children.Add(websiteBtn);

        var closeBtn = new Button
        {
            Content = "Close",
            HorizontalAlignment = HorizontalAlignment.Right,
            Width = 80,
            Margin = new Thickness(0, 12, 0, 0),
        };
        closeBtn.Click += (_, _) => Close();
        stack.Children.Add(closeBtn);

        Content = stack;
    }

    private static void OpenUrl(string url)
    {
        try
        {
            // ProcessStartInfo with UseShellExecute=true is needed on .NET 8+ to launch URLs
            // through the OS — it routes to the default browser.
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
        }
        catch
        {
            // The user can copy the URL out of the dialog text if launching fails.
        }
    }

    private static string AssemblyTitle
    {
        get
        {
            object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyTitleAttribute), false);
            if (attributes.Length > 0)
            {
                var titleAttribute = (AssemblyTitleAttribute)attributes[0];
                if (!string.IsNullOrEmpty(titleAttribute.Title))
                {
                    return titleAttribute.Title;
                }
            }

            return "LiveSplit";
        }
    }

    private static string AssemblyDescription
    {
        get
        {
            object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyDescriptionAttribute), false);
            return attributes.Length == 0 ? string.Empty : ((AssemblyDescriptionAttribute)attributes[0]).Description;
        }
    }

    private static string AssemblyCopyright
    {
        get
        {
            object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false);
            return attributes.Length == 0 ? string.Empty : ((AssemblyCopyrightAttribute)attributes[0]).Copyright;
        }
    }
}

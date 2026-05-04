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
        Width = 340;
        Height = 390;
        CanResize = false;
        DialogTheme.ApplyWindow(this);

        string product = AssemblyTitle;
        if (Git.Branch is not null and not "master" and not "HEAD")
        {
            product = $"{product} ({Git.Branch})";
        }

        var stack = new StackPanel
        {
            Margin = new Thickness(18),
            Spacing = 7,
            Children =
            {
                new TextBlock { Text = product, FontSize = 18, FontWeight = FontWeight.Bold },
            },
        };

        var versionBtn = CreateLinkButton(Git.Version ?? "Unknown Version");
        versionBtn.Click += (_, _) =>
        {
            if (Git.RevisionUri != null)
            {
                OpenUrl(Git.RevisionUri.AbsoluteUri);
            }
        };
        stack.Children.Add(versionBtn);

        var websiteBtn = CreateLinkButton("livesplit.org");
        websiteBtn.Click += (_, _) => OpenUrl("http://livesplit.org");
        stack.Children.Add(websiteBtn);

        stack.Children.Add(new TextBlock { Text = "Made by:", Margin = new Thickness(0, 8, 0, 0) });

        var cryZeBtn = CreateLinkButton("CryZe");
        cryZeBtn.Click += (_, _) => OpenUrl("http://twitter.com/CryZe107");
        stack.Children.Add(cryZeBtn);

        var wooferBtn = CreateLinkButton("wooferzfg");
        wooferBtn.Click += (_, _) => OpenUrl("http://twitter.com/wooferzfg");
        stack.Children.Add(wooferBtn);

        stack.Children.Add(new TextBlock
        {
            Text = "We've put a lot of work into LiveSplit. If you like the program, please consider donating.",
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 14, 0, 4),
        });

        var donateButton = new Button
        {
            Content = "Donate",
            HorizontalAlignment = HorizontalAlignment.Center,
            MinWidth = 120,
        };
        donateButton.Click += (_, _) => OpenUrl("https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=R3Z2LGPKRNBNJ");
        stack.Children.Add(donateButton);

        var okButton = new Button
        {
            Content = "OK",
            HorizontalAlignment = HorizontalAlignment.Right,
            Width = 80,
            Margin = new Thickness(0, 12, 0, 0),
            IsDefault = true,
            IsCancel = true,
        };
        okButton.Click += (_, _) => Close();
        stack.Children.Add(okButton);

        Content = stack;
    }

    private static Button CreateLinkButton(string text)
        => new()
        {
            Content = text,
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(0, 2),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = DialogTheme.LinkBrush,
        };

    private static void OpenUrl(string url)
    {
        try
        {
            // ProcessStartInfo with UseShellExecute=true is needed on .NET 8+ to launch URLs
            // through the OS; it routes to the default browser.
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

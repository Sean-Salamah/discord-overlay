using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;

namespace DiscordOverlay;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow();
            desktop.MainWindow = mainWindow;
            CreateTrayIcon(desktop, mainWindow);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void CreateTrayIcon(IClassicDesktopStyleApplicationLifetime desktop, MainWindow mainWindow)
    {
        var toggleItem = new NativeMenuItem("Hide overlay");
        toggleItem.Click += (sender, e) =>
        {
            mainWindow.OverlayVisible = !mainWindow.OverlayVisible;
            toggleItem.Header = mainWindow.OverlayVisible ? "Hide overlay" : "Show overlay";
        };

        var quitItem = new NativeMenuItem("Quit");
        quitItem.Click += (sender, e) => desktop.Shutdown();

        var menu = new NativeMenu();
        menu.Add(toggleItem);
        menu.Add(new NativeMenuItemSeparator());
        menu.Add(quitItem);

        var trayIcon = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://DiscordOverlay/Assets/tray-icon.png"))),
            ToolTipText = "Discord Overlay",
            Menu = menu
        };

        TrayIcon.SetIcons(this, new TrayIcons { trayIcon });
    }
}

using System.Windows;
using AltTabCustom.Core;
using AltTabCustom.Settings;
using AltTabCustom.UI;
using Forms = System.Windows.Forms;

namespace AltTabCustom;

public partial class App : Application
{
    private const string MutexName = "AltTabCustom.SingleInstance.5F2A0E1C";

    private Mutex? _singleInstance;
    private SwitcherController? _controller;
    private Forms.NotifyIcon? _tray;
    private AppSettings _settings = new();
    private SettingsWindow? _settingsWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Only one instance — a second launch just exits quietly.
        _singleInstance = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        InstallExceptionLogging();
        Logger.Info("AltTabCustom starting up.");

        _settings = SettingsStore.Load();

        // Keep the saved "start with Windows" flag in sync with the registry.
        StartupManager.Apply(_settings.StartWithWindows);

        try
        {
            _controller = new SwitcherController(_settings);
            _controller.SettingsRequested += OpenSettings;
            _controller.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"AltTabCustom could not install its keyboard hook:\n\n{ex.Message}",
                "AltTabCustom", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        CreateTrayIcon();
    }

    private void InstallExceptionLogging()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            Logger.Error("Unhandled dispatcher exception", args.Exception);
            // Keep the tray app (and its hook) alive rather than crashing out.
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Logger.Error("Unhandled domain exception", args.ExceptionObject as Exception);

        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Logger.Error("Unobserved task exception", args.Exception);
            args.SetObserved();
        };
    }

    private void CreateTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Settings…", null, (_, _) => OpenSettings());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Shutdown());

        _tray = new Forms.NotifyIcon
        {
            Icon = LoadAppIcon(),
            Visible = true,
            Text = "AltTabCustom — customizable Alt+Tab",
            ContextMenuStrip = menu,
        };
        // Left-click opens Settings; right-click shows the menu (handled by the
        // ContextMenuStrip automatically).
        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left) OpenSettings();
        };
    }

    /// <summary>Load the bundled app.ico for the tray, falling back to a stock icon.</summary>
    private static System.Drawing.Icon LoadAppIcon()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/app.ico");
            using var stream = GetResourceStream(uri).Stream;
            return new System.Drawing.Icon(stream, System.Windows.Forms.SystemInformation.SmallIconSize);
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to load tray icon; using default", ex);
            return System.Drawing.SystemIcons.Application;
        }
    }

    private void OpenSettings()
    {
        if (_settingsWindow is { IsLoaded: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_settings);
        _settingsWindow.SettingsSaved += OnSettingsSaved;
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void OnSettingsSaved(AppSettings updated)
    {
        _settings = updated;
        SettingsStore.Save(updated);
        StartupManager.Apply(updated.StartWithWindows);
        _controller?.UpdateSettings(updated);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Dispose();
        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
        _singleInstance?.ReleaseMutex();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}

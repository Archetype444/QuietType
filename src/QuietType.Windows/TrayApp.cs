using System.Diagnostics;
using Microsoft.Win32;
using QuietType.Core;
namespace QuietType.Windows;

internal sealed class TrayApp : ApplicationContext
{
    internal static readonly string DataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuietType");
    private static readonly string SettingsPath = Path.Combine(DataDirectory, "settings.json");
    private Settings settings;
    private readonly NotifyIcon icon;
    private readonly InputMonitor input;
    private readonly CorrectionService service;
    private readonly Hotkeys hotkeys;
    private readonly Control dispatcher = new();
    private readonly ToolStripMenuItem pause, status, count;
    private SettingsForm? window;
    private long corrections;
    private readonly System.Windows.Forms.Timer refresh = new() { Interval = 2000 };
    public TrayApp(bool showSettings)
    {
        dispatcher.CreateControl();
        var firstRun = !File.Exists(SettingsPath);
        settings = Settings.Load(SettingsPath);
        settings.Save(SettingsPath);
        SetStartup(settings.StartWithWindows);
        input = new InputMonitor();
        service = new(settings, input, Path.Combine(AppContext.BaseDirectory, "assets"));
        hotkeys = new(Toggle, () => service.Undo(false));
        status = new("Starting…") { Enabled = false };
        count = new("No corrections this session") { Enabled = false };
        pause = new("Pause corrections", null, (_, _) => Toggle());
        var menu = new ContextMenuStrip { ShowImageMargin = false };
        menu.Items.Add(new ToolStripMenuItem("QuietType 1.0", null, (_, _) => OpenSettings()) { Font = new Font(SystemFonts.MenuFont!, FontStyle.Bold) });
        menu.Items.Add(status); menu.Items.Add(count); menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(pause);
        menu.Items.Add("Undo last correction    Ctrl+Alt+Z", null, (_, _) => service.Undo(true));
        menu.Items.Add("Settings…", null, (_, _) => OpenSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit QuietType", null, (_, _) => ExitThread());
        var executableIcon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
        icon = new NotifyIcon { Text = "QuietType · private autocorrect", Icon = executableIcon ?? SystemIcons.Application, ContextMenuStrip = menu, Visible = true };
        icon.DoubleClick += (_, _) => OpenSettings();
        service.StatusChanged += _ => Dispatch(UpdateStatus);
        service.Corrected += () => { Interlocked.Increment(ref corrections); Dispatch(UpdateStatus); };
        refresh.Tick += (_, _) => UpdateStatus(); refresh.Start();
        if (firstRun || showSettings) OpenSettings();
        UpdateStatus();
    }
    private void Dispatch(Action action) { if (!dispatcher.IsDisposed && dispatcher.IsHandleCreated) { try { dispatcher.BeginInvoke(action); } catch (InvalidOperationException) { } } }
    private void Toggle() => Apply(settings with { Enabled = !settings.Enabled });
    private void UpdateStatus()
    {
        status.Text = service.Status;
        pause.Text = settings.Enabled ? "Pause corrections    Ctrl+Alt+P" : "Resume corrections    Ctrl+Alt+P";
        count.Text = $"{Interlocked.Read(ref corrections):N0} corrections this session";
        icon.Text = settings.Enabled ? "QuietType · private autocorrect" : "QuietType · paused";
        window?.UpdateStatus(service.Status, corrections, service.LastInferenceMs);
    }
    private void Apply(Settings next)
    {
        try
        {
            var normalized = next.Normalize();
            SetStartup(normalized.StartWithWindows);
            normalized.Save(SettingsPath);
            settings = normalized;
            service.Configure(settings);
            UpdateStatus();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { MessageBox.Show("QuietType couldn't save your settings. Check that your Windows account can write to its local application data folder.", "QuietType", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
    private void OpenSettings()
    {
        if (window is not null && !window.IsDisposed) { window.Show(); window.Activate(); return; }
        window = new SettingsForm(settings, Apply, hotkeys.ToggleAvailable, hotkeys.UndoAvailable);
        window.FormClosed += (_, _) => window = null;
        window.Show(); UpdateStatus();
    }
    internal static void SetStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (enabled) key.SetValue("QuietType", $"\"{Environment.ProcessPath}\" --background");
        else key.DeleteValue("QuietType", false);
    }
    protected override void ExitThreadCore()
    {
        icon.Visible = false; refresh.Stop(); input.Invalidate();
        service.Dispose(); input.Dispose(); hotkeys.Dispose();
        window?.Close();
        base.ExitThreadCore();
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { icon.Dispose(); refresh.Dispose(); dispatcher.Dispose(); }
        base.Dispose(disposing);
    }
}

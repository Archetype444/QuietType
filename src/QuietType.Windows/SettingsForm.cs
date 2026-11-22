using QuietType.Core;
namespace QuietType.Windows;

internal sealed class SettingsForm : Form
{
    private readonly Settings original;
    private readonly Action<Settings> apply;
    private readonly CheckBox enabled = Check("Enable autocorrect"), ai = Check("Use the bundled local AI for context"), realWords = Check("Also correct real-word mix-ups (experimental)"), startup = Check("Start quietly when I sign in to Windows");
    private readonly NumericUpDown delay = new() { Minimum = 150, Maximum = 1500, Increment = 50, Width = 100 };
    private readonly TextBox excluded = Editor(), words = Editor();
    private readonly Label live = new() { AutoSize = true, ForeColor = Color.FromArgb(42, 116, 93), MaximumSize = new Size(670, 0) };
    private static readonly Color Ink = Color.FromArgb(31, 44, 47), Muted = Color.FromArgb(91, 104, 106), Paper = Color.FromArgb(247, 249, 247);
    public SettingsForm(Settings settings, Action<Settings> apply, bool toggleAvailable, bool undoAvailable)
    {
        original = settings; this.apply = apply;
        Text = "QuietType · Settings";
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
        Font = new Font("Segoe UI", 10);
        BackColor = Paper; ForeColor = Ink;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(760, 690); MinimumSize = new Size(710, 650); StartPosition = FormStartPosition.CenterScreen;
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(26, 20, 26, 14), RowCount = 4, ColumnCount = 1 };
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        var header = Flow(); header.Controls.Add(new Label { Text = "QuietType", AutoSize = true, Font = new Font("Segoe UI", 25, FontStyle.Bold), ForeColor = Ink });
        header.Controls.Add(Paragraph("A little help with your words. Everything stays on this PC."));
        outer.Controls.Add(header, 0, 0);
        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(14, 8) };
        var general = Page(tabs, "General");
        enabled.Checked = settings.Enabled; ai.Checked = settings.UseAi; realWords.Checked = settings.CorrectRealWords; startup.Checked = settings.StartWithWindows; delay.Value = settings.IdleDelayMs;
        general.Controls.Add(enabled); general.Controls.Add(ai);
        general.Controls.Add(Paragraph("Two CPU threads at most for inference. No GPU use. AI sleeps after three idle minutes."));
        general.Controls.Add(startup);
        var delayRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 8, 0, 8) };
        delayRow.Controls.Add(new Label { Text = "Pause before correcting (milliseconds)", AutoSize = true, Padding = new Padding(0, 4, 12, 0) }); delayRow.Controls.Add(delay);
        general.Controls.Add(delayRow); general.Controls.Add(realWords);
        general.Controls.Add(Paragraph("Leave experimental mix-ups off for fewer surprises. Uncertain words are kept."));
        general.Controls.Add(new Label { Text = "Try it here", AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(0, 12, 0, 5) });
        var test = new RichTextBox { Name = "QuietTypeTest", AccessibleName = "QuietType typing test", Width = 635, Height = 85, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 12), DetectUrls = false, Text = "" };
        general.Controls.Add(test); general.Controls.Add(Paragraph("Type “teh ”, then pause briefly. Ctrl+Alt+Z undoes the last correction while the cursor stays there."));
        var appsPage = Page(tabs, "App exclusions");
        appsPage.Controls.Add(Title("Leave these apps alone"));
        appsPage.Controls.Add(Paragraph("One executable name per line, such as code or bitwarden. No folder path needed. Password fields and browser address bars are skipped automatically. Terminals, developer tools and common password managers are excluded by default."));
        excluded.Text = string.Join(Environment.NewLine, settings.ExcludedApps); appsPage.Controls.Add(excluded);
        var wordsPage = Page(tabs, "Personal words");
        wordsPage.Controls.Add(Title("Your words, your spelling"));
        wordsPage.Controls.Add(Paragraph("One word per line. These words are never corrected. Only words you explicitly add here are saved; QuietType doesn't collect a typing history."));
        words.Text = string.Join(Environment.NewLine, settings.PersonalWords); wordsPage.Controls.Add(words);
        var about = Page(tabs, "Privacy & help");
        about.Controls.Add(Title("Offline by design"));
        about.Controls.Add(Paragraph("• No accounts, cloud requests, telemetry, ads or automatic update checks.\n• No clipboard access, screenshots or typing logs.\n• Reads up to 384 characters before and 128 after the cursor in a supported editable field.\n• Recent undo data stays in memory for up to 20 seconds.\n• Settings and your personal words stay in %LOCALAPPDATA%\\QuietType.\n• The installed app contains the model and works without internet access."));
        about.Controls.Add(Title("Everyday controls"));
        about.Controls.Add(Paragraph("Right-click the tray icon for Pause, Undo, Settings or Quit. Double-click it to open Settings. Closing this window keeps QuietType running quietly."));
        about.Controls.Add(Paragraph($"Ctrl+Alt+P: pause/resume{(toggleAvailable ? "" : " (shortcut already in use)")}\nCtrl+Alt+Z: undo last correction{(undoAvailable ? "" : " (shortcut already in use)")}\nUndo requires unchanged text and cursor position. It protects that spelling for this session."));
        about.Controls.Add(Paragraph("Supports English. Custom editors, games, elevated windows and some browser fields may be unavailable; QuietType skips them. Text already sent to an app is subject to that app's own privacy behaviour."));
        about.Controls.Add(Paragraph("QuietType 1.0.0 · MIT\nDistilBERT (Apache-2.0) · ONNX Runtime (MIT)\nSource, documentation and dependency notices: github.com/Archetype444/QuietType"));
        outer.Controls.Add(tabs, 0, 1);
        outer.Controls.Add(live, 0, 2);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var save = new Button { Text = "Save settings", AutoSize = true, BackColor = Color.FromArgb(40, 102, 83), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Padding = new Padding(12, 6, 12, 6) };
        save.Click += (_, _) => Save();
        var close = new Button { Text = "Close", AutoSize = true, Padding = new Padding(12, 6, 12, 6) }; close.Click += (_, _) => Close();
        buttons.Controls.Add(save); buttons.Controls.Add(close); outer.Controls.Add(buttons, 0, 3);
        Controls.Add(outer);
    }
    private static FlowLayoutPanel Flow() => new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
    private static FlowLayoutPanel Page(TabControl tabs, string title)
    {
        var page = new TabPage(title) { BackColor = Paper, Padding = new Padding(14) };
        var flow = Flow(); page.Controls.Add(flow); tabs.TabPages.Add(page); return flow;
    }
    private static Label Title(string text) => new() { Text = text, AutoSize = true, Font = new Font("Segoe UI", 12, FontStyle.Bold), Margin = new Padding(0, 8, 0, 10) };
    private static Label Paragraph(string text) => new() { Text = text, AutoSize = true, MaximumSize = new Size(635, 0), ForeColor = Muted, Margin = new Padding(0, 0, 0, 12) };
    private static CheckBox Check(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(0, 5, 0, 8) };
    private static TextBox Editor() => new() { Multiline = true, AcceptsReturn = true, ScrollBars = ScrollBars.Vertical, Width = 630, Height = 285, Font = new Font("Consolas", 11) };
    private void Save()
    {
        apply(original with { Enabled = enabled.Checked, UseAi = ai.Checked, CorrectRealWords = realWords.Checked, StartWithWindows = startup.Checked, IdleDelayMs = (int)delay.Value,
            ExcludedApps = excluded.Lines, PersonalWords = words.Lines });
    }
    public void UpdateStatus(string status, long corrections, double milliseconds)
    {
        live.Text = $"{status}  ·  {corrections:N0} corrections this session" + (milliseconds > 0 ? $"  ·  last AI check {milliseconds:N0} ms" : "");
    }
}

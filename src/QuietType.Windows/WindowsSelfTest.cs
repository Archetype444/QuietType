using System.Text.Json;
using System.Runtime.InteropServices;
using QuietType.Core;
namespace QuietType.Windows;

internal static class WindowsSelfTest
{
    public static int Run(string[] args)
    {
        string output = args.Length > 1 ? args[1] : "windows-self-test.json";
        var checks = new List<string>();
        var errors = new List<string>();
        var form = new Form { Text = "QuietType automated compatibility test", Width = 640, Height = 420, StartPosition = FormStartPosition.CenterScreen };
        var editor = new RichTextBox { Name = "QuietTypeTest", Dock = DockStyle.Top, Height = 120, Text = "teh ", DetectUrls = false };
        var password = new TextBox { Dock = DockStyle.Top, UseSystemPasswordChar = true, Text = "fixture-secret" };
        var plain = new TextBox { Dock = DockStyle.Top, Text = "speling ", Name = "QuietTypeTest" };
        form.Controls.Add(plain); form.Controls.Add(password); form.Controls.Add(editor);
        form.Shown += async (_, _) =>
        {
            try
            {
                void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); checks.Add(name); }
                Check(Marshal.SizeOf<Native.INPUT>() == 40, "Win64 SendInput structure layout");
                form.Activate(); editor.Focus(); await Task.Delay(300);
                editor.SelectionStart = editor.Text.Length; editor.SelectionLength = 0;
                var snapshot = await Task.Run(() => TextAccess.Capture(new Settings(), true));
                Check(snapshot is not null && snapshot.Before == "teh " && snapshot.After == "", "RichEdit context at caret");
                Check(Native.ReplaceSuffix(4,"the "),"Native input batch accepted");
                await Task.Delay(200);
                Check(editor.Text == "the ","Correction preserves trailing space");
                Check(Native.ReplaceSuffix(4,"teh "),"Undo input batch accepted");
                await Task.Delay(200);
                Check(editor.Text == "teh ","Undo restores original text");
                editor.Select(0,2); await Task.Delay(100);
                Check(await Task.Run(()=>TextAccess.Capture(new Settings(),true)) is null,"Selected text is protected");
                password.Focus(); await Task.Delay(100);
                Check(await Task.Run(()=>TextAccess.Capture(new Settings(),true)) is null,"Password field is protected");
                editor.ReadOnly = true; editor.Focus(); editor.Select(editor.Text.Length,0); await Task.Delay(100);
                Check(await Task.Run(()=>TextAccess.Capture(new Settings(),true)) is null,"Read-only editor is protected");
                editor.ReadOnly = false; editor.Focus(); editor.Select(editor.Text.Length,0); await Task.Delay(100);
                var excluded = new Settings { ExcludedApps = [System.Diagnostics.Process.GetCurrentProcess().ProcessName.ToLowerInvariant()] };
                Check(await Task.Run(()=>TextAccess.Capture(excluded,true)) is null,"Excluded application is protected");
                plain.Focus(); plain.Select(plain.Text.Length,0); await Task.Delay(150);
                var plainSnapshot=await Task.Run(()=>TextAccess.Capture(new Settings(),true));
                Check(plainSnapshot is not null && plainSnapshot.Before=="speling ","Native text box context at caret");
                Check(!TextAccess.SamePosition(snapshot!,plainSnapshot!),"Changed field invalidates pending correction");
                using var hooks = new InputMonitor();
                checks.Add("Keyboard and mouse hooks install and dispose");
                using var keys = new Hotkeys(()=>{},()=>{});
                Check(keys.ToggleAvailable && keys.UndoAvailable,"Global tray hotkeys register");
            }
            catch(Exception e) { errors.Add(e.GetType().Name+": "+e.Message); }
            finally { form.Close(); }
        };
        Application.Run(form);
        File.WriteAllText(output,JsonSerializer.Serialize(new {passed=checks.Count,checks,errors}, new JsonSerializerOptions{WriteIndented=true}));
        return errors.Count==0 ? 0 : 1;
    }
}

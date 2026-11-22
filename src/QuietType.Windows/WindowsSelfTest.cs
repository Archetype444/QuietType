using System.Text.Json;
using System.Runtime.InteropServices;
using QuietType.Core;
namespace QuietType.Windows;

internal static class WindowsSelfTest
{
    private static void SendFixtureSpace()
    {
        Native.INPUT[] inputs = [new() { Type=1, Data=new() { Keyboard=new() { Key=0x20 } } }, new() { Type=1, Data=new() { Keyboard=new() { Key=0x20, Flags=2 } } }];
        if(Native.SendInput(2,inputs,Marshal.SizeOf<Native.INPUT>())!=2) throw new InvalidOperationException("Fixture input failed");
    }
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
                using var service = new CorrectionService(new Settings { UseAi = false }, hooks, Path.Combine(AppContext.BaseDirectory,"assets"));
                form.Activate(); editor.ReadOnly = false; editor.Text = "teh"; editor.Focus(); editor.Select(editor.Text.Length,0);
                await Task.Delay(150);
                SendFixtureSpace();
                for(int i=0;i<60 && editor.Text!="the ";i++) await Task.Delay(50);
                Check(editor.Text=="the ","End-to-end hook, debounce, context and correction");
                service.Undo(false);
                for(int i=0;i<40 && editor.Text!="teh ";i++) await Task.Delay(50);
                Check(editor.Text=="teh ","End-to-end service undo");
                service.Configure(new Settings { UseAi = true });
                editor.Text="Please check my speling"; editor.Focus(); editor.Select(editor.Text.Length,0);
                await Task.Delay(150); SendFixtureSpace();
                for(int i=0;i<160 && editor.Text!="Please check my spelling ";i++) await Task.Delay(50);
                Check(editor.Text=="Please check my spelling ","End-to-end bundled neural correction");
                service.Configure(new Settings { Enabled=false });
                editor.Text="teh"; editor.Focus(); editor.Select(editor.Text.Length,0);
                await Task.Delay(150); SendFixtureSpace(); await Task.Delay(500);
                Check(editor.Text=="teh ","Pause prevents correction");
                service.Configure(new Settings { UseAi=false });
                string longPrefix=new string('a',500)+" ";
                editor.Text=longPrefix+"teh"; editor.Focus(); editor.Select(editor.Text.Length,0);
                await Task.Delay(150); SendFixtureSpace();
                for(int i=0;i<60 && editor.Text!=longPrefix+"the ";i++) await Task.Delay(50);
                Check(editor.Text==longPrefix+"the ","Long document correction");
                service.Undo(false);
                for(int i=0;i<40 && editor.Text!=longPrefix+"teh ";i++) await Task.Delay(50);
                Check(editor.Text==longPrefix+"teh ","Long document undo preserves bounded context");
                using var preview = new SettingsForm(new Settings(),_=>{},true,true);
                preview.Show(); await Task.Delay(150);
                using var image=new Bitmap(preview.Width,preview.Height);
                preview.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));
                image.Save(Path.ChangeExtension(output,"png"));
                checks.Add("Settings window renders successfully");

            }
            catch(Exception e) { errors.Add(e.GetType().Name+": "+e.Message); }
            finally { form.Close(); }
        };
        Application.Run(form);
        File.WriteAllText(output,JsonSerializer.Serialize(new {passed=checks.Count,checks,errors}, new JsonSerializerOptions{WriteIndented=true}));
        return errors.Count==0 ? 0 : 1;
    }
}

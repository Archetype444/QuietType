using System.Diagnostics;
using System.Runtime.InteropServices;
namespace QuietType.Windows;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        if (args.Contains("--self-test")) return WindowsSelfTest.Run(args);
        using var mutex = new Mutex(true, "Local\\QuietType.Tray.v1", out bool created);
        if (!created) return 0;
        try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal; } catch (InvalidOperationException) { }
        EnableEfficiencyMode();
        try { using var tray = new TrayApp(args.Contains("--settings")); Application.Run(tray); return 0; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            MessageBox.Show("QuietType could not start. Close any other copy and reinstall if necessary. No typing data has been saved.", "QuietType", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
    private static void EnableEfficiencyMode()
    {
        var state = new POWER_THROTTLING_STATE { Version = 1, ControlMask = 1, StateMask = 1 };
        SetProcessInformation(Process.GetCurrentProcess().Handle, 4, ref state, (uint)Marshal.SizeOf<POWER_THROTTLING_STATE>());
    }
    [StructLayout(LayoutKind.Sequential)] private struct POWER_THROTTLING_STATE { public uint Version, ControlMask, StateMask; }
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetProcessInformation(nint process, int informationClass, ref POWER_THROTTLING_STATE data, uint size);
}

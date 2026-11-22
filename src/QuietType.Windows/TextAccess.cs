using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Automation.Text;
using QuietType.Core;
namespace QuietType.Windows;

internal sealed record TextSnapshot(nint Window, int ProcessId, string App, string RuntimeId, string Before, string After);
internal static class TextAccess
{
    public static TextSnapshot? Capture(Settings settings, bool allowOwnProcess = false)
    {
        var window = Native.GetForegroundWindow();
        if (window == 0 || Native.ModifierDown || Native.Down(0x10)) return null;
        Native.GetWindowThreadProcessId(window, out var processId);
        if (processId == 0) return null;
        using var process = Process.GetProcessById((int)processId);
        string name = process.ProcessName.ToLowerInvariant();
        if (settings.ExcludedApps.Contains(name, StringComparer.OrdinalIgnoreCase)) return null;
        // Standard native password edits are excluded independently of accessibility metadata.
        var gui = new Native.GUITHREADINFO { Size = (uint)Marshal.SizeOf<Native.GUITHREADINFO>() };
        var thread = Native.GetWindowThreadProcessId(window, out _);
        if (!Native.GetGUIThreadInfo(thread, ref gui) || (gui.Focus != 0 && (Native.GetWindowLong(gui.Focus, -16) & 0x20) != 0 && IsNativeEdit(gui.Focus))) return null;
        if ((gui.Flags & 0x1E) != 0) return null; // Menus, move/size mode or system menu.
        var element = AutomationElement.FocusedElement;
        if (element is null) return null;
        var properties = element.Current;
        if (properties.ProcessId != processId || properties.IsPassword || !properties.IsEnabled || !properties.HasKeyboardFocus) return null;
        if (properties.ControlType != ControlType.Edit && properties.ControlType != ControlType.Document) return null;
        if (properties.NativeWindowHandle != 0 && (Native.GetWindowLong(properties.NativeWindowHandle, -16) & 0x20) != 0 && IsNativeEdit(properties.NativeWindowHandle)) return null;
        var id = properties.AutomationId.ToLowerInvariant();
        var label = properties.Name.ToLowerInvariant();
        if (!allowOwnProcess && processId == Environment.ProcessId && id != "quiettypetest") return null;
        if (id.Contains("address") || id.Contains("urlbar") || id.Contains("omnibox") || label.Contains("address and search") || label.Contains("password") || label.Contains("security code") || label.Contains("verification code")) return null;
        if (!element.TryGetCurrentPattern(TextPattern.Pattern, out var pattern)) return null;
        var text = (TextPattern)pattern;
        var selection = text.GetSelection();
        if (selection.Length != 1 || selection[0].CompareEndpoints(TextPatternRangeEndpoint.Start, selection[0], TextPatternRangeEndpoint.End) != 0) return null;
        var caret = selection[0];
        if (caret.GetAttributeValue(TextPattern.IsReadOnlyAttribute) is not bool readOnly || readOnly) return null;
        var left = caret.Clone();
        left.MoveEndpointByUnit(TextPatternRangeEndpoint.Start, TextUnit.Character, -384);
        var right = caret.Clone();
        right.MoveEndpointByUnit(TextPatternRangeEndpoint.End, TextUnit.Character, 128);
        var rawBefore = left.GetText(-1);
        if (rawBefore.Length > 1536) return null;
        var before = TextRules.BoundedTail(rawBefore, 384);
        var after = right.GetText(128);
        // ASCII suffix editing only; preceding Unicode context remains read-only.
        return new(window, (int)processId, name, string.Join(".", element.GetRuntimeId()), before, after);
    }
    private static bool IsNativeEdit(nint handle)
    {
        var cls = new System.Text.StringBuilder(256);
        GetClassName(handle, cls, cls.Capacity);
        return cls.ToString().Contains("edit", StringComparison.OrdinalIgnoreCase);
    }
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] private static extern int GetClassName(nint window, System.Text.StringBuilder name, int count);
    public static bool SamePosition(TextSnapshot first, TextSnapshot second) => first.Window == second.Window && first.ProcessId == second.ProcessId && first.RuntimeId == second.RuntimeId && first.Before == second.Before && first.After == second.After;
}
